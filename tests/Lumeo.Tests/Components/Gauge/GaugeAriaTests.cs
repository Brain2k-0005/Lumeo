using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Gauge;

/// <summary>
/// Regression for the wave-3 accessibility edge cases:
/// <list type="bullet">
/// <item>#42 — <c>aria-valuenow</c> was emitted raw while the visual clamps to
/// <c>[Min,Max]</c>, so an out-of-range value desynced the announced value from
/// the meter. It must now be clamped to the declared range.</item>
/// <item>#43 — an empty/whitespace <c>Label</c> slipped past the <c>??</c>
/// fallback, stripping the meter's accessible name and blanking the visible
/// value. It must now fall back to the percentage.</item>
/// </list>
/// </summary>
public class GaugeAriaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public GaugeAriaTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void AriaValueNow_Clamped_When_Value_Above_Max()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 150)
            .Add(g => g.Min, 0)
            .Add(g => g.Max, 100)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Radial));

        var el = cut.Find("[role='meter']");
        Assert.Equal("100", el.GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void AriaValueNow_Clamped_When_Value_Below_Min()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, -20)
            .Add(g => g.Min, 0)
            .Add(g => g.Max, 100)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Linear));

        var el = cut.Find("[role='meter']");
        Assert.Equal("0", el.GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void AriaValueNow_Unchanged_When_Value_In_Range()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 75)
            .Add(g => g.Min, 0)
            .Add(g => g.Max, 100)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Radial));

        var el = cut.Find("[role='meter']");
        Assert.Equal("75", el.GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void Whitespace_Label_Falls_Back_To_Percentage_For_AriaLabel()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 50)
            .Add(g => g.Min, 0)
            .Add(g => g.Max, 100)
            .Add(g => g.Label, "   ")
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Radial));

        var el = cut.Find("[role='meter']");
        Assert.Equal("50%", el.GetAttribute("aria-label"));
    }

    [Fact]
    public void Whitespace_Label_Falls_Back_To_Percentage_For_VisibleValue()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 50)
            .Add(g => g.Min, 0)
            .Add(g => g.Max, 100)
            .Add(g => g.Label, "")
            .Add(g => g.ShowValue, true)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Radial));

        Assert.Contains("50%", cut.Markup);
    }

    [Fact]
    public void Real_Label_Still_Used_As_AriaLabel()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 80)
            .Add(g => g.Label, "CPU")
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Radial));

        var el = cut.Find("[role='meter']");
        Assert.Equal("CPU", el.GetAttribute("aria-label"));
    }
}
