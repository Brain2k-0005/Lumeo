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
///      nested back inside such a hole is text again).
///
///      A <c>.razor</c> file is scanned as what it is: MARKUP regions
///      alternating with C# CODE regions (<c>@code { }</c>,
///      <c>@functions { }</c>, <c>@{ }</c>). The same character means
///      different things on each side — in markup, a quote after '=' opens
///      an attribute value whose text is not code (except the
///      <c>@</c>-expressions embedded in it, which are), and an apostrophe
///      is punctuation; in a code region those quotes delimit ordinary C#
///      literals whose content is text throughout, and an apostrophe opens
///      a char literal:
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

        var codeOnly = StripNonCode(fileContent,
            razor: repoRelativePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase));

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
    /// A <c>.razor</c> file is scanned as what it is: an alternation of MARKUP regions and
    /// C# CODE regions (<c>@code { }</c>, <c>@functions { }</c>, <c>@{ }</c>). The distinction
    /// is not cosmetic — the same character means different things on each side. In markup an
    /// attribute value is delimited by <c>"</c> or <c>'</c> and its content is text, except for
    /// the <c>@</c>-expressions embedded in it, which are code. In a code region those same
    /// quotes delimit ordinary C# literals whose content is text all the way through, and an
    /// apostrophe is a char literal rather than punctuation in a sentence.
    ///
    /// Newlines survive so the result stays line-aligned with the input.
    /// </summary>
    private static string StripNonCode(string text, bool razor = false)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;
        // A .cs file is code from the first character. A .razor file starts in markup and
        // enters code only through an explicit block.
        var inCode = !razor;
        var codeDepth = 0;
        // Inside a markup tag, element and attribute NAMES are what a reference looks like;
        // outside one, the same characters are prose.
        var inTag = false;
        // Set while the tag being read opens a raw-text element, so its body can be blanked.
        string? rawText = null;

        while (i < text.Length)
        {
            var c = text[i];

            // Razor comment. @* *@ is not a C# block comment, and a "(state-on-data-change,
            // Gantt-class)" note in a Scrollspy host was published as Gantt coverage.
            if (c == '@' && Next(text, i) == '*')
            {
                i = BlankTo(sb, text, i, Find(text, "*@", i + 2, after: true));
                continue;
            }

            if (razor && !inCode)
            {
                // Entering a code region: @code { … }, @functions { … }, @{ … }. Checked before
                // '@' is read as an expression, or the block header would be eaten as one.
                var open = RazorCodeBlockStart(text, i);
                if (open > i)
                {
                    sb.Append(text[i..open]);
                    i = open;
                    inCode = true;
                    codeDepth = 1;
                    continue;
                }

                // <!-- ... --> is text. Sample markup in a comment is not an instantiated
                // component, and keeping the element name inside it published false coverage
                // (Codex review round 12).
                if (c == '<' && text.AsSpan(i).StartsWith("<!--"))
                {
                    i = BlankTo(sb, text, i, Find(text, "-->", i + 4, after: true));
                    continue;
                }
                if (c == '<')
                {
                    inTag = true;
                    // The ELEMENT NAME is the reference; everything after it in the tag is not.
                    var nameEnd = EndOfElementName(text, i);
                    sb.Append(text[i..nameEnd]);
                    rawText = RawTextElement(text, i, nameEnd);
                    i = nameEnd;
                    continue;
                }
                if (c == '>')
                {
                    inTag = false;
                    sb.Append(c);
                    i++;

                    // <script>, <style>, <textarea>, <title>: their bodies are DATA, so markup
                    // inside them is a sample rather than a component. Without this the generic
                    // '<' branch kept `const s = " + '"' + "<Sheet />" + '"' + "` as an element name
                    // and published false coverage (Codex review round 13).
                    if (rawText is not null)
                    {
                        var close = EndOfRawTextBody(text, i, rawText);
                        i = AppendRawTextBody(sb, text, i, close);
                        rawText = null;
                    }
                    continue;
                }

                if (inTag)
                {
                    // An attribute value's text is not code — <Host Title='Sheet' /> must not
                    // read as a Sheet reference — but any @-expression inside it is. The quote
                    // must FOLLOW an '=' to be a delimiter.
                    if ((c is '"' or '\'') && FollowsAnEquals(text, i))
                    {
                        // A generic component's type parameter takes TYPE SYNTAX, not display
                        // text: <Grid TItem="Lumeo.Sheet" /> is a real reference (Codex review
                        // round 12). Recognised by the @typeparam naming convention — T, or T
                        // followed by an uppercase letter — because nothing here knows the
                        // component's declared parameters.
                        // A DIRECTIVE attribute (@key, @onclick, @ref, @bind, @attributes) parses
                        // its value as C# even without a leading '@' on the value itself, so
                        // `<div @key=" + '"' + "typeof(Lumeo.Sheet)" + '"' + ">` is a real reference
                        // (Codex review round 13).
                        i = IsTypeParameterAttribute(text, i) || IsDirectiveAttribute(text, i)
                            || IsUnambiguouslyCSharpValue(text, i)
                            ? AppendTypeAttribute(sb, text, i)
                            : AppendMarkupAttribute(sb, text, i);
                        continue;
                    }

                    // An @-expression as an attribute value is code.
                    if (c == '@')
                    {
                        var expr = EndOfRazorExpression(text, i);
                        if (expr > i)
                        {
                            sb.Append(StripNonCode(text[i..expr]));
                            i = expr;
                            continue;
                        }
                    }

                    // Everything else inside the tag is an ATTRIBUTE NAME, which is a parameter,
                    // not a type. Keeping them published false coverage: label.json claimed
                    // MegaMenuDisabledHost.razor for `<MegaMenuItem Label="Products">`, and
                    // text.json claimed SplitButtonRotationTests.razor for `Text="Save"`
                    // (Codex review round 8).
                    i = BlankTo(sb, text, i, i + 1);
                    continue;
                }

                // Markup TEXT. Blanked, because a reference in markup is a <Sheet /> tag and
                // never prose: a trigger captioned "Toggle" was claiming Toggle coverage for
                // CollapsibleTests.razor (Codex review round 7). Embedded @-expressions are code.
                // '@@' is an escape: Razor renders a literal '@' and what follows is TEXT.
                // Blanking one character at a time let the second '@' open a transition, so
                // `<p>@@typeof(Lumeo.Sheet)</p>` published prose as a reference (round 15).
                if (c == '@' && Next(text, i) is '@' or '*' or ':')
                {
                    i = BlankTo(sb, text, i, EndOfTransition(text, i));
                    continue;
                }
                if (c == '@')
                {
                    var expr = EndOfTransition(text, i);
                    if (expr > i)
                    {
                        sb.Append(StripNonCode(text[i..expr]));

                        // A control directive's BODY mixes C# statements with markup islands,
                        // and treating it all as markup blanked the statements (round 15).
                        var body = StartOfControlBody(text, expr);
                        i = body > expr ? AppendRazorControlBody(sb, text, expr, body) : expr;
                        continue;
                    }
                }

                i = BlankTo(sb, text, i, i + 1);
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
            // A char literal, so that '"' does not open a string and swallow the file.
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
                    // Inside a code region a literal is a literal, Razor file or not: a
                    // const string that merely LOOKS like markup is still string data.
                    i = AppendStringLiteral(sb, text, i, quote, dollars, verbatim);
                    continue;
                }
            }

            if (razor && inCode)
            {
                // An inline Razor template — Render(@<Collapsible>Toggle</Collapsible>) — steps
                // back into MARKUP for the length of the fragment, and staying in C# mode left
                // its label text reading as code. That is a live defect: toggle.json currently
                // claims CollapsibleTests.razor purely because "Toggle" is the trigger's caption
                // (Codex review round 7).
                if (c == '@' && Next(text, i) == '<')
                {
                    var end = EndOfElementSubtree(text, i);
                    sb.Append(' ');                                     // the '@' itself
                    sb.Append(StripNonCode(text[(i + 1)..end], razor: true));
                    i = end;
                    continue;
                }

                if (c == '{') codeDepth++;
                else if (c == '}' && --codeDepth == 0) inCode = false;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// End of ANY Razor transition starting at <paramref name="i"/>, or <paramref name="i"/> when
    /// there is none: the <c>@@</c> escape, a <c>@* *@</c> comment, a <c>@{ }</c> code block, a
    /// <c>@:</c> line, an explicit <c>@(...)</c> expression, or an implicit member chain.
    ///
    /// <para>
    /// One place, because the alternative kept costing review rounds: every scanner that walks
    /// markup — the element-subtree walker, raw-text close discovery, attribute values, control
    /// bodies — has to step over transitions, and each of them had grown its own partial version.
    /// A fix applied to one of them left the others reading a comparison operator as a tag or an
    /// end tag inside a comment as a real close.
    /// </para>
    /// </summary>
    private static int EndOfTransition(string text, int i)
    {
        if (i >= text.Length || text[i] != '@') return i;

        var next = Next(text, i);
        if (next == '@') return i + 2;                                  // escape
        if (next == '*') return Find(text, "*@", i + 2, after: true);   // comment
        if (next == ':')                                                // literal line
        {
            var nl = text.IndexOf('\n', i);
            return nl < 0 ? text.Length : nl;
        }
        if (next == '{')                                                // code block
        {
            var depth = 0;
            for (var k = i + 1; k < text.Length; k++)
            {
                if (text[k] == '{') depth++;
                else if (text[k] == '}' && --depth == 0) return k + 1;
            }
            return text.Length;
        }

        return EndOfRazorExpression(text, i);
    }

    /// <summary>
    /// End of the element subtree that begins at <paramref name="start"/> — the '@' of an inline
    /// <c>@&lt;...&gt;</c> template, or the '&lt;' of a markup island inside a control body.
    /// Element depth decides, not the enclosing C# braces.
    ///
    /// <para>
    /// ONE implementation for both. They were separate near-copies, and every fix — Razor
    /// transitions, HTML comments, raw-text bodies, void elements, quoted '&gt;' — had to be made
    /// twice or the second site kept the bug.
    /// </para>
    /// </summary>
    private static int EndOfElementSubtree(string text, int start)
    {
        var depth = 0;
        var i = text[start] == '@' ? start + 1 : start;

        while (i < text.Length)
        {
            if (text[i] == '@')
            {
                var t = EndOfTransition(text, i);
                if (t > i) { i = t; continue; }
            }
            if (text[i] != '<') { i++; continue; }
            if (text.AsSpan(i).StartsWith("<!--")) { i = Find(text, "-->", i + 4, after: true); continue; }

            var closing = Next(text, i) == '/';
            var gt = EndOfTag(text, i);
            if (gt < 0) return text.Length;

            var raw = RawTextElement(text, i, EndOfElementName(text, i));
            if (raw is not null)
            {
                depth++;
                i = EndOfRawTextBody(text, gt + 1, raw);
                continue;
            }

            if (closing) { if (--depth <= 0) return gt + 1; }
            else if (text[gt - 1] != '/' && !IsVoidElement(text, i)) depth++;
            else if (depth == 0) return gt + 1;

            i = gt + 1;
        }
        return text.Length;
    }

    /// <summary>
    /// Index of the '&gt;' closing the tag that opens at <paramref name="start"/>, skipping
    /// quoted attribute values. `&lt;Host Title="a &gt; b" /&gt;` closes at the LAST one, and
    /// taking the first left the inline template unbalanced so the scanner consumed the C#
    /// after it as markup (Codex review round 10).
    /// </summary>
    private static int EndOfTag(string text, int start)
    {
        var i = start;
        while (i < text.Length)
        {
            var c = text[i];
            if (c is '"' or '\'')
            {
                var delimiter = c;
                i++;
                // Across newlines: a Razor attribute value may wrap, and ending it at the line
                // break made a '>' on the continuation look like the tag's end (Codex review
                // round 12).
                while (i < text.Length && text[i] != delimiter)
                {
                    // A Razor expression inside the value carries its own literals, and one of
                    // them may hold the delimiter or a '>' — `Title="@($\"a > b\")"`. Stopping at
                    // the next matching quote ended the attribute early and the '>' in that
                    // string was read as the tag's end, leaving the template open through the
                    // C# that followed (Codex review round 11).
                    if (text[i] == '@')
                    {
                        var expr = EndOfRazorExpression(text, i);
                        if (expr > i) { i = expr; continue; }
                    }
                    i++;
                }
                if (i < text.Length && text[i] == delimiter) i++;
                continue;
            }
            if (c == '>') return i;
            i++;
        }
        return -1;
    }

    /// <summary>True when the tag opening at <paramref name="start"/> names an HTML void
    /// element, which has no closing tag whether or not it is written self-closing.</summary>
    private static bool IsVoidElement(string text, int start)
    {
        string[] voids =
        [
            "area", "base", "br", "col", "embed", "hr", "img", "input",
            "link", "meta", "param", "source", "track", "wbr",
        ];

        var i = start + 1;
        var nameStart = i;
        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '-')) i++;
        var name = text[nameStart..i];

        foreach (var v in voids)
        {
            if (string.Equals(name, v, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// End of the parenthesised run opening at <paramref name="open"/>, or
    /// <paramref name="fallback"/> when it never closes. Literals and comments inside it are
    /// SKIPPED rather than counted: a parenthesis in a string is not a bracket, and counting one
    /// truncated the condition at it — `@if (Check(")") &amp;&amp; typeof(Lumeo.Sheet) != null)`
    /// kept only the prefix and the reference after it was blanked as prose (Codex review
    /// rounds 7 and 9).
    /// </summary>
    private static int EndOfBalancedParens(string text, int open, int fallback)
    {
        var depth = 0;
        var i = open;

        while (i < text.Length)
        {
            var c = text[i];

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
                var (quote, dollars, verbatim) = ReadStringPrefix(text, i);
                if (quote >= 0) { i = EndOfString(text, quote, verbatim, dollars); continue; }
            }

            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i + 1;
            i++;
        }

        return fallback;
    }

    /// <summary>Bracket twin of <see cref="EndOfBalancedParens"/>, for an indexer suffix.</summary>
    private static int EndOfBalancedBrackets(string text, int open, int fallback)
    {
        var depth = 0;
        var i = open;

        while (i < text.Length)
        {
            var c = text[i];
            // Comments too, exactly as the paren twin does: a ']' inside one is text, and
            // reading it as the indexer's close truncated the expression (Codex review round 12).
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
                var (quote, dollars, verbatim) = ReadStringPrefix(text, i);
                if (quote >= 0) { i = EndOfString(text, quote, verbatim, dollars); continue; }
            }

            if (c == '[') depth++;
            else if (c == ']' && --depth == 0) return i + 1;
            i++;
        }

        return fallback;
    }

    /// <summary>Scans an implicit expression starting at <paramref name="j"/> as though the
    /// '@' were there — used for the operand of Razor's implicit <c>await</c>.</summary>
    private static int InsertedTransition(string text, int j)
    {
        // EndOfRazorExpression expects the '@' at its index, so hand it j-1 only when that
        // really is one; otherwise scan the identifier chain directly from j.
        var k = j;
        while (k < text.Length && (IsIdentifierChar(text[k]) || text[k] == '.')) k++;

        while (k < text.Length && (text[k] == '(' || text[k] == '['))
        {
            var close = text[k] == '(' ? EndOfBalancedParens(text, k, k) : EndOfBalancedBrackets(text, k, k);
            if (close <= k) break;
            k = close;
        }
        return k;
    }

    /// <summary>End of the element name that opens at the '&lt;' at <paramref name="start"/>,
    /// including the '&lt;' and any '/' of a closing tag.</summary>
    private static int EndOfElementName(string text, int start)
    {
        var i = start + 1;
        if (i < text.Length && text[i] == '/') i++;
        while (i < text.Length && (IsIdentifierChar(text[i]) || text[i] == '.' || text[i] == '-')) i++;
        return i;
    }

    /// <summary>
    /// Index of the closing tag that ends <paramref name="name"/>'s raw-text body, or the end
    /// of the input. The name must be followed by whitespace, '/' or '>' — a plain substring
    /// search let `&lt;/scripture&gt;` end a &lt;script&gt; body (Codex review round 14).
    /// </summary>
    private static int EndOfRawTextBody(string text, int from, string name)
    {
        var needle = "</" + name;
        var at = from;

        while (true)
        {
            at = text.IndexOf(needle, at, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return text.Length;

            // An end tag spelled inside a Razor transition — a literal, a COMMENT, a code
            // block — is data, not the close. Walking the body forward through the one
            // transition skipper covers all of them; the earlier version only knew about
            // expressions, so `@* </script> *@` still ended the body (Codex review round 16).
            var over = SkipTransitionsUpTo(text, from, at);
            if (over > at) { at = over; continue; }

            var after = at + needle.Length;
            if (after >= text.Length) return text.Length;
            if (char.IsWhiteSpace(text[after]) || text[after] == '/' || text[after] == '>') return at;

            at = after;
        }
    }

    /// <summary>Walks from <paramref name="from"/> and returns the end of the first Razor
    /// transition that CONTAINS <paramref name="at"/>, or <paramref name="at"/> when none
    /// does.</summary>
    private static int SkipTransitionsUpTo(string text, int from, int at)
    {
        var i = from;
        while (i < at)
        {
            if (text[i] != '@') { i++; continue; }

            var end = EndOfTransition(text, i);
            if (end <= i) { i++; continue; }
            if (end > at) return end;
            i = end;
        }
        return at;
    }

    /// <summary>Appends a raw-text body: its markup-like text is DATA, but a Razor transition
    /// inside it is still evaluated by Razor and therefore still code.</summary>
    private static int AppendRawTextBody(StringBuilder sb, string text, int from, int to)
    {
        var i = from;
        while (i < to)
        {
            if (text[i] == '@' && Next(text, i) is '*' or '@')
            {
                i = BlankTo(sb, text, i, Math.Min(to, EndOfTransition(text, i)));
                continue;
            }
            if (text[i] == '@')
            {
                // Expressions AND code blocks: `<script>@{ var t = typeof(X); }</script>` is
                // evaluated by Razor, and only expressions were recognised (round 16).
                var end = EndOfTransition(text, i);
                if (end > i && end <= to)
                {
                    sb.Append(StripNonCode(text[i..end]));
                    i = end;
                    continue;
                }
            }

            i = BlankTo(sb, text, i, i + 1);
        }
        return to;
    }

    /// <summary>The raw-text element name the tag at [start, nameEnd) opens, or null. Their
    /// bodies are character data rather than markup.</summary>
    private static string? RawTextElement(string text, int start, int nameEnd)
    {
        var i = start + 1;
        if (i < text.Length && text[i] == '/') return null;   // a CLOSING tag opens nothing

        // Nor does a SELF-CLOSING one. Razor accepts `<textarea />`, and this repo writes it
        // that way, so treating it as opening a body made the missing end tag swallow the
        // rest of the file (Codex review round 14).
        var gt = EndOfTag(text, start);
        if (gt > start && text[gt - 1] == '/') return null;

        var name = text[i..nameEnd];
        foreach (var raw in new[] { "script", "style", "textarea", "title" })
        {
            if (string.Equals(name, raw, StringComparison.OrdinalIgnoreCase)) return raw;
        }
        return null;
    }

    /// <summary>End of an <c>else</c> / <c>else if (...)</c> / <c>catch (...)</c> /
    /// <c>finally</c> that continues the construct just closed at <paramref name="from"/>, or
    /// <paramref name="from"/> when the construct really has ended.</summary>
    private static int StartOfChainedBranch(string text, int from)
    {
        var i = from;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;

        var wordStart = i;
        while (i < text.Length && char.IsLetter(text[i])) i++;
        var word = text[wordStart..i];

        if (word is not ("else" or "catch" or "finally")) return from;

        // `else if (...)` and `catch (...)` carry a parenthesised part that is C# as well.
        var j = i;
        while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
        if (word == "else" && text.AsSpan(j).StartsWith("if")) { j += 2; while (j < text.Length && char.IsWhiteSpace(text[j])) j++; }
        if (j < text.Length && text[j] == '(')
        {
            var close = EndOfBalancedParens(text, j, i);
            if (close > j) return close;
        }
        return i;
    }

    /// <summary>Index of the '{' opening a control directive's body after the expression that
    /// ends at <paramref name="from"/>, or <paramref name="from"/> when there is none.</summary>
    private static int StartOfControlBody(string text, int from)
    {
        var i = from;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        return i < text.Length && text[i] == '{' ? i : from;
    }

    /// <summary>
    /// Appends a Razor control body — <c>@if (…) { … }</c> and friends. Razor allows C#
    /// STATEMENTS there alongside markup, so the body is scanned as code with markup islands:
    /// a run starting at '&lt;' is an element and goes through the markup scanner, everything
    /// else through the code one. Treating the whole body as markup blanked real statements;
    /// treating it all as code would resurrect every caption inside it.
    /// </summary>
    private static int AppendRazorControlBody(StringBuilder sb, string text, int from, int brace)
    {
        sb.Append(text[from..brace]);
        sb.Append('{');

        var i = brace + 1;
        var depth = 1;
        var codeFrom = i;

        while (i < text.Length)
        {
            var c = text[i];

            // '@:' emits a literal line — markup, not a statement — and letting the code
            // scanner have it preserved its text as a reference (Codex review round 16).
            if (c == '@' && Next(text, i) == ':')
            {
                sb.Append(StripNonCode(text[codeFrom..i]));
                var lineEnd = EndOfTransition(text, i);
                BlankTo(sb, text, i, lineEnd);
                i = lineEnd;
                codeFrom = i;
                continue;
            }

            if (c == '<' && i + 1 < text.Length && (char.IsLetter(text[i + 1]) || text[i + 1] == '/'))
            {
                sb.Append(StripNonCode(text[codeFrom..i]));
                var end = EndOfElementSubtree(text, i);
                sb.Append(StripNonCode(text[i..end], razor: true));
                i = end;
                codeFrom = i;
                continue;
            }

            // A brace inside a literal or a comment is not structure — counting one returned
            // from the body early and the statements after it were blanked as markup
            // (Codex review round 16).
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
                var (quote, dollars, verbatim) = ReadStringPrefix(text, i);
                if (quote >= 0) { i = EndOfString(text, quote, verbatim, dollars); continue; }
            }

            if (c == '{') { depth++; i++; continue; }
            if (c == '}')
            {
                if (--depth == 0)
                {
                    sb.Append(StripNonCode(text[codeFrom..i]));
                    sb.Append('}');

                    // `else`, `else if`, `catch`, `finally` continue the same construct, and
                    // returning here left those unprefixed branches to the markup scanner,
                    // which blanked their statements (Codex review round 16).
                    var chained = StartOfChainedBranch(text, i + 1);
                    if (chained > i + 1)
                    {
                        sb.Append(StripNonCode(text[(i + 1)..chained]));
                        var nextBrace = StartOfControlBody(text, chained);
                        return nextBrace > chained
                            ? AppendRazorControlBody(sb, text, chained, nextBrace)
                            : chained;
                    }
                    return i + 1;
                }
                i++;
                continue;
            }

            i++;
        }

        sb.Append(StripNonCode(text[codeFrom..]));
        return text.Length;
    }

    /// <summary>True when the character before <paramref name="i"/>, skipping whitespace, is
    /// an '=' — the only place a quote opens a markup attribute value.</summary>
    private static bool FollowsAnEquals(string text, int i)
    {
        var j = i - 1;
        while (j >= 0 && (text[j] == ' ' || text[j] == '	')) j--;
        return j >= 0 && text[j] == '=';
    }

    /// <summary>Index just past the opening brace of an <c>@code</c> / <c>@functions</c> /
    /// <c>@{</c> block starting at <paramref name="i"/>, or <paramref name="i"/> when there
    /// is none.</summary>
    private static int RazorCodeBlockStart(string text, int i)
    {
        if (text[i] != '@' || i + 1 >= text.Length) return i;

        var j = i + 1;
        if (text[j] == '{') return j + 1;

        foreach (var keyword in new[] { "code", "functions" })
        {
            if (j + keyword.Length > text.Length) continue;
            if (!text.AsSpan(j, keyword.Length).SequenceEqual(keyword)) continue;

            var k = j + keyword.Length;
            while (k < text.Length && char.IsWhiteSpace(text[k])) k++;
            if (k < text.Length && text[k] == '{') return k + 1;
        }
        return i;
    }

    /// <summary>True when the attribute whose value opens at <paramref name="quote"/> is named
    /// like a generic type parameter: exactly <c>T</c>, or <c>T</c> followed by an uppercase
    /// letter (TItem, TValue, TKey — the @typeparam convention).</summary>
    private static bool IsTypeParameterAttribute(string text, int quote)
    {
        var i = quote - 1;
        while (i >= 0 && (text[i] == ' ' || text[i] == '=')) i--;
        var end = i + 1;
        while (i >= 0 && IsIdentifierChar(text[i])) i--;

        var name = text[(i + 1)..end];
        return name.Length > 0
               && name[0] == 'T'
               && (name.Length == 1 || char.IsUpper(name[1]));
    }

    /// <summary>
    /// True when the value is C# by SHAPE rather than by the attribute's name. A Blazor
    /// component parameter can carry an expression with no leading '@' — this repo writes
    /// <c>NotFoundPage="typeof(Pages.NotFound)"</c> — and nothing here knows a component's
    /// declared parameter types, so only a form that cannot be display text counts
    /// (Codex review round 16).
    /// </summary>
    private static bool IsUnambiguouslyCSharpValue(string text, int quote)
    {
        var delimiter = text[quote];
        var end = quote + 1;
        while (end < text.Length && text[end] != delimiter && text[end] != '\n') end++;
        if (end >= text.Length || text[end] != delimiter) return false;

        var value = text[(quote + 1)..end].Trim();
        return value.StartsWith("typeof(", StringComparison.Ordinal) && value.EndsWith(")", StringComparison.Ordinal);
    }

    /// <summary>True when the attribute whose value opens at <paramref name="quote"/> is a Razor
    /// DIRECTIVE attribute — its name starts with '@'.</summary>
    private static bool IsDirectiveAttribute(string text, int quote)
    {
        var i = quote - 1;
        while (i >= 0 && (text[i] == ' ' || text[i] == '=')) i--;
        while (i >= 0 && (IsIdentifierChar(text[i]) || text[i] == '-' || text[i] == ':')) i--;
        return i >= 0 && text[i] == '@';
    }

    /// <summary>Appends a type-valued attribute, keeping its value as code.</summary>
    private static int AppendTypeAttribute(StringBuilder sb, string text, int start)
    {
        var delimiter = text[start];
        BlankTo(sb, text, start, start + 1);

        // Skipping nested literals while looking for the terminator: a C# string inside the
        // value carries the same quote, and stopping at it processed the rest as markup —
        // `@onclick=" + '"' + "@(() => { Log(" + '"' + "x" + '"' + "); })" + '"' + "`
        // lost everything after Log's argument (Codex review round 14).
        var i = start + 1;
        var depth = 0;
        while (i < text.Length)
        {
            // The attribute's own terminator is the delimiter at BRACKET DEPTH ZERO. Inside
            // the expression the same character opens an ordinary C# string, and requiring a
            // $/@ prefix to tell them apart was wrong — a nested literal is usually bare
            // (Codex review round 14).
            if (text[i] == delimiter && depth == 0) break;
            if (text[i] == '/' && Next(text, i) == '/')
            {
                var nl = text.IndexOf('\n', i);
                i = nl < 0 ? text.Length : nl;
                continue;
            }
            if (text[i] == '/' && Next(text, i) == '*') { i = Find(text, "*/", i + 2, after: true); continue; }
            if (text[i] == '\'' && IsCharLiteral(text, i)) { i = EndOfCharLiteral(text, i); continue; }

            var (q, d, v) = ReadStringPrefix(text, i);
            if (q >= 0) { i = EndOfString(text, q, v, d); continue; }

            if (text[i] is '(' or '{' or '[') depth++;
            else if (text[i] is ')' or '}' or ']') depth--;
            i++;
        }

        sb.Append(StripNonCode(text[(start + 1)..i]));
        return i < text.Length ? BlankTo(sb, text, i, i + 1) : text.Length;
    }

    /// <summary>Appends a markup attribute value, blanked except for the Razor expressions
    /// inside it, and returns the index just past its closing quote.</summary>
    private static int AppendMarkupAttribute(StringBuilder sb, string text, int start)
    {
        var delimiter = text[start];
        BlankTo(sb, text, start, start + 1);

        var i = start + 1;
        while (i < text.Length && text[i] != delimiter)
        {
            // Across newlines: a Razor attribute value may wrap, and ending it at the break
            // left the tag open so markup inside the still-quoted value read as live
            // (Codex review round 16).

            // The SAME escape rule as markup text: `Title="@@typeof(X)"` is literal, and
            // handling it only in the text path left attributes publishing prose (round 16).
            if (text[i] == '@' && Next(text, i) is '@' or '*')
            {
                i = BlankTo(sb, text, i, EndOfTransition(text, i));
                continue;
            }
            if (text[i] == '@')
            {
                var expr = EndOfTransition(text, i);
                if (expr > i)
                {
                    sb.Append(StripNonCode(text[i..expr]));
                    i = expr;
                    continue;
                }
            }

            BlankTo(sb, text, i, i + 1);
            i++;
        }

        return i < text.Length ? BlankTo(sb, text, i, i + 1) : text.Length;
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

            // No Razor allowance here. This is a C# literal — reached either from a .cs file or
            // from inside a .razor CODE region — and its content is text all the way through,
            // even when it happens to look like markup: `@code { const string E = "@(typeof(
            // Lumeo.Sheet))"; }` is string data, not a reference (Codex review round 6).
            // Markup attribute values take AppendMarkupAttribute instead.
            BlankTo(sb, text, i, i + 1);
            i++;
        }

        return text.Length;
    }

    /// <summary>End of the Razor expression starting at the '@' at <paramref name="start"/>:
    /// an explicit <c>@(...)</c> with balanced parentheses, or an implicit <c>@Foo.Bar(...)</c>
    /// member chain. Returns <paramref name="start"/> when this is not one (a stray '@', or the
    /// '@@' escape).</summary>
    private static int EndOfRazorExpression(string text, int start)
    {
        var i = start + 1;
        if (i >= text.Length || text[i] == '@') return start;

        if (text[i] == '(') return EndOfBalancedParens(text, i, start);

        if (!char.IsLetter(text[i]) && text[i] != '_') return start;

        var wordStart = i;
        while (i < text.Length && (IsIdentifierChar(text[i]) || text[i] == '.')) i++;

        // A control directive's CONDITION is C#, and stopping at the keyword left it to be
        // blanked as prose — `@if (typeof(Lumeo.Sheet) != null) { <Drawer /> }` lost a real
        // reference the same expression in an @code block would have kept (Codex review round 8).
        // The BODY stays markup, which is what it is.
        var keyword = text[wordStart..i];

        // Razor's implicit await allows the space that normally ENDS an implicit expression,
        // so `@await RenderAsync(typeof(X))` stopped at the keyword (Codex review round 16).
        if (keyword == "await")
        {
            var j = i;
            while (j < text.Length && (text[j] == ' ' || text[j] == '\t')) j++;
            if (j < text.Length && (char.IsLetter(text[j]) || text[j] == '_'))
                return EndOfRazorExpression(text, j - 1 >= 0 && text[j - 1] == '@' ? j - 1 : InsertedTransition(text, j));
        }

        // A file directive's ARGUMENT is a type, and the implicit-expression scan stopped at the
        // space before it: `@inherits TestHost<Lumeo.Sheet>` and `@inject Lumeo.Sheet Subject`
        // kept only the keyword while the type itself was blanked as markup prose (Codex review
        // round 9). The rest of the line is C#.
        string[] typeBearing = ["inherits", "inject", "implements", "typeparam", "attribute", "namespace", "layout", "model", "page", "preservewhitespace"];
        if (Array.IndexOf(typeBearing, keyword) >= 0)
        {
            var nl = text.IndexOf('\n', i);
            return nl < 0 ? text.Length : nl;
        }

        string[] directives = ["if", "else", "foreach", "for", "while", "switch", "lock", "using", "do"];
        if (Array.IndexOf(directives, keyword) >= 0)
        {
            var j = i;
            while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
            if (j < text.Length && text[j] == '(') return EndOfBalancedParens(text, j, i);
        }

        // An implicit expression may CALL something, and this method's own contract says
        // @Foo.Bar(...) — but it stopped at the identifier, so `@Get(typeof(Lumeo.Sheet))` left
        // its argument to be blanked as attribute text (Codex review round 10). The suffix is a
        // CHAIN, not a single call: `@Get().Render(typeof(Lumeo.Sheet))` continues past the
        // first pair of parentheses, and stopping there dropped the reference in the second
        // (round 11).
        while (true)
        {
            // CONTIGUOUS only. Razor ends an implicit expression at whitespace, so skipping it
            // read `@Get() (Sheet)` as a second invocation and kept the display text as code
            // (Codex review round 12).
            var j = i;
            if (j >= text.Length) break;

            if (text[j] == '(' || text[j] == '[')
            {
                var close = text[j] == '(' ? EndOfBalancedParens(text, j, i) : EndOfBalancedBrackets(text, j, i);
                if (close <= i) break;
                i = close;
                continue;
            }

            // The null-forgiving '!' binds tighter than the member access after it, so it sits
            // between the identifier and the '.' — `@Provider!.Render(...)` ended at Provider
            // without this (Codex review round 16).
            if (text[j] == '!' && j + 1 < text.Length && text[j + 1] != '=') { i = j + 1; continue; }

            // '.' or '?.' — conditional access is a member chain too, and stopping before the
            // '?' ended `@Provider?.Render(...)` at the identifier (Codex review round 12).
            var dot = text[j] == '?' && j + 1 < text.Length && text[j + 1] == '.' ? j + 1 : j;
            if (text[dot] == '.' && dot + 1 < text.Length
                && (char.IsLetter(text[dot + 1]) || text[dot + 1] == '_'))
            {
                var k = dot + 1;
                while (k < text.Length && IsIdentifierChar(text[k])) k++;
                i = k;
                continue;
            }

            break;
        }

        return i;
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
                var (quote, dollars, verbatim) = ReadStringPrefix(text, i);
                if (quote >= 0) { i = EndOfString(text, quote, verbatim, dollars); continue; }
            }
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
            i++;
        }
        return text.Length;
    }

    /// <summary>End of a literal whose opening quote is at <paramref name="quote"/>, used only to
    /// skip past it.</summary>
    private static int EndOfString(string text, int quote, bool verbatim, int dollars = 0)
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
        // Holes exist only in an INTERPOLATED literal. Tracking them unconditionally read the
        // brace in `Check(" + '"' + "{" + '"' + ")` as a hole opener, after which the closing quote
        // looked like a nested literal and the scan ran to the end of the file — a regression
        // from the round-12 change, which is the one I shipped without a test (round 13).
        var holeDepth = 0;
        var interpolated = dollars > 0;
        while (i < text.Length)
        {
            if (!verbatim && text[i] == '\\') { i += 2; continue; }

            // Interpolation holes are CODE, and a literal inside one has its own quotes:
            // `$"{Get(" + '"' + "x" + '"' + ")}"` ended at the inner quote and the rest of the
            // expression was rescanned as text (Codex review round 12). Tracked by depth rather
            // than parsed, which is enough to know whether a quote can close the literal.
            // A brace inside a char literal is not a hole delimiter — `Test('\''}'\'')` closed one
            // early and the next quote was mistaken for the outer string's end (round 14).
            if (interpolated && holeDepth > 0 && text[i] == '/' && Next(text, i) == '/')
            {
                var nl = text.IndexOf('\n', i);
                i = nl < 0 ? text.Length : nl;
                continue;
            }
            if (interpolated && holeDepth > 0 && text[i] == '/' && Next(text, i) == '*')
            {
                // A brace in a comment is not a hole delimiter, the same way it is not a
                // bracket for the balancers (Codex review round 15).
                i = Find(text, "*/", i + 2, after: true);
                continue;
            }
            if (interpolated && holeDepth > 0 && text[i] == '\'' && IsCharLiteral(text, i))
            {
                i = EndOfCharLiteral(text, i);
                continue;
            }
            if (interpolated && text[i] == '{')
            {
                if (Next(text, i) == '{' && holeDepth == 0) { i += 2; continue; }
                holeDepth++;
                i++;
                continue;
            }
            if (interpolated && text[i] == '}' && holeDepth > 0) { holeDepth--; i++; continue; }

            if (text[i] == '"')
            {
                if (verbatim && Next(text, i) == '"') { i += 2; continue; }
                if (holeDepth > 0)
                {
                    var (q2, d2, v2) = ReadStringPrefix(text, i);
                    i = q2 >= 0 ? EndOfString(text, q2, v2, d2) : i + 1;
                    continue;
                }
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
