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
}
