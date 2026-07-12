using Lumeo.Cli;
using Xunit;

namespace Lumeo.Cli.Tests;

/// <summary>
/// <see cref="NamespaceRewriter"/> is the heart of <c>lumeo add</c>/<c>update</c>:
/// it rebrands the vendored source from the <c>Lumeo</c> root namespace to the
/// consumer's. These tests cover the Razor and C# directives, sub-namespace
/// preservation, the prefix-boundary guard and the file-type gate.
/// </summary>
public class NamespaceRewriterTests
{
    [Fact]
    public void Razor_Root_Namespace_Is_Rewritten()
    {
        Assert.Equal(
            "@namespace MyApp.Ui",
            NamespaceRewriter.Rewrite("@namespace Lumeo", "Button.razor", "MyApp.Ui"));
    }

    [Fact]
    public void Razor_Sub_Namespace_Is_Preserved_Under_The_New_Root()
    {
        Assert.Equal(
            "@namespace MyApp.Ui.UI.Button",
            NamespaceRewriter.Rewrite("@namespace Lumeo.UI.Button", "Button.razor", "MyApp.Ui"));
    }

    [Fact]
    public void CSharp_File_Scoped_Namespace_Is_Rewritten()
    {
        // The .cs rewrite also re-adds `using Lumeo;` so relative references (e.g. `Services.X`
        // meaning `Lumeo.Services.X`) still bind once the file leaves the Lumeo namespace.
        Assert.Equal(
            "using Lumeo;\nnamespace MyApp.Ui.Services;",
            NamespaceRewriter.Rewrite("namespace Lumeo.Services;", "ThemeService.cs", "MyApp.Ui"));
    }

    [Fact]
    public void CSharp_Block_Namespace_Is_Rewritten()
    {
        Assert.Equal(
            "using Lumeo;\nnamespace MyApp.Ui\n{",
            NamespaceRewriter.Rewrite("namespace Lumeo\n{", "ThemeService.cs", "MyApp.Ui"));
    }

    [Fact]
    public void Razor_Namespace_With_CRLF_Line_Endings_Is_Rewritten()
    {
        // Regression: a bare `$` anchor matches before \n, which on CRLF sits after
        // the \r, so `Lumeo$` never matched and Windows checkouts / `lumeo add
        // --local` left the @namespace as Lumeo, breaking every vendored .razor.
        Assert.Equal(
            "@namespace MyApp\r\n@inject Foo\r\n",
            NamespaceRewriter.Rewrite("@namespace Lumeo\r\n@inject Foo\r\n", "Button.razor", "MyApp"));
    }

    [Fact]
    public void Razor_Sub_Namespace_With_CRLF_Is_Rewritten()
    {
        Assert.Equal(
            "@namespace MyApp.UI.Button\r\n",
            NamespaceRewriter.Rewrite("@namespace Lumeo.UI.Button\r\n", "Button.razor", "MyApp"));
    }

    [Fact]
    public void CSharp_File_Scoped_Namespace_With_CRLF_Is_Rewritten()
    {
        Assert.Equal(
            "using Lumeo;\nnamespace MyApp.Services;\r\n",
            NamespaceRewriter.Rewrite("namespace Lumeo.Services;\r\n", "ThemeService.cs", "MyApp"));
    }

    [Fact]
    public void CSharp_Existing_Using_Lumeo_Is_Not_Duplicated()
    {
        // The using-Lumeo re-add is guarded: a file that already imports Lumeo doesn't get a second.
        Assert.Equal(
            "using Lumeo;\nnamespace MyApp.Services;",
            NamespaceRewriter.Rewrite("using Lumeo;\nnamespace Lumeo.Services;", "ThemeService.cs", "MyApp"));
    }

    [Fact]
    public void Only_The_Targeted_Directive_Line_Is_Touched()
    {
        const string src = "@namespace Lumeo\n@using Lumeo.Services\n<div>Lumeo</div>";
        var result = NamespaceRewriter.Rewrite(src, "Card.razor", "MyApp");

        // The @namespace line flips; the @using and the body text must not.
        Assert.Equal("@namespace MyApp\n@using Lumeo.Services\n<div>Lumeo</div>", result);
    }

    [Fact]
    public void A_Coincidental_Prefix_Is_Not_Rewritten()
    {
        // "LumeoExtras" must survive — only an exact Lumeo root (or Lumeo.*) is a match.
        const string src = "namespace LumeoExtras;";
        Assert.Equal(src, NamespaceRewriter.Rewrite(src, "X.cs", "MyApp"));
    }

    [Fact]
    public void Non_Lumeo_Namespaces_Are_Left_Alone()
    {
        const string src = "namespace Acme.Widgets;";
        Assert.Equal(src, NamespaceRewriter.Rewrite(src, "X.cs", "MyApp"));
    }

    [Fact]
    public void Unrelated_File_Types_Pass_Through_Unchanged()
    {
        const string src = "@namespace Lumeo\nnamespace Lumeo;";
        // Neither .razor nor .cs -> no rewrite at all.
        Assert.Equal(src, NamespaceRewriter.Rewrite(src, "notes.txt", "MyApp"));
    }

    [Fact]
    public void Razor_Rules_Do_Not_Apply_The_CSharp_Namespace_Replacement()
    {
        // In a .razor file only @namespace is rewritten; a bare C# `namespace` line
        // (unusual, but possible in an embedded code block) is left as-is.
        const string src = "namespace Lumeo.Services;";
        Assert.Equal(src, NamespaceRewriter.Rewrite(src, "Component.razor", "MyApp"));
    }

    // PR #357 round-9 (finding 2): a vendored component's OWN markup reference to a sibling
    // component in the SAME batch (e.g. ToastProvider.razor's `<Toast>`) must be fully qualified
    // — otherwise it stays resolvable only via implicit same-namespace visibility, which breaks
    // the instant the consumer's `_Imports.razor` ALSO has `@using Lumeo` in scope (RZ10009, the
    // officially templated app's own default setup). See CliStandaloneE2ETests'
    // Add_Vendor_Toast_Compiles_Against_The_Officially_Supported_Template_Setup for the full E2E.
    [Fact]
    public void Sibling_Component_Tags_Are_Fully_Qualified_When_Names_Are_Supplied()
    {
        const string src = "<Toast Variant=\"Foo\">\n  <ToastTitle>Hi</ToastTitle>\n</Toast>";
        var result = NamespaceRewriter.Rewrite(src, "ToastProvider.razor", "Acme.Ui",
            new[] { "Toast", "ToastTitle", "ToastProvider" });

        Assert.Equal(
            "<Acme.Ui.Toast Variant=\"Foo\">\n  <Acme.Ui.ToastTitle>Hi</Acme.Ui.ToastTitle>\n</Acme.Ui.Toast>",
            result);
    }

    [Fact]
    public void Sibling_Qualification_Is_A_No_Op_When_No_Names_Are_Supplied()
    {
        // Default parameter / explicit null / empty collection must all leave tag markup
        // untouched — this is the pre-round-9 behaviour every other caller still relies on.
        const string src = "<Toast Variant=\"Foo\"></Toast>";
        Assert.Equal(src, NamespaceRewriter.Rewrite(src, "ToastProvider.razor", "Acme.Ui"));
        Assert.Equal(src, NamespaceRewriter.Rewrite(src, "ToastProvider.razor", "Acme.Ui", null));
        Assert.Equal(src, NamespaceRewriter.Rewrite(src, "ToastProvider.razor", "Acme.Ui", Array.Empty<string>()));
    }

    [Fact]
    public void Sibling_Qualification_Only_Matches_Tag_Positions_Not_Unrelated_Word_Occurrences()
    {
        // "Toast" appearing as a plain word — inside a string literal, or as part of a LONGER
        // identifier like "ToastItem"/"QueuedToast" (no word boundary at the join) — must survive
        // untouched. Only the two literal `<Toast`/`</Toast` tag positions are matched.
        const string src =
            "<Toast Title=\"@L[\"Toast.Close\"]\">\n"
          + "  <div class=\"ToastItem\"></div>\n"
          + "</Toast>\n"
          + "@code {\n"
          + "    private readonly List<QueuedToast> _pending = new();\n"
          + "}";
        var result = NamespaceRewriter.Rewrite(src, "ToastProvider.razor", "Acme.Ui", new[] { "Toast" });

        Assert.Contains("<Acme.Ui.Toast Title=\"@L[\"Toast.Close\"]\">", result); // tag qualified, string literal untouched
        Assert.Contains("class=\"ToastItem\"", result);                          // unrelated attribute value untouched
        Assert.Contains("</Acme.Ui.Toast>", result);                             // closing tag qualified
        Assert.Contains("List<QueuedToast>", result);                            // different identifier, not a match at all
    }

    [Fact]
    public void Sibling_Qualification_Does_Not_Apply_To_CSharp_Files()
    {
        // .cs code-behind files never get the markup-tag rewrite — only .razor markup has this
        // ambiguity (TagHelper resolution, not ordinary C# same-namespace-wins type resolution;
        // see NamespaceRewriter's doc comment).
        const string src = "namespace Lumeo;\npublic partial class ToastProvider { }";
        Assert.Equal(
            "using Lumeo;\nnamespace Acme.Ui;\npublic partial class ToastProvider { }",
            NamespaceRewriter.Rewrite(src, "ToastProvider.razor.cs", "Acme.Ui", new[] { "ToastProvider" }));
    }
}
