using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.TextReveal;

/// <summary>
/// Regression coverage for the Wave-3 battle-test bugs on <see cref="Lumeo.TextReveal"/>:
///   #19  Stagger=0 (and Threshold=0) were silently coalesced to JS defaults by `|| default`.
///   #20  Out-of-range Threshold (&gt;1 / &lt;0) threw in the IntersectionObserver, leaving the
///        text permanently invisible plus an unhandled circuit exception.
///   #62  Reveal registered once on firstRender and JS set transition-delay only then, so a
///        later change to the bound Text added un-staggered words.
///
/// The fix renders the per-word transition-delay declaratively in the Razor markup (so Blazor
/// keeps it in sync with the word list, and an intentional Stagger of 0 is honored), and clamps
/// Threshold to [0,1] in the component before it reaches JS. The threshold assertions inspect
/// the recorded motion.revealText interop call (arg [1] = anonymous { stagger, threshold }).
/// </summary>
public class TextRevealBattleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TextRevealBattleTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- #62 + #19 (stagger): declarative per-word transition-delay ---

    [Fact]
    public void Word_Spans_Carry_Declarative_Stagger_Delay()
    {
        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, "a b c")
            .Add(t => t.Stagger, 100));

        var words = cut.FindAll("[data-motion-word]");
        // Pre-fix the delay was applied by JS at runtime, so the markup carried no
        // transition-delay at all; the declarative style makes Blazor own it.
        Assert.Contains("transition-delay:0ms", Style(words[0]));
        Assert.Contains("transition-delay:100ms", Style(words[1]));
        Assert.Contains("transition-delay:200ms", Style(words[2]));
    }

    [Fact]
    public void Stagger_Zero_Is_Honored_Not_Coalesced_To_Default()
    {
        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, "a b")
            .Add(t => t.Stagger, 0));

        var words = cut.FindAll("[data-motion-word]");
        // Pre-fix JS `(options.stagger) || 80` forced every word to 80ms when the author
        // intended an all-at-once reveal (Stagger=0). Declarative markup keeps it 0.
        Assert.Contains("transition-delay:0ms", Style(words[0]));
        Assert.Contains("transition-delay:0ms", Style(words[1]));
    }

    [Fact]
    public void Changing_Bound_Text_Restaggers_Newly_Added_Words()
    {
        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, "a b")
            .Add(t => t.Stagger, 50));

        // The bound Text grows after the first render.
        cut.Render(p => p.Add(t => t.Text, "a b c d"));

        var words = cut.FindAll("[data-motion-word]");
        Assert.Equal(4, words.Count);
        // Pre-fix the reveal registered once on firstRender and JS set transitionDelay only
        // then, so words 3 and 4 (added later) had NO stagger. Declarative markup keeps
        // every word in sync with its index.
        Assert.Contains("transition-delay:100ms", Style(words[2]));
        Assert.Contains("transition-delay:150ms", Style(words[3]));
    }

    // --- #20: out-of-range Threshold clamped before interop ---

    [Fact]
    public void OutOfRange_Threshold_Is_Clamped_To_One_Before_Interop()
    {
        // Track the satellite Lumeo.Motion JS module; the component imports it by this
        // bare (unversioned) url, matching the sibling motion tests' setup.
        var motion = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        motion.Mode = JSRuntimeMode.Loose;

        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, "Hello world")
            .Add(t => t.Threshold, 5.0));

        // Pre-fix the raw 5.0 reached `new IntersectionObserver(..., { threshold: 5 })`,
        // which throws (legal range is [0,1]) and leaves the text permanently hidden.
        cut.WaitForAssertion(() => Assert.Equal(1.0, RevealThreshold()));
    }

    [Fact]
    public void Negative_Threshold_Is_Clamped_To_Zero_Before_Interop()
    {
        var motion = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        motion.Mode = JSRuntimeMode.Loose;

        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, "Hello world")
            .Add(t => t.Threshold, -3.0));

        cut.WaitForAssertion(() => Assert.Equal(0.0, RevealThreshold()));
    }

    /// <summary>Inline style with whitespace stripped, so the assertion is insensitive to
    /// any CSS normalization the HTML parser may apply (e.g. a space after the colon).</summary>
    private static string Style(AngleSharp.Dom.IElement el)
        => (el.GetAttribute("style") ?? string.Empty).Replace(" ", string.Empty);

    /// <summary>Reads the <c>threshold</c> arg of the recorded motion.revealText interop call.
    /// The options bag is a Dictionary&lt;string, object?&gt; (trim-safe — see
    /// ComponentInteropService.MotionRevealText), not an anonymous type.</summary>
    private double RevealThreshold()
    {
        var inv = _ctx.JSInterop.Invocations
            .First(i => i.Identifier == "motion.revealText");
        var options = (Dictionary<string, object?>)inv.Arguments[1]!;
        return (double)options["threshold"]!;
    }
}
