using Bunit;
using Microsoft.AspNetCore.Components;
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

    // --- Pattern B: FormField splat-id override ---

    [Fact]
    public void Consumer_Splatted_Id_Inside_FormField_Does_Not_Break_Label_For()
    {
        // Regression (Pattern B): a consumer-splatted id="..." on InputMask inside a FormField
        // used to render AFTER the generated EffectiveId attribute and win, leaving the
        // label's `for` pointing at an id that didn't exist on the <input>. EffectiveAttributes
        // now strips the splatted id when inside a FormField so the generated id is used by both.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Phone");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.InputMask>(0);
                b.AddAttribute(1, "Mask", "###-###-####");
                b.AddAttribute(2, "AdditionalAttributes", new Dictionary<string, object> { ["id"] = "consumer-id" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var inputId = cut.Find("input").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");
        Assert.False(string.IsNullOrEmpty(inputId));
        // The label for and input id must agree; the consumer-splatted id must be suppressed.
        Assert.Equal(labelFor, inputId);
        Assert.NotEqual("consumer-id", inputId);
    }

    [Fact]
    public void Consumer_Splatted_Id_Outside_FormField_Still_Reaches_Input()
    {
        // Guard: id-stripping only applies inside a FormField. A standalone InputMask
        // with a consumer-supplied id via AdditionalAttributes must still use that id.
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "standalone-mask-id" }));

        Assert.Equal("standalone-mask-id", cut.Find("input").GetAttribute("id"));
    }
}