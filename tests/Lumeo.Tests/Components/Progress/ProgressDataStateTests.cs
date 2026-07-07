using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Progress;

/// <summary>
/// shadcn-parity Wave 2: Radix-style data-state (loading|complete|indeterminate)
/// plus data-value / data-max on <see cref="Lumeo.Progress"/>.
/// </summary>
public class ProgressDataStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ProgressDataStateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void DataState_Loading_When_Partial()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p.Add(x => x.Value, 40));
        var bar = cut.Find("[role=progressbar]");
        Assert.Equal("loading", bar.GetAttribute("data-state"));
        Assert.Equal("40", bar.GetAttribute("data-value"));
        Assert.Equal("100", bar.GetAttribute("data-max"));
    }

    [Fact]
    public void DataState_Complete_When_Full()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p.Add(x => x.Value, 100));
        Assert.Equal("complete", cut.Find("[role=progressbar]").GetAttribute("data-state"));
    }

    [Fact]
    public void DataState_Indeterminate_Drops_DataValue()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(x => x.Value, 40)
            .Add(x => x.IsIndeterminate, true));

        var bar = cut.Find("[role=progressbar]");
        Assert.Equal("indeterminate", bar.GetAttribute("data-state"));
        Assert.False(bar.HasAttribute("data-value"));   // unknown value → attribute omitted
        Assert.Equal("100", bar.GetAttribute("data-max"));
    }

    [Fact]
    public void NoLabel_Linear_Indicator_Carries_Data_Hooks()
    {
        // Round-8 (Codex): the ShowLabel=true branch put data-state/value/max on BOTH
        // the progressbar root and its fill indicator, but the default (no-label)
        // linear branch put them only on the root — the indicator div was bare.
        // Fix mirrors the label branch: the indicator carries all three too.
        var cut = _ctx.Render<Lumeo.Progress>(p => p.Add(x => x.Value, 40));
        // Default: ShowLabel=false, Shape=Linear → root is the progressbar, its only
        // child div is the fill indicator.
        var indicator = cut.Find("[role=progressbar] > div");
        Assert.Equal("loading", indicator.GetAttribute("data-state"));
        Assert.Equal("40", indicator.GetAttribute("data-value"));
        Assert.Equal("100", indicator.GetAttribute("data-max"));
    }

    [Fact]
    public void Custom_Max_Reflected_In_DataMax()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(x => x.Value, 5)
            .Add(x => x.Max, 10));

        var bar = cut.Find("[role=progressbar]");
        Assert.Equal("10", bar.GetAttribute("data-max"));
        Assert.Equal("5", bar.GetAttribute("data-value"));
        Assert.Equal("loading", bar.GetAttribute("data-state"));
    }
}
