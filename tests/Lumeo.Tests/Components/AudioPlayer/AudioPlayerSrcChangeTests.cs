using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.AudioPlayer;

/// <summary>
/// Regression tests for the state-on-data-change bug (#110): a playlist that
/// swaps <see cref="L.AudioPlayer.Src"/> reuses the SAME component instance, so
/// the element's transient state (playing / currentTime / duration) must reset
/// on a real Src change — while an unrelated same-Src re-render must NOT clobber
/// in-progress playback.
/// </summary>
public class AudioPlayerSrcChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AudioPlayerSrcChangeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.AudioPlayer> Render(string src)
        => _ctx.Render<L.AudioPlayer>(p => p.Add(a => a.Src, src));

    // The play/pause button's aria-label is the DOM-observable mirror of the
    // internal _playing flag: "Pause" while playing, "Play" while paused.
    private static string PlayButtonLabel(IRenderedComponent<L.AudioPlayer> cut)
        => cut.Find("button[aria-label='Play'], button[aria-label='Pause']")
              .GetAttribute("aria-label")!;

    [Fact]
    public void Changing_Src_Resets_Stale_Playing_State()
    {
        var cut = Render("/song-a.mp3");

        // Drive the player into the "playing" state the way the browser would:
        // the <audio> element fires `play`, which OnPlayInternal turns into
        // _playing = true (button label -> "Pause").
        cut.Find("audio").TriggerEvent("onplay", new EventArgs());
        Assert.Equal("Pause", PlayButtonLabel(cut));

        // Swap the source (playlist next-track) on the SAME instance.
        cut.Render(p => p.Add(a => a.Src, "/song-b.mp3"));

        // Without the OnParametersSet reset the stale _playing=true survives and
        // the button still reads "Pause" for a track that hasn't started. With
        // the fix the transient state resets to paused.
        Assert.Equal("Play", PlayButtonLabel(cut));
    }

    [Fact]
    public void Same_Src_ReRender_Does_Not_Clobber_Playing_State()
    {
        var cut = Render("/song-a.mp3");

        cut.Find("audio").TriggerEvent("onplay", new EventArgs());
        Assert.Equal("Pause", PlayButtonLabel(cut));

        // An unrelated re-render that re-supplies the SAME Src (e.g. a parent
        // StateHasChanged) must compare equal and leave playback untouched.
        cut.Render(p => p.Add(a => a.Src, "/song-a.mp3"));

        Assert.Equal("Pause", PlayButtonLabel(cut));
    }
}
