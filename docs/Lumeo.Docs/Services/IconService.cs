namespace Lumeo.Docs.Services;

public class IconService
{
    public string ActiveLibrary { get; private set; } = "lucide";
    public event Action? OnChange;

    public void SetLibrary(string library)
    {
        if (ActiveLibrary == library) return;
        ActiveLibrary = library;
        OnChange?.Invoke();
    }
}
