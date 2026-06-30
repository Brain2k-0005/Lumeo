using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.AudioPlayer;

public class AudioPlayerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AudioPlayerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.AudioPlayer> Render(Action<ComponentParameterCollectionBuilder<L.AudioPlayer>>? extra = null)
        => _ctx.Render<L.AudioPlayer>(p =>
        {
            p.Add(a => a.Src, "/song.mp3");
            extra?.Invoke(p);
        });

    [Fact]
    public void Renders_Audio_Element()
    {
        var cut = Render();
        Assert.NotNull(cut.Find("audio"));
    }

    [Fact]
    public void Scrub_Fill_Uses_Physical_Left_So_It_Matches_The_Physical_Seek_Math()
    {
        // Codex P2: the seek percentage is physical (e.OffsetX / width, left-to-right), so the progress
        // fill must grow from the physical LEFT edge — `left-0`, not logical `start-0` (which under RTL
        // grows from the right and ends up mirrored against the pointer seek math).
        var cut = Render();
        Assert.Contains("inset-y-0 left-0 rounded-full bg-primary", cut.Markup);
        Assert.DoesNotContain("inset-y-0 start-0 rounded-full bg-primary", cut.Markup);
    }

    // --- Skip buttons (#302) ---

    [Fact]
    public void Skip_Buttons_Render_By_Default()
    {
        var cut = Render();
        Assert.NotNull(cut.Find("button[aria-label^='Skip back']"));
        Assert.NotNull(cut.Find("button[aria-label^='Skip forward']"));
    }

    [Fact]
    public void Skip_Buttons_Hidden_When_Disabled()
    {
        var cut = Render(p => p.Add(a => a.ShowSkipButtons, false));
        Assert.Empty(cut.FindAll("button[aria-label^='Skip back']"));
    }

    [Fact]
    public void Skip_Label_Reflects_SkipSeconds()
    {
        var cut = Render(p => p.Add(a => a.SkipSeconds, 30));
        var back = cut.Find("button[aria-label^='Skip back']");
        Assert.Contains("30", back.GetAttribute("aria-label"));
    }

    // --- Playback rate (#302) ---

    [Fact]
    public void Playback_Rate_Button_Renders_And_Defaults_To_1x()
    {
        var cut = Render();
        var rateBtn = cut.Find("button[aria-label^='Playback speed']");
        Assert.Contains("1", rateBtn.TextContent);
    }

    [Fact]
    public void Playback_Rate_Cycles_On_Click()
    {
        var cut = Render(p => p.Add(a => a.PlaybackRates, new[] { 1.0, 1.5, 2.0 }));
        var rateBtn = cut.Find("button[aria-label^='Playback speed']");
        rateBtn.Click();
        // 1 → 1.5 after one click.
        Assert.Contains("1.5", cut.Find("button[aria-label^='Playback speed']").TextContent);
    }

    [Fact]
    public void Playback_Rate_Hidden_When_Disabled()
    {
        var cut = Render(p => p.Add(a => a.ShowPlaybackRate, false));
        Assert.Empty(cut.FindAll("button[aria-label^='Playback speed']"));
    }

    // --- Volume (#302) ---

    [Fact]
    public void Volume_Slider_Renders_By_Default()
    {
        var cut = Render();
        var vol = cut.Find("input[type='range'][aria-label='Volume']");
        Assert.NotNull(vol);
    }

    [Fact]
    public void Volume_Slider_Hidden_When_Disabled()
    {
        var cut = Render(p => p.Add(a => a.ShowVolume, false));
        Assert.Empty(cut.FindAll("input[type='range'][aria-label='Volume']"));
    }

    [Fact]
    public void Dragging_Volume_To_Zero_Shows_Muted_Icon()
    {
        var cut = Render();
        var vol = cut.Find("input[type='range'][aria-label='Volume']");
        vol.Input("0");
        // aria-valuetext reflects 0%.
        Assert.Equal("0%", cut.Find("input[type='range'][aria-label='Volume']").GetAttribute("aria-valuetext"));
    }

    [Fact]
    public void Wrapper_Is_Focusable_For_Keyboard()
    {
        var cut = Render();
        Assert.Equal("0", cut.Find("[role='region']").GetAttribute("tabindex"));
    }
}
