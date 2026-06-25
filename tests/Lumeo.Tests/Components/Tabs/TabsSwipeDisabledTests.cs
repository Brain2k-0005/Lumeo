using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tabs;

/// <summary>
/// Battle-test finding #48 (Tabs / edge-data) — swipe navigation must skip
/// Disabled tabs, exactly like keyboard arrow navigation
/// (TabsTrigger.FindNextEnabledTab). Without the fix, a swipe lands on and
/// ACTIVATES a disabled tab; with the fix it walks past it to the next enabled
/// neighbour (respecting SwipeWrap at the boundary).
/// </summary>
public class TabsSwipeDisabledTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TabsSwipeDisabledTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // three tabs: one | two (Disabled) | three
    private IRenderedComponent<IComponent> RenderTabs(
        string activeValue,
        EventCallback<string> activeValueChanged,
        bool swipeWrap = false)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tabs>(0);
            builder.AddAttribute(1, "ActiveValue", activeValue);
            builder.AddAttribute(2, "SwipeEnabled", true);
            builder.AddAttribute(3, "SwipeWrap", swipeWrap);
            builder.AddAttribute(4, "ActiveValueChanged", activeValueChanged);
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TabsList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    void Trigger(int seq, string val, string label, bool disabled = false)
                    {
                        inner.OpenComponent<L.TabsTrigger>(seq);
                        inner.AddAttribute(seq + 1, "Value", val);
                        inner.AddAttribute(seq + 2, "Disabled", disabled);
                        inner.AddAttribute(seq + 3, "ChildContent", (RenderFragment)(t => t.AddContent(0, label)));
                        inner.CloseComponent();
                    }
                    Trigger(0, "one", "One");
                    Trigger(4, "two", "Two", disabled: true);
                    Trigger(8, "three", "Three");
                }));
                b.CloseComponent();

                void Panel(int seq, string val, string text)
                {
                    b.OpenComponent<L.TabsContent>(seq);
                    b.AddAttribute(seq + 1, "Value", val);
                    b.AddAttribute(seq + 2, "ChildContent", (RenderFragment)(c => c.AddContent(0, text)));
                    b.CloseComponent();
                }
                Panel(2, "one", "Panel One");
                Panel(6, "two", "Panel Two");
                Panel(10, "three", "Panel Three");
            }));
            builder.CloseComponent();
        });

    private static Task Swipe(IRenderedComponent<IComponent> cut, string direction)
    {
        var tabs = cut.FindComponent<L.Tabs>().Instance;
        return cut.InvokeAsync(() => tabs.NavigateBySwipe(direction));
    }

    [Fact]
    public async Task Swipe_Next_SkipsDisabledTab()
    {
        var active = "one";
        var cb = EventCallback.Factory.Create<string>(this, (string v) => active = v);
        var cut = RenderTabs("one", cb);

        // Swipe forward from "one": the next tab ("two") is disabled, so it must
        // be skipped and "three" activated instead — not "two".
        await Swipe(cut, "next");

        Assert.Equal("three", active);
    }

    [Fact]
    public async Task Swipe_Prev_SkipsDisabledTab()
    {
        var active = "three";
        var cb = EventCallback.Factory.Create<string>(this, (string v) => active = v);
        var cut = RenderTabs("three", cb);

        // Swipe backward from "three": "two" is disabled, must land on "one".
        await Swipe(cut, "prev");

        Assert.Equal("one", active);
    }

    [Fact]
    public async Task Swipe_Next_NoEnabledNeighbour_DoesNothing()
    {
        // Active is the last enabled tab; the only forward neighbour is disabled
        // and SwipeWrap is off → swipe must be a no-op (no disabled activation).
        var active = "three";
        var changed = false;
        var cb = EventCallback.Factory.Create<string>(this, (string v) => { active = v; changed = true; });
        var cut = RenderTabs("three", cb, swipeWrap: false);

        await Swipe(cut, "next");

        Assert.False(changed);
        Assert.Equal("three", active);
    }
}
