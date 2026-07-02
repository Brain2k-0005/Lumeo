using Xunit;

namespace Lumeo.Tests.Theming;

/// <summary>
/// Source-level guard for the 4.1.0 radius-token wave (consumer report: the Switch —
/// and, on audit, ~70 more spots across ~40 components — ignored the theme radius
/// because of hardcoded <c>rounded-full</c>; in a sharp theme everything squared off
/// except those elements). Thematic rounding now maps to
/// <c>rounded-[calc(var(--radius)*N)]</c>, which is pixel-identical at stock radii and
/// squares off with the theme.
///
/// This test pins the wave: <c>rounded-full</c> may only appear in the audited
/// allowlist below — components whose roundness is SEMANTIC and must survive any theme
/// (radio indicators, spinners, drag affordances, circular avatar contracts and their
/// embedded-avatar followers). Adding <c>rounded-full</c> anywhere else fails this test:
/// either use the token mapping, or — if the new circle is genuinely semantic — add the
/// file to the allowlist WITH a category comment.
/// </summary>
public class RadiusTokenGuardTests
{
    // Audited 2026-07 (radius-token wave). Keep categorized; keep sorted within category.
    private static readonly HashSet<string> AllowedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        // Selection semantics — a square radio reads as a checkbox.
        "src/Lumeo/UI/RadioGroup/RadioGroupItem.razor",
        "src/Lumeo.DataGrid/UI/DataGrid/DataGridRow.razor",

        // Loading indicators — the circle IS the indicator.
        "src/Lumeo/UI/Spinner/Spinner.razor",

        // Pick-point / drag affordances — platform-recognized circles.
        "src/Lumeo/UI/ColorPicker/ColorPicker.razor",
        "src/Lumeo/UI/Drawer/DrawerContent.razor",

        // Circular-avatar contract (Shape=Circle stays literal; Themed opts into the
        // token) and its embedded-avatar followers.
        "src/Lumeo/UI/Avatar/Avatar.razor",
        "src/Lumeo/UI/Avatar/AvatarFallback.razor",
        "src/Lumeo/UI/Avatar/AvatarGroup.razor",
        "src/Lumeo/UI/Chip/Chip.razor",
        "src/Lumeo/UI/FileUpload/FileUpload.razor",
        "src/Lumeo/UI/Kanban/KanbanCard.razor",
        "src/Lumeo/UI/Mention/Mention.razor",
        "src/Lumeo/UI/Skeleton/SkeletonCircle.razor",

        // Mirrors the circular map-marker glyph — must match the marker, not the theme.
        "src/Lumeo.Maps/UI/Map/MapLegendItem.razor",
    };

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
    public void RoundedFull_Only_Appears_In_The_Audited_Semantic_Allowlist()
    {
        var root = RepoRoot();
        var offenders = new List<string>();

        foreach (var uiDir in Directory.EnumerateDirectories(Path.Combine(root, "src"))
                     .Select(p => Path.Combine(p, "UI"))
                     .Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(uiDir, "*.razor", SearchOption.AllDirectories))
            {
                if (!File.ReadAllText(file).Contains("rounded-full")) continue;
                var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (!AllowedFiles.Contains(rel))
                    offenders.Add(rel);
            }
        }

        Assert.True(offenders.Count == 0,
            "rounded-full outside the audited semantic allowlist (use rounded-[calc(var(--radius)*N)] " +
            "for thematic rounding, or extend the allowlist with a category comment): " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void Every_Allowlisted_File_Still_Exists_And_Still_Uses_RoundedFull()
    {
        // Keeps the allowlist honest — entries whose file was deleted/renamed or no
        // longer contains rounded-full must be pruned, or the list rots into noise.
        var root = RepoRoot();
        var stale = AllowedFiles
            .Where(rel =>
            {
                var abs = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                return !File.Exists(abs) || !File.ReadAllText(abs).Contains("rounded-full");
            })
            .ToList();

        Assert.True(stale.Count == 0, "stale allowlist entries (prune them): " + string.Join(", ", stale));
    }
}
