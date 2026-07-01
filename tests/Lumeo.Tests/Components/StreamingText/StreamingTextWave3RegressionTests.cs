using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.StreamingText;

/// <summary>
/// Battle-test wave 3 regressions for <see cref="Lumeo.StreamingText"/>.
///
///  #61 (edge-data) — the incremental diff sliced the streamed string at the raw
///       <c>_previousLength</c>. When a single Unicode code point (an emoji = a
///       UTF-16 surrogate PAIR) streamed in across two updates, the split fell
///       BETWEEN the high and low surrogate, tearing the pair in half across the
///       "already rendered" and "new suffix" spans — each half then rendered as a
///       U+FFFD replacement glyph instead of the emoji. The fix backs the split
///       off a surrogate boundary so the whole code point lands in the suffix span
///       intact.
///
///  #60 (state-on-data-change) — the fade-in suffix span carried no <c>@key</c>,
///       so Blazor reused the same element across appends and the CSS enter
///       animation only played for the FIRST chunk. The fix keys the span by the
///       diff offset so each append remounts it and replays the fade. <c>@key</c>
///       leaves NO markup/DOM trace bUnit can observe (the final HTML is identical
///       and animation replay is a pure browser concern), so the companion test
///       pins the incremental-suffix contract the key decorates: each append must
///       isolate ONLY the newest chunk in the fade span (the per-update remount
///       target). See notes — the animation replay itself is not bUnit-testable.
/// </summary>
public class StreamingTextWave3RegressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StreamingTextWave3RegressionTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // U+1F600 GRINNING FACE — a two-char UTF-16 surrogate pair: high \uD83D + low \uDE00.
    private const string Grinning = "😀";

    // #61 — an emoji that streams in across two updates (high surrogate first,
    // low surrogate next) must NOT be split across the two text spans.
    [Fact]
    public void Surrogate_Pair_Streamed_Across_Two_Updates_Stays_Whole_In_Suffix()
    {
        // Update 1: only the HIGH surrogate of the emoji has arrived after "Hi".
        // (_previousLength advances to 3 — right after the lone high surrogate.)
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, "Hi\uD83D"));

        // Update 2: the LOW surrogate arrives, completing the emoji. The previous
        // diff boundary (length 3) now sits BETWEEN the two surrogate halves.
        cut.Render(p => p
            .Add(s => s.Text, "Hi" + Grinning));

        // The suffix (fade-in) span must contain the COMPLETE emoji. Pre-fix the
        // boundary fell mid-pair, so the suffix was a lone low surrogate that
        // renders as U+FFFD instead of the grinning face.
        var suffix = cut.Find("span.fade-in");
        Assert.Equal(Grinning, suffix.TextContent);

        // ...and the already-rendered prefix span kept only "Hi" — it did not
        // retain the orphaned high surrogate half (pre-fix it was "Hi\uD83D").
        Assert.Equal("Hi", cut.FindAll("span > span")[0].TextContent);
    }

    // #60 — each append isolates ONLY the newest chunk in the fade span: the
    // remount target the @key keys. (The @key's animation replay is browser-only
    // and invisible to bUnit, so this pins the observable half of the fix — that
    // the per-update diff feeds a single fresh chunk into the keyed span.)
    [Fact]
    public void Each_Append_Isolates_Only_The_Newest_Chunk_In_The_Fade_Span()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, "Hello"));

        cut.Render(p => p.Add(s => s.Text, "Hello A"));
        cut.Render(p => p.Add(s => s.Text, "Hello AB"));

        // The fade span holds ONLY the last appended character — not the whole
        // string and not a stale earlier chunk.
        var suffix = cut.Find("span.fade-in");
        Assert.Equal("B", suffix.TextContent);

        // The already-rendered prefix carries everything before the newest chunk.
        Assert.Equal("Hello A", cut.FindAll("span > span")[0].TextContent);
    }
}
