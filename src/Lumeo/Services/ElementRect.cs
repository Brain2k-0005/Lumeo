namespace Lumeo.Services;

/// <summary>Bounding-rect data returned from JS interop.</summary>
public record ElementRect(double X, double Y, double Width, double Height, double BorderRadius = 0);

/// <summary>Viewport dimensions returned from JS interop.</summary>
public record ViewportSize(double Width, double Height);

/// <summary>
/// Snapshot of an HTMLMediaElement's live <c>duration</c> and
/// <c>currentTime</c>, returned from JS interop. Both values are coerced
/// to 0 when the element exposes NaN (pre-metadata) or Infinity (live
/// streams), so the consumer can treat them as plain finite doubles.
/// </summary>
public record MediaState(double Duration, double CurrentTime);
