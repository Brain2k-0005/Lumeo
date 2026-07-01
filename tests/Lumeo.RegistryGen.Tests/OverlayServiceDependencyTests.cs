using System.Text.Json;
using Xunit;

namespace Lumeo.RegistryGen.Tests;

/// <summary>
/// Codex P2 — ConfirmButton drives overlays IMPERATIVELY via IOverlayService
/// (Overlay.ShowAlertDialogAsync), so it never renders &lt;OverlayProvider&gt; in its own
/// markup and the &lt;Tag&gt; dependency scan in Program.cs can't see that it needs the
/// overlay host mounted somewhere in the tree. Without a declared dependency on `overlay`,
/// `lumeo eject` (which vendors only a component's DECLARED dependency closure) never
/// vendors OverlayProvider — a project with only ConfirmButton installed loses the host
/// entirely once the Lumeo package is stripped, and ShowAlertDialogAsync's Task never
/// completes. Program.cs now adds an `overlay` dependency for any component whose source
/// references IOverlayService, mirroring the existing FormFieldContext -&gt; form rule.
/// </summary>
public class OverlayServiceDependencyTests
{
    [Fact]
    public void ConfirmButton_Depends_On_The_Overlay_Host()
    {
        var registryPath = Path.Combine(FindRepoRoot(), "src", "Lumeo", "registry", "registry.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(registryPath));
        var confirmButton = doc.RootElement.GetProperty("components").GetProperty("confirm-button");
        var deps = confirmButton.GetProperty("dependencies").EnumerateArray()
            .Select(e => e.GetString()).ToList();

        Assert.Contains("overlay", deps);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Lumeo.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new DirectoryNotFoundException("Lumeo.slnx not found above " + AppContext.BaseDirectory);
    }
}
