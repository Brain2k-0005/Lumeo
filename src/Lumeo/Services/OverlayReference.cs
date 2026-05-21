using Lumeo.Services;

namespace Lumeo;

public sealed class OverlayReference
{
    private readonly OverlayService _service;

    public string Id { get; }
    public OverlayParameters? Parameters { get; }

    /// <summary>Backdrop z-index allocated to this overlay (content sits at
    /// <c>ZIndex + 1</c>). Exposed so consumer-rendered overlays opened from
    /// inside this one can stack above without colliding.</summary>
    public int ZIndex { get; }

    internal OverlayReference(string id, OverlayParameters? parameters, OverlayService service, int zIndex)
    {
        Id = id;
        Parameters = parameters;
        _service = service;
        ZIndex = zIndex;
    }

    public void Close(object? result = null) => _service.Close(Id, result);

    public void Cancel() => _service.Cancel(Id);
}
