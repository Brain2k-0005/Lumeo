using Microsoft.AspNetCore.Components;

namespace Lumeo.Services;

/// <summary>
/// Provides methods to show, dismiss, and update toast notifications.
/// Inject this interface in consumers to enable mocking in tests.
/// </summary>
public interface IToastService
{
    event Action<ToastMessage>? OnShow;
    event Action<string>? OnDismiss;
    event Action<string, ToastOptions>? OnUpdate;

    string Show(string title, string? description = null, ToastVariant variant = ToastVariant.Default);
    string Show(ToastOptions options);
    string Show(RenderFragment content, ToastVariant variant = ToastVariant.Default, int? duration = null);
    string Success(string title, string? description = null);
    string Error(string title, string? description = null);
    string Warning(string title, string? description = null);
    string Info(string title, string? description = null);
    void Dismiss(string toastId);
    void DismissAll();
    void Update(string toastId, ToastOptions options);
    Task<string> Promise<T>(
        Func<Task<T>> action,
        ToastOptions loading,
        Func<T, ToastOptions> success,
        Func<Exception, ToastOptions> error);
}
