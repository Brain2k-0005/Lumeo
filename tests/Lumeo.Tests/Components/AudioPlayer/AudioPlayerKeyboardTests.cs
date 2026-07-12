using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.AudioPlayer;

/// <summary>
/// Keyboard coverage for AudioPlayer's custom media-transport handler
/// (HandleKeyDown on the `role="region"` wrapper): Space/"k"/"K" toggle
/// play/pause, ArrowLeft/ArrowRight skip ±SkipSeconds, "m"/"M" toggles mute.
/// These keys only work while the WRAPPER itself is the event target — there
/// is no global/document-level listener — so a second, untouched player
/// instance must never react to a key pressed on the first. The `<audio>`
/// element's own play/pause never flips synchronously: HandleKeyDown only
/// issues the JS interop call (PlayMedia/PauseMedia/SeekMedia/SetMediaVolume)
/// and waits for the element's own onplay/onpause events to update `_playing`
/// — mirroring how the browser actually drives this component (see
/// AudioPlayerSrcChangeTests for the same pattern).
/// </summary>
public class AudioPlayerKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AudioPlayerKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.AudioPlayer> Render(Action<ComponentParameterCollectionBuilder<L.AudioPlayer>>? extra = null)
        => _ctx.Render<L.AudioPlayer>(p =>
        {
            p.Add(a => a.Src, "/song.mp3");
            extra?.Invoke(p);
        });

    private static string PlayButtonLabel(IRenderedComponent<L.AudioPlayer> cut)
        => cut.Find("button[aria-label='Play'], button[aria-label='Pause']")
              .GetAttribute("aria-label")!;

    private static string MuteButtonLabel(IRenderedComponent<L.AudioPlayer> cut)
        => cut.Find("button[aria-label='Mute'], button[aria-label='Unmute']")
              .GetAttribute("aria-label")!;

    // --- Space / k / K toggle play/pause ---

    [Fact]
    public void Space_On_Wrapper_Invokes_PlayMedia_When_Paused()
    {
        var cut = Render();

        cut.Find("[role='region']").KeyDown(" ");

        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "playMedia");
        Assert.DoesNotContain(_ctx.JSInterop.Invocations, i => i.Identifier == "pauseMedia");
    }

    [Fact]
    public void Space_On_Wrapper_Invokes_PauseMedia_When_Already_Playing()
    {
        var cut = Render();

        // Drive to the "playing" state the way the <audio> element would.
        cut.Find("audio").TriggerEvent("onplay", new EventArgs());
        Assert.Equal("Pause", PlayButtonLabel(cut));

        cut.Find("[role='region']").KeyDown(" ");

        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "pauseMedia");
    }

    [Theory]
    [InlineData("k")]
    [InlineData("K")]
    public void Lowercase_And_Uppercase_K_Toggle_Play_Pause(string key)
    {
        var cut = Render();

        cut.Find("[role='region']").KeyDown(key);

        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "playMedia");
    }

    // --- ArrowRight / ArrowLeft skip ±SkipSeconds ---

    [Fact]
    public void ArrowRight_Skips_Forward_By_Default_SkipSeconds()
    {
        var cut = Render();

        cut.Find("[role='region']").KeyDown("ArrowRight");

        var seek = _ctx.JSInterop.Invocations.Last(i => i.Identifier == "seekMedia");
        Assert.Equal(10.0, (double)seek.Arguments[1]!);
    }

    [Fact]
    public void ArrowRight_Respects_Custom_SkipSeconds()
    {
        var cut = Render(p => p.Add(a => a.SkipSeconds, 30));

        cut.Find("[role='region']").KeyDown("ArrowRight");

        var seek = _ctx.JSInterop.Invocations.Last(i => i.Identifier == "seekMedia");
        Assert.Equal(30.0, (double)seek.Arguments[1]!);
    }

    [Fact]
    public void ArrowLeft_Skip_Clamps_At_Zero_From_The_Start()
    {
        var cut = Render();

        // _currentTime starts at 0 — skipping -10s must clamp to 0, not go negative.
        cut.Find("[role='region']").KeyDown("ArrowLeft");

        var seek = _ctx.JSInterop.Invocations.Last(i => i.Identifier == "seekMedia");
        Assert.Equal(0.0, (double)seek.Arguments[1]!);
    }

    // --- m / M toggle mute ---

    [Theory]
    [InlineData("m")]
    [InlineData("M")]
    public void M_Toggles_Mute_And_Invokes_SetMediaVolume_Muted(string key)
    {
        var cut = Render();
        Assert.Equal("Mute", MuteButtonLabel(cut));

        cut.Find("[role='region']").KeyDown(key);

        Assert.Equal("Unmute", MuteButtonLabel(cut));
        var setVolume = _ctx.JSInterop.Invocations.Last(i => i.Identifier == "setMediaVolume");
        Assert.True((bool)setVolume.Arguments[2]!);
    }

    // --- Unhandled keys are inert ---

    [Fact]
    public void Unhandled_Key_Does_Not_Invoke_Any_Media_Interop()
    {
        var cut = Render();

        cut.Find("[role='region']").KeyDown("a");

        Assert.DoesNotContain(_ctx.JSInterop.Invocations, i =>
            i.Identifier is "playMedia" or "pauseMedia" or "seekMedia" or "setMediaVolume");
    }

    // --- Scoped to the player's own root, not a global listener ---

    [Fact]
    public void Key_Handling_Is_Scoped_To_The_Player_That_Received_The_Event()
    {
        var player1 = Render();
        var player2 = Render();

        // Press Space only on the SECOND player's wrapper.
        player2.Find("[role='region']").KeyDown(" ");

        // Exactly one playMedia call fired (from player2), and player1's own
        // play/pause state is completely untouched.
        Assert.Single(_ctx.JSInterop.Invocations, i => i.Identifier == "playMedia");
        Assert.Equal("Play", PlayButtonLabel(player1));
    }

    // --- The seek bar is pointer-only today, not independently keyable ---

    [Fact]
    public void Seek_Bar_Has_No_Tabindex_Or_Keydown_Of_Its_Own()
    {
        // The progress bar carries role="slider" (APG would expect Left/Right/
        // Home/End on the thumb itself), but it is driven purely by pointer
        // events — no tabindex, no @onkeydown. Arrow-key skipping happens at
        // the WRAPPER level instead (see ArrowRight/ArrowLeft tests above).
        // This test documents current behavior; it is not a fix for #image-compare's
        // SPECIAL slider gap, which is a different component.
        var cut = Render();

        var slider = cut.Find("[role='slider']");
        Assert.Null(slider.GetAttribute("tabindex"));
    }
}
