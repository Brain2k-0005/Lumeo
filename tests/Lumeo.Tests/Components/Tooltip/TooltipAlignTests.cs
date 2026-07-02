using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tooltip;

/// <summary>
/// Coverage for W1: TooltipContent.Align parameter.
/// Verifies that the Align value is passed as the third argument to positionFixed
/// (the "align" slot), that data-align is rendered on the tooltip element, and that
/// a live Align change while the tooltip is open re-runs positionFixed with the new
/// value — mirroring the existing TooltipRepositionOnSideChangeTests pattern.
/// </summary>
public class TooltipAlignTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TooltipAlignTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Host that drives TooltipContent.Align from its own parameter so a
    // re-render via probe.Render(...) changes the nested content's param
    // while the tooltip stays open — mirrors TooltipSideProbe in
    // TooltipRepositionOnSideChangeTests.
    private sealed class TooltipAlignProbe : ComponentBase
    {
        [Parameter] public L.Align Align { get; set; } = L.Align.Center;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "Align", Align);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Tooltip text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    private static void OpenTooltip(IRenderedComponent<TooltipAlignProbe> cut)
        => cut.Find("div").TriggerEvent("onmouseenter", new MouseEventArgs());

    private int PositionFixedCalls()
        => _ctx.JSInterop.Invocations.Count(i => i.Identifier == "positionFixed");

    // ---- data-align attribute ----------------------------------------------------------

    [Theory]
    [InlineData(L.Align.Center, "center")]
    [InlineData(L.Align.Start, "start")]
    [InlineData(L.Align.End, "end")]
    public void DataAlign_Attribute_Matches_Align_Parameter(L.Align align, string expected)
    {
        var cut = _ctx.Render<TooltipAlignProbe>(p => p.Add(x => x.Align, align));
        OpenTooltip(cut);

        var tooltip = cut.Find("[role='tooltip']");
        Assert.Equal(expected, tooltip.GetAttribute("data-align"));
    }

    // ---- positionFixed align argument --------------------------------------------------

    [Fact]
    public void Default_Align_Center_Passes_Center_To_PositionFixed()
    {
        var cut = _ctx.Render<TooltipAlignProbe>(p => p.Add(x => x.Align, L.Align.Center));
        OpenTooltip(cut);

        Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "positionFixed" && Equals(i.Arguments[2], "center"));
    }

    [Fact]
    public void Align_Start_Passes_Start_To_PositionFixed()
    {
        var cut = _ctx.Render<TooltipAlignProbe>(p => p.Add(x => x.Align, L.Align.Start));
        OpenTooltip(cut);

        Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "positionFixed" && Equals(i.Arguments[2], "start"));
    }

    [Fact]
    public void Align_End_Passes_End_To_PositionFixed()
    {
        var cut = _ctx.Render<TooltipAlignProbe>(p => p.Add(x => x.Align, L.Align.End));
        OpenTooltip(cut);

        Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "positionFixed" && Equals(i.Arguments[2], "end"));
    }

    // ---- live Align change re-runs PositionFixed ---------------------------------------

    [Fact]
    public void Changing_Align_While_Open_ReRuns_PositionFixed()
    {
        var cut = _ctx.Render<TooltipAlignProbe>(p => p.Add(x => x.Align, L.Align.Center));
        OpenTooltip(cut);

        var callsAfterOpen = PositionFixedCalls();
        Assert.True(callsAfterOpen >= 1);

        cut.Render(p => p.Add(x => x.Align, L.Align.Start));

        Assert.True(PositionFixedCalls() > callsAfterOpen);
        Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "positionFixed" && Equals(i.Arguments[2], "start"));
    }

    [Fact]
    public void Same_Align_ReRender_While_Open_Does_Not_ReRun_PositionFixed()
    {
        var cut = _ctx.Render<TooltipAlignProbe>(p => p.Add(x => x.Align, L.Align.Center));
        OpenTooltip(cut);
        var callsAfterOpen = PositionFixedCalls();

        cut.Render(p => p.Add(x => x.Align, L.Align.Center));

        Assert.Equal(callsAfterOpen, PositionFixedCalls());
    }
}
