using Xunit;
using Lumeo.RegistryGen;

namespace Lumeo.RegistryGen.Tests;

public class RazorParameterScannerTests
{
    private static string WriteTempRazor(string contents)
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumeo-rg-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "TestComponent.razor");
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public void Extracts_basic_parameters_with_defaults()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
<button>@ChildContent</button>
@code {
    [Parameter] public string? Class { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public int Count { get; set; } = 42;
    [Parameter] public RenderFragment? ChildContent { get; set; }
}");
        var s = RazorParameterScanner.Scan(path);
        Assert.False(s.ParseFailed, s.ParseError);
        Assert.Equal("Lumeo", s.Namespace);
        Assert.Equal(4, s.Parameters.Length);
        var count = s.Parameters.Single(p => p.Name == "Count");
        Assert.Equal("int", count.Type);
        Assert.Equal("42", count.Default);
        Assert.False(count.IsCascading);
    }

    [Fact]
    public void Detects_capture_unmatched_and_cascading()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    [CascadingParameter] public string? Theme { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }
}");
        var s = RazorParameterScanner.Scan(path);
        Assert.False(s.ParseFailed);
        var theme = s.Parameters.Single(p => p.Name == "Theme");
        Assert.True(theme.IsCascading);
        var add = s.Parameters.Single(p => p.Name == "AdditionalAttributes");
        Assert.True(add.CaptureUnmatched);
    }

    [Fact]
    public void Surfaces_event_callbacks_separately()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
}");
        var s = RazorParameterScanner.Scan(path);
        Assert.Equal(2, s.Events.Length);
        Assert.Contains(s.Events, e => e.Name == "OpenChanged" && e.Type.StartsWith("EventCallback"));
        Assert.Contains(s.Events, e => e.Name == "OnClick");
    }

    [Fact]
    public void Captures_nested_enums_and_records()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    [Parameter] public Variant V { get; set; }

    public enum Variant { Default, Outline, Ghost }

    public record Ctx(string Id, bool IsOpen);
}");
        var s = RazorParameterScanner.Scan(path);
        Assert.Single(s.Enums);
        Assert.Equal("Variant", s.Enums[0].Name);
        Assert.Equal(new[] { "Default", "Outline", "Ghost" }, s.Enums[0].Values);
        Assert.Single(s.Records);
        Assert.Equal("Ctx", s.Records[0].Name);
        Assert.Contains("string Id", s.Records[0].Signature);
    }

    [Fact]
    public void Extracts_xml_doc_summary_into_description()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    /// <summary>Visual variant of the button.</summary>
    [Parameter] public string Variant { get; set; } = ""Default"";
}");
        var s = RazorParameterScanner.Scan(path);
        var v = Assert.Single(s.Parameters);
        Assert.Equal("Visual variant of the button.", v.Description);
    }

    [Fact]
    public void Inherits_and_implements_directives_are_captured()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@inherits LayoutComponentBase
@implements IAsyncDisposable
@implements IDisposable
@code {
    [Parameter] public string? Class { get; set; }
}");
        var s = RazorParameterScanner.Scan(path);
        Assert.Equal("LayoutComponentBase", s.InheritsFrom);
        Assert.Contains("IAsyncDisposable", s.Implements);
        Assert.Contains("IDisposable", s.Implements);
    }

    [Fact]
    public void Markup_only_razor_is_not_a_failure()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
<div>just markup</div>");
        var s = RazorParameterScanner.Scan(path);
        Assert.False(s.ParseFailed);
        Assert.Empty(s.Parameters);
    }
}
