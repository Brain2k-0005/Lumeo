using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tabs;

/// <summary>
/// B13 — deleting a closable tab from the keyboard (Delete) must move focus to a
/// neighbouring tab rather than dropping it to &lt;body&gt; (WCAG 2.4.3).
/// </summary>
public class TabsCloseFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public TabsCloseFocusTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderClosableTabs()
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tabs>(0);
            builder.AddAttribute(1, "ActiveValue", "one");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TabsList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    void Tab(int seq, string val, string label)
                    {
                        inner.OpenComponent<L.TabsTrigger>(seq);
                        inner.AddAttribute(seq + 1, "Value", val);
                        inner.AddAttribute(seq + 2, "IsClosable", true);
                        inner.AddAttribute(seq + 3, "OnClose", EventCallback.Factory.Create(this, () => { }));
                        inner.AddAttribute(seq + 4, "ChildContent", (RenderFragment)(t => t.AddContent(0, label)));
                        inner.CloseComponent();
                    }
                    Tab(0, "one", "One");
                    Tab(10, "two", "Two");
                    Tab(20, "three", "Three");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Delete_On_A_Closable_Tab_Moves_Focus_To_The_Next_Tab()
    {
        var cut = RenderClosableTabs();
        var tabs = cut.FindAll("[role='tab']");
        var secondTabId = tabs[1].GetAttribute("id");

        tabs[0].KeyDown(new KeyboardEventArgs { Key = "Delete" }); // delete the first tab

        Assert.Contains(_ctx.JSInterop.Invocations,
            i => i.Identifier == "focusElementById" && (i.Arguments[0] as string) == secondTabId);
    }

    [Fact]
    public void Delete_On_The_Last_Tab_Moves_Focus_To_The_Previous_Tab()
    {
        var cut = RenderClosableTabs();
        var tabs = cut.FindAll("[role='tab']");
        var secondTabId = tabs[1].GetAttribute("id");

        tabs[2].KeyDown(new KeyboardEventArgs { Key = "Delete" }); // delete the last tab

        Assert.Contains(_ctx.JSInterop.Invocations,
            i => i.Identifier == "focusElementById" && (i.Arguments[0] as string) == secondTabId);
    }
}
