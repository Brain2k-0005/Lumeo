namespace Lumeo.Internal;

/// <summary>
/// Generates per-instance DOM ids using the convention <c>"{prefix}-{guidN}"</c>.
/// Centralised so the prefix scheme stays consistent and IDs are greppable
/// in browser devtools.
/// </summary>
internal static class LumeoIds
{
    public static string New(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}
