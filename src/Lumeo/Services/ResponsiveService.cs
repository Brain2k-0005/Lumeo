using Microsoft.JSInterop;

namespace Lumeo.Services;

/// <summary>
/// Default <see cref="IResponsiveService"/> implementation. Talks to the JS
/// <c>registerViewportListener</c> helper which attaches a single
/// <c>window.addEventListener('resize')</c> per circuit and pings back via
/// <see cref="OnViewportChange"/>. Resize events are debounced JS-side
/// (~100ms) so a slow drag of the browser corner doesn't flood the .NET
/// side with callbacks.
/// </summary>
public sealed class ResponsiveService : IResponsiveService
{
    private readonly IComponentInteropService _interop;
    private DotNetObjectReference<ResponsiveService>? _selfRef;
    private bool _initialised;
    private double _width;
    private double _height;
    private Breakpoint _current = Breakpoint.Md;

    public ResponsiveService(IComponentInteropService interop)
    {
        _interop = interop;
    }

    public double Width => _width;
    public double Height => _height;
    public Breakpoint Current => _current;

    public bool IsMobile => _current is Breakpoint.Xs or Breakpoint.Sm;
    public bool IsTablet => _current is Breakpoint.Md;
    public bool IsDesktop => _current is Breakpoint.Lg or Breakpoint.Xl or Breakpoint.Xxl;

    public event Action<ViewportInfo>? ViewportChanged;

    public async ValueTask EnsureInitialisedAsync()
    {
        if (_initialised) return;
        _initialised = true;
        _selfRef = DotNetObjectReference.Create(this);
        // The JS helper returns the initial size synchronously so callers don't
        // have to wait for a synthetic resize event before reading Width/Height.
        // Defensive null check: bUnit's loose-mode JSInterop returns null for
        // unmocked invokes that have a reference-type return (ViewportSize is a
        // positional record = reference type). In that case we keep the
        // service at its zero-defaults — consumers will read Width=0 until a
        // real OnViewportChange call comes in from JS.
        var initial = await _interop.RegisterViewportListener(_selfRef);
        if (initial is not null)
        {
            ApplySize(initial.Width, initial.Height, fireEvent: true);
        }
    }

    /// <summary>Invoked from JS on every (debounced) viewport resize.</summary>
    [JSInvokable]
    public void OnViewportChange(double width, double height)
    {
        ApplySize(width, height, fireEvent: true);
    }

    private void ApplySize(double width, double height, bool fireEvent)
    {
        var changed = _width != width || _height != height;
        _width = width;
        _height = height;
        var newBp = ViewportInfo.FromWidth(width);
        var bpChanged = newBp != _current;
        _current = newBp;
        // Fire the event whenever any dimension changed, not just on breakpoint
        // crossing — consumers that want fine-grained reactivity (e.g. dynamic
        // chart resize) need every resize; consumers that only care about
        // breakpoint crossings can filter inside their handler. The cost is
        // already paid by the JS debounce.
        if (changed && fireEvent)
        {
            ViewportChanged?.Invoke(new ViewportInfo(width, height, newBp));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_initialised)
        {
            try { await _interop.UnregisterViewportListener(); }
            catch (JSDisconnectedException) { }
        }
        _selfRef?.Dispose();
        _selfRef = null;
    }
}
