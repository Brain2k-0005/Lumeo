using Bunit;
using Xunit;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Services;

public class ComponentInteropServiceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private ComponentInteropService _service = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        _service = _ctx.Services.GetRequiredService<ComponentInteropService>();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- RegisterClickOutside / UnregisterClickOutside ---

    [Fact]
    public async Task RegisterClickOutside_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.RegisterClickOutside("elem-1", null, () => Task.CompletedTask).AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task RegisterClickOutside_With_Trigger_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.RegisterClickOutside("elem-2", "trigger-2", () => Task.CompletedTask).AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task UnregisterClickOutside_After_Register_Does_Not_Throw()
    {
        await _service.RegisterClickOutside("elem-3", null, () => Task.CompletedTask);

        var exception = await Record.ExceptionAsync(() =>
            _service.UnregisterClickOutside("elem-3").AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task UnregisterClickOutside_Without_Prior_Register_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.UnregisterClickOutside("never-registered").AsTask());

        Assert.Null(exception);
    }

    // --- OnClickOutside ---

    [Fact]
    public async Task OnClickOutside_Calls_Registered_Handler()
    {
        var called = false;
        await _service.RegisterClickOutside("elem-click", null, () =>
        {
            called = true;
            return Task.CompletedTask;
        });

        await _service.OnClickOutside("elem-click");

        Assert.True(called);
    }

    [Fact]
    public async Task OnClickOutside_Unknown_ElementId_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.OnClickOutside("unknown-element"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task OnClickOutside_After_Unregister_Does_Not_Call_Handler()
    {
        var called = false;
        await _service.RegisterClickOutside("elem-unreg", null, () =>
        {
            called = true;
            return Task.CompletedTask;
        });
        await _service.UnregisterClickOutside("elem-unreg");

        await _service.OnClickOutside("elem-unreg");

        Assert.False(called);
    }

    // --- LockScroll / UnlockScroll ---

    [Fact]
    public async Task LockScroll_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.LockScroll().AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task UnlockScroll_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.UnlockScroll().AsTask());

        Assert.Null(exception);
    }

    // --- SetupFocusTrap / RemoveFocusTrap ---

    [Fact]
    public async Task SetupFocusTrap_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.SetupFocusTrap("focus-elem").AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task RemoveFocusTrap_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.RemoveFocusTrap("focus-elem").AsTask());

        Assert.Null(exception);
    }

    // --- FocusElement ---

    [Fact]
    public async Task FocusElement_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.FocusElement("some-element").AsTask());

        Assert.Null(exception);
    }

    // --- PositionFixed ---

    [Fact]
    public async Task PositionFixed_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.PositionFixed("content-id", "ref-id", "start", false).AsTask());

        Assert.Null(exception);
    }

    // --- DrawerSwipe ---

    [Fact]
    public async Task RegisterDrawerSwipe_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.RegisterDrawerSwipe("drawer-elem", () => Task.CompletedTask).AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task UnregisterDrawerSwipe_Does_Not_Throw()
    {
        await _service.RegisterDrawerSwipe("drawer-elem-2", () => Task.CompletedTask);

        var exception = await Record.ExceptionAsync(() =>
            _service.UnregisterDrawerSwipe("drawer-elem-2").AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task OnSwipeDismiss_Calls_Only_Matching_Handler()
    {
        int handler1Count = 0;
        int handler2Count = 0;
        await _service.RegisterDrawerSwipe("d1", () => { handler1Count++; return Task.CompletedTask; });
        await _service.RegisterDrawerSwipe("d2", () => { handler2Count++; return Task.CompletedTask; });

        await _service.OnSwipeDismiss("d1");

        Assert.Equal(1, handler1Count);
        Assert.Equal(0, handler2Count);
    }

    // --- CarouselSwipe ---

    [Fact]
    public async Task RegisterCarouselSwipe_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.RegisterCarouselSwipe(
                "carousel-1",
                "horizontal",
                _ => Task.CompletedTask,
                (_, _) => Task.CompletedTask).AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task UnregisterCarouselSwipe_Does_Not_Throw()
    {
        await _service.RegisterCarouselSwipe(
            "carousel-2",
            "horizontal",
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask);

        var exception = await Record.ExceptionAsync(() =>
            _service.UnregisterCarouselSwipe("carousel-2").AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task OnSwipe_Calls_Only_Matching_Handler()
    {
        var directions = new List<string>();
        await _service.RegisterCarouselSwipe(
            "c1",
            "horizontal",
            dir => { directions.Add(dir); return Task.CompletedTask; },
            (_, _) => Task.CompletedTask);
        await _service.RegisterCarouselSwipe(
            "c2",
            "horizontal",
            dir => Task.CompletedTask,
            (_, _) => Task.CompletedTask);

        await _service.OnSwipe("c1", "left");

        Assert.Single(directions);
        Assert.Equal("left", directions[0]);
    }

    [Fact]
    public async Task OnScrollPosition_Calls_Only_Matching_Handler()
    {
        double receivedPos = -1;
        double receivedMax = -1;

        await _service.RegisterCarouselSwipe(
            "c2",
            "horizontal",
            _ => Task.CompletedTask,
            (pos, max) => { receivedPos = pos; receivedMax = max; return Task.CompletedTask; });
        await _service.RegisterCarouselSwipe(
            "c3",
            "horizontal",
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask);

        await _service.OnScrollPosition("c2", 100.0, 500.0);

        Assert.Equal(100.0, receivedPos);
        Assert.Equal(500.0, receivedMax);
    }

    // --- ResizeHandle ---

    [Fact]
    public async Task RegisterResizeHandle_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.RegisterResizeHandle(
                "handle-1",
                "horizontal",
                _ => Task.CompletedTask,
                () => Task.CompletedTask).AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task UnregisterResizeHandle_Does_Not_Throw()
    {
        await _service.RegisterResizeHandle(
            "handle-2",
            "horizontal",
            _ => Task.CompletedTask,
            () => Task.CompletedTask);

        var exception = await Record.ExceptionAsync(() =>
            _service.UnregisterResizeHandle("handle-2").AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task OnResize_Calls_Only_Matching_Handler()
    {
        double receivedDelta = 0;
        bool otherCalled = false;
        await _service.RegisterResizeHandle(
            "h1",
            "horizontal",
            delta => { receivedDelta = delta; return Task.CompletedTask; },
            () => Task.CompletedTask);
        await _service.RegisterResizeHandle(
            "h3",
            "horizontal",
            _ => { otherCalled = true; return Task.CompletedTask; },
            () => Task.CompletedTask);

        await _service.OnResize("h1", 42.5);

        Assert.Equal(42.5, receivedDelta);
        Assert.False(otherCalled);
    }

    [Fact]
    public async Task OnResizeEnd_Calls_Only_Matching_Handler()
    {
        var called = false;
        bool otherCalled = false;
        await _service.RegisterResizeHandle(
            "h2",
            "vertical",
            _ => Task.CompletedTask,
            () => { called = true; return Task.CompletedTask; });
        await _service.RegisterResizeHandle(
            "h4",
            "vertical",
            _ => Task.CompletedTask,
            () => { otherCalled = true; return Task.CompletedTask; });

        await _service.OnResizeEnd("h2");

        Assert.True(called);
        Assert.False(otherCalled);
    }

    // --- Scrollspy ---

    [Fact]
    public async Task RegisterScrollspy_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.RegisterScrollspy("container-1", 0, true, _ => Task.CompletedTask).AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task UnregisterScrollspy_Does_Not_Throw()
    {
        await _service.RegisterScrollspy("container-2", 0, true, _ => Task.CompletedTask);

        var exception = await Record.ExceptionAsync(() =>
            _service.UnregisterScrollspy("container-2").AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task OnScrollspyUpdate_Calls_Registered_Handler()
    {
        string? receivedId = null;
        await _service.RegisterScrollspy("spy-1", 0, true, id =>
        {
            receivedId = id;
            return Task.CompletedTask;
        });

        await _service.OnScrollspyUpdate("spy-1", "section-2");

        Assert.Equal("section-2", receivedId);
    }

    [Fact]
    public async Task OnScrollspyUpdate_Unknown_Container_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.OnScrollspyUpdate("unknown-container", "section-1"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ScrollspyScrollTo_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.ScrollspyScrollTo("container-3", "section-1", true).AsTask());

        Assert.Null(exception);
    }

    // --- ElementRect record ---

    [Fact]
    public void ElementRect_Record_Properties_Are_Correct()
    {
        var rect = new ComponentInteropService.ElementRect(10, 20, 300, 150);

        Assert.Equal(10, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(300, rect.Width);
        Assert.Equal(150, rect.Height);
    }

    // --- DisposeAsync ---

    [Fact]
    public async Task DisposeAsync_Does_Not_Throw()
    {
        await _service.RegisterClickOutside("disp-elem", null, () => Task.CompletedTask);

        var exception = await Record.ExceptionAsync(() =>
            _service.DisposeAsync().AsTask());

        Assert.Null(exception);
    }

    // --- ToastSwipe key fix ---

    [Fact]
    public async Task OnToastSwipeDismiss_Calls_Handler_By_ToastId()
    {
        string? receivedId = null;
        await _service.RegisterToastSwipe("element-1", "toast-1", (id) => { receivedId = id; return Task.CompletedTask; });

        await _service.OnToastSwipeDismiss("toast-1");

        Assert.Equal("toast-1", receivedId);
    }

    // --- BackToTop instance-specific ---

    [Fact]
    public async Task OnScrollVisibilityChanged_Calls_Only_Matching_Handler()
    {
        bool? handler1Visible = null;
        bool? handler2Visible = null;
        await _service.RegisterBackToTop("bt1", 100, (v) => { handler1Visible = v; return Task.CompletedTask; });
        await _service.RegisterBackToTop("bt2", 200, (v) => { handler2Visible = v; return Task.CompletedTask; });

        await _service.OnScrollVisibilityChanged("bt1", true);

        Assert.True(handler1Visible);
        Assert.Null(handler2Visible);
    }

    // --- Toast Swipe Unregister ---

    [Fact]
    public async Task UnregisterToastSwipe_Removes_Handler_By_ToastId()
    {
        string? receivedId = null;
        await _service.RegisterToastSwipe("element-1", "toast-1", (id) => { receivedId = id; return Task.CompletedTask; });

        await _service.UnregisterToastSwipe("toast-1", "element-1");
        await _service.OnToastSwipeDismiss("toast-1");

        Assert.Null(receivedId); // Handler should have been removed
    }

    [Fact]
    public async Task UnregisterToastSwipe_Does_Not_Throw_For_Unknown_Id()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.UnregisterToastSwipe("unknown-toast", "unknown-element").AsTask());

        Assert.Null(exception);
    }

    // --- UnpositionFixed ---

    [Fact]
    public async Task UnpositionFixed_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.UnpositionFixed("content-id").AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task PositionFixed_Then_UnpositionFixed_Does_Not_Throw()
    {
        await _service.PositionFixed("content-id", "ref-id", "start", false);

        var exception = await Record.ExceptionAsync(() =>
            _service.UnpositionFixed("content-id").AsTask());

        Assert.Null(exception);
    }

    // --- ColumnResize ---

    [Fact]
    public async Task OnColumnResize_Calls_Only_Matching_Handler()
    {
        double receivedDelta = 0;
        bool otherCalled = false;
        await _service.RegisterColumnResize("col-1",
            delta => { receivedDelta = delta; return Task.CompletedTask; },
            () => Task.CompletedTask);
        await _service.RegisterColumnResize("col-2",
            _ => { otherCalled = true; return Task.CompletedTask; },
            () => Task.CompletedTask);

        await _service.OnColumnResize("col-1", 15.0);

        Assert.Equal(15.0, receivedDelta);
        Assert.False(otherCalled);
    }

    [Fact]
    public async Task OnColumnResizeEnd_Calls_Only_Matching_Handler()
    {
        bool called = false;
        bool otherCalled = false;
        await _service.RegisterColumnResize("col-3",
            _ => Task.CompletedTask,
            () => { called = true; return Task.CompletedTask; });
        await _service.RegisterColumnResize("col-4",
            _ => Task.CompletedTask,
            () => { otherCalled = true; return Task.CompletedTask; });

        await _service.OnColumnResizeEnd("col-3");

        Assert.True(called);
        Assert.False(otherCalled);
    }

    // --- OTP Paste ---

    [Fact]
    public async Task OnOtpPaste_Calls_Only_Matching_Handler()
    {
        string? received = null;
        bool otherCalled = false;
        await _service.RegisterOtpPaste("otp-1", 6,
            digits => { received = digits; return Task.CompletedTask; });
        await _service.RegisterOtpPaste("otp-2", 4,
            _ => { otherCalled = true; return Task.CompletedTask; });

        await _service.OnOtpPaste("otp-1", "123456");

        Assert.Equal("123456", received);
        Assert.False(otherCalled);
    }
}
