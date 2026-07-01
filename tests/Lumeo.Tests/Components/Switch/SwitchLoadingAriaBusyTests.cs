using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Switch;

// Regression: a Loading switch must tell assistive tech that an async operation is
// in flight (aria-busy=true on the button) rather than presenting a fully-togglable
// switch with a stale aria-checked. The loading Spinner must also be aria-hidden so
// its own role=status / aria-label="Loading" does not pollute the switch's
// accessible name. Battle-wave2-triage n=164.
public class SwitchLoadingAriaBusyTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchLoadingAriaBusyTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Loading_Switch_Sets_AriaBusy_True()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(b => b.Loading, true));

        var button = cut.Find("button");
        Assert.Equal("true", button.GetAttribute("aria-busy"));
    }

    [Fact]
    public void NonLoading_Switch_Sets_AriaBusy_False()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(b => b.Loading, false));

        var button = cut.Find("button");
        Assert.Equal("false", button.GetAttribute("aria-busy"));
    }

    [Fact]
    public void AriaBusy_Tracks_Loading_State_Change()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(b => b.Loading, true));

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-busy"));

        cut.Render(p => p.Add(b => b.Loading, false));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-busy"));
    }

    [Fact]
    public void Loading_Spinner_Is_AriaHidden_So_It_Does_Not_Pollute_Accessible_Name()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(b => b.Loading, true)
            .Add(b => b.AriaLabel, "Enable notifications"));

        // The spinner renders inside the thumb span; it must be aria-hidden so its
        // own role=status / aria-label="Loading" is excluded from the switch's name.
        var spinner = cut.Find("button [role=\"status\"]");
        Assert.Equal("true", spinner.GetAttribute("aria-hidden"));
    }
}
