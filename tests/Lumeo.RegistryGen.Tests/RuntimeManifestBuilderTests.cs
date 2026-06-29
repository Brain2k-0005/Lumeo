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

        // Overlay host (infrastructure).
        Assert.Contains("UI/Overlay/OverlayProvider.razor", files);
        Assert.Contains("UI/OverlayForm/OverlayForm.razor", files);
        Assert.Contains("overlay", components);
        Assert.Contains("overlay-form", components);

        // Paths are forward-slashed and de-duplicated/sorted.
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
