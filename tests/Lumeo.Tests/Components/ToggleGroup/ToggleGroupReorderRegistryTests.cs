using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ToggleGroup;

/// <summary>
/// #69 (PURE @key reorder leg): the roving arrow-key navigation must track the
/// LIVE DOM order, not the C# registration-order registry. A pure @key reorder
/// MOVES reused ToggleGroupItem instances to new DOM positions WITHOUT firing
/// their OnParametersSet, so the registration-order <c>_itemValues</c> list goes
/// stale — the add/remove/value-change legs (already covered by
/// ToggleGroupKeyboardNavTests) DO fire OnParametersSet and rebuild it, but a
/// pure swap does not. bUnit cannot physically reorder reused child instances, so
/// we model the EFFECT: seed the TrackingInteropService DOM-order probe
/// (<see cref="TrackingInteropService.OrderedDescendantIds"/>) with a reordered id
/// list and assert the arrow nav follows it. Focus moves are asserted via the
/// RECORDED interop FocusElement calls — never real DOM focus.
/// </summary>
public class ToggleGroupReorderRegistryTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ToggleGroupReorderRegistryTests()
    {
        _ctx.AddLumeoServices();
        // Replace the real interop with the tracking one so the DOM-order probe is
        // configurable and FocusElement calls are recorded.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment Items(params string[] values) => b =>
    {
        var seq = 0;
        foreach (var v in values)
        {
            var value = v;
            b.OpenComponent<L.ToggleGroupItem>(seq++);
            b.AddAttribute(seq++, "Value", value);
            b.AddAttribute(seq++, "ChildContent",
                (RenderFragment)(c => c.AddContent(0, value.ToUpperInvariant())));
            b.CloseComponent();
        }
    };

    [Fact]
    public void Arrow_Nav_Follows_Live_DOM_Order_After_A_Keyed_Reorder()
    {
        // The C# registry mounts in declaration order a, b, c. The live DOM (after a
        // pure @key reorder bUnit cannot physically perform) is a, c, b. The nav must
        // consult the DOM-order probe (Interop.GetOrderedDescendantIds), so ArrowRight
        // from 'a' moves to the DOM neighbour 'c' — NOT the registration neighbour 'b'.
        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.ChildContent, Items("a", "b", "c"))); // registry: a, b, c

        var buttons = cut.FindAll("button");
        var idA = buttons[0].GetAttribute("id");
        var idB = buttons[1].GetAttribute("id");
        var idC = buttons[2].GetAttribute("id");
        var containerId = cut.Find("[role='group']").GetAttribute("id");

        // Seed the live-DOM order as a, c, b (the reorder bUnit can't physically do).
        _interop.OrderedDescendantIds[containerId!] = new[] { idA!, idC!, idB! };

        // Nothing is selected and no item has been focused, so the nav anchors on the
        // first enabled item 'a'; ArrowRight then steps to its live-DOM neighbour.
        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // DOM-order neighbour after 'a' is 'c'. Without the DOM-order probe the
        // registration order (a, b, c) would have focused 'b'.
        Assert.Equal(idC, _interop.FocusedElementIds.Last());
        Assert.NotEqual(idB, _interop.FocusedElementIds.Last());
    }
}
