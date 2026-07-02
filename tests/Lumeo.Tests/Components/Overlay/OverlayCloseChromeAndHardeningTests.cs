using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Overlay;

/// <summary>
/// Regression tests for the 4.1.0 overlay chrome + hardening wave (consumer feedback):
///
/// W3 — ShowCloseButton: the shell X used to be coupled solely to PreventClose (modal
/// dialogs lost the X together with backdrop/Escape, and there was no way to hide it
/// for custom chrome). New bool? parameter on DialogContent/SheetContent (and
/// OverlayOptions for the service path): null = legacy coupling, true = force the X
/// even on a modal (it still routes through the dismiss guard), false = hide. Plus
/// stable hook classes (lumeo-dialog-close / lumeo-sheet-close) and z-10 so consumer
/// sticky headers can't paint over the button.
///
/// B4 hardening — pointer-events scoping: every overlay shell's full-viewport
/// fixed inset-0 wrapper is pointer-events-none with pointer-events-auto restored on
/// the backdrop + panel, so a wedged/off-screen panel can never leave an invisible
/// input-eating layer over the app (the reported stacked-drawer failure shape).
///
/// B3 — overflow-x-clip on the -mx-1 px-1 focus-ring-gutter scroll bodies
/// (OverlayForm, OverlayProvider's ScrollableBody, ScrollArea's FocusRingGutter) and
/// overflow-x-hidden on DialogContent's Scrollable wrapper: the negative margin makes
/// the body 8px wider than its parent, which grew a spurious horizontal scrollbar.
/// </summary>
public class OverlayCloseChromeAndHardeningTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OverlayCloseChromeAndHardeningTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderDialog(bool preventClose = false, bool? showCloseButton = null, bool scrollable = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "PreventClose", preventClose);
                if (showCloseButton is not null)
                    b.AddAttribute(2, "ShowCloseButton", showCloseButton);
                b.AddAttribute(3, "Scrollable", scrollable);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Dialog body")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private IRenderedComponent<IComponent> RenderSheet(bool preventClose = false, bool? showCloseButton = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Sheet>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetContent>(0);
                b.AddAttribute(1, "PreventClose", preventClose);
                if (showCloseButton is not null)
                    b.AddAttribute(2, "ShowCloseButton", showCloseButton);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Sheet body")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // ---- W3: ShowCloseButton decoupling ------------------------------------------------

    [Fact]
    public void Dialog_Default_Coupling_X_Follows_PreventClose()
    {
        Assert.NotEmpty(RenderDialog(preventClose: false).FindAll(".lumeo-dialog-close"));
        Assert.Empty(RenderDialog(preventClose: true).FindAll(".lumeo-dialog-close"));
    }

    [Fact]
    public void Dialog_ShowCloseButton_True_Forces_The_X_On_A_Modal()
    {
        // PreventClose keeps backdrop/Escape disabled, but the X renders and offers
        // the explicit way out (routes through the dismiss guard).
        var cut = RenderDialog(preventClose: true, showCloseButton: true);
        Assert.NotEmpty(cut.FindAll(".lumeo-dialog-close"));
    }

    [Fact]
    public void Dialog_ShowCloseButton_False_Hides_The_X_For_Custom_Chrome()
    {
        var cut = RenderDialog(preventClose: false, showCloseButton: false);
        Assert.Empty(cut.FindAll(".lumeo-dialog-close"));
    }

    [Fact]
    public void Sheet_ShowCloseButton_Behaves_Identically()
    {
        Assert.NotEmpty(RenderSheet().FindAll(".lumeo-sheet-close"));
        Assert.Empty(RenderSheet(preventClose: true).FindAll(".lumeo-sheet-close"));
        Assert.NotEmpty(RenderSheet(preventClose: true, showCloseButton: true).FindAll(".lumeo-sheet-close"));
        Assert.Empty(RenderSheet(showCloseButton: false).FindAll(".lumeo-sheet-close"));
    }

    [Fact]
    public void Close_Buttons_Carry_The_Stable_Hook_Class_And_ZIndex()
    {
        var dialogX = RenderDialog().Find(".lumeo-dialog-close");
        Assert.Contains("z-10", dialogX.ClassList);

        var sheetX = RenderSheet().Find(".lumeo-sheet-close");
        Assert.Contains("z-10", sheetX.ClassList);
    }

    // ---- B4 hardening: pointer-events scoping -------------------------------------------

    [Fact]
    public void Dialog_Wrapper_Is_PointerEvents_None_With_Auto_On_Backdrop_And_Panel()
    {
        var cut = RenderDialog();
        var wrapper = cut.FindAll("div").First(d => d.ClassList.Contains("inset-0") && d.ClassList.Contains("flex"));
        Assert.Contains("pointer-events-none", wrapper.ClassList);

        var backdrop = cut.FindAll("div").First(d => d.ClassList.Contains("animate-fade-in"));
        Assert.Contains("pointer-events-auto", backdrop.ClassList);

        var panel = cut.Find("[role='dialog']");
        Assert.Contains("pointer-events-auto", panel.ClassList);
    }

    [Fact]
    public void Sheet_Drawer_AlertDialog_Shells_Are_PointerEvents_Scoped()
    {
        var sheet = RenderSheet();
        Assert.Contains("pointer-events-none", sheet.FindAll("div").First(d => d.ClassList.Contains("inset-0") && !d.ClassList.Contains("animate-fade-in")).ClassList);
        Assert.Contains("pointer-events-auto", sheet.Find("[role='dialog']").ClassList);

        var drawer = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Drawer body")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
        Assert.Contains("pointer-events-none", drawer.FindAll("div").First(d => d.ClassList.Contains("inset-0") && !d.ClassList.Contains("animate-fade-in")).ClassList);
        Assert.Contains("pointer-events-auto", drawer.Find("[role='dialog']").ClassList);
    }

    // ---- B3: horizontal-overflow clipping -----------------------------------------------

    [Fact]
    public void Scrollable_Dialog_Body_Clips_Horizontal_Overflow()
    {
        var cut = RenderDialog(scrollable: true);
        var body = cut.FindAll("div").First(d => d.ClassList.Contains("max-h-[85vh]"));
        Assert.Contains("overflow-x-hidden", body.ClassList);
    }

    private sealed class DummyModel { public string? Name { get; set; } }

    [Fact]
    public void OverlayForm_Gutter_Body_Clips_Horizontal_Overflow()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.OverlayForm>(0);
            builder.AddAttribute(1, "Model", new DummyModel());
            builder.AddAttribute(2, "Body", (RenderFragment)(b => b.AddContent(0, "form body")));
            builder.CloseComponent();
        });
        var body = cut.FindAll("div").First(d => d.ClassList.Contains("-mx-1"));
        Assert.Contains("overflow-x-clip", body.ClassList);
    }

    [Fact]
    public void ScrollArea_FocusRingGutter_Clips_Horizontal_Overflow()
    {
        var cut = _ctx.Render<L.ScrollArea>(p =>
        {
            p.Add(x => x.FocusRingGutter, true);
            p.AddChildContent("content");
        });
        var root = cut.FindAll("div").First(d => d.ClassList.Contains("-mx-1"));
        Assert.Contains("overflow-x-clip", root.ClassList);
    }
}
