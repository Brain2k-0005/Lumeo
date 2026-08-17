using Xunit;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Source-level guard for the FullCalendar removal: the Scheduler renders through first-party
/// Blazor views, and no third-party calendar library may return — not as a loaded asset, not as
/// a declared CDN dependency, not as a claim in published package metadata, and not as a
/// self-hosted copy on the docs site.
///
/// Deliberately NOT a blanket ban on the token, the way
/// <c>IconDependencyGuardTests</c> bans <c>Blazicons</c>. Several comments under
/// <c>src/Lumeo.Scheduler/</c> still name FullCalendar on purpose: they record why an API is
/// shaped the way it is (exclusive end timestamps, "widest bar first" lane ordering, the
/// toolbar's extraction from the wrapper). Those are statements about the past and are worth
/// keeping. What must never come back is a live dependency — so this guard checks the four
/// places one would actually appear, and a returning historical note stays legal.
/// </summary>
public class SchedulerDependencyGuardTests
{
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Lumeo.slnx")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return dir!;
    }

    private const string Banned = "fullcalendar";

    /// <summary>
    /// True when the text REFERENCES the library rather than merely naming it. Prose writes
    /// "FullCalendar"; a reference is always one of a few machine-readable forms — an npm
    /// specifier, a vendored path, or the camelCase key the CDN override map uses. Matching
    /// those instead of the bare token is what lets the deliberate historical comments
    /// (lumeo-scheduler.css's header, the API-shape notes) stay where they are.
    /// </summary>
    private static string? FindReference(string text)
    {
        string[] referenceForms =
        [
            "@fullcalendar",   // npm package specifier
            "fullcalendar-",   // vendored directory / bundle file
            "fullcalendar@",   // versioned vendored directory
            "fullCalendar",    // window.lumeoCdn.fullCalendarCore & friends (lowercase f)
        ];

        foreach (var form in referenceForms)
        {
            var i = text.IndexOf(form, StringComparison.Ordinal);
            if (i >= 0) return text[i..Math.Min(text.Length, i + 60)];
        }
        return null;
    }

    [Fact]
    public void The_Scheduler_Package_Loads_No_Calendar_Library_At_Runtime()
    {
        // wwwroot is what actually ships to the browser. A returning loader would appear here
        // long before anything else in this file noticed.
        var root = RepoRoot();
        var wwwroot = Path.Combine(root, "src", "Lumeo.Scheduler", "wwwroot");
        var offenders = Directory.EnumerateFiles(wwwroot, "*.*", SearchOption.AllDirectories)
            .Where(f => FindReference(File.ReadAllText(f)) is not null)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Lumeo.Scheduler's wwwroot must not reference a third-party calendar library. " +
            "Offending files: " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_Calendar_Library_Is_Declared_As_A_Cdn_Dependency()
    {
        // CdnDeps is what the CLI reports to anyone self-hosting or going offline; a stale entry
        // there sends them looking for an asset the library never requests.
        var root = RepoRoot();
        var cdnDeps = Path.Combine(root, "tools", "Lumeo.RegistryGen", "CdnDeps.cs");

        Assert.Null(FindReference(File.ReadAllText(cdnDeps)));
    }

    [Fact]
    public void No_Package_Description_Or_Tag_Claims_A_Calendar_Library()
    {
        // This text is published to nuget.org, so it outlives any correction made only in the repo.
        var root = RepoRoot();
        var offenders = new List<string>();

        foreach (var csproj in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj",
                                                        SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(csproj);
            foreach (var element in new[] { "Description", "PackageTags" })
            {
                var open = $"<{element}>";
                var close = $"</{element}>";
                // Every occurrence, not just the first — a csproj may declare the element more
                // than once behind different conditions, and checking one would let the others
                // keep the claim.
                for (var start = text.IndexOf(open, StringComparison.Ordinal); start >= 0;
                     start = text.IndexOf(open, start + open.Length, StringComparison.Ordinal))
                {
                    var end = text.IndexOf(close, start, StringComparison.Ordinal);
                    if (end < 0) break;

                    var value = text[(start + open.Length)..end];
                    if (value.Contains(Banned, StringComparison.OrdinalIgnoreCase))
                        offenders.Add($"{Path.GetRelativePath(root, csproj).Replace('\\', '/')} <{element}>");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Published package metadata must not claim a third-party calendar library. " +
            "Offending entries: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_Published_Component_Summary_Claims_No_Calendar_Library()
    {
        // The longest-lived surface of all, and the one the csproj check does not reach: this
        // single string is regenerated into the docs catalog, the search index, the MCP
        // registry and the AI-facing skill catalog. Restoring "wrapping FullCalendar" there
        // passed every other test in this class (Codex review, PR #425).
        var root = RepoRoot();
        var program = Path.Combine(root, "tools", "Lumeo.RegistryGen", "Program.cs");
        var text = File.ReadAllText(program);

        // EVERY ["Scheduler"] = "..." entry, not the first one found. Program.cs keeps several
        // keyed maps, and the first is the package assignment (= "Lumeo.Scheduler") — anchoring
        // on it made this test pass while the summary map still said "wrapping FullCalendar",
        // which its own disable-check caught.
        const string key = "[\"Scheduler\"] = \"";
        var entries = new List<string>();
        for (var at = text.IndexOf(key, StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(key, at + key.Length, StringComparison.Ordinal))
        {
            var end = text.IndexOf('\n', at);
            entries.Add(text[at..(end < 0 ? text.Length : end)]);
        }

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.DoesNotContain(Banned, e, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_Docs_Host_Page_Self_Hosts_No_Calendar_Library()
    {
        // The docs site vendored the five packages to keep the page free of third-party requests.
        // With the library gone the vendored copies and their importmap wiring are dead weight
        // that still ships on every page load.
        var root = RepoRoot();
        var indexHtml = Path.Combine(root, "docs", "Lumeo.Docs", "wwwroot", "index.html");

        Assert.Null(FindReference(File.ReadAllText(indexHtml)));
    }

    [Fact]
    public void No_Vendored_Copy_Of_A_Calendar_Library_Remains_On_The_Docs_Site()
    {
        var root = RepoRoot();
        var vendor = Path.Combine(root, "docs", "Lumeo.Docs", "wwwroot", "lib", "lumeo-vendor");
        if (!Directory.Exists(vendor)) return;

        var offenders = Directory.EnumerateDirectories(vendor)
            .Select(d => Path.GetFileName(d)!)
            // preact was vendored only as FullCalendar core's own rendering dependency.
            .Where(n => n.Contains(Banned, StringComparison.OrdinalIgnoreCase)
                        || n.StartsWith("preact", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Vendored directories left behind by the FullCalendar removal: " + string.Join(", ", offenders));
    }
}
