using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.NumberInput;

public class NumberInputTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NumberInputTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Input_And_Two_Buttons()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>();

        Assert.NotNull(cut.Find("input[type='number']"));
        var buttons = cut.FindAll("button");
        Assert.Equal(2, buttons.Count);
    }

    [Fact]
    public void Container_Has_InlineFlex_Class()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("inline-flex", cls);
        Assert.Contains("items-center", cls);
    }

    [Fact]
    public void Disabled_True_Disables_Input_And_Buttons()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Disabled, true));

        Assert.True(cut.Find("input").HasAttribute("disabled"));
        var buttons = cut.FindAll("button");
        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Decrement_Button_Has_Minus_Aria_Label()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>();

        var decrementBtn = cut.Find("button[aria-label='Decrease']");
        Assert.NotNull(decrementBtn);
    }

    [Fact]
    public void Increment_Button_Has_Increase_Aria_Label()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>();

        var incrementBtn = cut.Find("button[aria-label='Increase']");
        Assert.NotNull(incrementBtn);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Class, "my-number-input"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-number-input", cls);
    }

    // --- Prefix ---

    [Fact]
    public void Prefix_Renders_Prefix_Text()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Prefix, "$")
            .Add(n => n.Value, 42.0));

        Assert.Contains("$", cut.Markup);
    }

    // --- Suffix ---

    [Fact]
    public void Suffix_Renders_Suffix_Text()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Suffix, "kg")
            .Add(n => n.Value, 10.0));

        Assert.Contains("kg", cut.Markup);
    }

    [Fact]
    public void Prefix_And_Suffix_Both_Render()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Prefix, "$")
            .Add(n => n.Suffix, "USD")
            .Add(n => n.Value, 99.0));

        Assert.Contains("$", cut.Markup);
        Assert.Contains("USD", cut.Markup);
    }
}
