using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InputMask;

public class InputMaskTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InputMaskTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.InputMask>();
        Assert.NotEmpty(cut.FindAll("input"));
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.InputMask>(p => p.Add(c => c.Class, "mask-cls"));
        var input = cut.Find("input");
        Assert.Contains("mask-cls", input.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "mask-input" }));
        Assert.Contains("data-testid=\"mask-input\"", cut.Markup);
    }

    [Fact]
    public void Mask_placeholder_shown_in_placeholder_attribute()
    {
        var cut = _ctx.Render<L.InputMask>(p => p.Add(c => c.Mask, "###-###"));
        var input = cut.Find("input");
        // Without an explicit Placeholder, the mask placeholder is built from the mask pattern
        var placeholder = input.GetAttribute("placeholder") ?? "";
        // ___-___ is derived from the mask using the default PromptChar '_'
        Assert.Contains("_", placeholder);
    }

    [Fact]
    public void Renders_as_disabled_when_disabled()
    {
        var cut = _ctx.Render<L.InputMask>(p => p.Add(c => c.Disabled, true));
        var input = cut.Find("input");
        Assert.NotNull(input.GetAttribute("disabled"));
    }
}
