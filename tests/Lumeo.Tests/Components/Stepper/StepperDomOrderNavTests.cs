using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.Stepper;

/// <summary>
/// Regression for the Stepper "triage reorder" leg: a keyed reorder MOVES reused
/// step tab buttons to new DOM positions WITHOUT rebuilding the C# <c>_steps</c>
/// registry, so the integer mount-order index goes stale. The roving / arrow-key
/// navigation must therefore consult the LIVE DOM order
/// (<see cref="IComponentInteropService.GetOrderedDescendantIds"/>) and step against
/// it, falling back to the registry-index order when the probe is empty/NULL.
///
/// bUnit cannot physically reorder reused child instances, so we model the EFFECT:
/// the markup still renders the tabs in registry order, but we seed the interop
/// double's <c>OrderedDescendantIds[tablistId]</c> with a REORDERED id list and
/// assert the roving tab stop follows THAT order — not the registry order.
///
/// Per the WAI-ARIA tablist contract this asserts MARKUP only (the roving
/// <c>tabindex</c> + the recorded interop probe call) — never real DOM focus — and
/// selection (<c>aria-selected</c> / <c>aria-current</c>) must stay put because the
/// arrow keys move focus, not activation (#197). Method/class names are distinct
/// from the sibling Stepper test classes in this folder.
/// </summary>
public class StepperDomOrderNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public StepperDomOrderNavTests()
    {
        _ctx.AddLumeoServices();
        // Override the loose-mode ComponentInteropService with the tracking double so
        // the DOM-order probe is configurable (last registration wins). The sibling
        // reorder tests for RadioGroup register it the same way.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Three plain steps, active = 0 so tab[0] starts as the single roving stop.
    private IRenderedComponent<Lumeo.Stepper> RenderThreeSteps()
        => _ctx.Render<Lumeo.Stepper>(p =>
        {
            p.Add(s => s.ActiveStep, 0);
            p.AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "One")
                .AddChildContent("body-one"));
            p.AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "Two")
                .AddChildContent("body-two"));
            p.AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "Three")
                .AddChildContent("body-three"));
        });

    [Fact]
    public async Task Arrow_And_End_Roving_Follows_Live_DOM_Order_After_A_Keyed_Reorder()
    {
        var cut = RenderThreeSteps(); // registry order: step0, step1, step2

        var tabs = cut.FindAll("[role='tab']");
        var id0 = tabs[0].GetAttribute("id");
        var id1 = tabs[1].GetAttribute("id");
        var id2 = tabs[2].GetAttribute("id");
        var containerId = cut.Find("[role='tablist']").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(containerId), "The tablist must carry a stable container id.");

        // Seed the live-DOM order as 0, 2, 1 — the keyed reorder bUnit can't do
        // physically. The C# registry still lists 0, 1, 2 (mount order).
        _interop.OrderedDescendantIds[containerId!] = new[] { id0!, id2!, id1! };

        // ArrowRight from the focused tab[0]: in DOM order its neighbour is step
        // index 2, NOT the registry neighbour (step index 1). The roving tab stop
        // must therefore land on tab[2].
        await cut.InvokeAsync(() =>
            cut.FindAll("[role='tab']")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }));

        var afterArrow = cut.FindAll("[role='tab']");
        Assert.Equal("0", afterArrow[2].GetAttribute("tabindex"));  // DOM-order neighbour
        Assert.Equal("-1", afterArrow[1].GetAttribute("tabindex")); // would be 0 under registry order
        Assert.Equal("-1", afterArrow[0].GetAttribute("tabindex"));
        Assert.Single(cut.FindAll("[role='tab'][tabindex='0']"));

        // The probe was actually consulted with the tablist id + the role=tab selector.
        Assert.Contains((containerId!, "[role='tab']"), _interop.OrderedDescendantCalls);

        // Focus != activation: selection / current stay on the ORIGINAL active step 0.
        Assert.Equal("true", afterArrow[0].GetAttribute("aria-selected"));
        Assert.Equal("step", afterArrow[0].GetAttribute("aria-current"));
        Assert.Equal("false", afterArrow[2].GetAttribute("aria-selected"));
        Assert.Null(afterArrow[2].GetAttribute("aria-current"));

        // End jumps to the LAST tab in DOM order (step index 1) — NOT registry-last
        // (step index 2). Same roving-only contract: selection is untouched.
        await cut.InvokeAsync(() =>
            cut.FindAll("[role='tab']")[2].KeyDown(new KeyboardEventArgs { Key = "End" }));

        var afterEnd = cut.FindAll("[role='tab']");
        Assert.Equal("0", afterEnd[1].GetAttribute("tabindex"));  // DOM-order last
        Assert.Equal("-1", afterEnd[2].GetAttribute("tabindex")); // would be 0 under registry order
        Assert.Single(cut.FindAll("[role='tab'][tabindex='0']"));
        Assert.Equal("true", afterEnd[0].GetAttribute("aria-selected"));
        Assert.Equal("step", afterEnd[0].GetAttribute("aria-current"));
    }
}
