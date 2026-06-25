using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Regression coverage for the structural-shrink update path (battle-wave2 #17).
///
/// ECharts' <c>setOption(option, { notMerge:false })</c> merges component arrays BY
/// INDEX. So when the consumer removes a series / data point / annotation from
/// <c>Option</c>, the dropped tail-end items survive on the canvas as ghosts unless the
/// update passes <c>replaceMerge:["series",…]</c>. Chart now fingerprints the option's
/// shape and, on any shrink, sends replaceMerge so the removals actually stick — while a
/// grown/unchanged option keeps the default index-merge so values still tween.
///
/// These tests assert on the 4th argument of the JS <c>updateChart(elementId, json,
/// notMerge, replaceMergeJson)</c> interop call. Without the fix, that argument was
/// hard-coded null on every non-phantom-flip update, so the shrink assertion fails.
/// </summary>
public class ChartReplaceMergeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo.Charts/js/echarts-interop.js");
        module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string ThreeSeries =
        "{\"series\":[{\"type\":\"bar\",\"data\":[1,2,3]},{\"type\":\"bar\",\"data\":[4,5,6]},{\"type\":\"bar\",\"data\":[7,8,9]}]}";
    private const string TwoSeries =
        "{\"series\":[{\"type\":\"bar\",\"data\":[1,2,3]},{\"type\":\"bar\",\"data\":[4,5,6]}]}";
    private const string ThreeSeriesShortData =
        "{\"series\":[{\"type\":\"bar\",\"data\":[1,2]},{\"type\":\"bar\",\"data\":[4,5,6]},{\"type\":\"bar\",\"data\":[7,8,9]}]}";

    // Helper: the replaceMerge JSON (4th arg) of the LAST updateChart invocation.
    // .Last(predicate) throws if no updateChart was recorded, which is itself a failure
    // (the re-render must have pushed an update).
    private string? LastUpdateReplaceMerge()
    {
        var update = _ctx.JSInterop.Invocations.Last(i => i.Identifier == "updateChart");
        // updateChart(elementId, optionsJson, notMerge, replaceMergeJson)
        return update.Arguments.Count > 3 ? update.Arguments[3] as string : null;
    }

    [Fact]
    public void Removing_A_Series_Sends_ReplaceMerge_Series()
    {
        var cut = _ctx.Render<L.Chart>(p => p.Add(x => x.OptionJson, ThreeSeries));

        // Consumer drops the 3rd series.
        cut.Render(p => p.Add(x => x.OptionJson, TwoSeries));

        var replaceMerge = LastUpdateReplaceMerge();
        Assert.NotNull(replaceMerge);
        Assert.Contains("series", replaceMerge);
    }

    [Fact]
    public void Shrinking_A_Series_Data_Length_Sends_ReplaceMerge_Series()
    {
        var cut = _ctx.Render<L.Chart>(p => p.Add(x => x.OptionJson, ThreeSeries));

        // Same series count, but the first series loses a data point.
        cut.Render(p => p.Add(x => x.OptionJson, ThreeSeriesShortData));

        var replaceMerge = LastUpdateReplaceMerge();
        Assert.NotNull(replaceMerge);
        Assert.Contains("series", replaceMerge);
    }

    [Fact]
    public void Growing_Series_Count_Does_Not_Send_ReplaceMerge()
    {
        var cut = _ctx.Render<L.Chart>(p => p.Add(x => x.OptionJson, TwoSeries));

        // Consumer adds a 3rd series — index-merge handles this cleanly, no replaceMerge
        // (which would needlessly tear down the kept series' animation state).
        cut.Render(p => p.Add(x => x.OptionJson, ThreeSeries));

        var replaceMerge = LastUpdateReplaceMerge();
        Assert.Null(replaceMerge);
    }
}
