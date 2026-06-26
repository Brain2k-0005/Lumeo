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
        Assert.Equal(
            "namespace MyApp.Ui.Services;",
            NamespaceRewriter.Rewrite("namespace Lumeo.Services;", "ThemeService.cs", "MyApp.Ui"));
    }

    [Fact]
    public void CSharp_Block_Namespace_Is_Rewritten()
    {
        Assert.Equal(
            "namespace MyApp.Ui\n{",
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
            "namespace MyApp.Services;\r\n",
            NamespaceRewriter.Rewrite("namespace Lumeo.Services;\r\n", "ThemeService.cs", "MyApp"));
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
}
