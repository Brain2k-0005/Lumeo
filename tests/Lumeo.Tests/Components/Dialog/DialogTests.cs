using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Dialog;

public class DialogTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DialogTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Helper: render the full Dialog tree
    private IRenderedComponent<IComponent> RenderDialog(bool isOpen, EventCallback<bool>? isOpenChanged = null, RenderFragment? content = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "IsOpen", isOpen);
            if (isOpenChanged.HasValue)
                builder.AddAttribute(2, "IsOpenChanged", isOpenChanged.Value);
            builder.AddAttribute(3, "ChildContent", content ?? (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.AddContent(0, "Default content");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Rendering ---

    [Fact]
    public void DialogContent_Not_Rendered_When_IsOpen_False()
    {
        var cut = RenderDialog(isOpen: false);
        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void DialogContent_Rendered_When_IsOpen_True()
    {
        var cut = RenderDialog(isOpen: true);
        Assert.NotEmpty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void DialogContent_Shows_Child_Text_When_Open()
    {
        var cut = RenderDialog(isOpen: true);
        Assert.Contains("Default content", cut.Markup);
    }

    // --- ARIA ---

    [Fact]
    public void DialogContent_Has_Role_Dialog()
    {
        var cut = RenderDialog(isOpen: true);
        var dialog = cut.Find("[role='dialog']");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
    }

    [Fact]
    public void DialogContent_Has_Aria_Modal_True()
    {
        var cut = RenderDialog(isOpen: true);
        var dialog = cut.Find("[role='dialog']");
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
    }

    // --- Backdrop ---

    [Fact]
    public void Backdrop_Rendered_When_Open()
    {
        var cut = RenderDialog(isOpen: true);
        // Backdrop is a fixed inset-0 div with bg-black/80
        var allDivs = cut.FindAll("div");
        var hasBackdrop = allDivs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("bg-black") && cls.Contains("fixed") && cls.Contains("inset-0");
        });
        Assert.True(hasBackdrop, "Backdrop div with bg-black/fixed/inset-0 should be present");
    }

    [Fact]
    public void Backdrop_Not_Rendered_When_Closed()
    {
        var cut = RenderDialog(isOpen: false);
        var allDivs = cut.FindAll("div");
        var hasBackdrop = allDivs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("bg-black") && cls.Contains("fixed") && cls.Contains("inset-0");
        });
        Assert.False(hasBackdrop, "Backdrop should not be present when dialog is closed");
    }

    // --- Open/Close via Trigger and Close ---

    [Fact]
    public void Clicking_DialogTrigger_Fires_IsOpenChanged_With_True()
    {
        bool? openedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openedValue = v);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "IsOpen", false);
            builder.AddAttribute(2, "IsOpenChanged", callback);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Open")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("div").Click();
        Assert.True(openedValue);
    }

    [Fact]
    public void Clicking_DialogClose_Fires_IsOpenChanged_With_False()
    {
        bool? closedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "IsOpenChanged", callback);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DialogClose>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Close")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Find div with "Close" text that doesn't contain the dialog content div
        var divs = cut.FindAll("div");
        var closeDiv = divs.FirstOrDefault(d => d.TextContent.Trim() == "Close");
        Assert.NotNull(closeDiv);
        // Click causes internal state change that removes the component from the tree
        // during re-render, which throws. The callback fires before the re-render.
        try { closeDiv!.Click(); } catch (ArgumentException) { }
        Assert.False(closedValue);
    }

    // --- Close button in content ---

    [Fact]
    public void Close_Button_Rendered_When_Dialog_Open()
    {
        var cut = RenderDialog(isOpen: true);
        // There should be a button with sr-only "Close" text (the X button)
        var buttons = cut.FindAll("button");
        Assert.NotEmpty(buttons);
        var hasCloseBtn = buttons.Any(b => b.InnerHtml.Contains("Close"));
        Assert.True(hasCloseBtn, "Dialog should have a close button with 'Close' sr-only text");
    }

    // --- Custom CSS ---

    [Fact]
    public void Custom_Class_Forwarded_On_DialogContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "Class", "my-dialog-class");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var dialog = cut.Find("[role='dialog']");
        Assert.Contains("my-dialog-class", dialog.GetAttribute("class"));
    }

    [Fact]
    public void DialogContent_Has_Default_Classes_When_Open()
    {
        var cut = RenderDialog(isOpen: true);
        var dialog = cut.Find("[role='dialog']");
        Assert.Contains("bg-background", dialog.GetAttribute("class"));
        Assert.Contains("border-border", dialog.GetAttribute("class"));
    }

    // --- AdditionalAttributes ---

    [Fact]
    public void Additional_Attributes_Forwarded_On_DialogContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object>
                {
                    ["data-testid"] = "my-dialog"
                });
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var dialog = cut.Find("[role='dialog']");
        Assert.Equal("my-dialog", dialog.GetAttribute("data-testid"));
    }

    // --- DialogHeader, DialogFooter, DialogTitle, DialogDescription ---

    [Fact]
    public void DialogHeader_Renders_ChildContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DialogHeader>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(h =>
                    {
                        h.OpenComponent<L.DialogTitle>(0);
                        h.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "My Title")));
                        h.CloseComponent();
                    }));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("My Title", cut.Markup);
    }

    [Fact]
    public void DialogDescription_Renders_ChildContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DialogDescription>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(d => d.AddContent(0, "My Description")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("My Description", cut.Markup);
    }

    [Fact]
    public void DialogTitle_Custom_Class_Forwarded()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DialogTitle>(0);
                    inner.AddAttribute(1, "Class", "title-custom");
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Title")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Find the element with "title-custom" class
        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e => (e.GetAttribute("class") ?? "").Contains("title-custom")));
    }
}
