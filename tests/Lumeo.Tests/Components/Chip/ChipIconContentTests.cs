using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chip;

/// <summary>
/// Tests for the <see cref="L.Chip.IconContent"/> leading-icon slot.
/// </summary>
public class ChipIconContentTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ChipIconContentTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- parity: ChildContent-only must produce the same markup as before IconContent existed ---

    [Fact]
    public void ChildContent_Only_Has_No_Icon_Wrapper_Span()
    {
        var cut = _ctx.Render<L.Chip>(p => p.AddChildContent("Label"));

        // There must be no span carrying the shrink-0 class that the IconContent
        // wrapper produces.  The ONLY span in the output should be the label span.
        var spans = cut.FindAll("span");
        Assert.All(spans, s => Assert.DoesNotContain("shrink-0", s.GetAttribute("class") ?? ""));
    }

    // --- IconContent renders in the leading slot with shrink-0 ---

    [Fact]
    public void IconContent_Renders_Leading_Wrapper_With_ShrinkZero()
    {
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.IconContent, b => b.AddMarkupContent(0, "<span class=\"dot\"></span>"))
            .AddChildContent("Label"));

        // The outer icon-slot span must carry inline-flex and shrink-0.
        var iconWrapper = cut.Find("span.shrink-0");
        Assert.Contains("inline-flex", iconWrapper.GetAttribute("class") ?? "");
    }

    [Fact]
    public void IconContent_Is_Leading_Before_Label_Span()
    {
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.IconContent, b => b.AddMarkupContent(0, "<span id=\"icon-inner\"></span>"))
            .AddChildContent("Label"));

        // Both the icon wrapper and the label span must exist; the icon wrapper
        // must appear earlier in the markup (lower index).
        var allSpans = cut.FindAll("span");
        var iconIdx = allSpans
            .Select((s, i) => (span: s, i))
            .First(x => x.span.QuerySelector("#icon-inner") is not null || x.span.Id == "icon-inner").i;
        var labelIdx = allSpans
            .Select((s, i) => (span: s, i))
            .Last(x => x.span.TextContent.Contains("Label")).i;

        Assert.True(iconIdx < labelIdx, "icon wrapper must precede the label span");
    }

    // --- sized content inside IconContent keeps its explicit box ---

    [Fact]
    public void IconContent_Sized_Dot_Span_Preserves_Inline_Style()
    {
        const string dotStyle = "width:8px;height:8px;border-radius:50%;background:#22c55e;display:inline-block;";

        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.IconContent, b =>
                b.AddMarkupContent(0, $"<span class=\"dot\" style=\"{dotStyle}\"></span>"))
            .AddChildContent("Online"));

        var dotSpan = cut.Find("span.dot");
        var style = dotSpan.GetAttribute("style") ?? "";
        // The style attribute must survive unchanged — shrink-0 on the wrapper
        // prevents the flex parent from collapsing this child.
        Assert.Contains("width:8px", style);
        Assert.Contains("height:8px", style);
    }

    // --- IconContent works with Closable ---

    [Fact]
    public void IconContent_And_Closable_Both_Render()
    {
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.IconContent, b => b.AddMarkupContent(0, "<span class=\"icon\"></span>"))
            .Add(c => c.Closable, true)
            .Add(c => c.OnClose, EventCallback.Empty)
            .AddChildContent("Tag"));

        Assert.NotNull(cut.Find("span.icon"));   // icon slot present
        Assert.NotNull(cut.Find("button"));       // close button present
    }

    // --- Icon string parameter still takes priority over IconContent ---

    [Fact]
    public void Icon_String_Takes_Priority_Over_IconContent()
    {
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Icon, "⭐")
            .Add(c => c.IconContent, b => b.AddMarkupContent(0, "<span class=\"slot-icon\"></span>"))
            .AddChildContent("Star"));

        // The string-Icon span (opacity-70) must be present; the slot wrapper must not.
        var opacitySpan = cut.FindAll("span")
            .FirstOrDefault(s => (s.GetAttribute("class") ?? "").Contains("opacity-70"));
        Assert.NotNull(opacitySpan);
        Assert.Empty(cut.FindAll("span.shrink-0.inline-flex"));
    }
}
