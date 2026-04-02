using Lumeo.Services;

namespace Lumeo;

internal sealed class DataGridLayoutService : IDisposable
{
    private readonly ComponentInteropService _interop;
    private System.Threading.Timer? _autoSaveTimer;
    private int _saveGeneration;
    private bool _layoutLoaded;
    private DataGridLayout? _defaultLayout;

    internal DataGridLayoutService(ComponentInteropService interop)
    {
        _interop = interop;
    }

    internal bool LayoutLoaded => _layoutLoaded;
    internal DataGridLayout? DefaultLayout { get => _defaultLayout; set => _defaultLayout = value; }

    internal async Task LoadPersistedLayoutAsync(string key, Func<DataGridLayout, Task> applyAction)
    {
        if (_layoutLoaded) return;
        try
        {
            var json = await _interop.LoadFromLocalStorage(key);
            if (!string.IsNullOrEmpty(json))
            {
                var layout = System.Text.Json.JsonSerializer.Deserialize<DataGridLayout>(json);
                if (layout is not null)
                {
                    _layoutLoaded = true;
                    await applyAction(layout);
                }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine($"[Lumeo DataGrid] Failed to load persisted layout: {ex.Message}"); }
    }

    internal async Task PersistAsync(string key, DataGridLayout layout, int? generation = null)
    {
        // If a generation is provided, only persist if it's still current
        if (generation.HasValue && generation.Value != _saveGeneration) return;
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(layout);
            await _interop.SaveToLocalStorage(key, json);
        }
        catch (Exception ex) { Console.Error.WriteLine($"[Lumeo DataGrid] Failed to save layout: {ex.Message}"); }
    }

    internal async Task ClearPersistedAsync(string key)
    {
        try { await _interop.RemoveFromLocalStorage(key); }
        catch (Exception ex) { Console.Error.WriteLine($"[Lumeo DataGrid] Failed to clear persisted layout: {ex.Message}"); }
    }

    internal async Task<List<DataGridNamedLayout>> GetPersonalLayoutsAsync(string key)
    {
        try
        {
            var json = await _interop.LoadFromLocalStorage(key);
            if (!string.IsNullOrEmpty(json))
                return System.Text.Json.JsonSerializer.Deserialize<List<DataGridNamedLayout>>(json) ?? new();
        }
        catch (Exception ex) { Console.Error.WriteLine($"[Lumeo DataGrid] Failed to load named layouts: {ex.Message}"); }
        return new();
    }

    internal async Task SaveNamedLayoutsAsync(string key, List<DataGridNamedLayout> layouts)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(layouts);
            await _interop.SaveToLocalStorage(key, json);
        }
        catch (Exception ex) { Console.Error.WriteLine($"[Lumeo DataGrid] Failed to save named layouts: {ex.Message}"); }
    }

    internal int CurrentSaveGeneration => _saveGeneration;

    internal void ScheduleAutoSave(Action persistCallback)
    {
        _autoSaveTimer?.Dispose();
        ++_saveGeneration;
        _autoSaveTimer = new System.Threading.Timer(_ =>
        {
            persistCallback();
        }, null, 500, System.Threading.Timeout.Infinite);
    }

    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
    }
}
