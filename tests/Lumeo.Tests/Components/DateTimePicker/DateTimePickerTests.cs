using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

public class DateTimePickerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DateTimePickerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.DateTimePicker>();
        var button = cut.Find("button");
        Assert.NotNull(button);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.DateTimePicker>(p => p.Add(c => c.Class, "my-dtp"));
        Assert.Contains("my-dtp", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.DateTimePicker>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "dtp" }));
        Assert.Contains("data-testid=\"dtp\"", cut.Markup);
    }

    [Fact]
    public void Shows_formatted_date_when_value_set()
    {
        var dt = new DateTime(2025, 6, 15, 10, 30, 0);
        var cut = _ctx.Render<L.DateTimePicker>(p => p.Add(c => c.Value, dt));
        // Should render some part of the date. Year is stable.
        Assert.Contains("2025", cut.Markup);
    }

    [Fact]
    public void Shows_placeholder_when_no_value()
    {
        var cut = _ctx.Render<L.DateTimePicker>(p => p.Add(c => c.Placeholder, "Select date and time"));
        Assert.Contains("Select date and time", cut.Markup);
    }
}
