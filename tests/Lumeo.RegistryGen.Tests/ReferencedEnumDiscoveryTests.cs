using System.Text.Json;
using Xunit;
using Lumeo.RegistryGen;

namespace Lumeo.RegistryGen.Tests;

/// <summary>
/// Codex P3 — an enum a component references via a <c>[Parameter]</c> but whose
/// declaration lives in a standalone <c>.cs</c> file (e.g. <c>MenuItemVariant.cs</c>
/// at the <c>src/Lumeo</c> root, shared by DropdownMenu/ContextMenu/Menubar) must
/// still surface in that component's <c>api.enums</c>. The nested-@code enum
/// discovery in <see cref="RazorParameterScanner"/> only sees enums declared inside
/// a component's own @code block, so <see cref="ComponentsApiEmitter"/> resolves the
/// rest from a project-wide index built off the package src roots.
/// </summary>
public class ReferencedEnumDiscoveryTests : IDisposable
{
    private readonly string _root;
    private readonly string _uiRoot;

    public ReferencedEnumDiscoveryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lumeo-rg-enum", Guid.NewGuid().ToString("N"));
        var srcLumeo = Path.Combine(_root, "src", "Lumeo");
        _uiRoot = Path.Combine(srcLumeo, "UI");

        // Standalone enum file at the package src ROOT — the shape RazorParameterScanner
        // can never see (it is not nested in any component's @code block).
        Directory.CreateDirectory(srcLumeo);
        File.WriteAllText(Path.Combine(srcLumeo, "WidgetVariant.cs"), @"namespace Lumeo;

/// <summary>Visual variant for a Widget.</summary>
public enum WidgetVariant
{
    /// <summary>The default look.</summary>
    Alpha,
    /// <summary>The alternate look.</summary>
    Beta,
}
");

        // Component that references the standalone enum from BOTH its root file and a
        // sub-component (proves sub-component parameters are scanned too).
        var widgetDir = Path.Combine(_uiRoot, "Widget");
        Directory.CreateDirectory(widgetDir);
        File.WriteAllText(Path.Combine(widgetDir, "Widget.razor"), @"@namespace Lumeo
<div></div>
@code {
    [Parameter] public WidgetVariant Variant { get; set; } = WidgetVariant.Alpha;
}");
        File.WriteAllText(Path.Combine(widgetDir, "WidgetItem.razor"), @"@namespace Lumeo
<span></span>
@code {
    [Parameter] public WidgetVariant Variant { get; set; }
}");

        // A component that references NO project enum — proves the discovery is
        // targeted (only enums a component actually references get added).
        var plainDir = Path.Combine(_uiRoot, "Plain");
        Directory.CreateDirectory(plainDir);
        File.WriteAllText(Path.Combine(plainDir, "Plain.razor"), @"@namespace Lumeo
<div></div>
@code {
    [Parameter] public bool Disabled { get; set; }
}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    private JsonElement EmitAndRead()
    {
        var outPath = Path.Combine(_root, "components-api.json");
        // repoRoot: null → skip the service-layer scan / example extraction (those
        // need the real repo). The enum index is built off uiRoots, independent of
        // repoRoot, so the discovery under test runs fully.
        var rc = ComponentsApiEmitter.Emit(
            outputPath: outPath,
            componentDirs: new[]
            {
                Path.Combine(_uiRoot, "Plain"),
                Path.Combine(_uiRoot, "Widget"),
            },
            uiRoots: new[] { _uiRoot },
            metaResolver: name => new ComponentsApiEmitter.ComponentMeta(
                name, "Test", null, name + " component.", "Lumeo",
                Array.Empty<string>(), Array.Empty<string>()),
            logger: TextWriter.Null,
            version: "0.0.0-test",
            repoRoot: null);
        Assert.True(rc >= 0);

        using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Referenced_Standalone_Enum_Is_Surfaced_With_Values()
    {
        var root = EmitAndRead();
        var widget = root.GetProperty("components").GetProperty("Widget");

        var enums = widget.GetProperty("enums");
        Assert.Equal(JsonValueKind.Array, enums.ValueKind);

        var variant = enums.EnumerateArray()
            .Single(e => e.GetProperty("name").GetString() == "WidgetVariant");

        var values = variant.GetProperty("values").EnumerateArray().Select(v => v.GetString()).ToArray();
        Assert.Equal(new[] { "Alpha", "Beta" }, values);
        // The enum's own XML-doc summary rides along, not just its name.
        Assert.Equal("Visual variant for a Widget.", variant.GetProperty("description").GetString());
    }

    [Fact]
    public void Referenced_Enum_Is_Added_Once_Not_Per_Referencing_File()
    {
        var root = EmitAndRead();
        var enums = root.GetProperty("components").GetProperty("Widget").GetProperty("enums");

        // Both Widget.razor and WidgetItem.razor take a WidgetVariant parameter, but
        // the resolved enum is de-duplicated to a single entry.
        var count = enums.EnumerateArray().Count(e => e.GetProperty("name").GetString() == "WidgetVariant");
        Assert.Equal(1, count);
    }

    [Fact]
    public void Component_Referencing_No_Project_Enum_Gets_No_Enums()
    {
        var root = EmitAndRead();
        var enums = root.GetProperty("components").GetProperty("Plain").GetProperty("enums");
        Assert.Empty(enums.EnumerateArray());
    }
}
