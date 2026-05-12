using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.DropdownButton;

public class DropdownButtonTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DropdownButtonTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Button_With_Text()
    {
        var cut = _ctx.Render<Lumeo.DropdownButton>(p => p
            .Add(b => b.Text, "Actions")
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        Assert.Contains("Actions", cut.Markup);
    }

    [Fact]
    public void Renders_Button_Element()
    {
        var cut = _ctx.Render<Lumeo.DropdownButton>(p => p
            .Add(b => b.Text, "Options")
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void Custom_Class_Is_Applied_To_Button()
    {
        var cut = _ctx.Render<Lumeo.DropdownButton>(p => p
            .Add(b => b.Text, "Actions")
            .Add(b => b.Class, "my-dropdown-btn")
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        var button = cut.Find("button");
        Assert.Contains("my-dropdown-btn", button.GetAttribute("class"));
    }

    [Fact]
    public void Variant_Is_Forwarded_To_Inner_Button()
    {
        var cut = _ctx.Render<Lumeo.DropdownButton>(p => p
            .Add(b => b.Text, "Secondary")
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Secondary)
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        var button = cut.Find("button");
        Assert.Contains("bg-secondary", button.GetAttribute("class"));
    }

    [Fact]
    public void Disabled_State_Disables_The_Button()
    {
        var cut = _ctx.Render<Lumeo.DropdownButton>(p => p
            .Add(b => b.Text, "Disabled")
            .Add(b => b.Disabled, true)
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        var button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void ChildContent_Renders_When_Text_Is_Null()
    {
        var cut = _ctx.Render<Lumeo.DropdownButton>(p => p
            .AddChildContent("<span class=\"custom-label\">Custom</span>")
            .Add(b => b.MenuContent, (RenderFragment)(_ => { })));

        Assert.NotNull(cut.Find(".custom-label"));
    }

    [Fact]
    public void Dropdown_Is_Not_Open_By_Default()
    {
        var cut = _ctx.Render<Lumeo.DropdownButton>(p => p
            .Add(b => b.Text, "Open")
            .Add(b => b.MenuContent, (RenderFragment)(b =>
            {
                b.OpenElement(0, "div");
                b.AddAttribute(1, "class", "menu-item-marker");
                b.CloseElement();
            })));

        // The menu content should not be in the DOM when closed
        Assert.Empty(cut.FindAll(".menu-item-marker"));
    }
}
