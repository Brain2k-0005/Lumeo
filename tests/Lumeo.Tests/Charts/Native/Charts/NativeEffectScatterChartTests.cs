using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Charts;

/// <summary>Covers <see cref="L.NativeEffectScatterChart"/> — every point gets a
/// ripple, whose <c>prefers-reduced-motion</c> handling lives in a hand-authored
/// CSS rule (lumeo.css's <c>.lumeo-chart-native-ripple</c>), not a Tailwind
/// utility, so it's covered by structural presence here rather than a computed
/// animation-name assertion (bUnit has no CSSOM/browser to read that from).</summary>
public class NativeEffectScatterChartTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Every_Point_Gets_A_Ripple_Circle()
    {
        var cut = _ctx.Render<L.NativeEffectScatterChart>(p => p.Add(b => b.Series, new List<L.NativeEffectScatterChart.EffectScatterSeriesData>
        {
            new() { Name = "S1", Points = new List<double[]> { new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 }, new[] { 5.0, 6.0 } } },
        }));

        Assert.Equal(3, cut.FindAll("circle.lumeo-chart-native-point").Count);
        Assert.Equal(3, cut.FindAll(".lumeo-chart-native-ripple").Count);
    }

    [Fact]
    public void Ripple_Is_Marked_Aria_Hidden_So_It_Is_Not_Announced_Twice()
    {
        var cut = _ctx.Render<L.NativeEffectScatterChart>(p => p.Add(b => b.Series, new List<L.NativeEffectScatterChart.EffectScatterSeriesData>
        {
            new() { Name = "S1", Points = new List<double[]> { new[] { 1.0, 2.0 } } },
        }));

        Assert.Equal("true", cut.Find(".lumeo-chart-native-ripple").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void SymbolSize_Defaults_To_Twenty_Matching_The_Legacy_Wrapper()
    {
        var cut = _ctx.Render<L.NativeEffectScatterChart>(p => p.Add(b => b.Series, new List<L.NativeEffectScatterChart.EffectScatterSeriesData>
        {
            new() { Name = "S1", Points = new List<double[]> { new[] { 1.0, 2.0 } } },
        }));

        var r = double.Parse(cut.Find("circle.lumeo-chart-native-point").GetAttribute("r")!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(10, r, 3); // 20/2, matches legacy's SymbolSize ?? 20 default
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Lumeo.slnx")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return dir!;
    }

    /// <summary>
    /// The actual rendered VISUAL property here — bUnit has no browser/CSSOM, so
    /// the ripple's keyframe scale is asserted directly against the source CSS
    /// text (same "read the real file" technique <c>RadiusTokenGuardTests</c>
    /// uses for a different guard). This pins the fix for the "no pulse at all"
    /// defect: the ring previously started at <c>scale(0.6)</c> — SMALLER than
    /// the marker it encircles. Since the ripple paints UNDER the solid marker
    /// (z-order), that put its highest-opacity moment (0.9 at the 0% keyframe)
    /// entirely hidden behind the opaque marker; by the time it grew past the
    /// marker's own edge (~17% into the cycle) opacity had already faded well
    /// below its starting value, so in practice no ring was ever visible. The
    /// fix starts the ring at <c>scale(1)</c> — exactly the marker's radius —
    /// so it's visible from the first frame. This test would have caught it:
    /// a keyframe starting below <c>scale(1)</c> is a concrete, predictable
    /// regression, not a vague "polish" complaint.
    /// </summary>
    [Fact]
    public void Ripple_Keyframe_Starts_At_Scale_One_Not_Smaller_Than_The_Marker()
    {
        var cssPath = Path.Combine(RepoRoot(), "src", "Lumeo", "wwwroot", "css", "lumeo.css");
        Assert.True(File.Exists(cssPath), $"lumeo.css not found at {cssPath}");
        var css = File.ReadAllText(cssPath);

        var keyframeStart = System.Text.RegularExpressions.Regex.Match(
            css, @"@keyframes\s+lumeo-chart-native-ripple\s*\{\s*0%\s*\{([^}]+)\}");
        Assert.True(keyframeStart.Success, "Could not find the lumeo-chart-native-ripple 0% keyframe block in lumeo.css.");

        var scaleMatch = System.Text.RegularExpressions.Regex.Match(keyframeStart.Groups[1].Value, @"scale\(([\d.]+)\)");
        Assert.True(scaleMatch.Success, "0% keyframe has no scale(...) transform.");

        var startScale = double.Parse(scaleMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(startScale >= 1.0,
            $"Ripple's 0% keyframe scale is {startScale}, which is SMALLER than the marker (scale 1.0) it's " +
            "supposed to encircle. A sub-1.0 starting scale hides the ring's highest-opacity moment behind the " +
            "solid marker, reproducing the 'no pulse at all' defect.");
    }
}
