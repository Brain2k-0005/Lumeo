using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toolbar;

/// <summary>
/// Triage #106 (medium, state-on-data-change) — in auto-measure mode the JS
/// ResizeObserver reports the real (fittingCount, totalCount). When everything
/// fits (fitting == total) the trigger-visibility test
/// (<c>VisibleCount &gt;= 0 &amp;&amp; VisibleCount &lt; TotalCount</c>) is false, so no
/// empty "⋯" overflow button is rendered.
///
/// The bug: <c>OnParametersSet</c> rebuilt the cascaded context with
/// <c>TotalCount = int.MaxValue</c>. On any unrelated parent re-render that ran
/// <c>OnParametersSet</c>, the test became <c>fitting &lt; int.MaxValue</c> → true,
/// so the empty overflow trigger re-appeared even though every item fit.
///
/// The fix persists the last measured total in <c>_autoTotalCount</c> and rebuilds
/// the auto-measure context against it (only the explicit-<c>VisibleCount</c> manual
/// path keeps int.MaxValue). bUnit can't run the real ResizeObserver, so the
/// measurement is driven directly through the internal <c>OnOverflowMeasured</c>
/// callback; the assertion is purely on the rendered markup (trigger present/absent).
/// </summary>
public class ToolbarOverflowStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ToolbarOverflowStateTests()
    {
        _ctx.AddLumeoServices();
        // No-op interop so the firstRender RegisterToolbarOverflow wiring doesn't
        // need a real JS runtime; the measurement is injected directly below.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static bool HasOverflowTrigger(IRenderedComponent<L.Toolbar> cut) =>
        cut.FindAll("[data-toolbar-overflow-trigger]").Count > 0;

    [Fact]
    public async Task AutoMeasure_All_Items_Fit_Survives_Unrelated_ReRender()
    {
        // Auto-measure mode: Overflow=true, VisibleCount left at its -1 default.
        var cut = _ctx.Render<L.Toolbar>(p => p
            .Add(t => t.Overflow, true)
            .AddChildContent("<button>A</button><button>B</button><button>C</button>"));

        // Simulate the JS ResizeObserver reporting that all 3 items fit (fitting == total).
        await cut.InvokeAsync(() => cut.Instance.OnOverflowMeasured(3, 3));

        // Everything fits → no empty "⋯" overflow trigger.
        Assert.False(HasOverflowTrigger(cut), "Overflow trigger should be hidden when all items fit.");

        // An unrelated parent re-render re-runs OnParametersSet (here: a cosmetic
        // OverflowLabel change, unrelated to layout). Before the fix this rebuilt
        // the context with TotalCount = int.MaxValue, resurrecting the empty trigger.
        cut.Render(p => p
            .Add(t => t.Overflow, true)
            .Add(t => t.OverflowLabel, "More actions")
            .AddChildContent("<button>A</button><button>B</button><button>C</button>"));

        // The measured "everything fits" state must survive the re-render.
        Assert.False(HasOverflowTrigger(cut), "Overflow trigger must stay hidden after an unrelated re-render when all items fit.");
    }

    [Fact]
    public async Task AutoMeasure_Overflowing_Still_Shows_Trigger_After_ReRender()
    {
        // Sanity guard: when items genuinely overflow (fitting < total), the trigger
        // is shown and the fix must not suppress it across re-renders.
        var cut = _ctx.Render<L.Toolbar>(p => p
            .Add(t => t.Overflow, true)
            .AddChildContent("<button>A</button><button>B</button><button>C</button>"));

        // Only 2 of 3 fit.
        await cut.InvokeAsync(() => cut.Instance.OnOverflowMeasured(2, 3));
        Assert.True(HasOverflowTrigger(cut), "Overflow trigger should be shown when not all items fit.");

        cut.Render(p => p
            .Add(t => t.Overflow, true)
            .Add(t => t.OverflowLabel, "More actions")
            .AddChildContent("<button>A</button><button>B</button><button>C</button>"));

        Assert.True(HasOverflowTrigger(cut), "Overflow trigger must remain after an unrelated re-render while items overflow.");
    }
}
