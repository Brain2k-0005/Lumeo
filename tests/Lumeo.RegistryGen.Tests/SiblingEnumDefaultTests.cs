using System.Text.Json;
using Xunit;
using Lumeo.RegistryGen;

namespace Lumeo.RegistryGen.Tests;

/// <summary>
/// Codex P2, PR #358 round 3 — end-to-end via <see cref="ComponentsApiEmitter"/>: a
/// sub-component parameter typed through its PARENT's nested enum (e.g.
/// <c>DataTableSortableHeader.SortDirection</c>, typed as
/// <c>DataTable&lt;object&gt;.SortDirection</c> with no initializer) must still resolve its
/// CLR-implicit default. Before this fix, <see cref="RazorParameterScanner"/> only saw enums
/// declared in the SAME file as the parameter, so the emitted registry showed
/// <c>default: null</c> even though the removed hand-written table showed the correct
/// zero-valued member.
/// </summary>
public class SiblingEnumDefaultTests : IDisposable
{
    private readonly string _root;
    private readonly string _uiRoot;

    public SiblingEnumDefaultTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lumeo-rg-sibling-enum", Guid.NewGuid().ToString("N"));
        _uiRoot = Path.Combine(_root, "src", "Lumeo", "UI");

        var dir = Path.Combine(_uiRoot, "Grid");
        Directory.CreateDirectory(dir);

        // Root component declares the nested enum — mirrors DataTable's
        // `public enum SortDirection { None, Ascending, Descending }`.
        File.WriteAllText(Path.Combine(dir, "Grid.razor"), @"@namespace Lumeo
<div></div>
@code {
    [Parameter] public SortDirection SortDir { get; set; }

    public enum SortDirection { None, Ascending, Descending }
}");

        // Sub-component references the PARENT's enum, qualified through the generic owner
        // type, with NO local declaration and NO initializer — the exact DataTableSortableHeader
        // shape.
        File.WriteAllText(Path.Combine(dir, "GridSortableHeader.razor"), @"@namespace Lumeo
<th></th>
@code {
    [Parameter] public Grid<object>.SortDirection SortDirection { get; set; }
}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public void SubComponent_Parameter_Typed_Through_Parent_Enum_Gets_Implicit_Default()
    {
        var outPath = Path.Combine(_root, "components-api.json");
        var rc = ComponentsApiEmitter.Emit(
            outputPath: outPath,
            componentDirs: new[] { Path.Combine(_uiRoot, "Grid") },
            uiRoots: new[] { _uiRoot },
            metaResolver: name => new ComponentsApiEmitter.ComponentMeta(
                name, "Test", null, name + " component.", "Lumeo",
                Array.Empty<string>(), Array.Empty<string>()),
            logger: TextWriter.Null,
            version: "0.0.0-test",
            repoRoot: null);
        Assert.True(rc >= 0);

        using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
        var grid = doc.RootElement.GetProperty("components").GetProperty("Grid");

        // Sanity: the root component's own [Parameter] resolves via the plain (same-file) path.
        var rootParam = grid.GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "SortDir");
        Assert.Equal("None", rootParam.GetProperty("default").GetString());

        // The bug: the sub-component's parameter, typed through the parent's QUALIFIED
        // nested enum, must resolve the SAME implicit default — not null/"—".
        var subHeader = grid.GetProperty("subComponents").GetProperty("GridSortableHeader");
        var subParam = subHeader.GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "SortDirection");
        Assert.Equal("None", subParam.GetProperty("default").GetString());
    }
}
