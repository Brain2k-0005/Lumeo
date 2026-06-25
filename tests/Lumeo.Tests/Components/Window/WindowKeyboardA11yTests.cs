using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Window;

/// <summary>
/// Battle-wave2 keyboard / a11y regressions for Window:
///
/// #95 — The window grabbed no focus on open and never restored it on close, so
///       keyboard / screen-reader users were never moved into the window and
///       focus was orphaned after Escape. On first show the component now calls
///       Interop.SaveFocus + Interop.FocusElement(windowId) (and RestoreFocus on
///       Close). bUnit cannot move real DOM focus, so the observable mechanism is
///       the Interop.FocusElement call recorded by the tracking fake.
///
/// #186 — The minimized window dropped role=dialog, tabindex and the Escape
///        keydown handler, so it could not be closed via keyboard and lost its
///        dialog semantics for assistive tech. The minimized container now carries
///        role=dialog / aria-modal / aria-labelledby / tabindex=-1 and wires
///        HandleKeyDown so Escape still closes it.
/// </summary>
public class WindowKeyboardA11yTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public WindowKeyboardA11yTests()
    {
        _ctx.AddLumeoServices();
        // Last interface registration wins, so Window resolves the tracking fake.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Window> RenderWindow(bool open) =>
        _ctx.Render<L.Window>(p => p
            .Add(w => w.Open, open)
            .Add(w => w.Title, "Test")
            .AddChildContent("body"));

    // ---- #95: focus moves into the window on open ----

    [Fact]
    public void Open_Moves_Focus_To_The_Window_Root()
    {
        var cut = RenderWindow(open: true);

        // The dialog root's own id is the focus target the component moves to.
        var windowId = cut.Find("[role='dialog']").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(windowId));

        // Without the fix nothing focuses the window root on open; with it the
        // component calls Interop.FocusElement(windowId) from OnAfterRenderAsync.
        Assert.Contains(windowId!, _interop.FocusElementCalls);
    }

    // ---- #186: minimized window keeps dialog semantics + Escape-to-close ----

    [Fact]
    public void Minimized_Window_Keeps_Dialog_Role_And_Labelling()
    {
        var cut = RenderWindow(open: true);

        cut.Find("button[aria-label='Minimize']").Click();

        // Once minimized the only rendered branch is the minimized title bar; it
        // must still expose dialog semantics so assistive tech can find and
        // announce it (it previously dropped role/tabindex/aria-labelledby).
        Assert.NotEmpty(cut.FindAll("button[aria-label='Restore']"));
        var minimized = cut.Find("[role='dialog']");
        Assert.Equal("-1", minimized.GetAttribute("tabindex"));

        var labelledBy = minimized.GetAttribute("aria-labelledby");
        Assert.False(string.IsNullOrEmpty(labelledBy));
        // The aria-labelledby IDREF must resolve to the title element.
        Assert.NotNull(cut.Find($"#{labelledBy}"));
    }

    [Fact]
    public void Escape_Closes_A_Minimized_Window()
    {
        var cut = RenderWindow(open: true);

        // Minimize, then press Escape on the minimized container.
        cut.Find("button[aria-label='Minimize']").Click();
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // Without the keydown handler the minimized window had no keyboard close
        // path; with the fix Escape routes through HandleKeyDown -> Close().
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("button[aria-label='Restore']")));
        Assert.Empty(cut.FindAll("[role='dialog']"));
    }
}
