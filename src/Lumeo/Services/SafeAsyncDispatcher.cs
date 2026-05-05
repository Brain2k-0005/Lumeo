using Microsoft.JSInterop;

namespace Lumeo.Services;

/// <summary>
/// Centralizes the "fire-and-forget UI dispatch from a sync event handler" pattern that
/// providers like Toast / Overlay / MegaMenu use. Replaces ad-hoc `_ = InvokeAsync(...).ContinueWith(...)`
/// blocks with a single helper that:
///   - Awaits the work via the supplied dispatcher (typically a Razor component's InvokeAsync)
///   - Swallows expected lifecycle exceptions (JSDisconnectedException, ObjectDisposedException,
///     OperationCanceledException) without noise
///   - Logs unexpected exceptions to Console.Error with a structured source tag
///
/// Important: this is for fire-and-forget dispatch only. Code that needs to observe completion
/// or surface errors to the caller should await directly.
/// </summary>
public static class SafeAsyncDispatcher
{
    /// <summary>
    /// Dispatch <paramref name="work"/> via <paramref name="invokeAsync"/> (typically a Blazor
    /// component's <c>InvokeAsync</c>) and swallow lifecycle-class exceptions silently. Other
    /// exceptions are logged with the <paramref name="source"/> tag and not rethrown.
    /// </summary>
    public static void FireAndForget(Func<Func<Task>, Task> invokeAsync, Func<Task> work, string source)
    {
        // Outer wrapper: protects against `invokeAsync(...)` itself faulting either
        // synchronously (e.g. renderer/circuit already disposed before the delegate
        // can be queued) OR returning a faulted Task. Without this wrapper that
        // outer Task is never observed and surfaces as an UnobservedTaskException.
        // The inner try/catch below still handles exceptions raised inside `work`.
        _ = SafeDispatchAsync(invokeAsync, work, source);
    }

    private static async Task SafeDispatchAsync(Func<Func<Task>, Task> invokeAsync, Func<Task> work, string source)
    {
        try
        {
            await invokeAsync(async () =>
            {
                try { await work(); }
                catch (JSDisconnectedException) { /* component unmounted mid-await */ }
                catch (ObjectDisposedException) { /* renderer disposed mid-await */ }
                catch (OperationCanceledException) { /* superseded by newer dispatch */ }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[{source}] dispatch error: {ex}");
                }
            }).ConfigureAwait(false);
        }
        catch (JSDisconnectedException) { /* circuit closed before dispatch ran */ }
        catch (ObjectDisposedException) { /* renderer disposed before dispatch ran */ }
        catch (OperationCanceledException) { /* dispatcher canceled before dispatch ran (covers TaskCanceledException) */ }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{source}] dispatcher error: {ex}");
        }
    }
}
