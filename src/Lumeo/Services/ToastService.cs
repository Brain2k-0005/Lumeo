using Microsoft.AspNetCore.Components;

namespace Lumeo.Services;

public sealed class ToastService : IToastService
{
    public event Action<ToastMessage>? OnShow;
    public event Action<string>? OnDismiss;
    public event Action<string, ToastOptions>? OnUpdate;

    public string Show(string title, string? description = null, ToastVariant variant = ToastVariant.Default)
    {
        var options = new ToastOptions { Title = title, Description = description, Variant = variant };
        return Show(options);
    }

    public string Show(ToastOptions options)
    {
        var id = Guid.NewGuid().ToString("N");
        var message = new ToastMessage(id, options);
        OnShow?.Invoke(message);
        return id;
    }

    public string Show(RenderFragment content, ToastVariant variant = ToastVariant.Default, int? duration = null)
    {
        return Show(new ToastOptions { CustomContent = content, Variant = variant, Duration = duration });
    }

    public string Success(string title, string? description = null)
        => Show(title, description, ToastVariant.Success);

    public string Error(string title, string? description = null)
        => Show(title, description, ToastVariant.Destructive);

    public string Warning(string title, string? description = null)
        => Show(title, description, ToastVariant.Warning);

    public string Info(string title, string? description = null)
        => Show(title, description, ToastVariant.Info);

    public void Dismiss(string toastId) => OnDismiss?.Invoke(toastId);

    public void DismissAll() => OnDismiss?.Invoke("*");

    public void Update(string toastId, ToastOptions options) => OnUpdate?.Invoke(toastId, options);

    public async Task<string> Promise<T>(
        Func<Task<T>> action,
        ToastOptions loading,
        Func<T, ToastOptions> success,
        Func<Exception, ToastOptions> error)
    {
        var id = Show(loading with { Duration = 0 });
        try
        {
            var result = await action();
            Update(id, success(result));
            return id;
        }
        catch (Exception ex)
        {
            Update(id, error(ex));
            return id;
        }
    }
}

public record ToastOptions
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public ToastVariant Variant { get; init; } = ToastVariant.Default;
    public int? Duration { get; init; }
    public RenderFragment? CustomContent { get; init; }
    public string? ActionLabel { get; init; }
    public Action? OnAction { get; init; }
    public Action? OnDismiss { get; init; }
    public bool Dismissible { get; init; } = true;
    public string? Class { get; init; }
}

public record ToastMessage(string Id, ToastOptions Options)
{
    public string Title => Options.Title ?? "";
    public string? Description => Options.Description;
    public ToastVariant Variant => Options.Variant;
}

public enum ToastVariant { Default, Success, Destructive, Warning, Info }
