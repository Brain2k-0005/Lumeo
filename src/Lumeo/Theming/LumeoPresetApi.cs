namespace Lumeo.Theming;

/// <summary>Default endpoint for the hosted preset-sharing API (Cloudflare Worker).
/// Used as a fallback when a consumer passes a preset code that can't be decoded
/// client-side (i.e. it's a server-stored ID referencing an arbitrary JSON config).
/// Consumers can override via <c>--api</c> / env var for self-hosting.</summary>
public static class LumeoPresetApi
{
    public const string BaseUrl = "https://api.lumeo.nativ.sh";
}
