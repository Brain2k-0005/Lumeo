using Microsoft.AspNetCore.Components;

namespace Lumeo;

internal sealed class DataGridServerService : IDisposable
{
    private CancellationTokenSource? _requestCts;
    private CancellationTokenSource? _searchCts;
    private int _requestGeneration;

    internal bool IsLoading { get; private set; }
    internal string? Error { get; private set; }

    internal async Task RequestDataAsync(
        int currentPage,
        int pageSize,
        List<SortDescriptor>? sorts,
        List<FilterDescriptor>? filters,
        string? globalSearch,
        string? groupBy,
        EventCallback<DataGridServerRequest> onServerRequest,
        EventCallback<Exception>? onError,
        Action stateChanged)
    {
        _requestCts?.Cancel();
        _requestCts?.Dispose();
        _requestCts = new CancellationTokenSource();
        var generation = ++_requestGeneration;

        Error = null;
        IsLoading = true;
        stateChanged();

        if (onServerRequest.HasDelegate)
        {
            var request = new DataGridServerRequest(
                currentPage,
                pageSize,
                sorts is { Count: > 0 } ? sorts : null,
                filters is { Count: > 0 } ? filters : null,
                globalSearch,
                groupBy,
                _requestCts.Token);
            try
            {
                await onServerRequest.InvokeAsync(request);
            }
            catch (OperationCanceledException)
            {
                // Request was superseded — ignore
                return;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                if (onError?.HasDelegate == true)
                    await onError.Value.InvokeAsync(ex);
            }
            finally
            {
                // Only the latest request should reset loading state
                if (generation == _requestGeneration)
                {
                    IsLoading = false;
                    stateChanged();
                }
            }
        }
        else
        {
            IsLoading = false;
            stateChanged();
        }
    }

    internal void DebounceSearch(Func<CancellationToken, Task> work, int delayMs = 300)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = RunDebouncedAsync(work, delayMs, token);
    }

    private async Task RunDebouncedAsync(Func<CancellationToken, Task> work, int delayMs, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delayMs, ct);
            if (ct.IsCancellationRequested) return;
            await work(ct);
        }
        catch (OperationCanceledException) { /* superseded by newer call or disposed */ }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DataGridServerService] debounced search error: {ex}");
        }
    }

    internal void ClearError() => Error = null;

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
        _requestCts?.Cancel();
        _requestCts?.Dispose();
    }
}
