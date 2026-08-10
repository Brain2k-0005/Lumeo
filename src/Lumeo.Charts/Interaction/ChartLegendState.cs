namespace Lumeo;

/// <summary>
/// Legend toggle/hover-dim state (spec §3.1) — pure Blazor, zero JS. Mirrors the
/// reference demo's <c>buildLegend()</c>: clicking a swatch toggles that
/// series' visibility (refusing to hide the LAST visible series, matching the
/// demo's own guard), hovering dims every OTHER series.
/// </summary>
internal sealed class ChartLegendState
{
    private readonly HashSet<string> _hidden = new();

    public string? HoveredKey { get; private set; }

    public IReadOnlyCollection<string> HiddenKeys => _hidden;

    public bool IsHidden(string key) => _hidden.Contains(key);

    /// <summary>Toggles <paramref name="key"/>'s visibility. Returns
    /// <c>false</c> (no-op) when toggling would hide the last remaining visible
    /// series out of <paramref name="totalSeriesCount"/>.</summary>
    public bool Toggle(string key, int totalSeriesCount)
    {
        if (_hidden.Contains(key))
        {
            _hidden.Remove(key);
            return true;
        }
        if (_hidden.Count >= totalSeriesCount - 1) return false;
        _hidden.Add(key);
        return true;
    }

    public void SetHover(string? key) => HoveredKey = key;

    /// <summary>True when some OTHER series is hovered (this one should dim).</summary>
    public bool IsDimmed(string key) => HoveredKey is not null && HoveredKey != key;
}
