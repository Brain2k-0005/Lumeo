using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Filter;

public class FilterBarTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FilterBarTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = _ctx.Render<FilterBar>(p => p
            .AddChildContent("<span id=\"child-content\">Filter Controls</span>"));

        Assert.NotNull(cut.Find("#child-content"));
    }

    [Fact]
    public void Renders_Pills_Slot_In_Flex_Wrapper()
    {
        var cut = _ctx.Render<FilterBar>(p => p
            .Add(c => c.Pills, b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "id", "pill-item");
                b.AddContent(2, "Status: Active");
                b.CloseElement();
            }));

        Assert.NotNull(cut.Find("#pill-item"));
        // Pills wrapper should have flex classes
        var flexDiv = cut.Find(".flex.items-center.gap-2.flex-wrap");
        Assert.NotNull(flexDiv);
    }

    [Fact]
    public void Does_Not_Render_Pills_Wrapper_When_Pills_Is_Null()
    {
        var cut = _ctx.Render<FilterBar>(p => p
            .AddChildContent("Some controls"));

        var flexDivs = cut.FindAll(".flex.items-center.gap-2.flex-wrap");
        Assert.Empty(flexDivs);
    }

    [Fact]
    public void Renders_Actions_Slot()
    {
        var cut = _ctx.Render<FilterBar>(p => p
            .Add(c => c.Actions, b =>
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "id", "action-btn");
                b.AddContent(2, "Clear all");
                b.CloseElement();
            }));

        Assert.NotNull(cut.Find("#action-btn"));
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<FilterBar>(p => p
            .Add(c => c.Class, "my-filter-bar")
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Contains("my-filter-bar", div.GetAttribute("class"));
    }

    [Fact]
    public void Has_Space_Y_4_Base_Class()
    {
        var cut = _ctx.Render<FilterBar>(p => p
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Contains("space-y-4", div.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<FilterBar>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "filter-bar",
                ["aria-label"] = "Filter controls"
            })
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Equal("filter-bar", div.GetAttribute("data-testid"));
        Assert.Equal("Filter controls", div.GetAttribute("aria-label"));
    }
}
