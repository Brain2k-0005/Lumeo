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
    public static string Rewrite(string content, string filePath, string targetNamespace,
        IReadOnlyCollection<string>? siblingComponentNames = null)
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

            // PR #357 round-9 (finding 2): a bare tag reference to a SIBLING component in the SAME
            // vendored batch (e.g. ToastProvider.razor's own `<Toast>`/`<ToastViewport>` markup) is
            // resolved by Razor's TagHelper matching against every namespace the file's `@using`
            // chain makes visible — including the consumer's own project-root `_Imports.razor`. The
            // officially templated app keeps `@using Lumeo` there (so a fresh, nothing-vendored-yet
            // project can use the PACKAGE's components unqualified), which is ALSO still in scope
            // after `lumeo add toast` vendors Acme.Ui.Toast/ToastProvider/ToastViewport — two
            // TagHelperDescriptors now match the exact same short tag name, and Razor doesn't
            // reject that as "ambiguous": it UNIONS both descriptors' parameters, surfacing as
            // bogus RZ10009 "parameter used twice" errors (and, for parameters whose TYPE itself
            // collides, an outright type mismatch) — the officially documented consumer setup fails
            // to build the instant Toast is added. Fully-qualifying every sibling tag reference
            // inside the vendored files themselves closes that regardless of whatever else is (or
            // isn't) in the consumer's own `_Imports.razor` — the vendored source no longer relies
            // on implicit same-namespace tag visibility at all. Scoped to the exact sibling names
            // vendored in THIS batch (never a blanket rewrite) so it can't touch an unrelated
            // same-named identifier (a local variable, a BCL/record type, a string literal — none
            // of those are immediately preceded by `<`/`</`, the only positions matched below).
            if (siblingComponentNames is { Count: > 0 })
            {
                foreach (var name in siblingComponentNames)
                {
                    // PR #357 (Codex finding): the bare lookbehind `(?<=<)` ALSO matched a sibling
                    // name used as a C# generic type ARGUMENT — e.g. Select depends on List, and
                    // Select.razor's own `EventCallback<List<string>?>` has "List" directly preceded
                    // by `<` (from `EventCallback<`) exactly like a real `<List` markup tag. That
                    // rewrote it to `EventCallback<Acme.Ui.List<string>?>`, pointing at the
                    // non-generic List COMPONENT instead of System.Collections.Generic.List<T> — the
                    // vendored file no longer compiles. A lookbehind excluding "preceded by an
                    // identifier character" was tried first and reverted: it also excludes plenty of
                    // GENUINE closing tags, which routinely sit directly against the preceding text
                    // node with no separator (`<ToastTitle>@toast.Options.Title</ToastTitle>` — the
                    // "e" ending "Title" is a word character immediately before "</ToastTitle>"'s
                    // `<`). The reliable discriminator is on the OTHER side: a real tag name in Razor
                    // markup is always followed by whitespace, `>`, or `/` (attributes, self-close, or
                    // the end of the start/end tag) — HTML/Razor syntax has no `<Tag<` construct.
                    // A generic type argument is the only thing a bare identifier can be immediately
                    // followed by `<` for (the next nested generic argument, however deep). The
                    // negative lookahead `(?!<)` after the name excludes exactly that, without
                    // touching any real tag position.
                    content = Regex.Replace(content, $@"(?<=<)(/?){Regex.Escape(name)}\b(?!<)",
                        $"$1{targetNamespace}.{name}");
                }
            }
        }
        else if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            // When a .cs file moves out of the Lumeo namespace, relative references it made by
            // virtue of living there — e.g. `Services.Foo` meaning `Lumeo.Services.Foo`, or a nested
            // `FormField.FormFieldContext` — no longer resolve under the consumer namespace. Re-add
            // `using Lumeo;` (only if absent) so those relative names still bind to Lumeo.*.
            var hadLumeoUsing = Regex.IsMatch(content, @"^using\s+Lumeo\s*;\s*$", RegexOptions.Multiline);
            content = Regex.Replace(content, @"^namespace\s+Lumeo(\.[A-Za-z0-9_.]*)?(\s*[;{])",
                m => (hadLumeoUsing ? "" : "using Lumeo;\n") + $"namespace {targetNamespace}{m.Groups[1].Value}{m.Groups[2].Value}",
                RegexOptions.Multiline);
        }
        return content;
    }
}
