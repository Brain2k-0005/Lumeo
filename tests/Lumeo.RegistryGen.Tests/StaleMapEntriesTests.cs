using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Lumeo.RegistryGen;

namespace Lumeo.RegistryGen.Tests;

public class StaleMapEntriesTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "lumeo-stale-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); } catch { /* best effort */ }
    }

    private string MakeUiRoot(params string[] relativePaths)
    {
        var ui = Path.Combine(_tmp, "Lumeo.Fake", "UI");
        Directory.CreateDirectory(ui);
        foreach (var rel in relativePaths)
        {
            var full = Path.Combine(ui, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "@namespace Lumeo");
        }
        return ui;
    }

    [Fact]
    public void Sub_component_razor_file_counts_as_existing()
    {
        var ui = MakeUiRoot("Map/Map.razor", "Map/MapMarker.razor", "Map/Nested/MapArc.razor");
        var existing = StaleMapEntries.DiscoverExistingNames(new[] { ui });

        Assert.Contains("Map", existing);
        Assert.Contains("MapMarker", existing);
        Assert.Contains("mapmarker", existing); // case-insensitive, like the maps
        Assert.Contains("MapArc", existing);    // nested deeper than one level
        Assert.Empty(StaleMapEntries.Find(new[] { "Map", "MapMarker", "MapArc" }, existing));
    }

    [Fact]
    public void Top_level_directory_counts_even_without_a_same_named_file()
    {
        var ui = MakeUiRoot("Chart/ChartSeries.razor");
        var existing = StaleMapEntries.DiscoverExistingNames(new[] { ui });
        Assert.Empty(StaleMapEntries.Find(new[] { "Chart", "ChartSeries" }, existing));
    }

    [Fact]
    public void Missing_names_are_reported_sorted_and_deduplicated()
    {
        var ui = MakeUiRoot("Button/Button.razor");
        var existing = StaleMapEntries.DiscoverExistingNames(new[] { ui });

        var stale = StaleMapEntries.Find(new[] { "Meteors", "Button", "Globe", "meteors" }, existing);

        Assert.Equal(new[] { "Globe", "Meteors" }, stale);
        var message = StaleMapEntries.Format(stale);
        Assert.StartsWith("2 hand-maintained map entries", message);
    }

    [Fact]
    public void Non_razor_files_do_not_count()
    {
        var ui = MakeUiRoot("Thing/Thing.razor", "Thing/Ghost.razor.cs", "Thing/Phantom.cs");
        var existing = StaleMapEntries.DiscoverExistingNames(new[] { ui });
        Assert.DoesNotContain("Ghost", existing);
        Assert.DoesNotContain("Phantom", existing);
    }

    /// <summary>
    /// Regression guard for #535: every key in the hand-maintained maps of Program.cs must name a
    /// component that exists under some src/Lumeo*/UI tree (directory or sub-component .razor).
    /// </summary>
    [Fact]
    public void Real_catalog_maps_have_zero_stale_entries()
    {
        var root = FindRepoRoot();
        var programSrc = File.ReadAllText(Path.Combine(root, "tools", "Lumeo.RegistryGen", "Program.cs"));
        var tree = CSharpSyntaxTree.ParseText(programSrc);

        var keys = new List<string>();
        foreach (var mapName in new[] { "componentToPackage", "categoryMap", "descriptions" })
        {
            var decl = tree.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .First(v => v.Identifier.Text == mapName);
            var init = decl.Initializer!.Value switch
            {
                ObjectCreationExpressionSyntax o => o.Initializer!,
                ImplicitObjectCreationExpressionSyntax i => i.Initializer!,
                _ => throw new InvalidOperationException($"{mapName}: unexpected initializer shape")
            };
            var mapKeys = init.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .Select(a => ((a.Left as ImplicitElementAccessSyntax)!.ArgumentList.Arguments[0].Expression as LiteralExpressionSyntax)!.Token.ValueText)
                .ToList();
            Assert.NotEmpty(mapKeys); // guards against the parser silently matching nothing
            keys.AddRange(mapKeys);
        }

        var uiRoots = Directory.EnumerateDirectories(Path.Combine(root, "src"), "Lumeo*")
            .Select(d => Path.Combine(d, "UI"))
            .Where(Directory.Exists)
            .ToArray();
        Assert.NotEmpty(uiRoots);

        var stale = StaleMapEntries.Find(keys, StaleMapEntries.DiscoverExistingNames(uiRoots));

        Assert.True(stale.Length == 0,
            "Stale hand-maintained map entries in tools/Lumeo.RegistryGen/Program.cs: " + string.Join(", ", stale));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Lumeo.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Lumeo.slnx not found above test output");
    }
}
