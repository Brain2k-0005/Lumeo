using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Overlay;

/// <summary>
/// LU-20 (regression from #521 / 5.11.0): DropdownMenuContent/ContextMenuContent/MenubarContent all
/// wrap their items in an inner <c>overflow-y-auto</c> scroll viewport, while the matching separator
/// keeps a full-bleed <c>-mx-1</c>. When the padding stayed on the OUTER panel (<c>p-1</c>) and the
/// viewport itself had no horizontal padding, the separator's <c>-mx-1</c> overhung the zero-padding
/// viewport by 4px each side — and <c>overflow-y: auto</c> implies <c>overflow-x: auto</c> once
/// <c>overflow-x</c> is left at its visible default, so that 4px overhang forced a horizontal
/// scrollbar in EVERY menu with a separator (scrollWidth 252 vs clientWidth 248 in the field report).
///
/// The fix moves the padding onto the scroll viewport (panel <c>p-0</c>-equivalent — no padding
/// class at all — scroll body <c>p-1</c>) and adds <c>overflow-x-hidden</c> to the viewport as a
/// belt. These tests lock the class split; they can't measure real scrollWidth/clientWidth (no
/// layout engine in bUnit) — see the E2E spec for that measurement.
/// </summary>
public class MenuSeparatorScrollbarRegressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MenuSeparatorScrollbarRegressionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static bool HasClassToken(string? classAttr, string token)
        => (classAttr ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(token);

    // --- DropdownMenu ---

    [Fact]
    public void DropdownMenuContent_Panel_Has_No_Padding_Class()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.ChildContent, DropdownChild()));

        var panelClass = cut.Find("[data-slot='dropdown-menu-content']").GetAttribute("class");
        Assert.False(HasClassToken(panelClass, "p-1"), $"panel class still carries p-1: {panelClass}");
    }

    [Fact]
    public void DropdownMenuContent_Viewport_Carries_The_Padding_And_Overflow_X_Hidden()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.ChildContent, DropdownChild()));

        var viewportClass = cut.Find("[data-slot='dropdown-menu-viewport']").GetAttribute("class");
        Assert.True(HasClassToken(viewportClass, "p-1"), $"viewport missing p-1: {viewportClass}");
        Assert.True(HasClassToken(viewportClass, "overflow-x-hidden"), $"viewport missing overflow-x-hidden: {viewportClass}");
        Assert.True(HasClassToken(viewportClass, "overflow-y-auto"), $"viewport missing overflow-y-auto: {viewportClass}");
    }

    private static RenderFragment DropdownChild() => b =>
    {
        b.OpenComponent<L.DropdownMenuTrigger>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Menu")));
        b.CloseComponent();
        b.OpenComponent<L.DropdownMenuContent>(2);
        b.AddAttribute(3, "Class", "w-64");
        b.AddAttribute(4, "ChildContent", (RenderFragment)(content =>
        {
            content.OpenComponent<L.DropdownMenuItem>(0);
            content.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "One")));
            content.CloseComponent();
            content.OpenComponent<L.DropdownMenuSeparator>(2);
            content.CloseComponent();
            content.OpenComponent<L.DropdownMenuItem>(3);
            content.AddAttribute(4, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Two")));
            content.CloseComponent();
        }));
        b.CloseComponent();
    };

    // --- ContextMenu ---

    [Fact]
    public void ContextMenuContent_Panel_Has_No_Padding_Class()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", ContextMenuChild());
            builder.CloseComponent();
        });

        var panelClass = cut.Find("[data-slot='context-menu-content']").GetAttribute("class");
        Assert.False(HasClassToken(panelClass, "p-1"), $"panel class still carries p-1: {panelClass}");
    }

    [Fact]
    public void ContextMenuContent_Viewport_Carries_The_Padding_And_Overflow_X_Hidden()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", ContextMenuChild());
            builder.CloseComponent();
        });

        var viewportClass = cut.Find("[data-slot='context-menu-viewport']").GetAttribute("class");
        Assert.True(HasClassToken(viewportClass, "p-1"), $"viewport missing p-1: {viewportClass}");
        Assert.True(HasClassToken(viewportClass, "overflow-x-hidden"), $"viewport missing overflow-x-hidden: {viewportClass}");
    }

    private static RenderFragment ContextMenuChild() => b =>
    {
        b.OpenComponent<L.ContextMenuContent>(0);
        b.AddAttribute(1, "Class", "w-64");
        b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
        {
            inner.OpenComponent<L.ContextMenuItem>(0);
            inner.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "One")));
            inner.CloseComponent();
            inner.OpenComponent<L.ContextMenuSeparator>(2);
            inner.CloseComponent();
            inner.OpenComponent<L.ContextMenuItem>(3);
            inner.AddAttribute(4, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Two")));
            inner.CloseComponent();
        }));
        b.CloseComponent();
    };

    // --- Menubar ---

    [Fact]
    public void MenubarContent_Panel_Has_No_Padding_Class()
    {
        var cut = RenderOpenMenubar();
        var panelClass = cut.Find("[data-slot='menubar-content']").GetAttribute("class");
        Assert.False(HasClassToken(panelClass, "p-1"), $"panel class still carries p-1: {panelClass}");
    }

    [Fact]
    public void MenubarContent_Viewport_Carries_The_Padding_And_Overflow_X_Hidden()
    {
        var cut = RenderOpenMenubar();
        var viewportClass = cut.Find("[data-slot='menubar-viewport']").GetAttribute("class");
        Assert.True(HasClassToken(viewportClass, "p-1"), $"viewport missing p-1: {viewportClass}");
        Assert.True(HasClassToken(viewportClass, "overflow-x-hidden"), $"viewport missing overflow-x-hidden: {viewportClass}");
    }

    private IRenderedComponent<IComponent> RenderOpenMenubar()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Menubar>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.MenubarMenu>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(menu =>
                {
                    menu.OpenComponent<L.MenubarTrigger>(0);
                    menu.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "File")));
                    menu.CloseComponent();

                    menu.OpenComponent<L.MenubarContent>(1);
                    menu.AddAttribute(2, "Class", "w-64");
                    menu.AddAttribute(3, "ChildContent", (RenderFragment)(content =>
                    {
                        content.OpenComponent<L.MenubarItem>(0);
                        content.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "One")));
                        content.CloseComponent();
                        content.OpenComponent<L.MenubarSeparator>(2);
                        content.CloseComponent();
                        content.OpenComponent<L.MenubarItem>(3);
                        content.AddAttribute(4, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Two")));
                        content.CloseComponent();
                    }));
                    menu.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("button").Click();
        return cut;
    }
}
