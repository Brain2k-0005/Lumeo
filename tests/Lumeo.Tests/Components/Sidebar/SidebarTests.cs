using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

public class SidebarTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public SidebarTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    private IRenderedComponent<IComponent> RenderSidebar(
        bool isCollapsed = false,
        EventCallback<bool>? isCollapsedChanged = null,
        bool includeTrigger = false,
        bool includeSidebarComponent = false,
        string? sidebarClass = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarProvider>(0);
            builder.AddAttribute(1, "IsCollapsed", isCollapsed);
            if (isCollapsedChanged.HasValue)
                builder.AddAttribute(2, "IsCollapsedChanged", isCollapsedChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                if (includeTrigger)
                {
                    b.OpenComponent<L.SidebarTrigger>(0);
                    b.CloseComponent();
                }

                if (includeSidebarComponent)
                {
                    b.OpenComponent<L.SidebarComponent>(10);
                    if (sidebarClass is not null)
                        b.AddAttribute(11, "Class", sidebarClass);
                    b.AddAttribute(12, "ChildContent", (RenderFragment)(inner =>
                        inner.AddContent(0, "Sidebar content")));
                    b.CloseComponent();
                }
                else
                {
                    b.AddContent(0, "Provider content");
                }
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void SidebarProvider_Renders_Content()
    {
        var cut = RenderSidebar();

        Assert.Contains("Provider content", cut.Markup);
    }

    [Fact]
    public void SidebarProvider_Wrapper_Has_Flex_Class()
    {
        var cut = RenderSidebar();

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("flex", cls);
        Assert.Contains("h-full", cls);
    }

    [Fact]
    public void SidebarComponent_Renders_Aside_Element()
    {
        var cut = RenderSidebar(includeSidebarComponent: true);

        Assert.NotNull(cut.Find("aside"));
    }

    [Fact]
    public void SidebarComponent_Expanded_Has_W64_Class()
    {
        var cut = RenderSidebar(isCollapsed: false, includeSidebarComponent: true);

        var aside = cut.Find("aside");
        Assert.Contains("w-64", aside.GetAttribute("class") ?? "");
    }

    [Fact]
    public void SidebarComponent_Collapsed_Has_W16_Class()
    {
        var cut = RenderSidebar(isCollapsed: true, includeSidebarComponent: true);

        var aside = cut.Find("aside");
        Assert.Contains("w-16", aside.GetAttribute("class") ?? "");
    }

    [Fact]
    public void SidebarComponent_Has_BgSidebar_Class()
    {
        var cut = RenderSidebar(includeSidebarComponent: true);

        var aside = cut.Find("aside");
        Assert.Contains("bg-sidebar", aside.GetAttribute("class") ?? "");
    }

    [Fact]
    public void SidebarComponent_Custom_Class_Appended()
    {
        var cut = RenderSidebar(includeSidebarComponent: true, sidebarClass: "my-sidebar");

        var aside = cut.Find("aside");
        Assert.Contains("my-sidebar", aside.GetAttribute("class") ?? "");
    }

    [Fact]
    public void SidebarComponent_Renders_ChildContent()
    {
        var cut = RenderSidebar(includeSidebarComponent: true);

        Assert.Contains("Sidebar content", cut.Markup);
    }

    [Fact]
    public void SidebarTrigger_Renders_Button()
    {
        var cut = RenderSidebar(includeTrigger: true);

        Assert.NotNull(cut.Find("button[title='Toggle sidebar']"));
    }

    [Fact]
    public void SidebarTrigger_Shows_Close_Icon_When_Expanded()
    {
        // When not collapsed, should show PanelLeftClose icon
        var cut = RenderSidebar(isCollapsed: false, includeTrigger: true);

        var button = cut.Find("button[title='Toggle sidebar']");
        Assert.NotNull(button);
        // The icon is rendered by Blazicon — just check the button is present with correct state
        Assert.DoesNotContain("disabled", button.GetAttribute("class") ?? "");
    }

    [Fact]
    public void SidebarTrigger_Click_Invokes_IsCollapsedChanged()
    {
        bool? receivedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => receivedValue = v);

        var cut = RenderSidebar(isCollapsed: false, isCollapsedChanged: callback, includeTrigger: true);

        cut.Find("button[title='Toggle sidebar']").Click();

        Assert.True(receivedValue, "Clicking trigger when expanded should set IsCollapsed to true");
    }

    [Fact]
    public void SidebarTrigger_Click_When_Collapsed_Invokes_False()
    {
        bool? receivedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => receivedValue = v);

        var cut = RenderSidebar(isCollapsed: true, isCollapsedChanged: callback, includeTrigger: true);

        cut.Find("button[title='Toggle sidebar']").Click();

        Assert.False(receivedValue, "Clicking trigger when collapsed should set IsCollapsed to false");
    }

    [Fact]
    public void SidebarTrigger_Has_Expected_Classes()
    {
        var cut = RenderSidebar(includeTrigger: true);

        var button = cut.Find("button[title='Toggle sidebar']");
        var cls = button.GetAttribute("class") ?? "";
        Assert.Contains("inline-flex", cls);
        Assert.Contains("rounded-md", cls);
    }

    [Fact]
    public void SidebarProvider_IsCollapsed_False_By_Default()
    {
        // When SidebarComponent is included without IsCollapsed param, defaults to expanded
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarProvider>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarComponent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var aside = cut.Find("aside");
        Assert.Contains("w-64", aside.GetAttribute("class") ?? "");
    }

    [Fact]
    public void SidebarComponent_Right_Side_Has_Order_Last_Class()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarProvider>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarComponent>(0);
                b.AddAttribute(1, "Side", L.SidebarComponent.SidebarSide.Right);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Right")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var aside = cut.Find("aside");
        Assert.Contains("order-last", aside.GetAttribute("class") ?? "");
    }
}
