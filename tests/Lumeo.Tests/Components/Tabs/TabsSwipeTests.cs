using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tabs;

public class TabsSwipeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TabsSwipeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Helper: render a basic Tabs + TabsList + two TabsContent panels.
    private IRenderedComponent<IComponent> RenderTabs(
        string activeValue = "one",
        bool swipeEnabled = false,
        bool swipeWrap = false,
        EventCallback<string>? activeValueChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tabs>(0);
            builder.AddAttribute(1, "ActiveValue", activeValue);
            builder.AddAttribute(2, "SwipeEnabled", swipeEnabled);
            builder.AddAttribute(3, "SwipeWrap", swipeWrap);
            if (activeValueChanged.HasValue)
                builder.AddAttribute(4, "ActiveValueChanged", activeValueChanged.Value);
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TabsList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.TabsTrigger>(0);
                    inner.AddAttribute(1, "Value", "one");
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(t => t.AddContent(0, "One")));
                    inner.CloseComponent();

                    inner.OpenComponent<L.TabsTrigger>(2);
                    inner.AddAttribute(3, "Value", "two");
                    inner.AddAttribute(4, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Two")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();

                b.OpenComponent<L.TabsContent>(2);
                b.AddAttribute(3, "Value", "one");
                b.AddAttribute(4, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Panel One")));
                b.CloseComponent();

                b.OpenComponent<L.TabsContent>(5);
                b.AddAttribute(6, "Value", "two");
                b.AddAttribute(7, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Panel Two")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- SwipeEnabled parameter ---

    [Fact]
    public void Tabs_SwipeEnabled_DefaultIsFalse()
    {
        // Render without specifying SwipeEnabled — should default to false with no errors.
        var cut = RenderTabs(activeValue: "one");
        // Active panel renders; no exception thrown.
        Assert.Contains("Panel One", cut.Markup);
    }

    [Fact]
    public void Tabs_SwipeEnabled_True_RendersWithoutError()
    {
        var cut = RenderTabs(activeValue: "one", swipeEnabled: true);
        Assert.Contains("Panel One", cut.Markup);
    }

    [Fact]
    public void Tabs_SwipeEnabled_True_AddsSwipeCssOnActivePanel()
    {
        var cut = RenderTabs(activeValue: "one", swipeEnabled: true);
        // The active TabsContent panel should include the 'touch-pan-y' class.
        var panel = cut.Find("[role='tabpanel']");
        Assert.Contains("touch-pan-y", panel.ClassName);
    }

    [Fact]
    public void Tabs_SwipeEnabled_False_NoSwipeCssOnPanel()
    {
        var cut = RenderTabs(activeValue: "one", swipeEnabled: false);
        var panel = cut.Find("[role='tabpanel']");
        Assert.DoesNotContain("touch-pan-y", panel.ClassName);
    }

    // --- SwipeWrap parameter ---

    [Fact]
    public void Tabs_SwipeWrap_DefaultIsFalse_RendersWithoutError()
    {
        var cut = RenderTabs(activeValue: "one", swipeEnabled: true, swipeWrap: false);
        Assert.Contains("Panel One", cut.Markup);
    }

    [Fact]
    public void Tabs_SwipeWrap_True_RendersWithoutError()
    {
        var cut = RenderTabs(activeValue: "one", swipeEnabled: true, swipeWrap: true);
        Assert.Contains("Panel One", cut.Markup);
    }

    // --- SetValue callable (two-way binding) ---

    [Fact]
    public async Task Tabs_SetValue_ChangesActivePanel()
    {
        var active = "one";
        EventCallback<string> cb = EventCallback.Factory.Create<string>(_ctx, (string v) => active = v);

        var cut = RenderTabs(activeValue: active, activeValueChanged: cb);

        // Simulate clicking the second trigger.
        var trigger = cut.FindAll("[role='tab']")[1];
        await trigger.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Equal("two", active);
    }
}
