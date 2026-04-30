using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ColorPicker;

public class ColorPickerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ColorPickerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.ColorPicker>();
        var button = cut.Find("button");
        Assert.NotNull(button);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p.Add(c => c.Class, "my-picker"));
        Assert.Contains("my-picker", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "cp-root" }));
        Assert.Contains("data-testid=\"cp-root\"", cut.Markup);
    }

    [Fact]
    public void Shows_hex_value_when_provided()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p.Add(c => c.Value, "#FF0000"));
        Assert.Contains("#FF0000", cut.Markup);
    }

    [Fact]
    public void Trigger_button_is_disabled_when_disabled()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p.Add(c => c.Disabled, true));
        var button = cut.Find("button");
        Assert.NotNull(button.GetAttribute("disabled"));
    }
}
