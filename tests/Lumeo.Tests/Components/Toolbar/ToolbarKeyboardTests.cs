using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toolbar;

/// <summary>
/// Regression tests for #235 — Toolbar had no roving tabindex / arrow-key
/// navigation (Radix Toolbar's defining keyboard behaviour) and no Orientation.
/// </summary>
public class ToolbarKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ToolbarKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Toolbar> RenderToolbar(L.Toolbar.ToolbarOrientation orientation = L.Toolbar.ToolbarOrientation.Horizontal)
        => _ctx.Render<L.Toolbar>(p => p
            .Add(t => t.Orientation, orientation)
            .AddChildContent("<button>A</button><button>B</button>"));

    [Fact]
    public void Toolbar_Is_A_Single_Tab_Stop()
    {
        var cut = RenderToolbar();
        Assert.Equal("0", cut.Find("[role='toolbar']").GetAttribute("tabindex"));
    }

    [Fact]
    public void Initialises_Roving_On_First_Render()
    {
        var cut = RenderToolbar();
        cut.WaitForAssertion(() => Assert.NotEmpty(_interop.InitToolbarRovingCalls));
    }

    [Fact]
    public void Horizontal_ArrowRight_Moves_Focus_Forward()
    {
        var cut = RenderToolbar();
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Contains(_interop.MoveToolbarFocusCalls, c => c.Delta == 1);
    }

    [Fact]
    public void Horizontal_ArrowLeft_Moves_Focus_Backward()
    {
        var cut = RenderToolbar();
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Contains(_interop.MoveToolbarFocusCalls, c => c.Delta == -1);
    }

    [Fact]
    public void Horizontal_Vertical_Arrows_Do_Not_Move_Focus()
    {
        var cut = RenderToolbar();
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Empty(_interop.MoveToolbarFocusCalls);
    }

    [Fact]
    public void Vertical_ArrowDown_Moves_Focus_Forward()
    {
        var cut = RenderToolbar(L.Toolbar.ToolbarOrientation.Vertical);
        Assert.Equal("vertical", cut.Find("[role='toolbar']").GetAttribute("aria-orientation"));

        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Contains(_interop.MoveToolbarFocusCalls, c => c.Delta == 1);
    }

    [Fact]
    public void Home_And_End_Jump_To_Edges()
    {
        var cut = RenderToolbar();
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "Home" });
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "End" });

        Assert.Contains(_interop.FocusToolbarEdgeCalls, c => !c.Last);
        Assert.Contains(_interop.FocusToolbarEdgeCalls, c => c.Last);
    }

    [Fact]
    public void Default_Orientation_Is_Horizontal()
    {
        var cut = RenderToolbar();
        Assert.Equal("horizontal", cut.Find("[role='toolbar']").GetAttribute("aria-orientation"));
    }
}
