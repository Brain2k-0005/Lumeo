using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TimePicker;

public class TimePickerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TimePickerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_trigger_button()
    {
        var cut = _ctx.Render<L.TimePicker>();
        var button = cut.Find("button");
        Assert.NotNull(button);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.TimePicker>(p => p.Add(c => c.Class, "tp-cls"));
        Assert.Contains("tp-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.TimePicker>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "time-picker" }));
        Assert.Contains("data-testid=\"time-picker\"", cut.Markup);
    }

    [Fact]
    public void Shows_formatted_time_when_value_set()
    {
        var time = new TimeSpan(14, 30, 0);
        var cut = _ctx.Render<L.TimePicker>(p => p.Add(c => c.Value, time));
        // 24-hour format by default: "14:30"
        Assert.Contains("14:30", cut.Markup);
    }

    [Fact]
    public void Shows_placeholder_when_no_value()
    {
        var cut = _ctx.Render<L.TimePicker>(p => p.Add(c => c.Placeholder, "Pick a time"));
        Assert.Contains("Pick a time", cut.Markup);
    }
}
