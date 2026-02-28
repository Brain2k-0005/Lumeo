using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.AlertDialog;

public class AlertDialogTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AlertDialogTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderAlertDialog(bool isOpen, EventCallback<bool>? isOpenChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "IsOpen", isOpen);
            if (isOpenChanged.HasValue)
                builder.AddAttribute(2, "IsOpenChanged", isOpenChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.AddContent(0, "Alert content");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Open / Close ---

    [Fact]
    public void AlertDialogContent_Not_Rendered_When_Closed()
    {
        var cut = RenderAlertDialog(isOpen: false);
        Assert.Empty(cut.FindAll("[role='alertdialog']"));
    }

    [Fact]
    public void AlertDialogContent_Rendered_When_Open()
    {
        var cut = RenderAlertDialog(isOpen: true);
        Assert.NotEmpty(cut.FindAll("[role='alertdialog']"));
    }

    [Fact]
    public void AlertDialogContent_Shows_Child_Text_When_Open()
    {
        var cut = RenderAlertDialog(isOpen: true);
        Assert.Contains("Alert content", cut.Markup);
    }

    // --- ARIA ---

    [Fact]
    public void AlertDialogContent_Has_Role_Alertdialog()
    {
        var cut = RenderAlertDialog(isOpen: true);
        var dialog = cut.Find("[role='alertdialog']");
        Assert.Equal("alertdialog", dialog.GetAttribute("role"));
    }

    [Fact]
    public void AlertDialogContent_Has_Aria_Modal_True()
    {
        var cut = RenderAlertDialog(isOpen: true);
        var dialog = cut.Find("[role='alertdialog']");
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
    }

    [Fact]
    public void AlertDialogContent_Has_Aria_Labelledby()
    {
        var cut = RenderAlertDialog(isOpen: true);
        var dialog = cut.Find("[role='alertdialog']");
        var labelledBy = dialog.GetAttribute("aria-labelledby");
        Assert.NotNull(labelledBy);
        Assert.NotEmpty(labelledBy);
    }

    [Fact]
    public void AlertDialogContent_Has_Aria_Describedby()
    {
        var cut = RenderAlertDialog(isOpen: true);
        var dialog = cut.Find("[role='alertdialog']");
        var describedBy = dialog.GetAttribute("aria-describedby");
        Assert.NotNull(describedBy);
        Assert.NotEmpty(describedBy);
    }

    // --- Backdrop ---

    [Fact]
    public void Backdrop_Rendered_When_Open()
    {
        var cut = RenderAlertDialog(isOpen: true);
        var allDivs = cut.FindAll("div");
        var hasBackdrop = allDivs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("bg-black") && cls.Contains("fixed") && cls.Contains("inset-0");
        });
        Assert.True(hasBackdrop, "Backdrop div should be present when open");
    }

    [Fact]
    public void Backdrop_Not_Rendered_When_Closed()
    {
        var cut = RenderAlertDialog(isOpen: false);
        var allDivs = cut.FindAll("div");
        var hasBackdrop = allDivs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("bg-black") && cls.Contains("fixed") && cls.Contains("inset-0");
        });
        Assert.False(hasBackdrop, "Backdrop should not be present when closed");
    }

    // --- Escape key ---

    [Fact]
    public void Escape_Key_Fires_IsOpenChanged_With_False()
    {
        bool? closedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);
        var cut = RenderAlertDialog(isOpen: true, isOpenChanged: callback);

        var dialog = cut.Find("[role='alertdialog']");
        dialog.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.False(closedValue);
    }

    // --- Trigger ---

    [Fact]
    public void Clicking_Trigger_Fires_IsOpenChanged_With_True()
    {
        bool? openedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openedValue = v);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "IsOpen", false);
            builder.AddAttribute(2, "IsOpenChanged", callback);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Open")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("div").Click();
        Assert.True(openedValue);
    }

    // --- Cancel / Action ---

    [Fact]
    public void AlertDialogCancel_Fires_IsOpenChanged_With_False()
    {
        bool? closedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "IsOpenChanged", callback);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.AlertDialogCancel>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Cancel")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        try { cut.Find("button").Click(); } catch (ArgumentException) { }
        Assert.False(closedValue);
    }

    [Fact]
    public void AlertDialogAction_Fires_OnAction_And_Closes()
    {
        bool actionFired = false;
        bool? closedValue = null;
        var openCallback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);
        var actionCallback = EventCallback.Factory.Create(_ctx, () => actionFired = true);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "IsOpenChanged", openCallback);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.AlertDialogAction>(0);
                    inner.AddAttribute(1, "OnAction", actionCallback);
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Confirm")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        try { cut.Find("button").Click(); } catch (ArgumentException) { }
        Assert.True(actionFired);
        Assert.False(closedValue);
    }

    // --- Custom Class ---

    [Fact]
    public void Custom_Class_Forwarded_On_AlertDialogContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "Class", "my-alert-class");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var dialog = cut.Find("[role='alertdialog']");
        Assert.Contains("my-alert-class", dialog.GetAttribute("class"));
    }

    [Fact]
    public void AlertDialogContent_Has_Default_Classes_When_Open()
    {
        var cut = RenderAlertDialog(isOpen: true);
        var dialog = cut.Find("[role='alertdialog']");
        Assert.Contains("bg-background", dialog.GetAttribute("class"));
        Assert.Contains("border-border", dialog.GetAttribute("class"));
    }

    // --- Header, Title, Description, Footer ---

    [Fact]
    public void AlertDialogTitle_Renders_Content()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.AlertDialogHeader>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(h =>
                    {
                        h.OpenComponent<L.AlertDialogTitle>(0);
                        h.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Confirm Action")));
                        h.CloseComponent();
                    }));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Confirm Action", cut.Markup);
    }

    [Fact]
    public void AlertDialogDescription_Renders_Content()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.AlertDialogDescription>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(d => d.AddContent(0, "This action cannot be undone.")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("This action cannot be undone.", cut.Markup);
    }
}
