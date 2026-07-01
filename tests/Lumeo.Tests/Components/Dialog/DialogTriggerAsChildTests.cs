using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Dialog;

/// <summary>G34 — DialogTrigger AsChild: a Lumeo &lt;Button&gt; child becomes the single
/// trigger element (no role=button wrapper), opening the dialog on click.</summary>
public class DialogTriggerAsChildTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DialogTriggerAsChildTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> Render(bool asChild = true, EventCallback<bool>? openChanged = null)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "IsOpen", false);
            if (openChanged.HasValue) builder.AddAttribute(2, "IsOpenChanged", openChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogTrigger>(0);
                b.AddAttribute(1, "AsChild", asChild);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(t =>
                {
                    t.OpenComponent<L.Button>(0);
                    t.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Open")));
                    t.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void AsChild_With_Button_Renders_One_Button_No_Wrapper()
    {
        var cut = Render();
        Assert.Empty(cut.FindAll("div[role='button']"));
        Assert.Single(cut.FindAll("button"));
        Assert.Equal("dialog", cut.Find("button").GetAttribute("aria-haspopup"));
    }

    [Fact]
    public void AsChild_Click_Opens_The_Dialog()
    {
        bool? opened = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => opened = v);
        var cut = Render(openChanged: cb);
        cut.Find("button").Click();
        Assert.True(opened);
    }

    [Fact]
    public void Without_AsChild_Keeps_The_Role_Button_Wrapper()
    {
        var cut = Render(asChild: false);
        Assert.NotEmpty(cut.FindAll("div[role='button']"));
    }
}
