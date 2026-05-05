using System.Text.Json;
using Xunit;

namespace Lumeo.RegistryGen.Tests;

/// <summary>
/// Verifies that <see cref="PerComponentEnricher.Enrich"/> populates each of
/// the fields the LLM-facing per-component JSON contract promises. Each test
/// builds a self-contained on-disk fixture (component dir + matching api block)
/// and asserts the resulting payload contains the expected enriched values.
/// </summary>
public class PerComponentEnricherTests : IDisposable
{
    private readonly string _root;

    public PerComponentEnricherTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lumeo-pce-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    private string WriteComponentFile(string componentName, string fileName, string content)
    {
        var dir = Path.Combine(_root, "src", "Lumeo", "UI", componentName);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static JsonElement Api(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void Adds_docsUrl_and_sourceUrl_for_every_file()
    {
        WriteComponentFile("Sheet", "Sheet.razor", "@namespace Lumeo\n<div></div>\n@code { [Parameter] public RenderFragment? ChildContent { get; set; } }");
        var entry = new Dictionary<string, object?>
        {
            ["files"] = new[] { "UI/Sheet/Sheet.razor" },
            ["nugetPackage"] = "Lumeo",
            ["category"] = "Overlay",
            ["description"] = "A sheet.",
        };
        var apiBlock = Api("""{ "parameters": [{"name":"ChildContent","type":"RenderFragment?","isCascading":false}] }""");

        PerComponentEnricher.Enrich(entry, "sheet", "Sheet", _root, apiBlock,
            new HashSet<string> { "Sheet" });

        Assert.Equal("https://lumeo.nativ.sh/components/sheet", entry["docsUrl"]);
        var urls = Assert.IsType<List<Dictionary<string, object?>>>(entry["sourceUrl"]);
        Assert.Single(urls);
        Assert.Equal("https://github.com/Brain2k-0005/Lumeo/tree/master/src/Lumeo/UI/Sheet/Sheet.razor",
            urls[0]["url"]);
    }

    [Fact]
    public void Source_array_contains_actual_file_content()
    {
        WriteComponentFile("Button", "Button.razor", "@namespace Lumeo\n<button>@ChildContent</button>");
        var entry = new Dictionary<string, object?>
        {
            ["files"] = new[] { "UI/Button/Button.razor" },
            ["nugetPackage"] = "Lumeo",
            ["category"] = "Forms",
            ["description"] = "Button.",
        };
        var apiBlock = Api("""{ "parameters": [] }""");

        PerComponentEnricher.Enrich(entry, "button", "Button", _root, apiBlock,
            new HashSet<string> { "Button" });

        var src = Assert.IsType<List<Dictionary<string, object?>>>(entry["source"]);
        Assert.Single(src);
        Assert.Contains("<button>", src[0]["content"]?.ToString());
    }

    [Fact]
    public void Slots_extracted_from_RenderFragment_parameters()
    {
        var entry = new Dictionary<string, object?>
        {
            ["files"] = new string[0],
            ["nugetPackage"] = "Lumeo",
            ["category"] = "Overlay",
            ["description"] = "Dialog.",
        };
        var apiBlock = Api("""
        {
          "parameters": [
            {"name":"ChildContent","type":"RenderFragment?","isCascading":false},
            {"name":"Title","type":"string","isCascading":false},
            {"name":"IconContent","type":"RenderFragment","isCascading":false}
          ]
        }
        """);

        PerComponentEnricher.Enrich(entry, "dialog", "Dialog", _root, apiBlock,
            new HashSet<string> { "Dialog" });

        var slots = Assert.IsType<List<Dictionary<string, object?>>>(entry["slots"]);
        Assert.Equal(2, slots.Count);
        Assert.Contains(slots, s => s["name"]?.ToString() == "ChildContent");
        Assert.Contains(slots, s => s["name"]?.ToString() == "IconContent");
    }

    [Fact]
    public void Service_dependencies_extracted_from_inject_directives()
    {
        WriteComponentFile("Sheet", "SheetContent.razor", """
@namespace Lumeo
@inject ComponentInteropService Interop
@inject Lumeo.Services.Localization.ILumeoLocalizer L
<div></div>
""");
        var entry = new Dictionary<string, object?>
        {
            ["files"] = new[] { "UI/Sheet/SheetContent.razor" },
            ["nugetPackage"] = "Lumeo",
            ["category"] = "Overlay",
            ["description"] = "Sheet content.",
        };
        var apiBlock = Api("""{ "parameters": [] }""");

        PerComponentEnricher.Enrich(entry, "sheet", "Sheet", _root, apiBlock,
            new HashSet<string> { "Sheet" });

        var services = Assert.IsType<List<Dictionary<string, object?>>>(entry["serviceDependencies"]);
        Assert.Equal(2, services.Count);
        Assert.Contains(services, s => s["service"]?.ToString() == "ComponentInteropService");
        Assert.Contains(services, s => s["service"]?.ToString() == "ILumeoLocalizer"
            && s["namespace"]?.ToString() == "Lumeo.Services.Localization");
    }

    [Fact]
    public void Cascading_dependencies_promoted_from_api_block()
    {
        var entry = new Dictionary<string, object?>
        {
            ["files"] = new string[0],
            ["nugetPackage"] = "Lumeo",
            ["category"] = "Overlay",
            ["description"] = "Sheet trigger.",
        };
        var apiBlock = Api("""
        {
          "parameters": [
            {"name":"Context","type":"Sheet.SheetContext","isCascading":true},
            {"name":"Shell","type":"OverlayShellMarker?","isCascading":true},
            {"name":"Class","type":"string?","isCascading":false}
          ]
        }
        """);

        PerComponentEnricher.Enrich(entry, "sheet", "Sheet", _root, apiBlock,
            new HashSet<string> { "Sheet" });

        var cascades = Assert.IsType<List<Dictionary<string, object?>>>(entry["cascadingDependencies"]);
        Assert.Equal(2, cascades.Count);
        var ctx = cascades.Single(c => c["name"]?.ToString() == "Context");
        Assert.Equal(true, ctx["required"]);
        var shell = cascades.Single(c => c["name"]?.ToString() == "Shell");
        Assert.Equal(false, shell["required"]);
    }

    [Fact]
    public void Related_components_include_subcomponents_and_internal_uses()
    {
        WriteComponentFile("Sheet", "Sheet.razor", "@namespace Lumeo\n<Stack><Button /></Stack>");
        WriteComponentFile("Sheet", "SheetContent.razor", "@namespace Lumeo\n<div></div>");
        var entry = new Dictionary<string, object?>
        {
            ["files"] = new[] { "UI/Sheet/Sheet.razor", "UI/Sheet/SheetContent.razor" },
            ["nugetPackage"] = "Lumeo",
            ["category"] = "Overlay",
            ["description"] = "Sheet.",
        };
        var apiBlock = Api("""{ "parameters": [] }""");

        // Stack and Button are known; Random is not.
        PerComponentEnricher.Enrich(entry, "sheet", "Sheet", _root, apiBlock,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sheet", "Stack", "Button" });

        var related = Assert.IsType<List<Dictionary<string, object?>>>(entry["relatedComponents"]);
        Assert.Contains(related, r => r["name"]?.ToString() == "SheetContent" && r["reason"]?.ToString() == "sub-component");
        Assert.Contains(related, r => r["name"]?.ToString() == "Stack" && r["reason"]?.ToString() == "used internally");
        Assert.Contains(related, r => r["name"]?.ToString() == "Button" && r["reason"]?.ToString() == "used internally");
    }

    [Fact]
    public void Keyboard_interactions_captured_from_KeyEventArgs_checks()
    {
        WriteComponentFile("Sheet", "SheetContent.razor", """
@namespace Lumeo
<div @onkeydown="HandleKeyDown"></div>
@code {
    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") await Close();
        if (e.Key == "Tab") return;
    }
    private Task Close() => Task.CompletedTask;
}
""");
        var entry = new Dictionary<string, object?>
        {
            ["files"] = new[] { "UI/Sheet/SheetContent.razor" },
            ["nugetPackage"] = "Lumeo",
            ["category"] = "Overlay",
            ["description"] = "Sheet content.",
        };
        var apiBlock = Api("""{ "parameters": [] }""");

        PerComponentEnricher.Enrich(entry, "sheet", "Sheet", _root, apiBlock,
            new HashSet<string> { "Sheet" });

        var kb = Assert.IsType<List<Dictionary<string, object?>>>(entry["keyboardInteractions"]);
        Assert.Contains(kb, k => k["key"]?.ToString() == "Escape" && k["action"]?.ToString()!.Contains("HandleKeyDown") == true);
        Assert.Contains(kb, k => k["key"]?.ToString() == "Tab");
    }

    [Fact]
    public void MdSummary_is_populated_with_parameter_table()
    {
        WriteComponentFile("Button", "Button.razor", "@namespace Lumeo\n<button></button>");
        var entry = new Dictionary<string, object?>
        {
            ["files"] = new[] { "UI/Button/Button.razor" },
            ["nugetPackage"] = "Lumeo",
            ["category"] = "Forms",
            ["description"] = "A button.",
            ["cssVars"] = new[] { "--color-primary" },
        };
        var apiBlock = Api("""
        {
          "parameters": [
            {"name":"Variant","type":"ButtonVariant","default":"ButtonVariant.Default","isCascading":false}
          ],
          "events": []
        }
        """);

        PerComponentEnricher.Enrich(entry, "button", "Button", _root, apiBlock,
            new HashSet<string> { "Button" });

        var md = entry["mdSummary"]?.ToString() ?? "";
        Assert.Contains("# Button", md);
        Assert.Contains("A button.", md);
        Assert.Contains("**Package:** `Lumeo`", md);
        Assert.Contains("https://lumeo.nativ.sh/components/button", md);
        Assert.Contains("| Button | Variant | `ButtonVariant`", md);
        Assert.Contains("--color-primary", md);
    }
}
