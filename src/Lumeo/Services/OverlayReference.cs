using Lumeo.Services;

namespace Lumeo;

public sealed class OverlayReference
{
    private readonly OverlayService _service;

    public string Id { get; }
    public OverlayParameters? Parameters { get; }

    internal OverlayReference(string id, OverlayParameters? parameters, OverlayService service)
    {
        Id = id;
        Parameters = parameters;
        _service = service;
    }

    public void Close(object? result = null) => _service.Close(Id, result);

    public void Cancel() => _service.Cancel(Id);
}
