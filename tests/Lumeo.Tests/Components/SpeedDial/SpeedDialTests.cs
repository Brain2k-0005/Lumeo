using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.SpeedDial;

public class SpeedDialTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SpeedDialTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_trigger_button()
    {
        var cut = _ctx.Render<L.SpeedDial>();
        var btn = cut.Find("button");
        Assert.NotNull(btn);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Class, "sd-cls"));
        Assert.Contains("sd-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.SpeedDial>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "speed-dial" }));
        Assert.Contains("data-testid=\"speed-dial\"", cut.Markup);
    }

    [Fact]
    public void Items_not_visible_by_default_closed()
    {
        var items = new List<L.SpeedDial.SpeedDialItem>
        {
            new() { Label = "Share" },
            new() { Label = "Print" }
        };
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, items));
        // Items should not be visible when closed
        Assert.DoesNotContain("Share", cut.Markup);
    }

    [Fact]
    public void Clicking_trigger_shows_items()
    {
        var items = new List<L.SpeedDial.SpeedDialItem>
        {
            new() { Label = "Share" }
        };
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, items));
        cut.Find("button").Click();
        Assert.Contains("Share", cut.Markup);
    }
}
