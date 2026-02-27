namespace Lumeo.Services;

public sealed class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void Show(string title, string? description = null, ToastVariant variant = ToastVariant.Default)
    {
        OnShow?.Invoke(new ToastMessage(title, description, variant));
    }

    public void Success(string title, string? description = null)
        => Show(title, description, ToastVariant.Success);

    public void Error(string title, string? description = null)
        => Show(title, description, ToastVariant.Destructive);

    public void Warning(string title, string? description = null)
        => Show(title, description, ToastVariant.Warning);

    public void Info(string title, string? description = null)
        => Show(title, description, ToastVariant.Info);
}

public record ToastMessage(string Title, string? Description, ToastVariant Variant);

public enum ToastVariant { Default, Success, Destructive, Warning, Info }
