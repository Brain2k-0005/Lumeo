using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumeo;

/// <summary>Stroke/fill style for a <see cref="ChartCanvasCommand"/>. Colors are
/// pre-resolved to concrete values by <c>resolveThemeColors</c> before a command
/// list is sent to JS — Canvas 2D cannot consume a live CSS custom property the
/// way SVG can (spec §2.5).</summary>
public sealed record ChartCanvasStyle(
    [property: JsonPropertyName("color")] string? Color = null,
    [property: JsonPropertyName("width")] double? Width = null);

/// <summary>
/// One imperative Canvas 2D draw instruction (spec Appendix A's
/// <c>canvasDraw</c> contract). C# computes 100% of the geometry; JS is a thin
/// executor with zero decisions — <c>op</c> maps 1:1 to a CanvasRenderingContext2D
/// method (<c>moveTo</c>/<c>lineTo</c>/<c>rect</c>/<c>arc</c>/<c>stroke</c>/
/// <c>fill</c>/<c>beginPath</c>/<c>closePath</c>/<c>clearRect</c>).
/// </summary>
public sealed record ChartCanvasCommand(
    [property: JsonPropertyName("op")] string Op,
    [property: JsonPropertyName("args")] double[]? Args = null,
    [property: JsonPropertyName("style")] ChartCanvasStyle? Style = null);

/// <summary>Serializes a Canvas fallback command list to the JSON string the
/// <c>chart-interop.js</c> <c>canvasDraw</c> call expects.</summary>
public static class ChartCanvasCommandBuilder
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string ToJson(IReadOnlyList<ChartCanvasCommand> commands) =>
        JsonSerializer.Serialize(commands, Options);
}
