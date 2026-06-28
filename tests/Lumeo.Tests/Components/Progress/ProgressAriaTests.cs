using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Progress;

/// <summary>
/// Regression tests for the Progress ARIA correctness fixes (battle-wave3):
///   • n=15 — Animation=Indeterminate must report aria-busy=true and omit
///     aria-valuenow (it previously keyed only off the IsIndeterminate bool,
///     announcing a stale determinate value while the bar swept).
///   • n=51 — aria-valuenow must be the CLAMPED value so assistive tech agrees
///     with the visually clamped fill (it previously emitted the raw Value).
/// </summary>
public class ProgressAriaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ProgressAriaTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- n=15: Animation enum's Indeterminate path must drive ARIA ---

    [Fact]
    public void Animation_Indeterminate_Reports_Busy_To_AssistiveTech()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Animation, Lumeo.Progress.ProgressAnimation.Indeterminate));

        var bar = cut.Find("[role='progressbar']");
        // The bar is visually indeterminate via the enum, so AT must hear busy.
        Assert.Equal("true", bar.GetAttribute("aria-busy"));
    }

    [Fact]
    public void Animation_Indeterminate_Omits_Determinate_Valuenow()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Animation, Lumeo.Progress.ProgressAnimation.Indeterminate));

        var bar = cut.Find("[role='progressbar']");
        // A sweeping (value-less) bar must NOT announce a stale aria-valuenow.
        Assert.Null(bar.GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void Determinate_Bar_Still_Reports_Valuenow_And_Not_Busy()
    {
        // Guard the normal path: no indeterminate animation, value preserved.
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 42)
            .Add(b => b.Max, 100));

        var bar = cut.Find("[role='progressbar']");
        Assert.Equal("false", bar.GetAttribute("aria-busy"));
        Assert.Equal("42", bar.GetAttribute("aria-valuenow"));
    }

    // --- n=51: aria-valuenow clamps to the track range ---

    [Fact]
    public void AriaValuenow_Clamps_Over_Range_Value_To_Max()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 150)
            .Add(b => b.Max, 100));

        var bar = cut.Find("[role='progressbar']");
        // Fill clamps to 100% — the announced value must agree, not raw 150.
        Assert.Equal("100", bar.GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void AriaValuenow_Clamps_Negative_Value_To_Zero()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, -25)
            .Add(b => b.Max, 100));

        var bar = cut.Find("[role='progressbar']");
        Assert.Equal("0", bar.GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void AriaValuenow_Clamps_Over_Range_Value_With_Custom_Max()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 99)
            .Add(b => b.Max, 10));

        var bar = cut.Find("[role='progressbar']");
        Assert.Equal("10", bar.GetAttribute("aria-valuemax"));
        Assert.Equal("10", bar.GetAttribute("aria-valuenow"));
    }
}
