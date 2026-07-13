namespace Lumeo.Services;

/// <summary>Bounding-rect data returned from JS interop.</summary>
public record ElementRect(double X, double Y, double Width, double Height, double BorderRadius = 0)
{
    // Trim safety: JSRuntime's reflection-based serializer must never bind the positional
    // ctor — the trimmer strips its parameter names ("ConstructorContainsNullParameterNames",
    // crashes the component under a trimmed publish). With this parameterless ctor STJ
    // uses property-based (de)serialization instead. Do not remove.
    public ElementRect() : this(0, 0, 0, 0) { }
}

/// <summary>Viewport dimensions returned from JS interop.</summary>
public record ViewportSize(double Width, double Height)
{
    // Trim safety: see ElementRect's parameterless ctor above. Do not remove.
    public ViewportSize() : this(0, 0) { }
}

/// <summary>
/// A point relative to a host element's top-left, returned from JS interop.
/// Used by TouchRipple to centre a ripple on the pointer even when the event
/// originated on a nested child.
/// </summary>
public record RipplePoint(double X, double Y)
{
    // Trim safety: see ElementRect's parameterless ctor above. Do not remove.
    public RipplePoint() : this(0, 0) { }
}

/// <summary>
/// Snapshot of an HTMLMediaElement's live <c>duration</c> and
/// <c>currentTime</c>, returned from JS interop. Both values are coerced
/// to 0 when the element exposes NaN (pre-metadata) or Infinity (live
/// streams), so the consumer can treat them as plain finite doubles.
/// </summary>
public record MediaState(double Duration, double CurrentTime)
{
    // Trim safety: see ElementRect's parameterless ctor above. Do not remove.
    public MediaState() : this(0, 0) { }
}
