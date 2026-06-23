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
            content = Regex.Replace(content, @"^@namespace\s+Lumeo(\.[A-Za-z0-9_.]*)?$",
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
