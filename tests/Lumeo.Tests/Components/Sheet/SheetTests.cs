using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sheet;

public class SheetTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SheetTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderSheet(
        bool isOpen,
        EventCallback<bool>? isOpenChanged = null,
        L.SheetContent.SheetSide side = L.SheetContent.SheetSide.Right)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Sheet>(0);
            builder.AddAttribute(1, "IsOpen", isOpen);
            if (isOpenChanged.HasValue)
                builder.AddAttribute(2, "IsOpenChanged", isOpenChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetContent>(0);
                b.AddAttribute(1, "Side", side);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.AddContent(0, "Sheet content");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Open / Close ---

    [Fact]
    public void SheetContent_Not_Rendered_When_Closed()
    {
        var cut = RenderSheet(isOpen: false);
        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void SheetContent_Rendered_When_Open()
    {
        var cut = RenderSheet(isOpen: true);
        Assert.NotEmpty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void SheetContent_Shows_Child_Text_When_Open()
    {
        var cut = RenderSheet(isOpen: true);
        Assert.Contains("Sheet content", cut.Markup);
    }

    // --- ARIA ---

    [Fact]
    public void SheetContent_Has_Role_Dialog()
    {
        var cut = RenderSheet(isOpen: true);
        var dialog = cut.Find("[role='dialog']");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
    }

    [Fact]
    public void SheetContent_Has_Aria_Modal_True()
    {
        var cut = RenderSheet(isOpen: true);
        var dialog = cut.Find("[role='dialog']");
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
    }

    [Fact]
    public void SheetContent_Has_Aria_Labelledby()
    {
        var cut = RenderSheet(isOpen: true);
        var dialog = cut.Find("[role='dialog']");
        var labelledBy = dialog.GetAttribute("aria-labelledby");
        Assert.NotNull(labelledBy);
        Assert.NotEmpty(labelledBy);
    }

    [Fact]
    public void SheetContent_Has_Aria_Describedby()
    {
        var cut = RenderSheet(isOpen: true);
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
        var cut = RenderSheet(isOpen: true, isOpenChanged: callback);

        var dialog = cut.Find("[role='dialog']");
        dialog.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.False(closedValue);
    }

    // --- Side variants ---

    [Fact]
    public void SheetContent_Right_Side_Has_Right_Classes()
    {
        var cut = RenderSheet(isOpen: true, side: L.SheetContent.SheetSide.Right);
        var dialog = cut.Find("[role='dialog']");
        var cls = dialog.GetAttribute("class") ?? "";
        Assert.Contains("right-0", cls);
    }

    [Fact]
    public void SheetContent_Left_Side_Has_Left_Classes()
    {
        var cut = RenderSheet(isOpen: true, side: L.SheetContent.SheetSide.Left);
        var dialog = cut.Find("[role='dialog']");
        var cls = dialog.GetAttribute("class") ?? "";
        Assert.Contains("left-0", cls);
    }

    [Fact]
    public void SheetContent_Top_Side_Has_Top_Classes()
    {
        var cut = RenderSheet(isOpen: true, side: L.SheetContent.SheetSide.Top);
        var dialog = cut.Find("[role='dialog']");
        var cls = dialog.GetAttribute("class") ?? "";
        Assert.Contains("top-0", cls);
    }

    [Fact]
    public void SheetContent_Bottom_Side_Has_Bottom_Classes()
    {
        var cut = RenderSheet(isOpen: true, side: L.SheetContent.SheetSide.Bottom);
        var dialog = cut.Find("[role='dialog']");
        var cls = dialog.GetAttribute("class") ?? "";
        Assert.Contains("bottom-0", cls);
    }

    // --- Backdrop ---

    [Fact]
    public void Backdrop_Rendered_When_Open()
    {
        var cut = RenderSheet(isOpen: true);
        var allDivs = cut.FindAll("div");
        var hasBackdrop = allDivs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("bg-black") && cls.Contains("fixed") && cls.Contains("inset-0");
        });
        Assert.True(hasBackdrop, "Backdrop should be present when sheet is open");
    }

    // --- Trigger ---

    [Fact]
    public void SheetTrigger_Fires_IsOpenChanged_With_True()
    {
        bool? openedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openedValue = v);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Sheet>(0);
            builder.AddAttribute(1, "IsOpen", false);
            builder.AddAttribute(2, "IsOpenChanged", callback);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Open")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("div[role='button']").Click();
        Assert.True(openedValue);
    }

    // --- Close button ---

    [Fact]
    public void Close_Button_Rendered_When_Sheet_Open()
    {
        var cut = RenderSheet(isOpen: true);
        var buttons = cut.FindAll("button");
        Assert.NotEmpty(buttons);
        var hasCloseBtn = buttons.Any(b => b.InnerHtml.Contains("Close"));
        Assert.True(hasCloseBtn, "Sheet should have a close button with 'Close' sr-only text");
    }

    // --- Custom Class ---

    [Fact]
    public void Custom_Class_Forwarded_On_SheetContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Sheet>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetContent>(0);
                b.AddAttribute(1, "Class", "my-sheet-class");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var dialog = cut.Find("[role='dialog']");
        Assert.Contains("my-sheet-class", dialog.GetAttribute("class"));
    }

    // --- Header, Title, Description ---

    [Fact]
    public void SheetTitle_Renders_Content()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Sheet>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.SheetHeader>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(h =>
                    {
                        h.OpenComponent<L.SheetTitle>(0);
                        h.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Sheet Title")));
                        h.CloseComponent();
                    }));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Sheet Title", cut.Markup);
    }

    [Fact]
    public void SheetDescription_Renders_Content()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Sheet>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.SheetDescription>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(d => d.AddContent(0, "Sheet Description")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Sheet Description", cut.Markup);
    }
}
