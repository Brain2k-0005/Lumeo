using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lumeo.RegistryGen;

/// <summary>
/// Extracts the PUBLIC, consumer-facing service-layer API from plain C# source
/// files (services, options records, global enums) with Roslyn. Mirrors
/// <see cref="RazorParameterScanner"/>'s approach — same <c>ExtractXmlSummary</c>
/// strategy and the same fail-soft contract: a file that cannot be parsed is
/// skipped (returns no types) rather than crashing the generator.
///
/// Unlike the Razor scanner this parses whole compilation units directly (no
/// <c>@code</c> block extraction needed) and emits one entry PER public type —
/// classes, records, interfaces, enums and static classes — each with its
/// public properties, public methods, and (for enums) members.
/// </summary>
public static class ServiceApiScanner
{
    public sealed record ServiceProperty(
        string Name,
        string Type,
        string? Default,
        string? Summary);

    public sealed record ServiceMethod(
        string Name,
        string ReturnType,
        string Signature,
        string? Summary);

    public sealed record ServiceEnumValue(
        string Name,
        string? Summary);

    public sealed record ServiceType(
        string Name,
        string Kind, // class | record | interface | enum | staticClass
        string? Namespace,
        string? Summary,
        ServiceProperty[] Properties,
        ServiceMethod[] Methods,
        ServiceEnumValue[] EnumValues);

    /// <summary>
    /// Parse a single C# source file and return every public type declared in
    /// it. Always returns a (possibly empty) list — never throws. A file that
    /// fails to read or tokenize yields an empty result and a warning via
    /// <paramref name="logger"/> (when supplied), exactly like the Razor scanner.
    /// </summary>
    public static IReadOnlyList<ServiceType> Scan(string csharpPath, TextWriter? logger = null)
    {
        string text;
        try { text = File.ReadAllText(csharpPath); }
        catch (Exception ex)
        {
            logger?.WriteLine($"[service-api] WARN: read failed for {csharpPath}: {ex.Message}");
            return Array.Empty<ServiceType>();
        }

        SyntaxTree tree;
        try
        {
            tree = CSharpSyntaxTree.ParseText(text);
        }
        catch (Exception ex)
        {
            logger?.WriteLine($"[service-api] WARN: parse failed for {csharpPath}: {ex.Message}");
            return Array.Empty<ServiceType>();
        }

        SyntaxNode root;
        try { root = tree.GetRoot(); }
        catch (Exception ex)
        {
            logger?.WriteLine($"[service-api] WARN: root read failed for {csharpPath}: {ex.Message}");
            return Array.Empty<ServiceType>();
        }

        var results = new List<ServiceType>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var decl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            // Only top-level (or namespace-scoped) public types — nested types
            // are exposed through their parent and skipped here to keep the
            // service catalog flat and consumer-focused.
            if (!IsPublic(decl)) continue;
            if (IsNestedType(decl)) continue;

            var name = decl.Identifier.Text;
            if (!seen.Add(name)) continue;

            var ns = ResolveNamespace(decl);
            var summary = ExtractXmlSummary(decl.GetLeadingTrivia());

            switch (decl)
            {
                case EnumDeclarationSyntax en:
                    results.Add(new ServiceType(
                        Name: name,
                        Kind: "enum",
                        Namespace: ns,
                        Summary: summary,
                        Properties: Array.Empty<ServiceProperty>(),
                        Methods: Array.Empty<ServiceMethod>(),
                        EnumValues: en.Members
                            .Select(m => new ServiceEnumValue(m.Identifier.Text, ExtractXmlSummary(m.GetLeadingTrivia())))
                            .ToArray()));
                    break;

                case RecordDeclarationSyntax rec:
                    results.Add(new ServiceType(
                        Name: name,
                        Kind: "record",
                        Namespace: ns,
                        Summary: summary,
                        Properties: CollectProperties(rec).ToArray(),
                        Methods: CollectMethods(rec).ToArray(),
                        EnumValues: Array.Empty<ServiceEnumValue>()));
                    break;

                case InterfaceDeclarationSyntax iface:
                    results.Add(new ServiceType(
                        Name: name,
                        Kind: "interface",
                        Namespace: ns,
                        Summary: summary,
                        Properties: CollectProperties(iface).ToArray(),
                        Methods: CollectMethods(iface).ToArray(),
                        EnumValues: Array.Empty<ServiceEnumValue>()));
                    break;

                case ClassDeclarationSyntax cls:
                    var isStatic = cls.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
                    results.Add(new ServiceType(
                        Name: name,
                        Kind: isStatic ? "staticClass" : "class",
                        Namespace: ns,
                        Summary: summary,
                        Properties: CollectProperties(cls).ToArray(),
                        Methods: CollectMethods(cls).ToArray(),
                        EnumValues: Array.Empty<ServiceEnumValue>()));
                    break;
            }
        }

        return results;
    }

    /// <summary>
    /// Scan a set of files, optionally restricting each file's emitted types to
    /// an explicit allow-list of type names. A null or empty allow-list means
    /// "emit every public type in the file". Files are processed independently;
    /// one bad file never aborts the rest. Returns types sorted by name.
    /// </summary>
    public static IReadOnlyList<ServiceType> ScanFiles(
        IEnumerable<(string Path, string[]? TypeFilter)> files,
        TextWriter? logger = null)
    {
        var all = new List<ServiceType>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (path, filter) in files)
        {
            if (!File.Exists(path))
            {
                logger?.WriteLine($"[service-api] WARN: source not found, skipping: {path}");
                continue;
            }

            var types = Scan(path, logger);
            var allow = filter is { Length: > 0 }
                ? new HashSet<string>(filter, StringComparer.Ordinal)
                : null;

            foreach (var t in types)
            {
                if (allow is not null && !allow.Contains(t.Name)) continue;
                if (!seen.Add(t.Name)) continue; // first declaration wins; dedupe across files
                all.Add(t);
            }
        }

        return all.OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();
    }

    // --- Member collection ------------------------------------------------

    private static IEnumerable<ServiceProperty> CollectProperties(TypeDeclarationSyntax type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Positional record parameters surface as properties.
        if (type is RecordDeclarationSyntax { ParameterList: { } pl })
        {
            foreach (var p in pl.Parameters)
            {
                var pname = p.Identifier.Text;
                if (!seen.Add(pname)) continue;
                yield return new ServiceProperty(
                    Name: pname,
                    Type: p.Type?.ToString() ?? "object",
                    Default: p.Default?.Value.ToString(),
                    Summary: ExtractXmlSummary(p.GetLeadingTrivia()));
            }
        }

        foreach (var prop in type.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (!IsPublic(prop)) continue;
            var pname = prop.Identifier.Text;
            if (!seen.Add(pname)) continue;
            yield return new ServiceProperty(
                Name: pname,
                Type: prop.Type.ToString(),
                Default: prop.Initializer?.Value.ToString(),
                Summary: ExtractXmlSummary(prop.GetLeadingTrivia()));
        }

        // public const / static readonly fields are part of the consumer API
        // (e.g. OverlayService.BaseZIndex / Step) — surface them as properties.
        foreach (var field in type.Members.OfType<FieldDeclarationSyntax>())
        {
            if (!IsPublic(field)) continue;
            foreach (var v in field.Declaration.Variables)
            {
                var fname = v.Identifier.Text;
                if (!seen.Add(fname)) continue;
                yield return new ServiceProperty(
                    Name: fname,
                    Type: field.Declaration.Type.ToString(),
                    Default: v.Initializer?.Value.ToString(),
                    Summary: ExtractXmlSummary(field.GetLeadingTrivia()));
            }
        }
    }

    private static IEnumerable<ServiceMethod> CollectMethods(TypeDeclarationSyntax type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
        {
            // Interface members have no explicit accessibility modifier and are
            // public by default; class/record members must be explicitly public.
            var isInterface = type is InterfaceDeclarationSyntax;
            if (!isInterface && !IsPublic(method)) continue;

            var mname = method.Identifier.Text;
            var typeParams = method.TypeParameterList?.ToString() ?? "";
            var paramList = method.ParameterList.Parameters
                .Select(FormatParameter)
                .ToArray();
            var signature = $"{mname}{typeParams}({string.Join(", ", paramList)})";

            // Distinguish overloads so they don't collapse to one entry.
            if (!seen.Add(signature)) continue;

            yield return new ServiceMethod(
                Name: mname,
                ReturnType: method.ReturnType.ToString(),
                Signature: signature,
                Summary: ExtractXmlSummary(method.GetLeadingTrivia()));
        }
    }

    private static string FormatParameter(ParameterSyntax p)
    {
        var modifiers = p.Modifiers.Count > 0 ? p.Modifiers.ToString() + " " : "";
        var type = p.Type?.ToString() ?? "";
        var def = p.Default is not null ? " = " + p.Default.Value.ToString() : "";
        return $"{modifiers}{type} {p.Identifier.Text}{def}".Trim();
    }

    // --- Accessibility / shape helpers ------------------------------------

    private static bool IsPublic(MemberDeclarationSyntax m) =>
        m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword));

    private static bool IsNestedType(BaseTypeDeclarationSyntax decl) =>
        decl.Parent is TypeDeclarationSyntax;

    private static string? ResolveNamespace(SyntaxNode node)
    {
        for (var n = node.Parent; n is not null; n = n.Parent)
        {
            switch (n)
            {
                case FileScopedNamespaceDeclarationSyntax fs:
                    return fs.Name.ToString();
                case NamespaceDeclarationSyntax ns:
                    return ns.Name.ToString();
            }
        }
        return null;
    }

    // --- XML summary extraction (mirrors RazorParameterScanner) ------------

    private static string? ExtractXmlSummary(SyntaxTriviaList trivia)
    {
        var sb = new StringBuilder();
        foreach (var t in trivia)
        {
            if (t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                sb.AppendLine(t.ToFullString());
            }
        }
        if (sb.Length == 0) return null;

        var raw = sb.ToString();
        var m = Regex.Match(raw, @"<summary>(.*?)</summary>", RegexOptions.Singleline);
        if (!m.Success) return null;
        var inner = m.Groups[1].Value;
        inner = Regex.Replace(inner, @"^\s*///\s?", "", RegexOptions.Multiline);
        inner = Regex.Replace(inner, @"\s+", " ").Trim();
        inner = Regex.Replace(inner, @"<see\s+cref=""[^""]*?\.?([A-Za-z0-9_]+)""\s*/>", "$1");
        inner = Regex.Replace(inner, @"<see\s+cref=""([^""]+)""\s*/>", "$1");
        inner = Regex.Replace(inner, @"<[^>]+>", "");
        inner = Regex.Replace(inner, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(inner) ? null : inner;
    }
}
