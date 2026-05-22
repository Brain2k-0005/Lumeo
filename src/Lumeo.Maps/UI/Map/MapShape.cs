namespace Lumeo;

/// <summary>
/// JSON-serializable payload describing one shape on the map. Used by Map.razor
/// to send polylines, polygons, circles, arcs (and GeoJSON layers) over to
/// map.js. Property names match the camelCase shape `createShape` reads in JS.
/// </summary>
internal sealed class MapShape
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
    public string? Tooltip { get; set; }
    public string? PopupHtml { get; set; }

    internal sealed class LatLon
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    internal sealed class LatLonBounds
    {
        public double South { get; set; }
        public double West { get; set; }
        public double North { get; set; }
        public double East { get; set; }
    }
}
