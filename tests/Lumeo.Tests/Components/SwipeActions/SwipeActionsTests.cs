using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.SwipeActions;

/// <summary>
/// #309 regressions:
///  - the foreground must capture the pointer once a horizontal drag commits
///    (so the drag doesn't stall when the pointer drifts off the element);
///  - the synthesized click that fires after a mouse drag must be swallowed
///    (otherwise a desktop swipe-open snaps shut immediately);
///  - a keyboard-only user must be able to reveal the actions;
///  - the container must advertise itself to assistive tech.
/// </summary>
public class SwipeActionsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public SwipeActionsTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment Trailing => b => b.AddMarkupContent(0, "<button type=\"button\">Delete</button>");

    private IRenderedComponent<L.SwipeActions> RenderWithTrailing() =>
        _ctx.Render<L.SwipeActions>(p => p
            .Add(s => s.TrailingActions, Trailing)
            .AddChildContent("<div>Row content</div>"));

    [Fact]
    public void Container_Has_Group_Role_And_Label()
    {
        var cut = RenderWithTrailing();

        var group = cut.Find("[role='group']");
        Assert.False(string.IsNullOrEmpty(group.GetAttribute("aria-label")));
    }

    [Fact]
    public void Horizontal_Drag_Captures_The_Pointer()
    {
        var cut = RenderWithTrailing();
        // The foreground is the focusable (tabindex=0) layer.
        var fg = cut.Find("[tabindex='0']");

        fg.PointerDown(new PointerEventArgs { PointerId = 7, ClientX = 200, ClientY = 100 });
        // Move clearly horizontally (past the 6px dead zone, dx dominates dy).
        fg.PointerMove(new PointerEventArgs { PointerId = 7, ClientX = 150, ClientY = 102 });

        var capture = Assert.Single(_interop.PointerCaptureCalls);
        Assert.Equal(7, capture.PointerId);

        // Pointer up releases the capture.
        fg.PointerUp(new PointerEventArgs { PointerId = 7, ClientX = 150, ClientY = 102 });
        var release = Assert.Single(_interop.PointerReleaseCalls);
        Assert.Equal(7, release.PointerId);
    }

    [Fact]
    public void Vertical_Gesture_Does_Not_Capture_The_Pointer()
    {
        var cut = RenderWithTrailing();
        var fg = cut.Find("[tabindex='0']");

        fg.PointerDown(new PointerEventArgs { PointerId = 1, ClientX = 200, ClientY = 100 });
        // Mostly-vertical move: the component must bail out and not capture.
        fg.PointerMove(new PointerEventArgs { PointerId = 1, ClientX = 202, ClientY = 160 });

        Assert.Empty(_interop.PointerCaptureCalls);
    }

    [Fact]
    public void Click_After_Drag_Open_Does_Not_Immediately_Close()
    {
        var cut = RenderWithTrailing();
        var fg = cut.Find("[tabindex='0']");

        // Drag left far enough to lock the trailing panel open.
        fg.PointerDown(new PointerEventArgs { PointerId = 3, ClientX = 300, ClientY = 100 });
        fg.PointerMove(new PointerEventArgs { PointerId = 3, ClientX = 180, ClientY = 100 }); // dx = -120
        fg.PointerUp(new PointerEventArgs { PointerId = 3, ClientX = 180, ClientY = 100 });

        // Panel is now open (foreground translated aside).
        Assert.Contains("translateX(-160px)", cut.Find("[tabindex='0']").GetAttribute("style"));

        // The browser fires a click right after the mouse drag — it must be
        // swallowed, leaving the panel open.
        cut.Find("[tabindex='0']").Click();

        Assert.Contains("translateX(-160px)", cut.Find("[tabindex='0']").GetAttribute("style"));

        // A subsequent genuine click then closes it.
        cut.Find("[tabindex='0']").Click();
        Assert.Contains("translateX(0px)", cut.Find("[tabindex='0']").GetAttribute("style"));
    }

    [Fact]
    public void ArrowLeft_Reveals_Trailing_Actions_For_Keyboard_Users()
    {
        var cut = RenderWithTrailing();
        var fg = cut.Find("[tabindex='0']");

        fg.KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Contains("translateX(-160px)", cut.Find("[tabindex='0']").GetAttribute("style"));

        // Escape closes again.
        cut.Find("[tabindex='0']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Contains("translateX(0px)", cut.Find("[tabindex='0']").GetAttribute("style"));
    }
}
