using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Switch;

// Pins all 7 Lumeo.Size rungs (batch B, spec §15) across the four Size-driven
// switch expressions: TrackSizeClasses (button), ThumbSizeClasses +
// ThumbTranslateClass (thumb span), ThumbSpinnerClass (Loading spinner
// wrapper). Asserts the RENDERED class attribute as an exact token (not a
// bare substring — e.g. "h-1" is a substring of "h-11", "px-1" is a
// substring of "px-1.5" — so a loose Contains could silently pass a wrong
// value), per the task brief's note that Cx.Merge's argument order has
// bitten this repo twice.
public class SwitchSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchSizeScaleTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertHasClasses(string? cls, params string[] tokens)
    {
        var actual = (cls ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
            Assert.Contains(token, actual);
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "h-2", "w-3")]
    [InlineData(Lumeo.Size.Xs, "h-3", "w-5")]
    [InlineData(Lumeo.Size.Sm, "h-4", "w-7")]
    [InlineData(Lumeo.Size.Md, "h-5", "w-9")]
    [InlineData(Lumeo.Size.Lg, "h-6", "w-11")]
    [InlineData(Lumeo.Size.Xl, "h-7", "w-[52px]")]
    [InlineData(Lumeo.Size.Xxl, "h-8", "w-[60px]")]
    public void Track_Renders_Correct_Size_Classes(Lumeo.Size size, string heightClass, string widthClass)
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Size, size));

        AssertHasClasses(cut.Find("button").GetAttribute("class"), heightClass, widthClass);
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "h-1", "w-1")]
    [InlineData(Lumeo.Size.Xs, "h-2", "w-2")]
    [InlineData(Lumeo.Size.Sm, "h-3", "w-3")]
    [InlineData(Lumeo.Size.Md, "h-4", "w-4")]
    [InlineData(Lumeo.Size.Lg, "h-5", "w-5")]
    [InlineData(Lumeo.Size.Xl, "h-6", "w-6")]
    [InlineData(Lumeo.Size.Xxl, "h-7", "w-7")]
    public void Thumb_Renders_Correct_Size_Classes(Lumeo.Size size, string heightClass, string widthClass)
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Size, size));

        AssertHasClasses(cut.Find("span").GetAttribute("class"), heightClass, widthClass);
    }

    // Formula-derived (spec §15b/§0.8): translate = trackW − thumbW − 4px border.
    [Theory]
    [InlineData(Lumeo.Size.Xxs, "translate-x-1")]
    [InlineData(Lumeo.Size.Xs, "translate-x-2")]
    [InlineData(Lumeo.Size.Sm, "translate-x-3")]
    [InlineData(Lumeo.Size.Md, "translate-x-4")]
    [InlineData(Lumeo.Size.Lg, "translate-x-5")]
    [InlineData(Lumeo.Size.Xl, "translate-x-6")]
    [InlineData(Lumeo.Size.Xxl, "translate-x-7")]
    public void Checked_Thumb_Renders_Correct_Translate_Class(Lumeo.Size size, string translateClass)
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(b => b.Size, size)
            .Add(b => b.Checked, true));

        AssertHasClasses(cut.Find("span").GetAttribute("class"), translateClass);
    }

    // Codex PR #388 finding 1: Spinner puts its size classes on its OWN inner
    // <svg> (RingCssClass), never on the wrapper div that a Class="..." param
    // merges into. The old Switch code passed a fixed Size="Sm" to Spinner and
    // tried to override the visible size via a wrapper Class="!h-X !w-X" — that
    // override landed on the wrong element and never reached the <svg>, so the
    // ring rendered a constant "animate-spin h-4 w-4" at EVERY Switch size
    // (predicted wrong value below every rung except Md, which coincidentally
    // also resolves to h-4 w-4). Asserting the div[role=status] wrapper (as the
    // previous version of this test did) can't catch that — the wrapper class
    // string DID contain the intended override tokens even though the ring
    // never moved. This asserts the actual rendered <svg>.
    //
    // Spinner only exposes 7 discrete rungs that don't map 1:1 onto the
    // thumb's own sizes, so Lg/Xxl are intentionally off by one Spinner rung
    // (rounded down/up respectively) rather than tied at a fixed size — see
    // SpinnerSizeForThumb's mapping comment in Switch.razor.
    [Theory]
    [InlineData(Lumeo.Size.Xxs, "h-2", "w-2")]  // predicted wrong (pre-fix): h-4 w-4
    [InlineData(Lumeo.Size.Xs, "h-2", "w-2")]   // predicted wrong (pre-fix): h-4 w-4
    [InlineData(Lumeo.Size.Sm, "h-3", "w-3")]   // predicted wrong (pre-fix): h-4 w-4
    [InlineData(Lumeo.Size.Md, "h-4", "w-4")]   // pre-fix already correct (coincidence: hardcoded Sm == mapped Sm)
    [InlineData(Lumeo.Size.Lg, "h-4", "w-4")]   // pre-fix also correct (coincidence: hardcoded Sm == mapped Sm)
    [InlineData(Lumeo.Size.Xl, "h-6", "w-6")]   // predicted wrong (pre-fix): h-4 w-4
    [InlineData(Lumeo.Size.Xxl, "h-8", "w-8")]  // predicted wrong (pre-fix): h-4 w-4
    public void Loading_Spinner_Ring_Svg_Renders_Correct_Size_Classes(Lumeo.Size size, string heightClass, string widthClass)
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(b => b.Size, size)
            .Add(b => b.Loading, true));

        var ring = cut.Find("button [role=\"status\"] svg");
        AssertHasClasses(ring.GetAttribute("class"), heightClass, widthClass);
    }

    // The Loading spinner's wrapper div (role=status) never carries a size
    // class of its own — Spinner sizes only the inner <svg>. This guards
    // against a regression back to the wrapper-class-override pattern that
    // caused finding 1: any future "!h-X !w-X" wrapper override would show up
    // here as an unexpected class on the wrapper without touching the ring.
    [Fact]
    public void Loading_Spinner_Wrapper_Has_No_Explicit_Size_Override_Classes()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(b => b.Size, Lumeo.Size.Xl)
            .Add(b => b.Loading, true));

        var wrapper = cut.Find("button [role=\"status\"]");
        var classes = (wrapper.GetAttribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.DoesNotContain(classes, c => c.StartsWith('!') || c.StartsWith("h-") || c.StartsWith("w-"));
    }

    // ============== Touch-target fix (owner decision, PR #388 follow-up) ==============
    // Sm (16px track) and Md (20px track, the default) sit below the 24px touch-target
    // minimum. The track IS the painted button (background lives on the <button> itself),
    // so real padding would visibly enlarge the pill — instead an invisible ::before
    // hit-zone (mirrors Chip's close button) extends the click/touch-catching area
    // without touching the track's own box. These tests establish BOTH halves: the
    // rendered TRACK size is unchanged (same h-N/w-N token as before) and the computed
    // hit box (track height + 2 * -inset-y) now reaches >=24px at Sm/Md.

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "h-2", null)]      // 8px, deliberately below 24 — no hit-area extension
    [InlineData(Lumeo.Size.Xs, "h-3", null)]       // 12px, deliberately below 24 — no hit-area extension
    [InlineData(Lumeo.Size.Sm, "h-4", "before:-inset-y-1")]     // 16 + 2*4 = 24px exact
    [InlineData(Lumeo.Size.Md, "h-5", "before:-inset-y-0.5")]   // 20 + 2*2 = 24px exact
    [InlineData(Lumeo.Size.Lg, "h-6", null)]       // 24px already, untouched
    [InlineData(Lumeo.Size.Xl, "h-7", null)]       // 28px already, untouched
    [InlineData(Lumeo.Size.Xxl, "h-8", null)]      // 32px already, untouched
    public void Track_Height_Unchanged_And_HitArea_Extension_Per_Rung(Lumeo.Size size, string trackHeightClass, string? hitAreaClass)
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Size, size));
        var track = cut.Find("button");
        var tokens = track.GetAttribute("class")!.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Half 1: the visible track height token is exactly what it was before the fix.
        Assert.Contains(trackHeightClass, tokens);

        // Half 2: the invisible hit-area extension is present only at Sm/Md.
        if (hitAreaClass is null)
        {
            Assert.DoesNotContain(tokens, t => t.Contains("-inset-y"));
        }
        else
        {
            Assert.Contains(hitAreaClass, tokens);
            Assert.Contains("relative", tokens);
            Assert.Contains("before:absolute", tokens);
        }
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, 8)]
    [InlineData(Lumeo.Size.Xs, 12)]
    [InlineData(Lumeo.Size.Sm, 24)]
    [InlineData(Lumeo.Size.Md, 24)]
    [InlineData(Lumeo.Size.Lg, 24)]
    [InlineData(Lumeo.Size.Xl, 28)]
    [InlineData(Lumeo.Size.Xxl, 32)]
    public void Computed_Hit_Box_Height_Per_Rung(Lumeo.Size size, double expectedHitBoxPx)
    {
        // Hit box height = track height (h-N) + 2 * the -inset-y extension (0 when absent).
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Size, size));
        var cls = cut.Find("button").GetAttribute("class")!;

        var trackMatch = System.Text.RegularExpressions.Regex.Match(cls, @"(?<![\w-])h-(?<n>[0-9.]+)(?!\S)");
        Assert.True(trackMatch.Success, $"no bare h-N token found in '{cls}'");
        var trackPx = double.Parse(trackMatch.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture) * 4.0;

        var insetMatch = System.Text.RegularExpressions.Regex.Match(cls, @"before:-inset-y-(?<n>[0-9.]+)");
        var insetPx = insetMatch.Success
            ? double.Parse(insetMatch.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture) * 4.0
            : 0.0;

        Assert.Equal(expectedHitBoxPx, trackPx + 2 * insetPx);
    }

    [Fact]
    public void HitArea_Extension_Does_Not_Change_Track_Width()
    {
        // -inset-y only (not -inset-x): the track's own WIDTH already clears 24px at
        // every rung, so the fix must not touch horizontal reach — regression guard
        // against a future edit widening the invisible zone unnecessarily.
        var sm = _ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Size, Lumeo.Size.Sm));
        var md = _ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Size, Lumeo.Size.Md));

        var smCls = sm.Find("button").GetAttribute("class")!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var mdCls = md.Find("button").GetAttribute("class")!.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains("w-7", smCls);   // unchanged track width
        Assert.Contains("w-9", mdCls);   // unchanged track width
        Assert.DoesNotContain(smCls, t => t.Contains("-inset-x"));
        Assert.DoesNotContain(mdCls, t => t.Contains("-inset-x"));
        // inset-x-0 (no leading '-') pins the pseudo-element's horizontal edges to the
        // button's own edges — same width as the track, not wider.
        Assert.Contains("before:inset-x-0", smCls);
        Assert.Contains("before:inset-x-0", mdCls);
    }
}
