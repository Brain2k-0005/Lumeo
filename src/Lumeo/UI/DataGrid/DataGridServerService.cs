using Microsoft.AspNetCore.Components;

namespace Lumeo;

internal sealed class DataGridServerService : IDisposable
{
    private CancellationTokenSource? _requestCts;
    private System.Threading.Timer? _searchDebounceTimer;

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
                IsLoading = false;
                stateChanged();
            }
        }
        else
        {
            IsLoading = false;
            stateChanged();
        }
    }

    internal void DebounceSearch(Action requestAction, int delayMs = 300)
    {
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new System.Threading.Timer(_ =>
        {
            requestAction();
        }, null, delayMs, System.Threading.Timeout.Infinite);
    }

    internal void ClearError() => Error = null;

    public void Dispose()
    {
        _searchDebounceTimer?.Dispose();
        _requestCts?.Cancel();
        _requestCts?.Dispose();
    }
}
