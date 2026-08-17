using System.Text;
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
///   2. A REAL TYPE REFERENCE in the file's code. Everything that is not
///      code is blanked first by a single left-to-right scanner: C#
///      block/line/XML-doc comments, Razor <c>@* *@</c> comments, and the
///      text of every string and char literal. So neither prose mentions
///      — a doc comment noting "same pattern already fixed for
///      Sheet/Drawer/Dialog" — nor quoted words — an expected button caption,
///      a bUnit parameter NAME passed as a string, a component name inside
///      an embedded JSON payload — ever count. Interpolation holes are
///      exempt in every literal form, raw ones included:
///      <c>$"{typeof(Sheet).Name}"</c> is code, not text (and a literal
///      nested back inside such a hole is text again):
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

        var codeOnly = StripNonCode(fileContent);

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

    /// <summary>
    /// Blank out everything in a test file that is NOT code — comments and literal text —
    /// leaving the code, and the holes inside interpolated strings, to be scanned.
    ///
    /// One left-to-right pass, because the alternative (a pipeline of regexes, one per
    /// construct) cannot express "whichever opens FIRST wins", and that is exactly where it
    /// breaks: stripping line comments before literals loses the reference in
    /// <c>var sep = "//"; Render&lt;Lumeo.Sheet&gt;();</c>, while stripping literals first
    /// eats a commented-out string's opening quote and runs on to the next one. A scanner
    /// has no ordering to get wrong.
    ///
    /// Newlines survive so the result stays line-aligned with the input.
    /// </summary>
    private static string StripNonCode(string text)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            // Razor comment. .razor test files are scanned too, and @* *@ is not a C# block
            // comment: a "(state-on-data-change, Gantt-class)" note in a Scrollspy host was
            // published as Gantt coverage until this case existed.
            if (c == '@' && Next(text, i) == '*')
            {
                i = BlankTo(sb, text, i, Find(text, "*@", i + 2, after: true));
                continue;
            }
            if (c == '/' && Next(text, i) == '/')
            {
                var nl = text.IndexOf('\n', i);
                i = BlankTo(sb, text, i, nl < 0 ? text.Length : nl);
                continue;
            }
            if (c == '/' && Next(text, i) == '*')
            {
                i = BlankTo(sb, text, i, Find(text, "*/", i + 2, after: true));
                continue;
            }
            // A char literal, so that '"' does not open a string and swallow the file — but
            // only when it really is one. .razor hosts are scanned too, and an apostrophe in
            // ordinary markup text (`<p>don't</p><Lumeo.Sheet />`) read as an unterminated
            // literal blanked the rest of the line, taking a real component tag with it.
            // Recognising Razor code regions properly would mean parsing Razor; the literal's
            // own shape is the cheaper and sufficient discriminator.
            if (c == '\'' && IsCharLiteral(text, i))
            {
                i = BlankTo(sb, text, i, EndOfCharLiteral(text, i));
                continue;
            }
            if (c is '"' or '$' or '@')
            {
                var (quote, dollars, verbatim) = ReadStringPrefix(text, i);
                if (quote >= 0)
                {
                    i = AppendStringLiteral(sb, text, i, quote, dollars, verbatim);
                    continue;
                }
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static char Next(string text, int i) => i + 1 < text.Length ? text[i + 1] : '\0';

    private static int Find(string text, string needle, int from, bool after)
    {
        var at = text.IndexOf(needle, Math.Min(from, text.Length), StringComparison.Ordinal);
        return at < 0 ? text.Length : (after ? at + needle.Length : at);
    }

    /// <summary>Reads an optional <c>$</c>/<c>@</c> prefix run starting at <paramref name="i"/>.
    /// Returns the index of the opening quote, or -1 when this is not a string literal at all
    /// (a lone <c>@</c> before an identifier, a <c>$</c> in some other position).</summary>
    private static (int quote, int dollars, bool verbatim) ReadStringPrefix(string text, int i)
    {
        var j = i;
        var dollars = 0;
        var verbatim = false;
        while (j < text.Length && (text[j] == '$' || text[j] == '@'))
        {
            if (text[j] == '$') dollars++; else verbatim = true;
            j++;
        }
        return j < text.Length && text[j] == '"' ? (j, dollars, verbatim) : (-1, 0, false);
    }

    /// <summary>True when the quote at <paramref name="start"/> opens something shaped like a
    /// C# char literal: a single character, or a backslash escape, closed before the line ends.
    /// Anything longer is prose.</summary>
    private static bool IsCharLiteral(string text, int start)
    {
        var i = start + 1;
        if (i >= text.Length) return false;

        if (text[i] == '\\')
        {
            // '\\n', '\\'', '\\u1234' — eight characters inside the quotes is the longest legal form.
            for (var j = i + 1; j < text.Length && j <= i + 8; j++)
            {
                if (text[j] == '\'') return true;
                if (text[j] == '\n') return false;
            }
            return false;
        }

        return text[i] != '\n' && i + 1 < text.Length && text[i + 1] == '\'';
    }

    private static int EndOfCharLiteral(string text, int start)
    {
        var i = start + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\') { i += 2; continue; }
            if (text[i] == '\'') return i + 1;
            if (text[i] == '\n') return i;          // unterminated — do not run past the line
            i++;
        }
        return text.Length;
    }

    /// <summary>Appends one string literal, blanked except for its interpolation holes, and
    /// returns the index just past it.</summary>
    private static int AppendStringLiteral(StringBuilder sb, string text, int start, int quote,
                                           int dollars, bool verbatim)
    {
        BlankTo(sb, text, start, quote);           // the $ / @ prefix itself

        var fence = 0;
        while (quote + fence < text.Length && text[quote + fence] == '"') fence++;
        // Raw strings open with three or more quotes and never carry the verbatim @.
        var raw = !verbatim && fence >= 3;
        var delimiter = raw ? fence : 1;

        BlankTo(sb, text, quote, quote + delimiter);
        var i = quote + delimiter;

        while (i < text.Length)
        {
            // Closing delimiter?
            if (text[i] == '"')
            {
                if (raw)
                {
                    var run = 0;
                    while (i + run < text.Length && text[i + run] == '"') run++;
                    if (run >= delimiter) return BlankTo(sb, text, i, i + run);
                    BlankTo(sb, text, i, i + run);
                    i += run;
                    continue;
                }
                if (verbatim && Next(text, i) == '"') { BlankTo(sb, text, i, i + 2); i += 2; continue; }
                return BlankTo(sb, text, i, i + 1);
            }
            if (!raw && !verbatim && text[i] == '\\') { BlankTo(sb, text, i, Math.Min(i + 2, text.Length)); i += 2; continue; }

            if (dollars > 0 && text[i] == '{')
            {
                var run = 0;
                while (i + run < text.Length && text[i + run] == '{') run++;
                // PARITY, not "a run of exactly two": every even-length run is escaped text
                // all the way down, so $"{{{{Sheet}}}}" is literal and must not be read as a
                // hole (Codex review round 4). One hole costs `dollars` braces, so the run
                // opens a hole when it contains an odd number of them.
                var opensHole = run >= dollars && (run / dollars) % 2 == 1;
                if (opensHole)
                {
                    var end = EndOfHole(text, i + dollars);
                    BlankTo(sb, text, i, i + dollars);
                    // The hole is code, so it gets the same treatment rather than being kept
                    // verbatim — a literal nested inside it is still text.
                    sb.Append(StripNonCode(text[(i + dollars)..end]));
                    i = end;
                    continue;
                }
                BlankTo(sb, text, i, i + run);
                i += run;
                continue;
            }

            BlankTo(sb, text, i, i + 1);
            i++;
        }

        return text.Length;
    }

    /// <summary>Scans from just inside a hole to its closing brace, skipping over literals so a
    /// brace inside one does not close it early.</summary>
    private static int EndOfHole(string text, int start)
    {
        var depth = 1;
        var i = start;
        while (i < text.Length)
        {
            var c = text[i];
            // Comments first, with the same precedence StripNonCode gives them: a brace inside
            // one is text, and reading it as the hole's terminator blanked the expression that
            // followed — `$"{ /* } */ typeof(Lumeo.Sheet) }"` lost its reference.
            if (c == '/' && Next(text, i) == '/')
            {
                var nl = text.IndexOf('\n', i);
                i = nl < 0 ? text.Length : nl;
                continue;
            }
            if (c == '/' && Next(text, i) == '*') { i = Find(text, "*/", i + 2, after: true); continue; }
            if (c == '\'' && IsCharLiteral(text, i)) { i = EndOfCharLiteral(text, i); continue; }
            if (c is '"' or '$' or '@')
            {
                var (quote, _, verbatim) = ReadStringPrefix(text, i);
                if (quote >= 0) { i = EndOfString(text, quote, verbatim); continue; }
            }
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
            i++;
        }
        return text.Length;
    }

    /// <summary>End of a literal whose opening quote is at <paramref name="quote"/>, used only to
    /// skip past it.</summary>
    private static int EndOfString(string text, int quote, bool verbatim)
    {
        var fence = 0;
        while (quote + fence < text.Length && text[quote + fence] == '"') fence++;
        if (!verbatim && fence >= 3)
        {
            var close = quote + fence;
            while (close < text.Length)
            {
                if (text[close] == '"')
                {
                    var run = 0;
                    while (close + run < text.Length && text[close + run] == '"') run++;
                    if (run >= fence) return close + run;
                    close += run;
                    continue;
                }
                close++;
            }
            return text.Length;
        }

        var i = quote + 1;
        while (i < text.Length)
        {
            if (!verbatim && text[i] == '\\') { i += 2; continue; }
            if (text[i] == '"')
            {
                if (verbatim && Next(text, i) == '"') { i += 2; continue; }
                return i + 1;
            }
            i++;
        }
        return text.Length;
    }

    /// <summary>Appends text[from..to) as blanks (newlines kept) and returns <paramref name="to"/>.</summary>
    private static int BlankTo(StringBuilder sb, string text, int from, int to)
    {
        to = Math.Min(to, text.Length);
        for (var i = from; i < to; i++) sb.Append(text[i] is '\n' or '\r' ? text[i] : ' ');
        return to;
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
