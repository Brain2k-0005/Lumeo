using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Gauge;

/// <summary>
/// Regression: at <c>Value == Min</c> the value arc rendered with
/// <c>stroke-dasharray="0 C"</c> and a round linecap — the caps still paint,
/// leaving a dot artifact at the arc start. The value arc is now omitted
/// entirely when the normalized value is 0 (only the track circle remains).
/// </summary>
public class GaugeZeroValueTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public GaugeZeroValueTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Radial_At_Zero_Renders_Only_Track_Circle()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 0)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Radial));

        Assert.Single(cut.FindAll("circle"));
    }

    [Fact]
    public void Radial_At_NonZero_Min_Renders_Only_Track_Circle()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 10)
            .Add(g => g.Min, 10)
            .Add(g => g.Max, 110)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Radial));

        Assert.Single(cut.FindAll("circle"));
    }

    [Fact]
    public void Arc_At_Zero_Renders_Only_Track_Circle()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 0)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Arc));

        Assert.Single(cut.FindAll("circle"));
    }

    [Fact]
    public void Radial_Above_Min_Renders_Value_Arc()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 1)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Radial));

        Assert.Equal(2, cut.FindAll("circle").Count);
    }
}
