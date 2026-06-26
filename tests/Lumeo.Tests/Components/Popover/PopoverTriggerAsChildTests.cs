using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Popover;

/// <summary>
/// G34 — asChild/Slot. PopoverTrigger AsChild renders NO wrapper and cascades a
/// TriggerSlot that a Lumeo &lt;Button&gt; auto-consumes, so the pair becomes ONE
/// &lt;button&gt; (no role=button div, no nested-interactive markup) with merged
/// class, composed click, and forwarded ARIA.
/// </summary>
public class PopoverTriggerAsChildTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PopoverTriggerAsChildTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderAsChild(
        bool open = false,
        EventCallback<bool>? openChanged = null,
        string? buttonClass = null,
        EventCallback<MouseEventArgs>? buttonOnClick = null,
        bool asChild = true)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Popover>(0);
            builder.AddAttribute(1, "Open", open);
            if (openChanged.HasValue) builder.AddAttribute(2, "OpenChanged", openChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PopoverTrigger>(0);
                b.AddAttribute(1, "AsChild", asChild);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(t =>
                {
                    t.OpenComponent<L.Button>(0);
                    if (buttonClass != null) t.AddAttribute(1, "Class", buttonClass);
                    if (buttonOnClick.HasValue) t.AddAttribute(2, "OnClick", buttonOnClick.Value);
                    t.AddAttribute(3, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Open")));
                    t.CloseComponent();
                }));
                b.CloseComponent();

                b.OpenComponent<L.PopoverContent>(1);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Popover content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void AsChild_With_Button_Renders_One_Button_And_No_Role_Button_Wrapper()
    {
        var cut = RenderAsChild(open: false);

        Assert.Empty(cut.FindAll("div[role='button']")); // no wrapper div
        var button = cut.Find("button");                 // exactly the merged trigger button
        Assert.Single(cut.FindAll("button"));
        Assert.Equal("dialog", button.GetAttribute("aria-haspopup"));
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Without_AsChild_Keeps_The_Role_Button_Wrapper()
    {
        // Backward compatibility: default behaviour is unchanged — the trigger still
        // renders its role=button div around the child (here a nested Button).
        var cut = RenderAsChild(open: false, asChild: false);
        Assert.NotEmpty(cut.FindAll("div[role='button']"));
    }

    [Fact]
    public void AsChild_Click_Fires_Both_Trigger_Toggle_And_Consumer_OnClick()
    {
        bool? opened = null;
        var consumerClicked = false;
        var openCb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => opened = v);
        var clickCb = EventCallback.Factory.Create<MouseEventArgs>(_ctx, (MouseEventArgs _) => consumerClicked = true);

        var cut = RenderAsChild(open: false, openChanged: openCb, buttonOnClick: clickCb);
        cut.Find("button").Click();

        Assert.True(opened);          // slot OnClick (Toggle) opened the popover
        Assert.True(consumerClicked); // the Button's own OnClick still fired
    }

    [Fact]
    public void AsChild_Merges_Consumer_Class_Onto_The_Single_Control()
    {
        // The whole point of asChild: the consumer's class/width reaches the real
        // control instead of a wrapper that the app then has to widen via CSS.
        var cut = RenderAsChild(open: false, buttonClass: "w-full");
        var cls = cut.Find("button").GetAttribute("class");
        Assert.Contains("w-full", cls);
        Assert.Contains("inline-flex", cls);
    }

    [Fact]
    public void AsChild_Aria_Expanded_And_Controls_Reflect_Open_State()
    {
        var cut = RenderAsChild(open: true);
        var button = cut.Find("button");
        Assert.Equal("true", button.GetAttribute("aria-expanded"));
        Assert.False(string.IsNullOrEmpty(button.GetAttribute("aria-controls")));
    }
}
