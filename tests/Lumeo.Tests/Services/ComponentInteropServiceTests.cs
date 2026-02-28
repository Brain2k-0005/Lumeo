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
    public async Task OnSwipeDismiss_Calls_All_Drawer_Handlers()
    {
        int callCount = 0;
        await _service.RegisterDrawerSwipe("d1", () => { callCount++; return Task.CompletedTask; });
        await _service.RegisterDrawerSwipe("d2", () => { callCount++; return Task.CompletedTask; });

        await _service.OnSwipeDismiss();

        Assert.Equal(2, callCount);
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
    public async Task OnSwipe_Calls_All_Carousel_Handlers()
    {
        var directions = new List<string>();
        await _service.RegisterCarouselSwipe(
            "c1",
            "horizontal",
            dir => { directions.Add(dir); return Task.CompletedTask; },
            (_, _) => Task.CompletedTask);

        await _service.OnSwipe("left");

        Assert.Single(directions);
        Assert.Equal("left", directions[0]);
    }

    [Fact]
    public async Task OnScrollPosition_Calls_All_Carousel_Scroll_Handlers()
    {
        double receivedPos = -1;
        double receivedMax = -1;

        await _service.RegisterCarouselSwipe(
            "c2",
            "horizontal",
            _ => Task.CompletedTask,
            (pos, max) => { receivedPos = pos; receivedMax = max; return Task.CompletedTask; });

        await _service.OnScrollPosition(100.0, 500.0);

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
    public async Task OnResize_Calls_All_Resize_Handlers()
    {
        double receivedDelta = 0;
        await _service.RegisterResizeHandle(
            "h1",
            "horizontal",
            delta => { receivedDelta = delta; return Task.CompletedTask; },
            () => Task.CompletedTask);

        await _service.OnResize(42.5);

        Assert.Equal(42.5, receivedDelta);
    }

    [Fact]
    public async Task OnResizeEnd_Calls_All_ResizeEnd_Handlers()
    {
        var called = false;
        await _service.RegisterResizeHandle(
            "h2",
            "vertical",
            _ => Task.CompletedTask,
            () => { called = true; return Task.CompletedTask; });

        await _service.OnResizeEnd();

        Assert.True(called);
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
}
