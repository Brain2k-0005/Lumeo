using Xunit;
using Lumeo.RegistryGen;

namespace Lumeo.RegistryGen.Tests;

public class RuntimeManifestBuilderTests
{
    [Fact]
    public void Build_Covers_Core_Substrate_And_Overlay_Host()
    {
        var coreSrc = Path.Combine(FindRepoRoot(), "src", "Lumeo");
        var (files, components) = RuntimeManifestBuilder.Build(coreSrc);

        // Shared C# substrate every component compiles against.
        Assert.Contains("Internal/Cx.cs", files);
        Assert.Contains("Internal/LumeoIds.cs", files);
        Assert.Contains("Services/OverlayService.cs", files);
        Assert.Contains("Services/IComponentInteropService.cs", files);
        Assert.Contains("Services/Localization/LumeoDefaultStrings.cs", files);
        Assert.Contains("Extensions/LumeoServiceExtensions.cs", files);
        Assert.Contains("_Imports.razor", files);

        // Root-level shared enums every component references.
        Assert.Contains("Size.cs", files);
        Assert.Contains("Side.cs", files);
        Assert.Contains("Orientation.cs", files);

        // Shared event-args (Dialog/Sheet/Drawer OnBeforeClose) — the one UI/ file the runtime needs.
        Assert.Contains("UI/Overlay/DismissEventArgs.cs", files);

        // No UI components: the service layer is decoupled (SignaturePadInit is generic), so the
        // runtime drags in no overlay host and no compile-closure components — only DismissEventArgs.
        Assert.Empty(components);
        Assert.DoesNotContain(files, f => f.StartsWith("UI/", StringComparison.Ordinal) && f != "UI/Overlay/DismissEventArgs.cs");

        // Paths are forward-slashed and sorted.
        Assert.All(files, f => Assert.DoesNotContain('\\', f));
        Assert.Equal(files.OrderBy(x => x, StringComparer.Ordinal).ToList(), files);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Lumeo.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new DirectoryNotFoundException("Lumeo.slnx not found above " + AppContext.BaseDirectory);
    }
}
