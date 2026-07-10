using Xunit;

namespace Lumeo.Tests.Icons;

/// <summary>
/// Source-level guard for the icon-decoupling wave (Lumeo.Icons / IconSource): the core library and
/// every satellite now own their icon story through the first-party <c>IconSource</c> + <c>SvgGlyph</c>
/// + <c>LumeoIcons</c> substrate. No project under <c>src/</c> may take a dependency on the third-party
/// <c>Blazicons</c> packages any more — not a <c>PackageReference</c>, not an <c>@using Blazicons</c>,
/// not a <c>Blazicons.SvgIcon</c> type reference, not a <c>&lt;Blazicon&gt;</c> render site.
///
/// This test pins that permanently: the literal string <c>Blazicons</c> must not appear in ANY
/// <c>*.razor</c> / <c>*.cs</c> / <c>*.csproj</c> under <c>src/</c>. Re-introducing it (a stray icon
/// import, a reverted render site, a copied csproj line) fails here.
///
/// Scope is <c>src/</c> ONLY — the shipped library and its satellites. (The docs and tools no
/// longer reference the packages either; the remaining mentions are deliberate exceptions:
/// THIS guard and the CLI vendor e2e must name the banned token to enforce the ban, and
/// generated <c>obj/</c> / <c>bin/</c> output plus historical changelog/migration records
/// document the decoupling.) Generated build output is excluded from the scan.
/// </summary>
public class IconDependencyGuardTests
{
    private static string RepoRoot()
    {
        // Walk up from the test assembly to the repo root (the dir containing Lumeo.slnx).
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Lumeo.slnx")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return dir!;
    }

    [Fact]
    public void Blazicons_Never_Appears_In_Any_Source_Under_Src()
    {
        var root = RepoRoot();
        var src = Path.Combine(root, "src");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (ext is not (".razor" or ".cs" or ".csproj")) continue;

            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            // Generated build output is not source — it lags the migration until the next build.
            if (rel.Contains("/obj/") || rel.Contains("/bin/")) continue;

            if (File.ReadAllText(file).Contains("Blazicons", StringComparison.Ordinal))
                offenders.Add(rel);
        }

        Assert.True(offenders.Count == 0,
            "Blazicons must not appear anywhere under src/ — the core owns its icons via IconSource / " +
            "SvgGlyph / LumeoIcons. Offending files: " + string.Join(", ", offenders));
    }
}
