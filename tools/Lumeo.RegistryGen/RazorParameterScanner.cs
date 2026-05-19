using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lumeo.RegistryGen;

/// <summary>
/// Extracts parameter / enum / record / event metadata from a Razor (.razor) component
/// by parsing the @code { ... } block with Roslyn. Emits a structured schema record
/// per file. Intentionally lossy: fails soft (returns a thin entry) on weird input
/// rather than crashing the generator.
/// </summary>
public static class RazorParameterScanner
{
    public sealed record ParameterInfo(
        string Name,
        string Type,
        string? Default,
        string? Description,
        bool IsCascading,
        bool CaptureUnmatched);

    public sealed record EventInfo(
        string Name,
        string Type,
        string? Description);

    public sealed record EnumInfo(
        string Name,
        string[] Values,
        string? Description);

    public sealed record RecordInfo(
        string Name,
        string Signature,
        string? Description);

    public sealed record RazorFileSchema(
        string FileName,
        string ComponentName,
        string? Namespace,
        string? InheritsFrom,
        string[] Implements,
        ParameterInfo[] Parameters,
        EventInfo[] Events,
        EnumInfo[] Enums,
        RecordInfo[] Records,
        bool ParseFailed,
        string? ParseError);

    private static readonly Regex CodeBlockRegex = new(
        @"@code\s*\{",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex NamespaceRegex = new(
        @"^\s*@namespace\s+([^\s]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex InheritsRegex = new(
        @"^\s*@inherits\s+([^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex ImplementsRegex = new(
        @"^\s*@implements\s+([^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Parse a single .razor file. Always returns a schema (with ParseFailed=true on failure).
    /// </summary>
    public static RazorFileSchema Scan(string razorPath)
    {
        var componentName = Path.GetFileNameWithoutExtension(razorPath);
        string text;
        try { text = File.ReadAllText(razorPath); }
        catch (Exception ex)
        {
            return new RazorFileSchema(
                FileName: Path.GetFileName(razorPath),
                ComponentName: componentName,
                Namespace: null,
                InheritsFrom: null,
                Implements: Array.Empty<string>(),
                Parameters: Array.Empty<ParameterInfo>(),
                Events: Array.Empty<EventInfo>(),
                Enums: Array.Empty<EnumInfo>(),
                Records: Array.Empty<RecordInfo>(),
                ParseFailed: true,
                ParseError: $"read failed: {ex.Message}");
        }

        var ns = NamespaceRegex.Match(text) is { Success: true } nsm ? nsm.Groups[1].Value.Trim() : null;
        var inherits = InheritsRegex.Match(text) is { Success: true } im ? im.Groups[1].Value.Trim() : null;
        var implementsList = ImplementsRegex.Matches(text)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var codeBlocks = ExtractCodeBlocks(text);
        if (codeBlocks.Count == 0)
        {
            // No @code block — markup-only razor (e.g. layouts). Not an error.
            return new RazorFileSchema(
                FileName: Path.GetFileName(razorPath),
                ComponentName: componentName,
                Namespace: ns,
                InheritsFrom: inherits,
                Implements: implementsList,
                Parameters: Array.Empty<ParameterInfo>(),
                Events: Array.Empty<EventInfo>(),
                Enums: Array.Empty<EnumInfo>(),
                Records: Array.Empty<RecordInfo>(),
                ParseFailed: false,
                ParseError: null);
        }

        var parameters = new List<ParameterInfo>();
        var events = new List<EventInfo>();
        var enums = new List<EnumInfo>();
        var records = new List<RecordInfo>();
        var seenParams = new HashSet<string>(StringComparer.Ordinal);
        var seenEvents = new HashSet<string>(StringComparer.Ordinal);
        var seenEnums = new HashSet<string>(StringComparer.Ordinal);
        var seenRecords = new HashSet<string>(StringComparer.Ordinal);
        var anyClassRecovered = false;

        // Parse each @code block on its own. A component may split members
        // across blocks (FileManager keeps [Parameter]s in the first block,
        // helper types in the last), and a block holding a markup-bearing
        // RenderFragment (Checkbox's first block) is not valid standalone C#.
        // Concatenating blocks corrupts the latter; parsing independently and
        // merging lets each block contribute whatever it validly yields.
        foreach (var codeBlock in codeBlocks)
        {
            // Wrap in a fake class so Roslyn can parse it as a normal compilation unit.
            // `partial` permits any member visibility/duplicates we may inadvertently introduce.
            var wrapped = $"using System; using System.Collections.Generic; using System.Threading.Tasks; using Microsoft.AspNetCore.Components; using Microsoft.AspNetCore.Components.Web;\npublic partial class _DocWrapper {{\n{codeBlock}\n}}";

            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(wrapped);
            }
            catch
            {
                // A markup-bearing block Roslyn can't tokenize — other blocks may still carry params.
                continue;
            }

            var classDecl = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl is null) continue;
            anyClassRecovered = true;

            foreach (var prop in classDecl.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                var attrs = prop.AttributeLists.SelectMany(al => al.Attributes).ToArray();
                var paramAttr = attrs.FirstOrDefault(a => IsAttr(a, "Parameter"));
                var cascadingAttr = attrs.FirstOrDefault(a => IsAttr(a, "CascadingParameter"));
                if (paramAttr is null && cascadingAttr is null) continue;

                var name = prop.Identifier.Text;
                if (!seenParams.Add(name)) continue;
                var type = prop.Type.ToString();
                var defaultValue = prop.Initializer?.Value.ToString();
                var description = ExtractXmlSummary(prop.GetLeadingTrivia());

                // Detect [Parameter(CaptureUnmatchedValues = true)]
                var captureUnmatched = false;
                if (paramAttr?.ArgumentList is { } al)
                {
                    foreach (var arg in al.Arguments)
                    {
                        if (arg.NameEquals?.Name.Identifier.Text == "CaptureUnmatchedValues"
                            && arg.Expression.ToString().Equals("true", StringComparison.OrdinalIgnoreCase))
                        {
                            captureUnmatched = true;
                        }
                    }
                }

                parameters.Add(new ParameterInfo(
                    Name: name,
                    Type: type,
                    Default: defaultValue,
                    Description: description,
                    IsCascading: cascadingAttr is not null,
                    CaptureUnmatched: captureUnmatched));

                // EventCallback<T> or EventCallback → also surface as event for convenience
                if (type.StartsWith("EventCallback", StringComparison.Ordinal) && seenEvents.Add(name))
                {
                    events.Add(new EventInfo(name, type, description));
                }
            }

            // Enums
            foreach (var en in classDecl.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                if (!seenEnums.Add(en.Identifier.Text)) continue;
                var values = en.Members.Select(m => m.Identifier.Text).ToArray();
                var desc = ExtractXmlSummary(en.GetLeadingTrivia());
                enums.Add(new EnumInfo(en.Identifier.Text, values, desc));
            }

            // Records (positional or block-bodied)
            foreach (var rec in classDecl.DescendantNodes().OfType<RecordDeclarationSyntax>())
            {
                if (!seenRecords.Add(rec.Identifier.Text)) continue;
                // Build a one-line signature: "DialogContext(string TitleId, string DescriptionId, bool IsOpen)"
                var sig = rec.Identifier.Text;
                if (rec.ParameterList is not null) sig += rec.ParameterList.ToString();
                var desc = ExtractXmlSummary(rec.GetLeadingTrivia());
                records.Add(new RecordInfo(rec.Identifier.Text, sig, desc));
            }
        }

        if (!anyClassRecovered)
        {
            return Failed(razorPath, componentName, ns, inherits, implementsList,
                "no class declaration recovered from any @code block");
        }

        return new RazorFileSchema(
            FileName: Path.GetFileName(razorPath),
            ComponentName: componentName,
            Namespace: ns,
            InheritsFrom: inherits,
            Implements: implementsList,
            Parameters: parameters.ToArray(),
            Events: events.ToArray(),
            Enums: enums.ToArray(),
            Records: records.ToArray(),
            ParseFailed: false,
            ParseError: null);
    }

    private static RazorFileSchema Failed(string razorPath, string componentName, string? ns, string? inherits, string[] implementsList, string error)
        => new(
            FileName: Path.GetFileName(razorPath),
            ComponentName: componentName,
            Namespace: ns,
            InheritsFrom: inherits,
            Implements: implementsList,
            Parameters: Array.Empty<ParameterInfo>(),
            Events: Array.Empty<EventInfo>(),
            Enums: Array.Empty<EnumInfo>(),
            Records: Array.Empty<RecordInfo>(),
            ParseFailed: true,
            ParseError: error);

    private static bool IsAttr(AttributeSyntax a, string targetName)
    {
        var name = a.Name.ToString();
        // Trim trailing "Attribute" if present, and any namespace prefix
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0) name = name.Substring(lastDot + 1);
        if (name.EndsWith("Attribute", StringComparison.Ordinal))
            name = name.Substring(0, name.Length - "Attribute".Length);
        return string.Equals(name, targetName, StringComparison.Ordinal);
    }

    private static string? ExtractXmlSummary(SyntaxTriviaList trivia)
    {
        // Collect ///-prefixed comment lines and pull text out of <summary>...</summary>
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
        // Pull text inside <summary>...</summary>
        var m = Regex.Match(raw, @"<summary>(.*?)</summary>", RegexOptions.Singleline);
        if (!m.Success) return null;
        var inner = m.Groups[1].Value;
        // Strip leading "/// " and collapse whitespace
        inner = Regex.Replace(inner, @"^\s*///\s?", "", RegexOptions.Multiline);
        inner = Regex.Replace(inner, @"\s+", " ").Trim();
        // Strip simple <see cref="..."/> -> Foo
        inner = Regex.Replace(inner, @"<see\s+cref=""[^""]*?\.?([A-Za-z0-9_]+)""\s*/>", "$1");
        inner = Regex.Replace(inner, @"<see\s+cref=""([^""]+)""\s*/>", "$1");
        // Strip any remaining tags
        inner = Regex.Replace(inner, @"<[^>]+>", "");
        inner = Regex.Replace(inner, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(inner) ? null : inner;
    }

    /// <summary>
    /// Return the body of EVERY `@code { ... }` block in the file, in source
    /// order. Each is parsed independently by the caller — taking only the
    /// last block silently dropped all 14 of FileManager's parameters, while
    /// concatenating blocks corrupted components whose earlier block holds a
    /// markup-bearing RenderFragment (e.g. Checkbox).
    /// </summary>
    private static List<string> ExtractCodeBlocks(string text)
    {
        var blocks = new List<string>();
        foreach (Match m in CodeBlockRegex.Matches(text))
        {
            var body = ExtractBraceBody(text, m.Index + m.Length - 1);
            if (body is not null) blocks.Add(body);
        }
        return blocks;
    }

    /// <summary>
    /// Return the text strictly between the brace at <paramref name="openBraceIdx"/>
    /// and its matching close. Brace-matching honors strings, chars, and
    /// // / /* comments. Returns null if unbalanced.
    /// </summary>
    private static string? ExtractBraceBody(string text, int openBraceIdx)
    {
        // Walk forward from openBraceIdx + 1 until matching close
        int depth = 1;
        int i = openBraceIdx + 1;
        while (i < text.Length && depth > 0)
        {
            char c = text[i];

            // Line comment
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }
            // Block comment
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i += 2;
                continue;
            }
            // Verbatim string @"..."
            if (c == '@' && i + 1 < text.Length && text[i + 1] == '"')
            {
                i += 2;
                while (i < text.Length)
                {
                    if (text[i] == '"' && (i + 1 >= text.Length || text[i + 1] != '"')) { i++; break; }
                    if (text[i] == '"' && i + 1 < text.Length && text[i + 1] == '"') { i += 2; continue; }
                    i++;
                }
                continue;
            }
            // Interpolated $"..." (best-effort — ignore nested {})
            if (c == '$' && i + 1 < text.Length && text[i + 1] == '"')
            {
                i += 2;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length) { i += 2; continue; }
                    i++;
                }
                i++;
                continue;
            }
            // Regular string
            if (c == '"')
            {
                i++;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length) { i += 2; continue; }
                    i++;
                }
                i++;
                continue;
            }
            // Char literal
            if (c == '\'')
            {
                i++;
                while (i < text.Length && text[i] != '\'')
                {
                    if (text[i] == '\\' && i + 1 < text.Length) { i += 2; continue; }
                    i++;
                }
                i++;
                continue;
            }
            if (c == '{') { depth++; i++; continue; }
            if (c == '}')
            {
                depth--;
                if (depth == 0) return text.Substring(openBraceIdx + 1, i - openBraceIdx - 1);
                i++;
                continue;
            }
            i++;
        }
        return null;
    }
}
