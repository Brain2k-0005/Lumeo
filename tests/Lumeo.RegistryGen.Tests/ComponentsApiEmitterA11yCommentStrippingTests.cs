using Xunit;

namespace Lumeo.RegistryGen.Tests;

/// <summary>
/// PR #356 round-7 (Codex P2) — ExtractA11y's signal regexes (role="...", aria-*,
/// @onkeydown, KeyboardEventArgs, tabindex, FocusAsync/FocusElement) used to scan a
/// .razor file's RAW text, including its comments. Comments are prose, and prose
/// routinely references those exact tokens to explain what a component does NOT do
/// (ScrollArea.razor's own doc comment argued FOR adding a tab stop by noting it
/// previously "had no tabindex/@onkeydown of its own") — a naive scan reads that as
/// live markup and mis-reports keyboardInteractive: true for a component with no key
/// handler at all. <see cref="ComponentsApiEmitter.StripCommentsForA11yScan"/> strips
/// Razor <c>@* ... *@</c> comments and C# <c>///</c> XML-doc lines before the signal
/// regexes run.
/// </summary>
public class ComponentsApiEmitterA11yCommentStrippingTests
{
    [Fact]
    public void Strips_A_Multiline_Razor_Comment_Mentioning_The_Keyboard_Token()
    {
        var text = """
            @* ScrollArea had no tabindex/@onkeydown of its own, so a mouse-only
               user could not reach it via Tab at all. *@
            <div tabindex="0"></div>
            """;

        var stripped = ComponentsApiEmitter.StripCommentsForA11yScan(text);

        Assert.DoesNotContain("@onkeydown", stripped);
        Assert.Contains("tabindex=\"0\"", stripped); // real markup untouched
    }

    [Fact]
    public void Strips_A_Single_Line_Razor_Comment()
    {
        var text = "@* aria-expanded=\"true\" only in prose *@\n<div></div>";

        var stripped = ComponentsApiEmitter.StripCommentsForA11yScan(text);

        Assert.DoesNotContain("aria-expanded", stripped);
    }

    [Fact]
    public void Strips_Xml_Doc_Lines_Mentioning_Keyboard_And_Role_Tokens()
    {
        var text = """
            @code {
                /// Exposed as <c>role="img"</c>; a real handler would use
                /// <c>KeyboardEventArgs</c> and <c>@onkeydown</c>.
                [Parameter] public string? Name { get; set; }
            }
            """;

        var stripped = ComponentsApiEmitter.StripCommentsForA11yScan(text);

        Assert.DoesNotContain("role=\"img\"", stripped);
        Assert.DoesNotContain("KeyboardEventArgs", stripped);
        Assert.DoesNotContain("@onkeydown", stripped);
        Assert.Contains("[Parameter] public string? Name { get; set; }", stripped); // real code untouched
    }

    [Fact]
    public void Leaves_Real_Markup_And_Code_Outside_Comments_Untouched()
    {
        var text = "<div role=\"toolbar\" @onkeydown=\"HandleKeyDown\" tabindex=\"0\"></div>";

        var stripped = ComponentsApiEmitter.StripCommentsForA11yScan(text);

        Assert.Equal(text, stripped);
    }

    /// <summary>
    /// PR #356 round-8 (Codex P2) — round-7 only stripped Razor <c>@* *@</c> and C#
    /// <c>///</c> lines, so an ordinary <c>//</c> line comment still fed the raw
    /// <c>Contains("@onkeydown")</c> check. InputMask.razor's exact case: a historical
    /// note ("the previous component also handled Backspace in @onkeydown") with no
    /// live key handler nearby.
    /// </summary>
    [Fact]
    public void Strips_A_Plain_Line_Comment_Mentioning_The_Keyboard_Token()
    {
        var text = """
            @code {
                // The previous component also handled Backspace in @onkeydown and fired
                // ValueChanged a SECOND time.
                private void HandleInput() { }
            }
            """;

        var stripped = ComponentsApiEmitter.StripCommentsForA11yScan(text);

        Assert.DoesNotContain("@onkeydown", stripped);
        Assert.Contains("private void HandleInput() { }", stripped); // real code untouched
    }

    [Fact]
    public void Strips_A_Block_Comment_Mentioning_Aria_And_Role_Tokens()
    {
        var text = """
            <div /* role="toolbar" aria-expanded="true" is what the OLD markup used */ class="x"></div>
            """;

        var stripped = ComponentsApiEmitter.StripCommentsForA11yScan(text);

        Assert.DoesNotContain("role=\"toolbar\"", stripped);
        Assert.DoesNotContain("aria-expanded", stripped);
        Assert.Contains("class=\"x\"", stripped);
    }

    /// <summary>
    /// The exact failure mode a naive <c>//</c>-stripping regex introduces: treating the
    /// <c>//</c> inside a quoted URL as a line-comment start and deleting the rest of the
    /// line, including real trailing markup/code.
    /// </summary>
    [Fact]
    public void Does_Not_Treat_A_Url_Inside_A_String_As_A_Line_Comment()
    {
        var text = "<a href=\"https://example.com/docs\" role=\"link\">docs</a>";

        var stripped = ComponentsApiEmitter.StripLineAndBlockComments(text);

        Assert.Equal(text, stripped);
    }

    [Fact]
    public void Does_Not_Treat_A_Url_Inside_A_Csharp_String_Literal_As_A_Line_Comment()
    {
        var text = """
            @code {
                private const string Docs = "https://example.com/a11y"; // real trailing comment
            }
            """;

        var stripped = ComponentsApiEmitter.StripLineAndBlockComments(text);

        Assert.Contains("\"https://example.com/a11y\";", stripped); // URL string untouched
        Assert.DoesNotContain("real trailing comment", stripped);   // actual comment IS stripped
    }

    [Fact]
    public void Strips_A_Block_Comment_Without_Eating_A_Url_Right_Before_It()
    {
        var text = "const string x = \"https://example.com\"; /* aria-hidden noise */ var y = 1;";

        var stripped = ComponentsApiEmitter.StripLineAndBlockComments(text);

        Assert.Contains("\"https://example.com\"", stripped);
        Assert.DoesNotContain("aria-hidden noise", stripped);
        Assert.Contains("var y = 1;", stripped);
    }
}
