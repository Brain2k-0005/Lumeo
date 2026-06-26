using System.Text.RegularExpressions;

namespace Lumeo.Cli;

/// <summary>
/// Rewrites the root <c>Lumeo</c> namespace of a vendored source file to the
/// consumer's target namespace. Razor files carry an <c>@namespace</c> directive;
/// C# files a <c>namespace</c> declaration. Only an exact <c>Lumeo</c> root (or a
/// <c>Lumeo.*</c> sub-namespace) is rewritten — a coincidental prefix like
/// <c>LumeoExtras</c> is left untouched. Files that are neither <c>.razor</c> nor
/// <c>.cs</c> pass through unchanged.
/// </summary>
internal static class NamespaceRewriter
{
    public static string Rewrite(string content, string filePath, string targetNamespace)
    {
        if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            // (?=\r?$) — assert end-of-line allowing an optional CR. A bare `$` in
            // Multiline matches the position *before* the \n, which on CRLF files
            // (Windows checkouts / `lumeo add --local`) sits AFTER the \r, so
            // `Lumeo$` never matched and the @namespace line was left as Lumeo —
            // breaking every vendored .razor on Windows. The zero-width lookahead
            // keeps the \r\n intact and still rejects `@namespace LumeoExtras`.
            content = Regex.Replace(content, @"^@namespace\s+Lumeo(\.[A-Za-z0-9_.]*)?(?=\r?$)",
                m => $"@namespace {targetNamespace}{m.Groups[1].Value}",
                RegexOptions.Multiline);
        }
        else if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            content = Regex.Replace(content, @"^namespace\s+Lumeo(\.[A-Za-z0-9_.]*)?(\s*[;{])",
                m => $"namespace {targetNamespace}{m.Groups[1].Value}{m.Groups[2].Value}",
                RegexOptions.Multiline);
        }
        return content;
    }
}
