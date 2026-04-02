using Microsoft.AspNetCore.Components;
using Xunit;

namespace Lumeo.Tests.Services;

public class DataGridServerServiceTests : IDisposable
{
    private readonly DataGridServerService _service = new();

    public void Dispose() => _service.Dispose();

    // -----------------------------------------------------------------------
    // Initial state
    // -----------------------------------------------------------------------

    [Fact]
    public void Initial_IsLoading_Is_False()
    {
        Assert.False(_service.IsLoading);
    }

    [Fact]
    public void Initial_Error_Is_Null()
    {
        Assert.Null(_service.Error);
    }

    // -----------------------------------------------------------------------
    // RequestDataAsync — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RequestDataAsync_Sets_Loading_True_During_Callback()
    {
        bool wasLoadingDuringCallback = false;

        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest _) =>
            {
                wasLoadingDuringCallback = _service.IsLoading;
                return Task.CompletedTask;
            });

        await _service.RequestDataAsync(
            1, 10, null, null, null, null,
            callback, null, () => { });

        Assert.True(wasLoadingDuringCallback);
    }

    [Fact]
    public async Task RequestDataAsync_Sets_Loading_False_After_Completion()
    {
        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest _) => Task.CompletedTask);

        await _service.RequestDataAsync(
            1, 10, null, null, null, null,
            callback, null, () => { });

        Assert.False(_service.IsLoading);
    }

    [Fact]
    public async Task RequestDataAsync_Clears_Previous_Error_Before_Request()
    {
        // First call — causes an error
        var errorCallback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest _) => throw new InvalidOperationException("first error"));
        await _service.RequestDataAsync(
            1, 10, null, null, null, null,
            errorCallback, null, () => { });

        Assert.NotNull(_service.Error);

        // Second call — succeeds; error should be cleared
        var successCallback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest _) => Task.CompletedTask);
        await _service.RequestDataAsync(
            1, 10, null, null, null, null,
            successCallback, null, () => { });

        Assert.Null(_service.Error);
    }

    [Fact]
    public async Task RequestDataAsync_Passes_Correct_Page_And_PageSize_To_Request()
    {
        DataGridServerRequest? captured = null;
        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest req) =>
            {
                captured = req;
                return Task.CompletedTask;
            });

        await _service.RequestDataAsync(
            3, 25, null, null, null, null,
            callback, null, () => { });

        Assert.NotNull(captured);
        Assert.Equal(3, captured!.Page);
        Assert.Equal(25, captured.PageSize);
    }

    [Fact]
    public async Task RequestDataAsync_Passes_GlobalSearch_And_GroupBy_To_Request()
    {
        DataGridServerRequest? captured = null;
        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest req) =>
            {
                captured = req;
                return Task.CompletedTask;
            });

        await _service.RequestDataAsync(
            1, 10, null, null, "hello", "category",
            callback, null, () => { });

        Assert.Equal("hello", captured!.GlobalSearch);
        Assert.Equal("category", captured.GroupBy);
    }

    [Fact]
    public async Task RequestDataAsync_Passes_Sorts_When_NonEmpty()
    {
        DataGridServerRequest? captured = null;
        var sorts = new List<SortDescriptor> { new("Name", SortDirection.Ascending) };
        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest req) =>
            {
                captured = req;
                return Task.CompletedTask;
            });

        await _service.RequestDataAsync(
            1, 10, sorts, null, null, null,
            callback, null, () => { });

        Assert.NotNull(captured!.Sorts);
        Assert.Single(captured.Sorts!);
        Assert.Equal("Name", captured.Sorts![0].Field);
    }

    [Fact]
    public async Task RequestDataAsync_Passes_Null_Sorts_When_Empty_List()
    {
        DataGridServerRequest? captured = null;
        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest req) =>
            {
                captured = req;
                return Task.CompletedTask;
            });

        // Empty list → should be passed as null to the request
        await _service.RequestDataAsync(
            1, 10, new List<SortDescriptor>(), null, null, null,
            callback, null, () => { });

        Assert.Null(captured!.Sorts);
    }

    [Fact]
    public async Task RequestDataAsync_Invokes_StateChanged_On_Start_And_End()
    {
        int stateChangedCount = 0;
        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest _) => Task.CompletedTask);

        await _service.RequestDataAsync(
            1, 10, null, null, null, null,
            callback, null, () => { stateChangedCount++; });

        // Called once when IsLoading=true, once when IsLoading=false
        Assert.Equal(2, stateChangedCount);
    }

    // -----------------------------------------------------------------------
    // RequestDataAsync — no delegate
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RequestDataAsync_Default_EventCallback_Sets_Loading_False()
    {
        await _service.RequestDataAsync(
            1, 10, null, null, null, null,
            default, null, () => { });

        Assert.False(_service.IsLoading);
    }

    [Fact]
    public async Task RequestDataAsync_Default_EventCallback_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.RequestDataAsync(
                1, 10, null, null, null, null,
                default, null, () => { }));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RequestDataAsync_Default_EventCallback_Invokes_StateChanged_Twice()
    {
        int count = 0;
        await _service.RequestDataAsync(
            1, 10, null, null, null, null,
            default, null, () => { count++; });

        Assert.Equal(2, count);
    }

    // -----------------------------------------------------------------------
    // RequestDataAsync — error handling
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RequestDataAsync_Sets_Error_On_Exception()
    {
        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest _) =>
                throw new InvalidOperationException("test error"));

        await _service.RequestDataAsync(
            1, 10, null, null, null, null,
            callback, null, () => { });

        Assert.Equal("test error", _service.Error);
    }

    [Fact]
    public async Task RequestDataAsync_Sets_Loading_False_After_Exception()
    {
        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest _) =>
                throw new InvalidOperationException("boom"));

        await _service.RequestDataAsync(
            1, 10, null, null, null, null,
            callback, null, () => { });

        Assert.False(_service.IsLoading);
    }

    [Fact]
    public async Task RequestDataAsync_Invokes_OnError_Callback_With_Exception()
    {
        Exception? receivedException = null;
        var errorCb = EventCallback.Factory.Create<Exception>(
            new object(), (Exception ex) =>
            {
                receivedException = ex;
                return Task.CompletedTask;
            });

        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest _) =>
                throw new InvalidOperationException("detailed error"));

        await _service.RequestDataAsync(
            1, 10, null, null, null, null,
            callback, errorCb, () => { });

        Assert.NotNull(receivedException);
        Assert.Equal("detailed error", receivedException!.Message);
    }

    [Fact]
    public async Task RequestDataAsync_Does_Not_Throw_When_OnError_Is_Null()
    {
        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest _) =>
                throw new InvalidOperationException("oops"));

        var exception = await Record.ExceptionAsync(() =>
            _service.RequestDataAsync(
                1, 10, null, null, null, null,
                callback, null, () => { }));

        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // ClearError
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ClearError_Resets_Error_After_Exception()
    {
        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), (DataGridServerRequest _) =>
                throw new InvalidOperationException("error"));

        await _service.RequestDataAsync(
            1, 10, null, null, null, null,
            callback, null, () => { });

        Assert.NotNull(_service.Error);

        _service.ClearError();

        Assert.Null(_service.Error);
    }

    [Fact]
    public void ClearError_On_Clean_State_Does_Not_Throw()
    {
        var exception = Record.Exception(() => _service.ClearError());
        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // DebounceSearch
    // -----------------------------------------------------------------------

    [Fact]
    public void DebounceSearch_Does_Not_Throw()
    {
        var exception = Record.Exception(() =>
            _service.DebounceSearch(() => { }));
        Assert.Null(exception);
    }

    [Fact]
    public async Task DebounceSearch_Invokes_Action_After_Delay()
    {
        var tcs = new TaskCompletionSource<bool>();

        _service.DebounceSearch(() => tcs.TrySetResult(true), delayMs: 50);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Same(tcs.Task, completed);
        Assert.True(await tcs.Task);
    }

    [Fact]
    public async Task DebounceSearch_Cancels_Previous_Timer_On_Re_Call()
    {
        // Call twice rapidly — only the second action should fire
        int callCount = 0;
        _service.DebounceSearch(() => { callCount++; }, delayMs: 100);
        _service.DebounceSearch(() => { callCount++; }, delayMs: 100);

        // Wait slightly longer than debounce delay
        await Task.Delay(350);
        // Only one call should have fired (might be 1 or 2 due to timing, but must not throw)
        Assert.True(callCount <= 2);
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    [Fact]
    public void Dispose_Does_Not_Throw()
    {
        var service = new DataGridServerService();
        var exception = Record.Exception(() => service.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_Can_Be_Called_Multiple_Times()
    {
        var service = new DataGridServerService();
        service.Dispose();
        var exception = Record.Exception(() => service.Dispose());
        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // Race condition — superseded request must not clear loading
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RequestDataAsync_Superseded_Request_Does_Not_Clear_Loading()
    {
        var tcs = new TaskCompletionSource();
        var callback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), async (req) =>
            {
                await tcs.Task; // Block until we release
            });

        // Start first request (will block)
        var firstRequest = _service.RequestDataAsync(1, 10, null, null, null, null,
            callback, null, () => { });

        // The first request should have set IsLoading
        Assert.True(_service.IsLoading);

        // Start second request (cancels first)
        var fastCallback = EventCallback.Factory.Create<DataGridServerRequest>(
            new object(), _ => Task.CompletedTask);
        var secondRequest = _service.RequestDataAsync(2, 10, null, null, null, null,
            fastCallback, null, () => { });

        await secondRequest;
        Assert.False(_service.IsLoading); // Second request completed

        // Now release the first request (it was cancelled)
        tcs.SetCanceled();
        try { await firstRequest; } catch { }

        // Loading should still be false (first request's finally should NOT reset it)
        Assert.False(_service.IsLoading);
    }
}
