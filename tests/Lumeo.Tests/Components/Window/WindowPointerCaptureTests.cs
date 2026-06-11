using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo.Tests.Components.Window;

/// <summary>
/// Regression tests for pointer capture on Window drag/resize: pointermove/up
/// were bound to the title bar / resize handle WITHOUT pointer capture, so a
/// fast drag left the element, moves stopped firing, and a pointerup outside
/// was lost — _isDragging stayed true and the window glued itself to the
/// cursor on the next hover. The handlers now capture the pointer on
/// pointerdown and release it on pointerup (same pattern as SplitterDivider).
/// bUnit can't drive real pointer capture, so these tests assert the interop
/// contract via the tracking fake plus the drag/resize math.
/// </summary>
public class WindowPointerCaptureTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public WindowPointerCaptureTests()
    {
        _ctx.AddLumeoServices();
        // Last interface registration wins, so Window resolves the tracking fake.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<Lumeo.Window> RenderWindow() =>
        _ctx.Render<Lumeo.Window>(p => p
            .Add(w => w.Open, true)
            .Add(w => w.Title, "Test")
            .AddChildContent("body"));

    [Fact]
    public void TitleBar_PointerDown_Captures_The_Pointer_On_The_TitleBar()
    {
        var cut = RenderWindow();
        var titleBar = cut.Find("[id^='window-titlebar']");

        titleBar.PointerDown(new PointerEventArgs { PointerId = 7, ClientX = 100, ClientY = 100 });

        var call = Assert.Single(_interop.PointerCaptureCalls);
        Assert.Equal(titleBar.GetAttribute("id"), call.ElementId);
        Assert.Equal(7, call.PointerId);
    }

    [Fact]
    public void TitleBar_PointerUp_Releases_The_Captured_Pointer()
    {
        var cut = RenderWindow();
        var titleBarId = cut.Find("[id^='window-titlebar']").GetAttribute("id");

        cut.Find("[id^='window-titlebar']").PointerDown(new PointerEventArgs { PointerId = 7, ClientX = 100, ClientY = 100 });
        cut.Find("[id^='window-titlebar']").PointerUp(new PointerEventArgs { PointerId = 7 });

        var call = Assert.Single(_interop.PointerReleaseCalls);
        Assert.Equal(titleBarId, call.ElementId);
        Assert.Equal(7, call.PointerId);
    }

    [Fact]
    public void Resize_Handle_Has_A_Stable_Id_And_Captures_On_PointerDown()
    {
        var cut = RenderWindow();
        var handle = cut.Find(".cursor-se-resize");
        var handleId = handle.GetAttribute("id");

        Assert.NotNull(handleId);
        Assert.StartsWith("window-resize-", handleId);

        handle.PointerDown(new PointerEventArgs { PointerId = 3, ClientX = 10, ClientY = 10 });

        var call = Assert.Single(_interop.PointerCaptureCalls);
        Assert.Equal(handleId, call.ElementId);
        Assert.Equal(3, call.PointerId);
    }

    [Fact]
    public void Resize_Handle_PointerUp_Releases_The_Captured_Pointer()
    {
        var cut = RenderWindow();
        var handleId = cut.Find(".cursor-se-resize").GetAttribute("id");

        cut.Find(".cursor-se-resize").PointerDown(new PointerEventArgs { PointerId = 3, ClientX = 10, ClientY = 10 });
        cut.Find(".cursor-se-resize").PointerUp(new PointerEventArgs { PointerId = 3 });

        var call = Assert.Single(_interop.PointerReleaseCalls);
        Assert.Equal(handleId, call.ElementId);
        Assert.Equal(3, call.PointerId);
    }

    [Fact]
    public void PointerDown_On_TitleBar_Buttons_Does_Not_Start_A_Drag_Or_Capture()
    {
        // The buttons wrapper stops pointerdown propagation: capturing the
        // pointer on the title bar while pressing a button would retarget the
        // subsequent click away from the button (close/minimize would break).
        // bUnit honors the :stopPropagation flag while bubbling, so the title
        // bar handler is unreachable from the button and bUnit reports that no
        // pointerdown handler was hit.
        var cut = RenderWindow();

        Assert.Throws<MissingEventHandlerException>(() =>
            cut.Find("button[aria-label='Close']").PointerDown(new PointerEventArgs { PointerId = 2 }));

        Assert.Empty(_interop.PointerCaptureCalls);
    }

    [Fact]
    public void TitleBar_Drag_Moves_The_Window()
    {
        var cut = RenderWindow();

        cut.Find("[id^='window-titlebar']").PointerDown(new PointerEventArgs { PointerId = 1, ClientX = 100, ClientY = 100 });
        cut.Find("[id^='window-titlebar']").PointerMove(new PointerEventArgs { PointerId = 1, ClientX = 150, ClientY = 130 });

        // Initial position is 80/80; viewport is unknown in tests so the
        // lower-bound-only fallback applies: 80+50=130, 80+30=110.
        var style = cut.Find("[role='dialog']").GetAttribute("style") ?? "";
        Assert.Contains("left:130px", style);
        Assert.Contains("top:110px", style);
    }

    [Fact]
    public void Resize_Handle_Drag_Resizes_The_Window()
    {
        var cut = RenderWindow();

        cut.Find(".cursor-se-resize").PointerDown(new PointerEventArgs { PointerId = 1, ClientX = 10, ClientY = 10 });
        cut.Find(".cursor-se-resize").PointerMove(new PointerEventArgs { PointerId = 1, ClientX = 60, ClientY = 50 });

        // Default size is 480x360; +50/+40 => 530x400.
        var style = cut.Find("[role='dialog']").GetAttribute("style") ?? "";
        Assert.Contains("width:530px", style);
        Assert.Contains("height:400px", style);
    }
}
