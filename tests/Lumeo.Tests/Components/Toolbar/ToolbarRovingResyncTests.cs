using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toolbar;

/// <summary>
/// Regression test for battle-wave2 #107 (keyboard-a11y, medium) — the roving
/// tabindex was initialised only on <c>firstRender</c>, so items added / removed /
/// enabled after load were never re-managed and the single-tab-stop invariant
/// broke (a newly-rendered item has no tabindex and becomes an extra tab stop).
///
/// bUnit cannot move real DOM focus, so we assert the OBSERVABLE MECHANISM: the
/// component re-invokes <c>InitToolbarRoving</c> (the JS pass that re-applies
/// "exactly one tabindex=0" over the CURRENT item set) after a render that
/// changes the item set — not just once on first render. We count the interop
/// calls via the TrackingInteropService.
/// </summary>
public class ToolbarRovingResyncTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ToolbarRovingResyncTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Reapplies_Roving_When_Item_Set_Changes_After_First_Render()
    {
        // Initial render: two items. Roving is initialised on first render.
        var cut = _ctx.Render<L.Toolbar>(p => p
            .AddChildContent("<button>A</button><button>B</button>"));

        cut.WaitForAssertion(() => Assert.NotEmpty(_interop.InitToolbarRovingCalls));
        var initialCalls = _interop.InitToolbarRovingCalls.Count;

        // A new item appears at runtime (e.g. a conditionally-rendered button).
        // Without the fix, OnAfterRenderAsync short-circuits on !firstRender and
        // the freshly-rendered item is left with no managed tabindex — a second
        // tab stop. With the fix, InitToolbarRoving runs again to re-establish
        // the single tab stop over the new item set.
        cut.Render(p => p
            .AddChildContent("<button>A</button><button>B</button><button>C</button>"));

        cut.WaitForAssertion(() =>
            Assert.True(
                _interop.InitToolbarRovingCalls.Count > initialCalls,
                "InitToolbarRoving must re-run after the item set changes, not only on first render."));
    }
}
