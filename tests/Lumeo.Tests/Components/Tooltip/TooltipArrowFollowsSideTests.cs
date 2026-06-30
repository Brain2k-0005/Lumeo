using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tooltip;

/// <summary>
/// Regression coverage for triage #92 (edge-data): the arrow was derived purely
/// from the C# <see cref="Lumeo.TooltipContent.Side"/> param, while the actual box
/// placement is owned by the JS collision logic in <c>positionFixed</c>, which can
/// auto-flip the box to the opposite side. Nothing exposed the resolved side to the
/// DOM, so the arrow could end up on the wrong edge (pointing away from the trigger),
/// and the arrow classes and the box could silently disagree.
///
/// The fix renders <c>data-side</c> on the role=tooltip content (the resolved-side
/// hook that <c>positionFixed</c> rewrites after a flip, and that a <c>[data-side]</c>
/// CSS rule can re-point the arrow from), and derives the arrow classes from the SAME
/// <c>SideStr</c> value that feeds <c>data-side</c> — so the arrow edge and the
/// data-side attribute can never diverge on the C# (no-flip / live-Side-change) path.
///
/// bUnit cannot run floating-ui, so these tests assert the observable DOM contract:
/// the <c>data-side</c> attribute exists, reflects the side, stays in sync with the
/// arrow's directional class, and both update together on a live Side change.
/// </summary>
public class TooltipArrowFollowsSideTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TooltipArrowFollowsSideTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Drives TooltipContent.Side from its own parameter so a re-render via
    // cut.Render(...) changes the nested content's Side while the tooltip stays open.
    // Mirrors TooltipSideProbe in TooltipRepositionOnSideChangeTests.
    private sealed class TooltipSideProbe : ComponentBase
    {
        [Parameter] public L.Side Side { get; set; } = L.Side.Top;

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
                b.AddAttribute(3, "Side", Side);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Tooltip text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    private static void OpenTooltip(IRenderedComponent<TooltipSideProbe> cut)
        => cut.Find("div").TriggerEvent("onmouseenter", new MouseEventArgs());

    // Maps a side to the load-bearing directional utility on the rotated-square arrow.
    private static string ArrowEdgeClassFor(string side) => side switch
    {
        "top" => "top-full",
        "bottom" => "bottom-full",
        "left" => "left-full",
        "right" => "right-full",
        _ => "top-full"
    };

    [Fact]
    public void Open_Tooltip_Content_Carries_DataSide_Reflecting_The_Side()
    {
        // Without the fix the content has no data-side attribute at all, so the
        // resolved side is invisible and the arrow has no hook to follow a flip.
        var cut = _ctx.Render<TooltipSideProbe>(p => p.Add(x => x.Side, L.Side.Bottom));
        OpenTooltip(cut);

        var content = cut.Find("[role='tooltip']");
        Assert.Equal("bottom", content.GetAttribute("data-side"));
    }

    [Fact]
    public void Arrow_Edge_Matches_DataSide_Single_Source_Of_Truth()
    {
        // The arrow's directional class is derived from the SAME SideStr that feeds
        // data-side, so the two can never disagree on the C# path.
        var cut = _ctx.Render<TooltipSideProbe>(p => p.Add(x => x.Side, L.Side.Right));
        OpenTooltip(cut);

        var content = cut.Find("[role='tooltip']");
        var dataSide = content.GetAttribute("data-side");
        Assert.Equal("right", dataSide);

        // The arrow is the inner div with the rotate-45 marker; its edge class must
        // correspond to data-side (right -> right-full), not a stale Top default.
        var arrow = content.QuerySelector(".rotate-45");
        Assert.NotNull(arrow);
        var arrowClass = arrow!.GetAttribute("class") ?? "";
        Assert.Contains(ArrowEdgeClassFor(dataSide!), arrowClass);
    }

    [Fact]
    public void Live_Side_Change_Updates_DataSide_And_Arrow_Together()
    {
        var cut = _ctx.Render<TooltipSideProbe>(p => p.Add(x => x.Side, L.Side.Top));
        OpenTooltip(cut);

        var content = cut.Find("[role='tooltip']");
        Assert.Equal("top", content.GetAttribute("data-side"));
        Assert.Contains("top-full", content.QuerySelector(".rotate-45")!.GetAttribute("class") ?? "");

        // Consumer flips the preferred Side while the tooltip stays open.
        cut.Render(p => p.Add(x => x.Side, L.Side.Bottom));

        content = cut.Find("[role='tooltip']");
        Assert.Equal("bottom", content.GetAttribute("data-side"));
        // The arrow edge must move with it — bottom side puts the arrow on bottom-full,
        // never the old top-full.
        var arrowClass = content.QuerySelector(".rotate-45")!.GetAttribute("class") ?? "";
        Assert.Contains("bottom-full", arrowClass);
        Assert.DoesNotContain("top-full", arrowClass);
    }

    [Fact]
    public void Collision_Flip_Resolved_Side_Moves_Arrow_And_DataSide()
    {
        // Codex P2: positionFixed can collision-flip a preferred-Top tooltip to render BELOW its trigger.
        // It now ECHOES the resolved side back; the arrow + data-side must follow that resolved side, not
        // the requested Top. bUnit can't run floating-ui, so we stub the JS return to simulate the flip.
        // The interop imports components.js with a version cache-buster, so stub THAT exact path.
        var v = typeof(Lumeo.Services.ComponentInteropService).Assembly
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(Lumeo.Services.ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        var module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        // positionFixed is called with 6 args, so match by identifier with an any-args predicate.
        module.Setup<string>("positionFixed", _ => true).SetResult("bottom");

        var cut = _ctx.Render<TooltipSideProbe>(p => p.Add(x => x.Side, L.Side.Top));
        OpenTooltip(cut);

        var content = cut.Find("[role='tooltip']");
        // The RESOLVED side (bottom) wins over the requested Top.
        Assert.Equal("bottom", content.GetAttribute("data-side"));
        var arrowClass = content.QuerySelector(".rotate-45")!.GetAttribute("class") ?? "";
        Assert.Contains("bottom-full", arrowClass);   // arrow moved to the trigger-facing edge
        Assert.DoesNotContain("top-full", arrowClass); // not the stale requested side
    }
}
