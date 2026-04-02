using Lumeo.Services.Interop;
using Xunit;

namespace Lumeo.Tests.Services.Interop;

/// <summary>
/// Tests for the internal adapter classes' callback-dispatch logic.
/// These tests exercise the OnCallback/OnSwipe/OnResize/etc. methods
/// directly by creating adapter instances and calling the OnXxx methods,
/// which only do dictionary lookups — no JS interop required.
/// </summary>
public class InteropAdapterTests
{
    // -----------------------------------------------------------------------
    // ClickOutsideInterop
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ClickOutside_OnCallback_Fires_Registered_Handler()
    {
        var adapter = new ClickOutsideInterop();
        var called = false;
        // Simulate what Register does without JS: add directly via the callback path
        // by first making a handler reachable through internal API.
        // We exercise OnCallback — the dictionary lookup itself — by using reflection
        // to seed the dictionary, mirroring the exact internal field name.
        var field = typeof(ClickOutsideInterop)
            .GetField("_handlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<Task>>)field.GetValue(adapter)!;
        dict["elem-1"] = () => { called = true; return Task.CompletedTask; };

        await adapter.OnCallback("elem-1");

        Assert.True(called);
    }

    [Fact]
    public async Task ClickOutside_OnCallback_Unknown_Id_Does_Not_Throw()
    {
        var adapter = new ClickOutsideInterop();
        var exception = await Record.ExceptionAsync(() => adapter.OnCallback("unknown"));
        Assert.Null(exception);
    }

    [Fact]
    public async Task ClickOutside_OnCallback_Only_Fires_Matching_Handler()
    {
        var adapter = new ClickOutsideInterop();
        var field = typeof(ClickOutsideInterop)
            .GetField("_handlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<Task>>)field.GetValue(adapter)!;

        int count1 = 0, count2 = 0;
        dict["e1"] = () => { count1++; return Task.CompletedTask; };
        dict["e2"] = () => { count2++; return Task.CompletedTask; };

        await adapter.OnCallback("e1");

        Assert.Equal(1, count1);
        Assert.Equal(0, count2);
    }

    [Fact]
    public void ClickOutside_Clear_Empties_Handlers()
    {
        var adapter = new ClickOutsideInterop();
        var field = typeof(ClickOutsideInterop)
            .GetField("_handlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<Task>>)field.GetValue(adapter)!;
        dict["elem-1"] = () => Task.CompletedTask;

        adapter.Clear();

        Assert.Empty(dict);
    }

    [Fact]
    public async Task ClickOutside_OnCallback_After_Clear_Does_Not_Fire()
    {
        var adapter = new ClickOutsideInterop();
        var field = typeof(ClickOutsideInterop)
            .GetField("_handlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<Task>>)field.GetValue(adapter)!;
        var called = false;
        dict["e"] = () => { called = true; return Task.CompletedTask; };

        adapter.Clear();
        await adapter.OnCallback("e");

        Assert.False(called);
    }

    // -----------------------------------------------------------------------
    // SwipeInterop — Drawer
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SwipeInterop_OnSwipeDismiss_Fires_Registered_Drawer_Handler()
    {
        var adapter = new SwipeInterop();
        var field = typeof(SwipeInterop)
            .GetField("_drawerSwipeHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<Task>>)field.GetValue(adapter)!;
        var called = false;
        dict["drawer-1"] = () => { called = true; return Task.CompletedTask; };

        await adapter.OnSwipeDismiss("drawer-1");

        Assert.True(called);
    }

    [Fact]
    public async Task SwipeInterop_OnSwipeDismiss_Unknown_Id_Does_Not_Throw()
    {
        var adapter = new SwipeInterop();
        var exception = await Record.ExceptionAsync(() => adapter.OnSwipeDismiss("unknown"));
        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // SwipeInterop — Carousel
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SwipeInterop_OnSwipe_Fires_Only_Matching_Handler()
    {
        var adapter = new SwipeInterop();
        var field = typeof(SwipeInterop)
            .GetField("_carouselSwipeHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<string, Task>>)field.GetValue(adapter)!;

        string? receivedDir = null;
        bool otherCalled = false;
        dict["c1"] = dir => { receivedDir = dir; return Task.CompletedTask; };
        dict["c2"] = _ => { otherCalled = true; return Task.CompletedTask; };

        await adapter.OnSwipe("c1", "left");

        Assert.Equal("left", receivedDir);
        Assert.False(otherCalled);
    }

    [Fact]
    public async Task SwipeInterop_OnScrollPosition_Fires_Matching_Handler()
    {
        var adapter = new SwipeInterop();
        var field = typeof(SwipeInterop)
            .GetField("_carouselScrollHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<double, double, Task>>)field.GetValue(adapter)!;

        double pos = -1, max = -1;
        dict["c1"] = (p, m) => { pos = p; max = m; return Task.CompletedTask; };

        await adapter.OnScrollPosition("c1", 50.0, 200.0);

        Assert.Equal(50.0, pos);
        Assert.Equal(200.0, max);
    }

    [Fact]
    public async Task SwipeInterop_OnScrollPosition_Unknown_Id_Does_Not_Throw()
    {
        var adapter = new SwipeInterop();
        var exception = await Record.ExceptionAsync(() => adapter.OnScrollPosition("unknown", 0, 0));
        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // SwipeInterop — Toast
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SwipeInterop_OnToastSwipeDismiss_Fires_Handler_By_ToastId()
    {
        var adapter = new SwipeInterop();
        var field = typeof(SwipeInterop)
            .GetField("_toastSwipeHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<string, Task>>)field.GetValue(adapter)!;

        string? received = null;
        dict["toast-1"] = id => { received = id; return Task.CompletedTask; };

        await adapter.OnToastSwipeDismiss("toast-1");

        Assert.Equal("toast-1", received);
    }

    [Fact]
    public async Task SwipeInterop_OnToastSwipeDismiss_Unknown_Id_Does_Not_Throw()
    {
        var adapter = new SwipeInterop();
        var exception = await Record.ExceptionAsync(() => adapter.OnToastSwipeDismiss("unknown"));
        Assert.Null(exception);
    }

    [Fact]
    public void SwipeInterop_Clear_Empties_All_Dictionaries()
    {
        var adapter = new SwipeInterop();

        var drawerField = typeof(SwipeInterop).GetField("_drawerSwipeHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var carouselField = typeof(SwipeInterop).GetField("_carouselSwipeHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var scrollField = typeof(SwipeInterop).GetField("_carouselScrollHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var toastField = typeof(SwipeInterop).GetField("_toastSwipeHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var drawerDict = (Dictionary<string, Func<Task>>)drawerField.GetValue(adapter)!;
        var carouselDict = (Dictionary<string, Func<string, Task>>)carouselField.GetValue(adapter)!;
        var scrollDict = (Dictionary<string, Func<double, double, Task>>)scrollField.GetValue(adapter)!;
        var toastDict = (Dictionary<string, Func<string, Task>>)toastField.GetValue(adapter)!;

        drawerDict["d"] = () => Task.CompletedTask;
        carouselDict["c"] = _ => Task.CompletedTask;
        scrollDict["s"] = (_, _) => Task.CompletedTask;
        toastDict["t"] = _ => Task.CompletedTask;

        adapter.Clear();

        Assert.Empty(drawerDict);
        Assert.Empty(carouselDict);
        Assert.Empty(scrollDict);
        Assert.Empty(toastDict);
    }

    // -----------------------------------------------------------------------
    // ResizeInterop — Panel
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResizeInterop_OnResize_Fires_Only_Matching_Handler()
    {
        var adapter = new ResizeInterop();
        var field = typeof(ResizeInterop)
            .GetField("_resizeHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<double, Task>>)field.GetValue(adapter)!;

        double receivedDelta = -1;
        bool otherCalled = false;
        dict["h1"] = delta => { receivedDelta = delta; return Task.CompletedTask; };
        dict["h2"] = _ => { otherCalled = true; return Task.CompletedTask; };

        await adapter.OnResize("h1", 42.5);

        Assert.Equal(42.5, receivedDelta);
        Assert.False(otherCalled);
    }

    [Fact]
    public async Task ResizeInterop_OnResizeEnd_Fires_Only_Matching_Handler()
    {
        var adapter = new ResizeInterop();
        var field = typeof(ResizeInterop)
            .GetField("_resizeEndHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<Task>>)field.GetValue(adapter)!;

        bool called = false, otherCalled = false;
        dict["h1"] = () => { called = true; return Task.CompletedTask; };
        dict["h2"] = () => { otherCalled = true; return Task.CompletedTask; };

        await adapter.OnResizeEnd("h1");

        Assert.True(called);
        Assert.False(otherCalled);
    }

    [Fact]
    public async Task ResizeInterop_OnResize_Unknown_Id_Does_Not_Throw()
    {
        var adapter = new ResizeInterop();
        var exception = await Record.ExceptionAsync(() => adapter.OnResize("unknown", 10.0));
        Assert.Null(exception);
    }

    [Fact]
    public async Task ResizeInterop_OnResizeEnd_Unknown_Id_Does_Not_Throw()
    {
        var adapter = new ResizeInterop();
        var exception = await Record.ExceptionAsync(() => adapter.OnResizeEnd("unknown"));
        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // ResizeInterop — Column
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResizeInterop_OnColumnResize_Fires_Only_Matching_Handler()
    {
        var adapter = new ResizeInterop();
        var field = typeof(ResizeInterop)
            .GetField("_columnResizeHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<double, Task>>)field.GetValue(adapter)!;

        double receivedDelta = -1;
        bool otherCalled = false;
        dict["col-1"] = delta => { receivedDelta = delta; return Task.CompletedTask; };
        dict["col-2"] = _ => { otherCalled = true; return Task.CompletedTask; };

        await adapter.OnColumnResize("col-1", 15.0);

        Assert.Equal(15.0, receivedDelta);
        Assert.False(otherCalled);
    }

    [Fact]
    public async Task ResizeInterop_OnColumnResizeEnd_Fires_Only_Matching_Handler()
    {
        var adapter = new ResizeInterop();
        var field = typeof(ResizeInterop)
            .GetField("_columnResizeEndHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<Task>>)field.GetValue(adapter)!;

        bool called = false, otherCalled = false;
        dict["col-1"] = () => { called = true; return Task.CompletedTask; };
        dict["col-2"] = () => { otherCalled = true; return Task.CompletedTask; };

        await adapter.OnColumnResizeEnd("col-1");

        Assert.True(called);
        Assert.False(otherCalled);
    }

    [Fact]
    public void ResizeInterop_Clear_Empties_All_Dictionaries()
    {
        var adapter = new ResizeInterop();

        var resizeField = typeof(ResizeInterop).GetField("_resizeHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var resizeEndField = typeof(ResizeInterop).GetField("_resizeEndHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var colResizeField = typeof(ResizeInterop).GetField("_columnResizeHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var colResizeEndField = typeof(ResizeInterop).GetField("_columnResizeEndHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var d1 = (Dictionary<string, Func<double, Task>>)resizeField.GetValue(adapter)!;
        var d2 = (Dictionary<string, Func<Task>>)resizeEndField.GetValue(adapter)!;
        var d3 = (Dictionary<string, Func<double, Task>>)colResizeField.GetValue(adapter)!;
        var d4 = (Dictionary<string, Func<Task>>)colResizeEndField.GetValue(adapter)!;

        d1["a"] = _ => Task.CompletedTask;
        d2["b"] = () => Task.CompletedTask;
        d3["c"] = _ => Task.CompletedTask;
        d4["d"] = () => Task.CompletedTask;

        adapter.Clear();

        Assert.Empty(d1);
        Assert.Empty(d2);
        Assert.Empty(d3);
        Assert.Empty(d4);
    }

    // -----------------------------------------------------------------------
    // ScrollInterop — Scrollspy
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ScrollInterop_OnScrollspyUpdate_Fires_Matching_Handler()
    {
        var adapter = new ScrollInterop();
        var field = typeof(ScrollInterop)
            .GetField("_scrollspyHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<string?, Task>>)field.GetValue(adapter)!;

        string? receivedId = null;
        dict["container-1"] = id => { receivedId = id; return Task.CompletedTask; };

        await adapter.OnScrollspyUpdate("container-1", "section-2");

        Assert.Equal("section-2", receivedId);
    }

    [Fact]
    public async Task ScrollInterop_OnScrollspyUpdate_Passes_Null_ActiveId()
    {
        var adapter = new ScrollInterop();
        var field = typeof(ScrollInterop)
            .GetField("_scrollspyHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<string?, Task>>)field.GetValue(adapter)!;

        string? receivedId = "initial";
        dict["c"] = id => { receivedId = id; return Task.CompletedTask; };

        await adapter.OnScrollspyUpdate("c", null);

        Assert.Null(receivedId);
    }

    [Fact]
    public async Task ScrollInterop_OnScrollspyUpdate_Unknown_Container_Does_Not_Throw()
    {
        var adapter = new ScrollInterop();
        var exception = await Record.ExceptionAsync(() => adapter.OnScrollspyUpdate("unknown", "s1"));
        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // ScrollInterop — BackToTop
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ScrollInterop_OnScrollVisibilityChanged_Fires_Matching_Handler()
    {
        var adapter = new ScrollInterop();
        var field = typeof(ScrollInterop)
            .GetField("_backToTopHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<bool, Task>>)field.GetValue(adapter)!;

        bool? received = null;
        dict["bt1"] = v => { received = v; return Task.CompletedTask; };

        await adapter.OnScrollVisibilityChanged("bt1", true);

        Assert.True(received);
    }

    [Fact]
    public async Task ScrollInterop_OnScrollVisibilityChanged_Only_Matching_Handler()
    {
        var adapter = new ScrollInterop();
        var field = typeof(ScrollInterop)
            .GetField("_backToTopHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<bool, Task>>)field.GetValue(adapter)!;

        bool? h1 = null, h2 = null;
        dict["bt1"] = v => { h1 = v; return Task.CompletedTask; };
        dict["bt2"] = v => { h2 = v; return Task.CompletedTask; };

        await adapter.OnScrollVisibilityChanged("bt1", false);

        Assert.False(h1);
        Assert.Null(h2);
    }

    // -----------------------------------------------------------------------
    // ScrollInterop — Affix
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ScrollInterop_OnAffixChanged_Fires_Matching_Handler()
    {
        var adapter = new ScrollInterop();
        var field = typeof(ScrollInterop)
            .GetField("_affixHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<bool, Task>>)field.GetValue(adapter)!;

        bool? received = null;
        dict["affix-1"] = v => { received = v; return Task.CompletedTask; };

        await adapter.OnAffixChanged("affix-1", true);

        Assert.True(received);
    }

    [Fact]
    public async Task ScrollInterop_OnAffixChanged_Unknown_Id_Does_Not_Throw()
    {
        var adapter = new ScrollInterop();
        var exception = await Record.ExceptionAsync(() => adapter.OnAffixChanged("unknown", true));
        Assert.Null(exception);
    }

    [Fact]
    public void ScrollInterop_Clear_Empties_All_Dictionaries()
    {
        var adapter = new ScrollInterop();
        var spyField = typeof(ScrollInterop).GetField("_scrollspyHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var btField = typeof(ScrollInterop).GetField("_backToTopHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var affixField = typeof(ScrollInterop).GetField("_affixHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var d1 = (Dictionary<string, Func<string?, Task>>)spyField.GetValue(adapter)!;
        var d2 = (Dictionary<string, Func<bool, Task>>)btField.GetValue(adapter)!;
        var d3 = (Dictionary<string, Func<bool, Task>>)affixField.GetValue(adapter)!;

        d1["a"] = _ => Task.CompletedTask;
        d2["b"] = _ => Task.CompletedTask;
        d3["c"] = _ => Task.CompletedTask;

        adapter.Clear();

        Assert.Empty(d1);
        Assert.Empty(d2);
        Assert.Empty(d3);
    }

    // -----------------------------------------------------------------------
    // UtilityInterop — OTP Paste
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UtilityInterop_OnOtpPaste_Fires_Matching_Handler()
    {
        var adapter = new UtilityInterop();
        var field = typeof(UtilityInterop)
            .GetField("_otpPasteHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<string, Task>>)field.GetValue(adapter)!;

        string? received = null;
        dict["otp-1"] = digits => { received = digits; return Task.CompletedTask; };

        await adapter.OnOtpPaste("otp-1", "123456");

        Assert.Equal("123456", received);
    }

    [Fact]
    public async Task UtilityInterop_OnOtpPaste_Only_Matching_Handler()
    {
        var adapter = new UtilityInterop();
        var field = typeof(UtilityInterop)
            .GetField("_otpPasteHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<string, Task>>)field.GetValue(adapter)!;

        string? r1 = null;
        bool otherCalled = false;
        dict["otp-1"] = d => { r1 = d; return Task.CompletedTask; };
        dict["otp-2"] = _ => { otherCalled = true; return Task.CompletedTask; };

        await adapter.OnOtpPaste("otp-1", "999");

        Assert.Equal("999", r1);
        Assert.False(otherCalled);
    }

    [Fact]
    public async Task UtilityInterop_OnOtpPaste_Unknown_Id_Does_Not_Throw()
    {
        var adapter = new UtilityInterop();
        var exception = await Record.ExceptionAsync(() => adapter.OnOtpPaste("unknown", "123"));
        Assert.Null(exception);
    }

    [Fact]
    public void UtilityInterop_Clear_Empties_OtpHandlers()
    {
        var adapter = new UtilityInterop();
        var field = typeof(UtilityInterop)
            .GetField("_otpPasteHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, Func<string, Task>>)field.GetValue(adapter)!;
        dict["otp-1"] = _ => Task.CompletedTask;

        adapter.Clear();

        Assert.Empty(dict);
    }
}
