using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.Dock;

/// <summary>
/// Regression tests for the Dock role/behavior mismatch flagged by the a11y gap
/// scan: Dock.razor's root carries <c>role="toolbar"</c> (the WAI-ARIA toolbar
/// pattern requires roving-tabindex Arrow-key navigation between items) but had
/// no @onkeydown/roving-tabindex logic of its own — item magnification is pure
/// mouse-follow via the MotionDock JS interop, so every item kept its own plain
/// Tab stop instead of the toolbar's single-tab-stop model. Fixed by reusing the
/// SAME APG toolbar interop Toolbar.razor already uses (#235):
/// InitToolbarRoving/MoveToolbarFocus/FocusToolbarEdge — which required switching
/// Dock's injected interop from the concrete ComponentInteropService to the
/// IComponentInteropService interface so tests can substitute
/// TrackingInteropService exactly like ToolbarKeyboardTests does.
/// </summary>
public class DockKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DockKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<Lumeo.Dock> RenderDock() =>
        _ctx.Render<Lumeo.Dock>(p => p
            .AddChildContent("<button>App One</button><button>App Two</button><button>App Three</button>"));

    [Fact]
    public void Dock_Root_Is_A_Single_Tab_Stop()
    {
        var cut = RenderDock();
        Assert.Equal("0", cut.Find("[role='toolbar']").GetAttribute("tabindex"));
    }

    [Fact]
    public void Initialises_Roving_On_Render()
    {
        // Temp-revert proof: commenting out the Interop.InitToolbarRoving call
        // added to Dock.razor's OnAfterRenderAsync makes this list stay empty —
        // this is not a tautological "does the mock always report a call".
        var cut = RenderDock();
        cut.WaitForAssertion(() => Assert.NotEmpty(_interop.InitToolbarRovingCalls));
    }

    [Fact]
    public void ArrowRight_Moves_Focus_Forward_Between_Dock_Items()
    {
        // Temp-revert proof: without the HandleKeyDown switch case, ArrowRight
        // falls straight through with no interop call and this assertion fails.
        var cut = RenderDock();
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Contains(_interop.MoveToolbarFocusCalls, c => c.Delta == 1);
    }

    [Fact]
    public void ArrowLeft_Moves_Focus_Backward_Between_Dock_Items()
    {
        var cut = RenderDock();
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Contains(_interop.MoveToolbarFocusCalls, c => c.Delta == -1);
    }

    [Fact]
    public void Home_And_End_Jump_To_First_And_Last_Dock_Item()
    {
        var cut = RenderDock();
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "Home" });
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "End" });

        Assert.Contains(_interop.FocusToolbarEdgeCalls, c => !c.Last);
        Assert.Contains(_interop.FocusToolbarEdgeCalls, c => c.Last);
    }

    [Fact]
    public void Unrelated_Key_Does_Not_Move_Focus()
    {
        var cut = RenderDock();
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "a" });
        Assert.Empty(_interop.MoveToolbarFocusCalls);
        Assert.Empty(_interop.FocusToolbarEdgeCalls);
    }
}
