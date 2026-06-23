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

    public sealed record ServiceEvent(
        string Name,
        string Type,
        string? Summary);

    public sealed record ServiceType(
        string Name,
        string Kind, // class | record | interface | enum | staticClass
        string? Namespace,
        string? Summary,
        ServiceProperty[] Properties,
        ServiceMethod[] Methods,
        ServiceEvent[] Events,
        ServiceEnumValue[] EnumValues)
    {
        /// <summary>Base type name as written in source (e.g. "OverlayOptions"),
        /// or null when none / framework-only. Used to merge inherited members
        /// from another scanned type within the same batch. Not serialized.</summary>
        public string? BaseTypeName { get; init; }

        /// <summary>Enclosing type name when this is a nested public type (e.g.
        /// "ViewportInfo" for ViewportInfo.Breakpoints), else null. Lets
        /// ScanFiles admit a nested type when its parent is allow-listed.
        /// Not serialized.</summary>
        public string? ParentName { get; init; }
    }

    /// <summary>
    /// Parse a single C# source file and return every public type declared in
    /// it. Always returns a (possibly empty) list — never throws. A file that
    /// fails to read or tokenize yields an empty result and a warning via
    /// <paramref name="logger"/> (when supplied), exactly like the Razor scanner.
    /// </summary>
    public static IReadOnlyList<ServiceType> Scan(string csharpPath, TextWriter? logger = null)
    {
        string text;
        // Normalize CRLF -> LF so extracted XML-doc text is platform-stable.
        try { text = File.ReadAllText(csharpPath).Replace("\r\n", "\n").Replace("\r", "\n"); }
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
            // Top-level public types AND nested public types (the latter are
            // part of the consumer-facing surface — e.g. ViewportInfo.Breakpoints
            // constants, ComponentInteropService.TextareaCaretInfo return types).
            // Nested types carry ParentName so ScanFiles can admit them when the
            // parent is allow-listed.
            if (!IsPublic(decl)) continue;

            var name = decl.Identifier.Text;
            if (!seen.Add(name)) continue;

            var parentName = (decl.Parent as BaseTypeDeclarationSyntax)?.Identifier.Text;
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
                        Events: Array.Empty<ServiceEvent>(),
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
                        Events: CollectEvents(rec).ToArray(),
                        EnumValues: Array.Empty<ServiceEnumValue>())
                    { BaseTypeName = ResolveBaseTypeName(rec) });
                    break;

                case InterfaceDeclarationSyntax iface:
                    results.Add(new ServiceType(
                        Name: name,
                        Kind: "interface",
                        Namespace: ns,
                        Summary: summary,
                        Properties: CollectProperties(iface).ToArray(),
                        Methods: CollectMethods(iface).ToArray(),
                        Events: CollectEvents(iface).ToArray(),
                        EnumValues: Array.Empty<ServiceEnumValue>())
                    { BaseTypeName = ResolveBaseTypeName(iface) });
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
                        Events: CollectEvents(cls).ToArray(),
                        EnumValues: Array.Empty<ServiceEnumValue>())
                    { BaseTypeName = ResolveBaseTypeName(cls) });
                    break;
            }

            // Tag the just-added type with its enclosing type (if nested) so
            // ScanFiles can admit it under an allow-listed parent.
            if (parentName is not null && results.Count > 0)
                results[^1] = results[^1] with { ParentName = parentName };
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
                // Admit a type when its own name is allow-listed, OR it's a
                // nested public type whose parent is allow-listed (so listing
                // "ViewportInfo" pulls in ViewportInfo.Breakpoints, etc.).
                if (allow is not null
                    && !allow.Contains(t.Name)
                    && !(t.ParentName is not null && allow.Contains(t.ParentName)))
                    continue;
                if (!seen.Add(t.Name)) continue; // first declaration wins; dedupe across files
                all.Add(t);
            }
        }

        // Merge inherited members: a derived options record (e.g.
        // SheetOverlayOptions : OverlayOptions) declares no members of its own,
        // so resolve its base chain within THIS scanned batch and fold the
        // base's properties + events in (base-first, then own; dedupe by name).
        // Only types present in the scan are resolved — framework bases are
        // left untouched.
        var byName = all.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var merged = all.Select(t => MergeInherited(t, byName)).ToList();

        return merged.OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Walk <paramref name="type"/>'s base chain (limited to types present in
    /// <paramref name="byName"/>) and prepend each base's properties and events,
    /// most-derived overriding less-derived by name. A small visited set guards
    /// against cyclic or self-referential base lists.
    /// </summary>
    private static ServiceType MergeInherited(
        ServiceType type,
        IReadOnlyDictionary<string, ServiceType> byName)
    {
        if (type.BaseTypeName is null) return type;

        var props = new List<ServiceProperty>(type.Properties);
        var events = new List<ServiceEvent>(type.Events);
        var ownPropNames = new HashSet<string>(type.Properties.Select(p => p.Name), StringComparer.Ordinal);
        var ownEventNames = new HashSet<string>(type.Events.Select(e => e.Name), StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { type.Name };

        var baseName = type.BaseTypeName;
        while (baseName is not null
            && visited.Add(baseName)
            && byName.TryGetValue(baseName, out var baseType))
        {
            // Base members come first; skip any already provided by a more
            // derived type so overrides win.
            foreach (var p in baseType.Properties)
            {
                if (ownPropNames.Add(p.Name)) props.Insert(0, p);
            }
            foreach (var e in baseType.Events)
            {
                if (ownEventNames.Add(e.Name)) events.Insert(0, e);
            }
            baseName = baseType.BaseTypeName;
        }

        return type with { Properties = props.ToArray(), Events = events.ToArray() };
    }

    // --- Member collection ------------------------------------------------

    private static IEnumerable<ServiceProperty> CollectProperties(TypeDeclarationSyntax type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Interface members carry no explicit accessibility modifier and are
        // public by default; class/record members must be explicitly public.
        var isInterface = type is InterfaceDeclarationSyntax;

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
            if (!isInterface && !IsPublic(prop)) continue;
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
            if (!isInterface && !IsPublic(field)) continue;
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

    private static IEnumerable<ServiceEvent> CollectEvents(TypeDeclarationSyntax type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Interface members are public by default; class/record events must be
        // explicitly public to count as consumer-facing API.
        var isInterface = type is InterfaceDeclarationSyntax;

        // `event Action? OnShow;` — one declaration may hold several names.
        foreach (var evt in type.Members.OfType<EventFieldDeclarationSyntax>())
        {
            if (!isInterface && !IsPublic(evt)) continue;
            var typeName = evt.Declaration.Type.ToString();
            var summary = ExtractXmlSummary(evt.GetLeadingTrivia());
            foreach (var v in evt.Declaration.Variables)
            {
                var ename = v.Identifier.Text;
                if (!seen.Add(ename)) continue;
                yield return new ServiceEvent(ename, typeName, summary);
            }
        }

        // `event Action OnShow { add { } remove { } }` — explicit accessors.
        foreach (var evt in type.Members.OfType<EventDeclarationSyntax>())
        {
            if (!isInterface && !IsPublic(evt)) continue;
            var ename = evt.Identifier.Text;
            if (!seen.Add(ename)) continue;
            yield return new ServiceEvent(
                ename,
                evt.Type.ToString(),
                ExtractXmlSummary(evt.GetLeadingTrivia()));
        }
    }

    /// <summary>
    /// First base type listed in a declaration's base list, stripped of any
    /// generic argument list. Returns null when there is no base list. The
    /// caller decides whether the name matches another scanned type before
    /// merging inherited members — implemented interfaces are listed here too,
    /// but those simply won't resolve to a scanned <em>base record/class</em>.
    /// </summary>
    private static string? ResolveBaseTypeName(TypeDeclarationSyntax type)
    {
        var first = type.BaseList?.Types.FirstOrDefault()?.Type;
        return first switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => null,
        };
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
