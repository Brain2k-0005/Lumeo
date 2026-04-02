using Bunit;
using Xunit;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo.Tests.Services;

/// <summary>
/// Targeted interop verification tests that check ComponentInteropService
/// calls the correct JS functions with the correct arguments.
///
/// Unlike the main ComponentInteropServiceTests (which only verify no-throw),
/// these tests use VerifyInvoke to assert exact JS function names and argument
/// values. A renamed or mis-argued JS call will cause these tests to fail.
///
/// Note on bUnit module interop: SetupVoid/Setup on BunitJSModuleInterop causes
/// TaskCompletionSource tasks that never complete (a bUnit 2.x limitation with
/// module-scoped handlers). The correct pattern is loose mode + VerifyInvoke
/// post-call, which gives the same "strict" verification via assertions.
/// </summary>
public class StrictInteropTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;
    private ComponentInteropService _service = null!;

    public Task InitializeAsync()
    {
        // Module interop requires loose mode — strict verification is done via VerifyInvoke
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _module = _ctx.JSInterop.SetupModule("./_content/Lumeo/js/components.js");
        _module.Mode = JSRuntimeMode.Loose;

        _ctx.Services.AddScoped<ComponentInteropService>();
        _service = _ctx.Services.GetRequiredService<ComponentInteropService>();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Dispose the service synchronously before the context to avoid
        // the service's DisposeAsync attempting JS calls on a disposed context.
        _service.Dispose();
        await _ctx.DisposeAsync();
    }

    // --- PositionFixed ---

    [Fact]
    public async Task PositionFixed_Invokes_Correct_JS_Function_With_All_Args()
    {
        await _service.PositionFixed("content-1", "ref-1", "center", true, "bottom");

        var invocation = _module.VerifyInvoke("positionFixed");
        Assert.Equal("content-1", invocation.Arguments[0]);
        Assert.Equal("ref-1", invocation.Arguments[1]);
        Assert.Equal("center", invocation.Arguments[2]);
        Assert.Equal(true, invocation.Arguments[3]);
        Assert.Equal("bottom", invocation.Arguments[4]);
    }

    [Fact]
    public async Task PositionFixed_Default_Args_Are_Start_False_Bottom()
    {
        await _service.PositionFixed("content-1", "ref-1");

        var invocation = _module.VerifyInvoke("positionFixed");
        Assert.Equal("start", invocation.Arguments[2]);
        Assert.Equal(false, invocation.Arguments[3]);
        Assert.Equal("bottom", invocation.Arguments[4]);
    }

    // --- UnpositionFixed ---

    [Fact]
    public async Task UnpositionFixed_Invokes_Correct_JS_Function()
    {
        await _service.UnpositionFixed("content-1");

        var invocation = _module.VerifyInvoke("unpositionFixed");
        Assert.Equal("content-1", invocation.Arguments[0]);
    }

    // --- ClickOutside ---

    [Fact]
    public async Task RegisterClickOutside_Invokes_JS_With_ElementId_And_TriggerElementId()
    {
        await _service.RegisterClickOutside("elem-1", "trigger-1", () => Task.CompletedTask);

        var invocation = _module.VerifyInvoke("registerClickOutside");
        Assert.Equal("elem-1", invocation.Arguments[0]);
        Assert.Equal("trigger-1", invocation.Arguments[1]);
    }

    [Fact]
    public async Task RegisterClickOutside_Without_Trigger_Passes_Null_As_Second_Arg()
    {
        await _service.RegisterClickOutside("elem-2", null, () => Task.CompletedTask);

        var invocation = _module.VerifyInvoke("registerClickOutside");
        Assert.Equal("elem-2", invocation.Arguments[0]);
        Assert.Null(invocation.Arguments[1]);
    }

    [Fact]
    public async Task UnregisterClickOutside_Invokes_JS_With_ElementId()
    {
        await _service.RegisterClickOutside("elem-1", null, () => Task.CompletedTask);
        await _service.UnregisterClickOutside("elem-1");

        var invocation = _module.VerifyInvoke("unregisterClickOutside");
        Assert.Equal("elem-1", invocation.Arguments[0]);
    }

    // --- Toast Swipe ---

    [Fact]
    public async Task RegisterToastSwipe_Invokes_JS_With_ElementId_And_ToastId()
    {
        await _service.RegisterToastSwipe("element-1", "toast-1", _ => Task.CompletedTask);

        var invocation = _module.VerifyInvoke("registerToastSwipe");
        // JS call: registerToastSwipe(elementId, toastId, selfRef)
        Assert.Equal("element-1", invocation.Arguments[0]);
        Assert.Equal("toast-1", invocation.Arguments[1]);
    }

    [Fact]
    public async Task UnregisterToastSwipe_Invokes_JS_With_ElementId()
    {
        await _service.RegisterToastSwipe("element-1", "toast-1", _ => Task.CompletedTask);
        await _service.UnregisterToastSwipe("toast-1", "element-1");

        var invocation = _module.VerifyInvoke("unregisterToastSwipe");
        Assert.Equal("element-1", invocation.Arguments[0]);
    }

    // --- Drawer Swipe ---

    [Fact]
    public async Task RegisterDrawerSwipe_Invokes_JS_With_ElementId_And_Direction()
    {
        await _service.RegisterDrawerSwipe("drawer-1", "left", () => Task.CompletedTask);

        var invocation = _module.VerifyInvoke("registerDrawerSwipe");
        Assert.Equal("drawer-1", invocation.Arguments[0]);
        Assert.Equal("left", invocation.Arguments[1]);
    }

    [Fact]
    public async Task RegisterDrawerSwipe_Default_Overload_Passes_Down_As_Direction()
    {
        // The overload without a direction argument should default to "down"
        await _service.RegisterDrawerSwipe("drawer-2", () => Task.CompletedTask);

        var invocation = _module.VerifyInvoke("registerDrawerSwipe");
        Assert.Equal("drawer-2", invocation.Arguments[0]);
        Assert.Equal("down", invocation.Arguments[1]);
    }

    // --- BackToTop ---

    [Fact]
    public async Task RegisterBackToTop_Invokes_JS_With_Id_SelfRef_Threshold_In_Order()
    {
        // Service calls: InvokeVoidAsync("registerBackToTop", id, GetSelfRef(), threshold)
        // So arg order is: [0]=id, [1]=DotNetRef, [2]=threshold
        await _service.RegisterBackToTop("bt-1", 300, _ => Task.CompletedTask);

        var invocation = _module.VerifyInvoke("registerBackToTop");
        Assert.Equal("bt-1", invocation.Arguments[0]);
        // Arguments[1] is DotNetObjectReference — not assertable by value
        Assert.Equal(300, invocation.Arguments[2]);
    }

    [Fact]
    public async Task UnregisterBackToTop_Invokes_JS_With_Id()
    {
        await _service.RegisterBackToTop("bt-1", 300, _ => Task.CompletedTask);
        await _service.UnregisterBackToTop("bt-1");

        var invocation = _module.VerifyInvoke("unregisterBackToTop");
        Assert.Equal("bt-1", invocation.Arguments[0]);
    }

    // --- LockScroll / UnlockScroll ---

    [Fact]
    public async Task LockScroll_Invokes_Correct_JS_Function()
    {
        await _service.LockScroll();

        _module.VerifyInvoke("lockScroll");
    }

    [Fact]
    public async Task UnlockScroll_Invokes_Correct_JS_Function()
    {
        await _service.UnlockScroll();

        _module.VerifyInvoke("unlockScroll");
    }

    // --- FocusTrap ---

    [Fact]
    public async Task SetupFocusTrap_Invokes_Correct_JS_With_ElementId()
    {
        await _service.SetupFocusTrap("trap-1");

        var invocation = _module.VerifyInvoke("setupFocusTrap");
        Assert.Equal("trap-1", invocation.Arguments[0]);
    }

    [Fact]
    public async Task RemoveFocusTrap_Invokes_Correct_JS_With_ElementId()
    {
        await _service.RemoveFocusTrap("trap-1");

        var invocation = _module.VerifyInvoke("removeFocusTrap");
        Assert.Equal("trap-1", invocation.Arguments[0]);
    }

    // --- FocusElement ---

    [Fact]
    public async Task FocusElement_Invokes_FocusElementById_Not_FocusElement()
    {
        // The JS function is "focusElementById", not "focusElement" — easy rename bug to catch
        await _service.FocusElement("some-elem");

        var invocation = _module.VerifyInvoke("focusElementById");
        Assert.Equal("some-elem", invocation.Arguments[0]);
    }

    // --- Scrollspy ---

    [Fact]
    public async Task RegisterScrollspy_Invokes_JS_With_ContainerId_Offset_Smooth()
    {
        // Service calls: registerScrollspy(containerId, offset, smooth, selfRef)
        await _service.RegisterScrollspy("container-1", 50, true, _ => Task.CompletedTask);

        var invocation = _module.VerifyInvoke("registerScrollspy");
        Assert.Equal("container-1", invocation.Arguments[0]);
        Assert.Equal(50, invocation.Arguments[1]);
        Assert.Equal(true, invocation.Arguments[2]);
    }

    [Fact]
    public async Task UnregisterScrollspy_Invokes_JS_With_ContainerId()
    {
        await _service.RegisterScrollspy("container-1", 0, false, _ => Task.CompletedTask);
        await _service.UnregisterScrollspy("container-1");

        var invocation = _module.VerifyInvoke("unregisterScrollspy");
        Assert.Equal("container-1", invocation.Arguments[0]);
    }

    // --- Carousel Swipe ---

    [Fact]
    public async Task RegisterCarouselSwipe_Invokes_JS_With_ElementId_And_Orientation()
    {
        await _service.RegisterCarouselSwipe("carousel-1", "horizontal",
            _ => Task.CompletedTask, (_, _) => Task.CompletedTask);

        var invocation = _module.VerifyInvoke("registerCarouselSwipe");
        Assert.Equal("carousel-1", invocation.Arguments[0]);
        Assert.Equal("horizontal", invocation.Arguments[1]);
    }

    [Fact]
    public async Task UnregisterCarouselSwipe_Invokes_JS_With_ElementId()
    {
        await _service.RegisterCarouselSwipe("carousel-1", "horizontal",
            _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        await _service.UnregisterCarouselSwipe("carousel-1");

        var invocation = _module.VerifyInvoke("unregisterCarouselSwipe");
        Assert.Equal("carousel-1", invocation.Arguments[0]);
    }

    // --- Scrollspy ScrollTo ---

    [Fact]
    public async Task ScrollspyScrollTo_Invokes_JS_With_Correct_Args()
    {
        await _service.ScrollspyScrollTo("container-1", "section-2", true);

        var inv = _module.VerifyInvoke("scrollspyScrollTo");
        Assert.Equal("container-1", inv.Arguments[0]);
        Assert.Equal("section-2", inv.Arguments[1]);
        Assert.Equal(true, inv.Arguments[2]);
    }

    [Fact]
    public async Task ScrollspyScrollTo_Smooth_False_Passes_False_As_Third_Arg()
    {
        await _service.ScrollspyScrollTo("container-2", "section-3", false);

        var inv = _module.VerifyInvoke("scrollspyScrollTo");
        Assert.Equal("container-2", inv.Arguments[0]);
        Assert.Equal("section-3", inv.Arguments[1]);
        Assert.Equal(false, inv.Arguments[2]);
    }

    // --- Affix ---

    [Fact]
    public async Task RegisterAffix_Invokes_JS_With_Correct_Args()
    {
        // JS call: registerAffix(elementId, offsetTop, offsetBottom, target, selfRef)
        await _service.RegisterAffix("elem-1", 100, 50, "#target", _ => Task.CompletedTask);

        var inv = _module.VerifyInvoke("registerAffix");
        Assert.Equal("elem-1", inv.Arguments[0]);
        Assert.Equal(100, inv.Arguments[1]);
        Assert.Equal(50, inv.Arguments[2]);
        Assert.Equal("#target", inv.Arguments[3]);
        // Arguments[4] is DotNetObjectReference — not assertable by value
    }

    [Fact]
    public async Task RegisterAffix_Null_OffsetBottom_And_Target_Passes_Nulls()
    {
        await _service.RegisterAffix("elem-2", 0, null, null, _ => Task.CompletedTask);

        var inv = _module.VerifyInvoke("registerAffix");
        Assert.Equal("elem-2", inv.Arguments[0]);
        Assert.Equal(0, inv.Arguments[1]);
        Assert.Null(inv.Arguments[2]);
        Assert.Null(inv.Arguments[3]);
    }

    [Fact]
    public async Task UnregisterAffix_Invokes_JS_With_ElementId()
    {
        await _service.RegisterAffix("elem-1", 0, null, null, _ => Task.CompletedTask);
        await _service.UnregisterAffix("elem-1");

        var inv = _module.VerifyInvoke("unregisterAffix");
        Assert.Equal("elem-1", inv.Arguments[0]);
    }

    // --- Column Resize ---

    [Fact]
    public async Task RegisterColumnResize_Invokes_JS_With_HandleId()
    {
        // JS call: registerColumnResize(handleId, selfRef) — no direction
        await _service.RegisterColumnResize("handle-1", _ => Task.CompletedTask, () => Task.CompletedTask);

        var inv = _module.VerifyInvoke("registerColumnResize");
        Assert.Equal("handle-1", inv.Arguments[0]);
        // Arguments[1] is DotNetObjectReference — not assertable by value
    }

    [Fact]
    public async Task UnregisterColumnResize_Invokes_JS_With_HandleId()
    {
        await _service.RegisterColumnResize("handle-1", _ => Task.CompletedTask, () => Task.CompletedTask);
        await _service.UnregisterColumnResize("handle-1");

        var inv = _module.VerifyInvoke("unregisterColumnResize");
        Assert.Equal("handle-1", inv.Arguments[0]);
    }

    // --- OTP Paste ---

    [Fact]
    public async Task RegisterOtpPaste_Invokes_JS_With_BaseId_And_Length()
    {
        // JS call: registerOtpPaste(baseId, length, selfRef)
        await _service.RegisterOtpPaste("otp-1", 6, _ => Task.CompletedTask);

        var inv = _module.VerifyInvoke("registerOtpPaste");
        Assert.Equal("otp-1", inv.Arguments[0]);
        Assert.Equal(6, inv.Arguments[1]);
        // Arguments[2] is DotNetObjectReference — not assertable by value
    }

    [Fact]
    public async Task UnregisterOtpPaste_Invokes_JS_With_BaseId_And_Length()
    {
        await _service.RegisterOtpPaste("otp-1", 6, _ => Task.CompletedTask);
        await _service.UnregisterOtpPaste("otp-1", 6);

        var inv = _module.VerifyInvoke("unregisterOtpPaste");
        Assert.Equal("otp-1", inv.Arguments[0]);
        Assert.Equal(6, inv.Arguments[1]);
    }

    // --- AutoResize ---

    [Fact]
    public async Task SetupAutoResize_Invokes_JS_With_ElementId_And_MaxRows()
    {
        await _service.SetupAutoResize("textarea-1", 5);

        var inv = _module.VerifyInvoke("setupAutoResize");
        Assert.Equal("textarea-1", inv.Arguments[0]);
        Assert.Equal(5, inv.Arguments[1]);
    }

    // --- Download ---

    [Fact]
    public async Task DownloadFile_Invokes_JS_With_FileName_Content_MimeType()
    {
        await _service.DownloadFile("test.csv", "base64data", "text/csv");

        var inv = _module.VerifyInvoke("downloadFile");
        Assert.Equal("test.csv", inv.Arguments[0]);
        Assert.Equal("base64data", inv.Arguments[1]);
        Assert.Equal("text/csv", inv.Arguments[2]);
    }

    [Fact]
    public async Task DownloadFile_Default_MimeType_Is_OctetStream()
    {
        await _service.DownloadFile("archive.bin", "binarydata");

        var inv = _module.VerifyInvoke("downloadFile");
        Assert.Equal("archive.bin", inv.Arguments[0]);
        Assert.Equal("binarydata", inv.Arguments[1]);
        Assert.Equal("application/octet-stream", inv.Arguments[2]);
    }

    // --- Clipboard ---

    [Fact]
    public async Task CopyToClipboard_Invokes_JS_With_Text()
    {
        await _service.CopyToClipboard("hello world");

        var inv = _module.VerifyInvoke("copyToClipboard");
        Assert.Equal("hello world", inv.Arguments[0]);
    }

    // --- LocalStorage ---

    [Fact]
    public async Task SaveToLocalStorage_Invokes_JS_With_Key_And_Value()
    {
        await _service.SaveToLocalStorage("my-key", "my-value");

        var inv = _module.VerifyInvoke("saveToLocalStorage");
        Assert.Equal("my-key", inv.Arguments[0]);
        Assert.Equal("my-value", inv.Arguments[1]);
    }

    [Fact]
    public async Task LoadFromLocalStorage_Invokes_JS_With_Key_And_Returns_Value()
    {
        _module.Setup<string?>("loadFromLocalStorage", "my-key").SetResult("stored-value");
        var result = await _service.LoadFromLocalStorage("my-key");

        Assert.Equal("stored-value", result);
    }

    [Fact]
    public async Task RemoveFromLocalStorage_Invokes_JS_With_Key()
    {
        await _service.RemoveFromLocalStorage("my-key");

        var inv = _module.VerifyInvoke("removeFromLocalStorage");
        Assert.Equal("my-key", inv.Arguments[0]);
    }

    // --- Resize Handle ---

    [Fact]
    public async Task RegisterResizeHandle_Invokes_JS_With_ElementId_And_Direction()
    {
        // JS call: registerResizeHandle(elementId, direction, selfRef)
        await _service.RegisterResizeHandle("handle-1", "horizontal", _ => Task.CompletedTask, () => Task.CompletedTask);

        var inv = _module.VerifyInvoke("registerResizeHandle");
        Assert.Equal("handle-1", inv.Arguments[0]);
        Assert.Equal("horizontal", inv.Arguments[1]);
        // Arguments[2] is DotNetObjectReference — not assertable by value
    }

    [Fact]
    public async Task UnregisterResizeHandle_Invokes_JS_With_ElementId()
    {
        await _service.RegisterResizeHandle("handle-1", "vertical", _ => Task.CompletedTask, () => Task.CompletedTask);
        await _service.UnregisterResizeHandle("handle-1");

        var inv = _module.VerifyInvoke("unregisterResizeHandle");
        Assert.Equal("handle-1", inv.Arguments[0]);
    }

    // --- Carousel ScrollTo ---

    [Fact]
    public async Task CarouselScrollTo_Invokes_JS_With_ElementId_Index_Behavior()
    {
        await _service.CarouselScrollTo("carousel-1", 3, "instant");

        var inv = _module.VerifyInvoke("carouselScrollTo");
        Assert.Equal("carousel-1", inv.Arguments[0]);
        Assert.Equal(3, inv.Arguments[1]);
        Assert.Equal("instant", inv.Arguments[2]);
    }

    [Fact]
    public async Task CarouselScrollTo_Default_Behavior_Is_Smooth()
    {
        await _service.CarouselScrollTo("carousel-2", 0);

        var inv = _module.VerifyInvoke("carouselScrollTo");
        Assert.Equal("carousel-2", inv.Arguments[0]);
        Assert.Equal(0, inv.Arguments[1]);
        Assert.Equal("smooth", inv.Arguments[2]);
    }

    // --- ScrollToTop ---

    [Fact]
    public async Task ScrollToTop_Invokes_JS_With_No_Args()
    {
        await _service.ScrollToTop();

        _module.VerifyInvoke("scrollToTop");
    }

    // --- FocusMenuItemByIndex ---

    [Fact]
    public async Task FocusMenuItemByIndex_Invokes_JS_With_ContainerId_And_Index()
    {
        await _service.FocusMenuItemByIndex("menu-1", 2);

        var inv = _module.VerifyInvoke("focusMenuItemByIndex");
        Assert.Equal("menu-1", inv.Arguments[0]);
        Assert.Equal(2, inv.Arguments[1]);
    }

    // --- GetMenuItemCount ---

    [Fact]
    public async Task GetMenuItemCount_Returns_JS_Result()
    {
        _module.Setup<int>("getMenuItemCount", "menu-1").SetResult(5);
        var count = await _service.GetMenuItemCount("menu-1");

        Assert.Equal(5, count);
    }
}
