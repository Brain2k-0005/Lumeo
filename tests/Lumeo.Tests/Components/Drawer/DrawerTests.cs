using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

public class DrawerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DrawerTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderDrawer(bool isOpen, EventCallback<bool>? isOpenChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", isOpen);
            if (isOpenChanged.HasValue)
                builder.AddAttribute(2, "IsOpenChanged", isOpenChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.AddContent(0, "Drawer content");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Open / Close ---

    [Fact]
    public void DrawerContent_Not_Rendered_When_Closed()
    {
        var cut = RenderDrawer(isOpen: false);
        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void DrawerContent_Rendered_When_Open()
    {
        var cut = RenderDrawer(isOpen: true);
        Assert.NotEmpty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void DrawerContent_Shows_Child_Text_When_Open()
    {
        var cut = RenderDrawer(isOpen: true);
        Assert.Contains("Drawer content", cut.Markup);
    }

    // --- ARIA ---

    [Fact]
    public void DrawerContent_Has_Role_Dialog()
    {
        var cut = RenderDrawer(isOpen: true);
        var dialog = cut.Find("[role='dialog']");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
    }

    [Fact]
    public void DrawerContent_Has_Aria_Modal_True()
    {
        var cut = RenderDrawer(isOpen: true);
        var dialog = cut.Find("[role='dialog']");
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
    }

    [Fact]
    public void DrawerContent_Has_Aria_Labelledby()
    {
        var cut = RenderDrawer(isOpen: true);
        var dialog = cut.Find("[role='dialog']");
        var labelledBy = dialog.GetAttribute("aria-labelledby");
        Assert.NotNull(labelledBy);
        Assert.NotEmpty(labelledBy);
    }

    [Fact]
    public void DrawerContent_Has_Aria_Describedby()
    {
        var cut = RenderDrawer(isOpen: true);
        var dialog = cut.Find("[role='dialog']");
        var describedBy = dialog.GetAttribute("aria-describedby");
        Assert.NotNull(describedBy);
        Assert.NotEmpty(describedBy);
    }

    // --- Escape key ---

    [Fact]
    public void Escape_Key_Fires_IsOpenChanged_With_False()
    {
        bool? closedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);
        var cut = RenderDrawer(isOpen: true, isOpenChanged: callback);

        var dialog = cut.Find("[role='dialog']");
        dialog.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.False(closedValue);
    }

    // --- Backdrop ---

    [Fact]
    public void Backdrop_Rendered_When_Open()
    {
        var cut = RenderDrawer(isOpen: true);
        var allDivs = cut.FindAll("div");
        var hasBackdrop = allDivs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("bg-black") && cls.Contains("fixed") && cls.Contains("inset-0");
        });
        Assert.True(hasBackdrop, "Backdrop should be present when drawer is open");
    }

    [Fact]
    public void Backdrop_Not_Rendered_When_Closed()
    {
        var cut = RenderDrawer(isOpen: false);
        var allDivs = cut.FindAll("div");
        var hasBackdrop = allDivs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("bg-black") && cls.Contains("fixed") && cls.Contains("inset-0");
        });
        Assert.False(hasBackdrop, "Backdrop should not be present when drawer is closed");
    }

    // --- Default positioning (bottom) ---

    [Fact]
    public void DrawerContent_Has_Bottom_Positioning_Classes()
    {
        var cut = RenderDrawer(isOpen: true);
        var dialog = cut.Find("[role='dialog']");
        var cls = dialog.GetAttribute("class") ?? "";
        Assert.Contains("bottom-0", cls);
    }

    // --- Handle bar ---

    [Fact]
    public void DrawerContent_Has_Handle_Bar_When_Open()
    {
        var cut = RenderDrawer(isOpen: true);
        var allDivs = cut.FindAll("div");
        // Handle bar: mx-auto mt-4 h-2 w-[100px] rounded-full bg-muted
        var hasHandle = allDivs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("rounded-full") && cls.Contains("bg-muted");
        });
        Assert.True(hasHandle, "Drawer should have a handle bar div");
    }

    // --- Trigger ---

    [Fact]
    public void DrawerTrigger_Fires_IsOpenChanged_With_True()
    {
        bool? openedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openedValue = v);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", false);
            builder.AddAttribute(2, "IsOpenChanged", callback);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Open")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("div[role='button']").Click();
        Assert.True(openedValue);
    }

    // --- Custom Class ---

    [Fact]
    public void Custom_Class_Forwarded_On_DrawerContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "Class", "my-drawer-class");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var dialog = cut.Find("[role='dialog']");
        Assert.Contains("my-drawer-class", dialog.GetAttribute("class"));
    }

    // --- Header, Title, Description ---

    [Fact]
    public void DrawerTitle_Renders_Content()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DrawerHeader>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(h =>
                    {
                        h.OpenComponent<L.DrawerTitle>(0);
                        h.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Drawer Title")));
                        h.CloseComponent();
                    }));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Drawer Title", cut.Markup);
    }

    [Fact]
    public void DrawerDescription_Renders_Content()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DrawerDescription>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(d => d.AddContent(0, "Drawer Description")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Drawer Description", cut.Markup);
    }
}
