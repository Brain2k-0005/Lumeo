using System.Text.RegularExpressions;

namespace Lumeo.RegistryGen;

/// <summary>
/// Single source of truth for "does this test file exercise this component?" —
/// the question behind the public registry/MCP per-component "tests" metadata
/// (see <see cref="PerComponentEnricher"/>, section 9).
///
/// This exact question has been patched piecemeal across several review waves —
/// comment stripping, a LINQ <c>.Select(...)</c> collision, suffixed test-class
/// ids, and (most recently) a sibling-prefix collision ("Input" claiming
/// "InputMask"/"InputFile" mentions) plus a member-access collision ("Text"
/// claiming every bUnit <c>.TextContent</c> assertion) — and each prior patch
/// silently dropped or bypassed an earlier one (the comment-stripping wave
/// deleted the LINQ-collision guard entirely; the suffix-lookahead wave then
/// reintroduced an even broader version of the same class of bug). This type
/// replaces all of that with ONE explicit contract, pinned down by a focused
/// unit-test matrix in ComponentTestMatcherTests covering every known
/// false-positive/false-negative case from those waves.
///
/// CONTRACT — a test file counts as covering <c>componentName</c> when, and
/// only when, one of:
///
///   1. DEDICATED FOLDER OWNERSHIP. The file's repo-relative path has a
///      directory segment that case-insensitively equals componentName
///      exactly — this repo's established one-folder-per-component
///      convention (e.g. tests/Lumeo.Tests/Components/Sheet/SheetTests.cs
///      owns "Sheet"). No content scanning needed or wanted: a file placed
///      in a component's own folder is coverage for that component by
///      construction.
///
///   2. A REAL TYPE REFERENCE in the file's code (block/line/XML-doc
///      comments stripped first, so prose mentions — e.g. a doc comment
///      noting "same pattern already fixed for Sheet/Drawer/Dialog" — never
///      count):
///
///        a) An EXACT identifier match (word boundary on both sides) that is
///           not a member/property/method access on some unrelated receiver.
///           A match preceded by '.' only counts when the qualifier
///           immediately before the dot is exactly "Lumeo" or "L" (this
///           codebase's `using L = Lumeo;` alias convention for referencing
///           component types) — so `Render&lt;Lumeo.Sheet&gt;`/`L.Sheet`
///           count, but `cut.TextContent` and `items.Select(...)` do not
///           (the qualifier there is a local variable, not the namespace).
///           A bare match that opens its OWN generic argument list
///           (`List&lt;bool&gt; field`) also doesn't count — for a component
///           name colliding with a BCL/framework generic (List, Stack, ...)
///           that's the framework type, not the component — UNLESS the
///           match itself sits inside an enclosing generic argument list
///           (`Render&lt;DataGrid&lt;Person&gt;&gt;`), which is left alone.
///
///        b) A SUFFIXED TEST IDENTIFIER: the file's own name stem, or a
///           `class` name declared in the file, starts with componentName
///           immediately followed by another uppercase letter (a PascalCase
///           segment boundary) — e.g. "DataGridSmokeTests" /
///           "SelectInteractionTests". Only counts when componentName is the
///           LONGEST name in knownComponentNames that is itself a valid
///           prefix of that identifier this same way — so
///           "InputMaskDisplayTests" is coverage for "InputMask", never for
///           the shorter, unrelated sibling "Input" — AND the file does not
///           live under a "Services" folder (DataGridExportServiceTests.cs,
///           ToastServiceTests.cs, ... suffix-match the component they're
///           scoped to by naming convention, but exercise the Service class,
///           not the component; a real reference inside them still counts
///           via 2a above).
/// </summary>
public static class ComponentTestMatcher
{
    private static readonly Regex BlockComment = new(@"/\*[\s\S]*?\*/", RegexOptions.Compiled);
    private static readonly Regex LineComment = new(@"//[^\n]*", RegexOptions.Compiled);
    private static readonly Regex ClassNameRegex = new(@"\bclass\s+(\w+)", RegexOptions.Compiled);

    /// <summary>
    /// True when the given test file (identified by its repo-relative path
    /// and raw content) counts as coverage for componentName under the
    /// contract documented on this type.
    /// </summary>
    public static bool IsCoverage(
        string componentName,
        string repoRelativePath,
        string fileContent,
        IReadOnlyCollection<string> knownComponentNames)
    {
        if (OwnsDedicatedFolder(repoRelativePath, componentName)) return true;

        var codeOnly = StripComments(fileContent);

        if (HasRealTypeReference(codeOnly, componentName)) return true;

        // The suffix fallback only fires for files OUTSIDE a "Services" folder.
        // This repo's convention names service test files after the component
        // they're scoped to (DataGridExportServiceTests.cs, DataGridLayoutServiceTests.cs,
        // DataGridServerServiceTests.cs, ToastServiceTests.cs, ...), which suffix-matches
        // the component name by construction — but those files exercise a Service
        // class, not the component itself, and a real component reference inside
        // them (if any) is still picked up by the 2a content check above. Living
        // in "Services" is the "another component signal" a suffix match alone
        // lacks: it's the established convention this codebase already uses to
        // mark "not a dedicated component test folder" (mirrors OwnsDedicatedFolder).
        if (IsUnderServicesFolder(repoRelativePath)) return false;

        var stem = PathStem(repoRelativePath);
        if (IsLongestSuffixedMatch(stem, componentName, knownComponentNames)) return true;

        foreach (Match m in ClassNameRegex.Matches(codeOnly))
        {
            if (IsLongestSuffixedMatch(m.Groups[1].Value, componentName, knownComponentNames)) return true;
        }

        return false;
    }

    // ----- (1) dedicated folder ownership -----

    private static bool OwnsDedicatedFolder(string repoRelativePath, string componentName)
    {
        var segments = repoRelativePath.Replace('\\', '/').Split('/');
        // Last segment is the file name itself — only directory segments count.
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (string.Equals(segments[i], componentName, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool IsUnderServicesFolder(string repoRelativePath)
    {
        var segments = repoRelativePath.Replace('\\', '/').Split('/');
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (string.Equals(segments[i], "Services", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // ----- (2a) real type reference in code -----

    private static string StripComments(string text)
    {
        var codeOnly = BlockComment.Replace(text, " ");
        return LineComment.Replace(codeOnly, " ");
    }

    private static bool HasRealTypeReference(string codeOnly, string componentName)
    {
        var regex = new Regex(@"\b" + Regex.Escape(componentName) + @"\b");
        foreach (Match m in regex.Matches(codeOnly))
        {
            if (m.Index == 0 || codeOnly[m.Index - 1] != '.')
            {
                // A bare match immediately opening its OWN generic argument list
                // ("List<bool> field", "Dictionary<string,int>") is a local type
                // DECLARATION — for a component whose name collides with a BCL/
                // framework generic (List, Stack, Queue, ...) that is near-always
                // the colliding framework type, not the Lumeo component. The one
                // legitimate bare-generic shape in this codebase is a generic
                // Lumeo component used AS a type argument to another generic call
                // (Render<DataGrid<Person>>) — there the match itself sits INSIDE
                // somebody else's argument list (immediately preceded by '<' or
                // ',', skipping whitespace), which this guard leaves untouched.
                if (IsBareGenericTypeDeclaration(codeOnly, m.Index, m.Length)) continue;
                return true; // bare word / generic arg / ctor — real.
            }
            if (IsQualifiedByLumeoAlias(codeOnly, m.Index)) return true; // Lumeo.X / L.X — real.
            // else: member/property/method access on some other receiver (cut.TextContent,
            // items.Select(...)) — keep scanning, this particular match doesn't count.
        }
        return false;
    }

    /// <summary>True when the match at [matchIndex, matchIndex+matchLength) is
    /// immediately followed by '&lt;' (it opens its own generic argument list)
    /// and is NOT itself sitting inside an enclosing generic argument list
    /// (i.e. not immediately preceded — modulo whitespace — by '&lt;' or ',').</summary>
    private static bool IsBareGenericTypeDeclaration(string text, int matchIndex, int matchLength)
    {
        var end = matchIndex + matchLength;
        if (end >= text.Length || text[end] != '<') return false;

        var i = matchIndex - 1;
        while (i >= 0 && char.IsWhiteSpace(text[i])) i--;
        return i < 0 || (text[i] != '<' && text[i] != ',');
    }

    /// <summary>text[dotIndex - 1] is the '.' immediately before the match at dotIndex.
    /// True when the identifier immediately preceding that dot is exactly "Lumeo" or "L".</summary>
    private static bool IsQualifiedByLumeoAlias(string text, int dotIndex)
    {
        var dot = dotIndex - 1;
        var start = dot;
        while (start > 0 && IsIdentifierChar(text[start - 1])) start--;
        var qualifier = text.Substring(start, dot - start);
        return qualifier is "Lumeo" or "L";
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    // ----- (2b) suffixed test identifier -----

    private static string PathStem(string repoRelativePath)
    {
        var fileName = repoRelativePath.Replace('\\', '/').Split('/')[^1];
        var dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
    }

    private static bool IsLongestSuffixedMatch(string identifier, string componentName,
        IReadOnlyCollection<string> knownComponentNames)
    {
        if (!IsSuffixedPrefix(identifier, componentName)) return false;
        foreach (var other in knownComponentNames)
        {
            if (other.Length > componentName.Length && IsSuffixedPrefix(identifier, other)) return false;
        }
        return true;
    }

    /// <summary>True when identifier equals name, or starts with name immediately
    /// followed by an uppercase letter (a new PascalCase segment).</summary>
    private static bool IsSuffixedPrefix(string identifier, string name)
    {
        if (!identifier.StartsWith(name, StringComparison.Ordinal)) return false;
        return identifier.Length == name.Length || char.IsUpper(identifier[name.Length]);
    }
}
