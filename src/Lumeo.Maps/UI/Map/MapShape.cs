namespace Lumeo;

/// <summary>
/// JSON-serializable payload describing one shape on the map. Used by Map.razor
/// to send polylines, polygons, circles, arcs (and GeoJSON layers) over to
/// map.js. Property names match the camelCase shape `createShape` reads in JS.
/// </summary>
internal class MapShape
{
    public string Type { get; set; } = "polyline";
    public LatLon[]? Points { get; set; }
    public LatLon? Center { get; set; }
    public double? RadiusMeters { get; set; }
    public LatLon? From { get; set; }
    public LatLon? To { get; set; }
    public double? Curvature { get; set; }
    public LatLonBounds? Bounds { get; set; }
    public object? Geojson { get; set; }

    public string? Color { get; set; }
    public string? FillColor { get; set; }
    public double? Weight { get; set; }
    public double? Opacity { get; set; }
    public double? FillOpacity { get; set; }
    public string? DashArray { get; set; }

    /// <summary>Stroke color applied when the cursor hovers the shape. Null = no hover effect.</summary>
    public string? HoverColor { get; set; }
    /// <summary>Stroke width applied on hover. Null = same as <see cref="Weight"/>.</summary>
    public double? HoverWeight { get; set; }
    /// <summary>When true the shape animates its line drawing on first appear.</summary>
    public bool Animate { get; set; }

    public string? Tooltip { get; set; }
    public string? PopupHtml { get; set; }

    internal sealed class LatLon
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
        /// <summary>Per-point intensity weight used by the heatmap renderer (0–1).
        /// Ignored by polyline/polygon shapes.</summary>
        public double? Weight { get; set; }
    }

    internal sealed class LatLonBounds
    {
        public double South { get; set; }
        public double West { get; set; }
        public double North { get; set; }
        public double East { get; set; }
    }
}

/// <summary>
/// Extended shape payload for heatmap layers. Inherits MapShape so it serializes
/// cleanly with <c>Type = "heatmap"</c> and adds heatmap-specific properties.
/// </summary>
internal sealed class MapHeatmapShape : MapShape
{
    public int Radius { get; set; } = 20;
    /// <summary>Replaces the base <see cref="MapShape.Opacity"/> so heatmap
    /// opacity can be serialized as a top-level <c>opacity</c> field (the
    /// base property is <c>double?</c> already, we just ensure a default).</summary>
    public new double Opacity { get; set; } = 0.8;
    /// <summary>Optional custom color-ramp stops for the MapLibre heatmap layer.
    /// Serialized as a raw JSON array of alternating stop/color pairs.</summary>
    public object[]? ColorRamp { get; set; }
}
