using Xunit;

namespace Lumeo.Tests.Packaging;

/// <summary>
/// LU-17 (field report against Lumeo 5.11.0): Directory.Build.targets used to pack the
/// REPO-ROOT README.md into every project that opted in via
/// <c>&lt;PackageReadmeFile&gt;README.md&lt;/PackageReadmeFile&gt;</c> — satellites included.
/// So Lumeo.Flow's nupkg (and every other satellite's) shipped the generic core-library
/// README with no mention of its own component, nuget.org showing zero satellite-specific
/// description.
///
/// The fix (Directory.Build.targets) prefers a project-local README.md, next to the
/// .csproj, when one exists. This test pins the OTHER half of that fix: every project
/// under <c>src/</c> that opts into PackageReadmeFile actually HAS a project-local
/// README.md — so a future satellite that copies an existing .csproj (and its
/// PackageReadmeFile line) but forgets to add its own README.md silently falls back to
/// the generic root one, undetected, exactly like Lumeo.Flow did.
/// </summary>
public class SatelliteReadmeGuardTests
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
    public void Every_Project_Opted_Into_PackageReadmeFile_Has_Its_Own_ReadmeMd()
    {
        var root = RepoRoot();
        var src = Path.Combine(root, "src");
        var missing = new List<string>();

        foreach (var csproj in Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, csproj).Replace('\\', '/');
            if (rel.Contains("/obj/") || rel.Contains("/bin/")) continue;

            var text = File.ReadAllText(csproj);
            if (!text.Contains("<PackageReadmeFile>README.md</PackageReadmeFile>", StringComparison.Ordinal))
                continue;

            // The core Lumeo package (src/Lumeo) IS the repo-root project — its own
            // README.md lives at the repo root, one level up from src/Lumeo. Every other
            // opted-in project ("satellite") must carry its own next to its .csproj.
            var projectDir = Path.GetDirectoryName(csproj)!;
            var isCoreLumeoProject = Path.GetFileName(projectDir) == "Lumeo";
            if (isCoreLumeoProject) continue;

            var ownReadme = Path.Combine(projectDir, "README.md");
            if (!File.Exists(ownReadme)) missing.Add(rel);
        }

        Assert.True(missing.Count == 0,
            "These projects opt into PackageReadmeFile=README.md but have no project-local " +
            "README.md next to their .csproj, so Directory.Build.targets falls back to the " +
            "generic repo-root README (LU-17): " + string.Join(", ", missing));
    }
}
