using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.SplitButton;

public class SplitButtonTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SplitButtonTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Root_Wrapper()
    {
        var cut = _ctx.Render<Lumeo.SplitButton>(p => p
            .Add(b => b.Text, "Save")
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Renders_Two_Buttons()
    {
        var cut = _ctx.Render<Lumeo.SplitButton>(p => p
            .Add(b => b.Text, "Save")
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        var buttons = cut.FindAll("button");
        Assert.Equal(2, buttons.Count);
    }

    [Fact]
    public void Primary_Button_Displays_Text()
    {
        var cut = _ctx.Render<Lumeo.SplitButton>(p => p
            .Add(b => b.Text, "Deploy")
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        Assert.Contains("Deploy", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Applied_To_Root()
    {
        var cut = _ctx.Render<Lumeo.SplitButton>(p => p
            .Add(b => b.Text, "Save")
            .Add(b => b.Class, "my-split-btn")
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        var root = cut.Find("div");
        Assert.Contains("my-split-btn", root.GetAttribute("class"));
    }

    [Fact]
    public void Variant_Forwarded_To_Both_Buttons()
    {
        var cut = _ctx.Render<Lumeo.SplitButton>(p => p
            .Add(b => b.Text, "Delete")
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Destructive)
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        var buttons = cut.FindAll("button");
        Assert.All(buttons, btn =>
            Assert.Contains("bg-destructive", btn.GetAttribute("class")));
    }

    [Fact]
    public void Disabled_Disables_Both_Buttons()
    {
        var cut = _ctx.Render<Lumeo.SplitButton>(p => p
            .Add(b => b.Text, "Save")
            .Add(b => b.Disabled, true)
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        var buttons = cut.FindAll("button");
        Assert.All(buttons, btn => Assert.True(btn.HasAttribute("disabled")));
    }

    [Fact]
    public void OnClick_Fires_When_Primary_Button_Clicked()
    {
        var clicked = false;
        var cut = _ctx.Render<Lumeo.SplitButton>(p => p
            .Add(b => b.Text, "Run")
            .Add(b => b.OnClick, _ => clicked = true)
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        // The primary button is the first button
        cut.FindAll("button")[0].Click();
        Assert.True(clicked);
    }

    [Fact]
    public void ChildContent_Renders_In_Primary_Button()
    {
        var cut = _ctx.Render<Lumeo.SplitButton>(p => p
            .AddChildContent("<span class=\"primary-label\">Launch</span>")
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        Assert.NotNull(cut.Find(".primary-label"));
    }

    [Fact]
    public void Dropdown_Is_Closed_By_Default()
    {
        var cut = _ctx.Render<Lumeo.SplitButton>(p => p
            .Add(b => b.Text, "Save")
            .Add(b => b.MenuContent, (RenderFragment)(b =>
            {
                b.OpenElement(0, "div");
                b.AddAttribute(1, "class", "split-menu-marker");
                b.CloseElement();
            })));

        Assert.Empty(cut.FindAll(".split-menu-marker"));
    }
}
