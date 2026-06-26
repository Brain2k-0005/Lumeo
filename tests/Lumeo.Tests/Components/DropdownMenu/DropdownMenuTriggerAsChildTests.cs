using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>G34 — DropdownMenuTrigger AsChild: a Lumeo &lt;Button&gt; child becomes the
/// single trigger, with the menu's group/data-state (chevron hook), id, and ARIA
/// forwarded onto it.</summary>
public class DropdownMenuTriggerAsChildTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DropdownMenuTriggerAsChildTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> Render(bool open = false, bool asChild = true, EventCallback<bool>? openChanged = null)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DropdownMenu>(0);
            builder.AddAttribute(1, "Open", open);
            if (openChanged.HasValue) builder.AddAttribute(2, "OpenChanged", openChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DropdownMenuTrigger>(0);
                b.AddAttribute(1, "AsChild", asChild);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(t =>
                {
                    t.OpenComponent<L.Button>(0);
                    t.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Menu")));
                    t.CloseComponent();
                }));
                b.CloseComponent();

                b.OpenComponent<L.DropdownMenuContent>(1);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "items")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void AsChild_Renders_One_Button_With_Menu_Aria_DataState_Group_And_Id()
    {
        var cut = Render(open: false);

        Assert.Empty(cut.FindAll("div[role='button']"));
        Assert.Single(cut.FindAll("button"));
        var b = cut.Find("button");
        Assert.Equal("menu", b.GetAttribute("aria-haspopup"));
        Assert.Equal("closed", b.GetAttribute("data-state"));      // chevron hook forwarded
        Assert.Contains("group", b.GetAttribute("class"));          // group ancestor for group-data-[state=open]
        Assert.False(string.IsNullOrEmpty(b.GetAttribute("id")));   // trigger id rides along
    }

    [Fact]
    public void AsChild_DataState_And_Expanded_Reflect_Open()
    {
        var cut = Render(open: true);
        var b = cut.Find("button[aria-haspopup='menu']");
        Assert.Equal("open", b.GetAttribute("data-state"));
        Assert.Equal("true", b.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void AsChild_Click_Toggles_The_Menu()
    {
        bool? opened = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => opened = v);
        var cut = Render(open: false, openChanged: cb);
        cut.Find("button").Click();
        Assert.True(opened);
    }

    [Fact]
    public void Without_AsChild_Keeps_The_Role_Button_Wrapper()
    {
        var cut = Render(open: false, asChild: false);
        Assert.NotEmpty(cut.FindAll("div[role='button']"));
    }
}
