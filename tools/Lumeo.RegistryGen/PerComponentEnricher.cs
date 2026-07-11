using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lumeo.RegistryGen;

/// <summary>
/// Enriches the per-component JSON payload with everything an LLM agent needs
/// to use a component without ever opening source. Mutates the entry in place.
///
/// Adds:
///   - docsUrl, sourceUrl[], source[] (with content)
///   - slots (RenderFragment params)
///   - serviceDependencies (@inject scan)
///   - cascadingDependencies (CascadingParameter promotion)
///   - relatedComponents (sub-components + cross-component refs)
///   - keyboardInteractions (heuristic regex scan)
///   - tests (test files mentioning the component)
///   - mdSummary (self-contained Markdown reference)
/// </summary>
public static class PerComponentEnricher
{
    private const string GitHubBase = "https://github.com/Brain2k-0005/Lumeo/tree/master";
    private const string DocsBase = "https://lumeo.nativ.sh/components";
    private const int MaxSourceBytes = 64 * 1024;

    public static void Enrich(
        IDictionary<string, object?> entry,
        string componentKey,
        string componentName,
        string repoRoot,
        JsonElement apiEntry,
        IReadOnlyCollection<string> knownComponentNames,
        TextWriter? logger = null)
    {
        // 1. docsUrl
        entry["docsUrl"] = $"{DocsBase}/{componentKey}";

        // Resolve files list and which package's src dir it sits under.
        var files = ExtractFiles(entry);
        var packageRoot = ResolvePackageRoot(files, repoRoot);

        // 2. sourceUrl + 3. source
        var sourceUrls = new List<Dictionary<string, object?>>();
        var sourceContent = new List<Dictionary<string, object?>>();
        foreach (var rel in files)
        {
            // Skip generated partials.
            if (rel.EndsWith(".razor.g.cs", StringComparison.OrdinalIgnoreCase)) continue;

            var pkgSrcDir = packageRoot ?? "src/Lumeo";
            var url = $"{GitHubBase}/{pkgSrcDir}/{rel}";
            sourceUrls.Add(new Dictionary<string, object?> { ["file"] = rel, ["url"] = url });

            var abs = Path.Combine(repoRoot, pkgSrcDir.Replace('/', Path.DirectorySeparatorChar),
                rel.Replace('/', Path.DirectorySeparatorChar));
            string content = string.Empty;
            try
            {
                // Normalize CRLF -> LF so extracted text is platform-stable.
                if (File.Exists(abs)) content = File.ReadAllText(abs).Replace("\r\n", "\n").Replace("\r", "\n");
            }
            catch (Exception ex)
            {
                logger?.WriteLine($"[per-component-enrich] WARN: failed to read {abs}: {ex.Message}");
            }

            var bytes = Encoding.UTF8.GetByteCount(content);
            if (bytes > MaxSourceBytes)
            {
                logger?.WriteLine($"[per-component-enrich] WARN: {rel} is {bytes} bytes — truncating to {MaxSourceBytes}");
                // Truncate to the first MaxSourceBytes characters (best-effort; UTF-8 byte budget approx).
                content = content.Substring(0, Math.Min(content.Length, MaxSourceBytes)) + "\n… [truncated]";
            }

            sourceContent.Add(new Dictionary<string, object?>
            {
                ["path"] = rel,
                ["content"] = content,
            });
        }
        entry["sourceUrl"] = sourceUrls;
        entry["source"] = sourceContent;

        // 4. slots — derive from api.parameters where type starts with RenderFragment.
        // Walk root + every sub-component so e.g. SheetContent's ChildContent shows up.
        var slots = new List<Dictionary<string, object?>>();
        var seenSlots = new HashSet<string>(StringComparer.Ordinal);
        if (apiEntry.ValueKind == JsonValueKind.Object)
        {
            CollectSlots(apiEntry, slots, seenSlots, owner: componentName);
            if (apiEntry.TryGetProperty("subComponents", out var subs) && subs.ValueKind == JsonValueKind.Object)
            {
                foreach (var sub in subs.EnumerateObject())
                {
                    CollectSlots(sub.Value, slots, seenSlots, owner: sub.Name);
                }
            }
        }
        entry["slots"] = slots;

        // 5. serviceDependencies — @inject directives across .razor files.
        var services = new List<Dictionary<string, object?>>();
        var seenServices = new HashSet<string>(StringComparer.Ordinal);
        var razorFileRegex = new Regex(@"^@inject\s+([\w\.]+)\s+(\w+)\s*$", RegexOptions.Multiline);
        foreach (var sc in sourceContent)
        {
            var path = sc["path"]?.ToString() ?? "";
            if (!path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)) continue;
            var content = sc["content"]?.ToString() ?? "";
            foreach (Match m in razorFileRegex.Matches(content))
            {
                var fullType = m.Groups[1].Value;
                var key = fullType;
                if (!seenServices.Add(key)) continue;
                var dotIdx = fullType.LastIndexOf('.');
                var ns = dotIdx > 0 ? fullType.Substring(0, dotIdx) : "";
                var typeName = dotIdx > 0 ? fullType.Substring(dotIdx + 1) : fullType;
                services.Add(new Dictionary<string, object?>
                {
                    ["service"] = typeName,
                    ["namespace"] = ns,
                });
            }
        }
        entry["serviceDependencies"] = services;

        // 6. cascadingDependencies — promote CascadingParameters.
        var cascades = new List<Dictionary<string, object?>>();
        var seenCascades = new HashSet<string>(StringComparer.Ordinal);
        if (apiEntry.ValueKind == JsonValueKind.Object)
        {
            CollectCascading(apiEntry, cascades, seenCascades);
            if (apiEntry.TryGetProperty("subComponents", out var subs) && subs.ValueKind == JsonValueKind.Object)
            {
                foreach (var sub in subs.EnumerateObject())
                {
                    CollectCascading(sub.Value, cascades, seenCascades);
                }
            }
        }
        entry["cascadingDependencies"] = cascades;

        // 7. relatedComponents
        // (a) every other razor file in this component's folder (sub-components)
        // (b) any known Lumeo component referenced via <Tag in the source.
        var related = new List<Dictionary<string, object?>>();
        var seenRelated = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { componentName };

        // sub-components: file stems that aren't the root component
        foreach (var f in files)
        {
            if (!f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)) continue;
            var stem = Path.GetFileNameWithoutExtension(f);
            if (stem.Equals(componentName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!seenRelated.Add(stem)) continue;
            related.Add(new Dictionary<string, object?>
            {
                ["name"] = stem,
                ["reason"] = "sub-component",
            });
        }

        // cross-component references in source
        var tagRegex = new Regex(@"<([A-Z][A-Za-z0-9]*)\b", RegexOptions.Compiled);
        foreach (var sc in sourceContent)
        {
            var path = sc["path"]?.ToString() ?? "";
            if (!path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)) continue;
            var content = sc["content"]?.ToString() ?? "";
            foreach (Match m in tagRegex.Matches(content))
            {
                var tag = m.Groups[1].Value;
                if (seenRelated.Contains(tag)) continue;
                // Only count tags Lumeo actually ships.
                if (!knownComponentNames.Contains(tag)) continue;
                seenRelated.Add(tag);
                related.Add(new Dictionary<string, object?>
                {
                    ["name"] = tag,
                    ["reason"] = "used internally",
                });
            }
        }
        entry["relatedComponents"] = related;

        // 8. keyboardInteractions — heuristic.
        // Reuses ComponentsApiEmitter's KeyComparisonRegex + KnownKeys whitelist (not
        // a separate `.Key == "X"`-only regex) so this human-readable summary can
        // never fall behind api.a11y.keys again — a switch-statement (`case "X":`),
        // pattern-match (`.Key is "X" or "Y"`), or switch-expression-arm (`"X" => ...`)
        // key handler must be credited here exactly like it is there (PR #356
        // round-3, Codex P3).
        var keyboard = new List<Dictionary<string, object?>>();
        var seenKeyboard = new HashSet<string>(StringComparer.Ordinal);
        var methodRegex = new Regex(@"(?:private|public|protected|internal)\s+(?:async\s+)?(?:Task|ValueTask|void)\s+(\w+)\s*\(",
            RegexOptions.Compiled);
        foreach (var sc in sourceContent)
        {
            var path = sc["path"]?.ToString() ?? "";
            if (!path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith(".razor.cs", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
            var content = sc["content"]?.ToString() ?? "";

            // Pre-compute method spans for enclosing-method lookup.
            var methodPositions = new List<(int Start, string Name)>();
            foreach (Match mm in methodRegex.Matches(content))
            {
                methodPositions.Add((mm.Index, mm.Groups[1].Value));
            }

            foreach (Match km in ComponentsApiEmitter.KeyComparisonRegex.Matches(content))
            {
                // find enclosing method (the latest method header before this match) —
                // shared once per match, since every "k" capture in it sits at the
                // same switch/pattern site.
                string method = "(unknown)";
                for (int i = methodPositions.Count - 1; i >= 0; i--)
                {
                    if (methodPositions[i].Start < km.Index)
                    {
                        method = methodPositions[i].Name;
                        break;
                    }
                }
                foreach (Capture kc in km.Groups["k"].Captures)
                {
                    if (!ComponentsApiEmitter.KnownKeys.Contains(kc.Value)) continue; // same whitelist as the a11y scanner
                    var key = kc.Value == " " ? "Space" : kc.Value;
                    var dedup = key + "::" + method;
                    if (!seenKeyboard.Add(dedup)) continue;
                    keyboard.Add(new Dictionary<string, object?>
                    {
                        ["key"] = key,
                        ["action"] = $"{method} handles {key} in {Path.GetFileName(path)}",
                    });
                }
            }
        }
        entry["keyboardInteractions"] = keyboard;

        // 9. tests — scan tests/ for files that actually EXERCISE this component.
        var tests = new List<string>();
        var seenTests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var testRoots = new[]
        {
            Path.Combine(repoRoot, "tests", "Lumeo.Tests"),
            Path.Combine(repoRoot, "tests", "Lumeo.Docs.Tests"),
            Path.Combine(repoRoot, "tests", "Lumeo.Tests.E2E"),
        };
        // Precise "does this test render the component" signal — same family of
        // patterns as Program.cs's ComputeTestCoverage `renders` regex, extended to
        // tolerate a namespace/alias-qualified generic argument (`Render<Lumeo.X>`,
        // `Render<L.X>`) which dedicated-folder tests use routinely. A loose
        // whole-file `\bComponentName\b` word match (the previous heuristic) false-
        // attributed shared test files from an incidental DOC-COMMENT mention —
        // e.g. PullToRefreshKeyboardTests.cs -> file-viewer.json via "...counterpart
        // to FileViewer's...", and PivotGridKeyboardTests.cs -> steps.json via
        // "Card/Steps conditionally-interactive contract" — plus real false hits
        // from unrelated identifiers/params that happen to spell the component name
        // (Playwright's `MouseMoveOptions.Steps`, a `Stepper`/`Tour.Steps` mention)
        // (CodeRabbit Major, PR #356 round 1).
        // A fourth alternative catches non-render unit tests that still legitimately
        // belong to this component — e.g. FileViewerCsvTests.cs tests the static
        // `Lumeo.FileViewer.ParseCsv(...)` parsing helper without ever rendering the
        // Blazor component — by requiring the component's OWN qualified type name
        // (`Lumeo.X` / `L.X`), not a bare word match. This still excludes
        // `Tour.Steps`/`c.Steps`/comment mentions since neither "Lumeo" nor the "L"
        // alias precedes them.
        // Shared with Program.cs's ComputeTestCoverage via ComponentTestSignals — see
        // its doc comment for why these two must never diverge again (PR #356 round-2).
        var rendersRegex = ComponentTestSignals.BuildRendersRegex(componentName);
        // E2E specs don't render Razor markup at all — they navigate a real browser
        // to the component's docs route — so they need their own route-based signal
        // rather than the render regex (which would silently drop every E2E entry).
        var e2eRouteRegex = new Regex(
            $@"/components/{Regex.Escape(componentKey)}(?![a-z0-9-])",
            RegexOptions.Compiled);
        foreach (var root in testRoots)
        {
            if (!Directory.Exists(root)) continue;
            var isE2E = string.Equals(Path.GetFileName(root), "Lumeo.Tests.E2E", StringComparison.OrdinalIgnoreCase);
            // Test sources are both .cs and bUnit .razor files (StepsKeyboardTests.razor
            // etc. use `Render(@<X …>)`) — the previous *.cs-only glob silently dropped
            // every .razor test from this list across the WHOLE library.
            var testFiles = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Concat(isE2E ? Enumerable.Empty<string>() : Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories));
            foreach (var testFile in testFiles)
            {
                var rel = Path.GetRelativePath(repoRoot, testFile).Replace('\\', '/');
                // Folder ownership is the stronger signal: a file physically filed under
                // the component's OWN dedicated test folder (e.g.
                // tests/Lumeo.Tests/Components/Stepper/StepperItemTests.cs) belongs to
                // this component regardless of whether it happens to render <Stepper>
                // itself — a test for a sub-component (StepperItem), a shared helper, or
                // pure logic living in that folder is still THIS component's coverage.
                // The render-only regex below previously dropped every such file
                // (CodeRabbit, PR #356 round-2). Skipped for E2E, whose specs aren't
                // filed per-component the same way.
                var ownedByFolder = !isE2E && ComponentTestSignals.IsOwnedByFolder(rel, componentName);
                if (!ownedByFolder)
                {
                    string text;
                    try { text = File.ReadAllText(testFile).Replace("\r\n", "\n").Replace("\r", "\n"); }
                    catch { continue; }
                    var matches = isE2E ? e2eRouteRegex.IsMatch(text) : rendersRegex.IsMatch(text);
                    if (!matches) continue;
                }
                if (seenTests.Add(rel)) tests.Add(rel);
            }
        }
        tests.Sort(StringComparer.Ordinal);
        entry["tests"] = tests;

        // 10. mdSummary — deterministic Markdown reference block.
        entry["mdSummary"] = BuildMarkdownSummary(entry, componentName, slots, services, cascades, related, keyboard, apiEntry);
    }

    // ----- helpers -----

    private static List<string> ExtractFiles(IDictionary<string, object?> entry)
    {
        if (!entry.TryGetValue("files", out var files)) return new();
        return files switch
        {
            List<string> ls => ls,
            string[] arr => arr.ToList(),
            IEnumerable<object?> en => en.Select(x => x?.ToString() ?? "").Where(x => x.Length > 0).ToList(),
            _ => new(),
        };
    }

    /// <summary>Files are stored relative to the package src dir (e.g. "UI/Sheet/Sheet.razor").
    /// We need to figure out which package src dir that is by matching against the known set.</summary>
    private static string? ResolvePackageRoot(List<string> files, string repoRoot)
    {
        // Probe for each candidate: file exists at repoRoot/<candidate>/<file>?
        var candidates = new[] { "src/Lumeo", "src/Lumeo.Charts", "src/Lumeo.DataGrid", "src/Lumeo.Editor", "src/Lumeo.Scheduler", "src/Lumeo.Gantt", "src/Lumeo.Motion" };
        if (files.Count == 0) return "src/Lumeo";
        foreach (var c in candidates)
        {
            var probe = Path.Combine(repoRoot, c.Replace('/', Path.DirectorySeparatorChar),
                files[0].Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(probe)) return c;
        }
        return "src/Lumeo";
    }

    private static void CollectSlots(JsonElement apiBlock, List<Dictionary<string, object?>> slots,
        HashSet<string> seen, string owner)
    {
        if (!apiBlock.TryGetProperty("parameters", out var parameters)
            || parameters.ValueKind != JsonValueKind.Array) return;
        foreach (var p in parameters.EnumerateArray())
        {
            var type = p.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            if (!type.StartsWith("RenderFragment", StringComparison.Ordinal)) continue;
            var name = p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (name.Length == 0) continue;
            var key = owner + "::" + name;
            if (!seen.Add(key)) continue;
            var description = p.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString() : null;
            slots.Add(new Dictionary<string, object?>
            {
                ["name"] = name,
                ["owner"] = owner,
                ["type"] = type,
                ["description"] = description,
            });
        }
    }

    private static void CollectCascading(JsonElement apiBlock, List<Dictionary<string, object?>> cascades,
        HashSet<string> seen)
    {
        if (!apiBlock.TryGetProperty("parameters", out var parameters)
            || parameters.ValueKind != JsonValueKind.Array) return;
        foreach (var p in parameters.EnumerateArray())
        {
            var isCascading = p.TryGetProperty("isCascading", out var c)
                && c.ValueKind == JsonValueKind.True;
            if (!isCascading) continue;
            var name = p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (name.Length == 0 || !seen.Add(name)) continue;
            var type = p.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            cascades.Add(new Dictionary<string, object?>
            {
                ["name"] = name,
                ["type"] = type,
                ["required"] = !type.EndsWith("?", StringComparison.Ordinal),
            });
        }
    }

    private static string BuildMarkdownSummary(
        IDictionary<string, object?> entry,
        string componentName,
        List<Dictionary<string, object?>> slots,
        List<Dictionary<string, object?>> services,
        List<Dictionary<string, object?>> cascades,
        List<Dictionary<string, object?>> related,
        List<Dictionary<string, object?>> keyboard,
        JsonElement apiEntry)
    {
        var sb = new StringBuilder();
        var description = entry.TryGetValue("description", out var d) ? d?.ToString() ?? "" : "";
        var nuget = entry.TryGetValue("nugetPackage", out var pkg) ? pkg?.ToString() ?? "Lumeo" : "Lumeo";
        var category = entry.TryGetValue("category", out var c) ? c?.ToString() ?? "" : "";
        var subcategory = entry.TryGetValue("subcategory", out var sc) ? sc?.ToString() : null;
        var docsUrl = entry.TryGetValue("docsUrl", out var du) ? du?.ToString() ?? "" : "";

        sb.AppendLine($"# {componentName}");
        sb.AppendLine();
        sb.AppendLine(description);
        sb.AppendLine();
        var catLine = string.IsNullOrEmpty(subcategory) ? category : $"{category}/{subcategory}";
        sb.AppendLine($"**Package:** `{nuget}` · **Category:** {catLine}");
        sb.AppendLine($"**Docs:** {docsUrl}");
        sb.AppendLine();

        // Parameters table — root + sub-components.
        sb.AppendLine("## Parameters");
        sb.AppendLine("| Owner | Name | Type | Default | Description |");
        sb.AppendLine("|---|---|---|---|---|");
        AppendParamsRows(sb, apiEntry, componentName);
        if (apiEntry.ValueKind == JsonValueKind.Object
            && apiEntry.TryGetProperty("subComponents", out var subs)
            && subs.ValueKind == JsonValueKind.Object)
        {
            foreach (var sub in subs.EnumerateObject())
            {
                AppendParamsRows(sb, sub.Value, sub.Name);
            }
        }
        sb.AppendLine();

        // Slots
        sb.AppendLine("## Slots");
        if (slots.Count == 0) sb.AppendLine("- (none)");
        foreach (var s in slots)
        {
            sb.AppendLine($"- `{s["name"]}` ({s["owner"]}) — {s["description"] ?? "RenderFragment slot"}");
        }
        sb.AppendLine();

        // Events — root only.
        sb.AppendLine("## Events");
        var anyEvents = false;
        if (apiEntry.ValueKind == JsonValueKind.Object
            && apiEntry.TryGetProperty("events", out var events)
            && events.ValueKind == JsonValueKind.Array)
        {
            foreach (var ev in events.EnumerateArray())
            {
                anyEvents = true;
                var name = ev.TryGetProperty("name", out var n) ? n.GetString() : null;
                var type = ev.TryGetProperty("type", out var t) ? t.GetString() : null;
                var desc = ev.TryGetProperty("description", out var ed) && ed.ValueKind == JsonValueKind.String
                    ? ed.GetString() : null;
                sb.AppendLine($"- `{name}` (`{type}`){(desc is null ? "" : $" — {desc}")}");
            }
        }
        if (!anyEvents) sb.AppendLine("- (none)");
        sb.AppendLine();

        // Sub-components
        sb.AppendLine("## Sub-components");
        var subList = related.Where(r => string.Equals(r["reason"]?.ToString(), "sub-component", StringComparison.Ordinal)).ToList();
        if (subList.Count == 0) sb.AppendLine("- (none)");
        else sb.AppendLine("- " + string.Join(", ", subList.Select(s => $"`{s["name"]}`")));
        sb.AppendLine();

        // Cascading values
        sb.AppendLine("## Cascading values it expects");
        if (cascades.Count == 0) sb.AppendLine("- (none)");
        foreach (var ca in cascades)
        {
            sb.AppendLine($"- `{ca["name"]}` (`{ca["type"]}`)");
        }
        sb.AppendLine();

        // Services
        sb.AppendLine("## Services it injects");
        if (services.Count == 0) sb.AppendLine("- (none)");
        foreach (var sv in services)
        {
            sb.AppendLine($"- `{sv["service"]}` ({sv["namespace"]})");
        }
        sb.AppendLine();

        // Keyboard
        sb.AppendLine("## Keyboard");
        if (keyboard.Count == 0) sb.AppendLine("—");
        else
        {
            foreach (var kb in keyboard)
            {
                sb.AppendLine($"- `{kb["key"]}` — {kb["action"]}");
            }
        }
        sb.AppendLine();

        // Example
        sb.AppendLine("## Example");
        sb.AppendLine("```razor");
        sb.AppendLine($"<{componentName} />");
        sb.AppendLine("```");
        sb.AppendLine();

        // CSS variables
        var cssVars = entry.TryGetValue("cssVars", out var cvObj) && cvObj is string[] cvArr
            ? cvArr
            : Array.Empty<string>();
        sb.AppendLine("## CSS variables");
        sb.AppendLine(cssVars.Length == 0 ? "(none)" : string.Join(", ", cssVars.Select(v => $"`{v}`")));
        sb.AppendLine();

        // Files
        sb.AppendLine("## Files");
        var files = ExtractFiles(entry);
        foreach (var f in files) sb.AppendLine($"- `{f}`");

        // Newline normalisation is applied centrally at the JSON serialization
        // boundary (every JsonSerializer.Serialize result is run through
        // .Replace("\r\n", "\n")) so every multi-line field — mdSummary here, plus
        // descriptions/examples elsewhere — is byte-identical across OSes.
        return sb.ToString();
    }

    private static void AppendParamsRows(StringBuilder sb, JsonElement apiBlock, string owner)
    {
        if (apiBlock.ValueKind != JsonValueKind.Object) return;
        if (!apiBlock.TryGetProperty("parameters", out var parameters)
            || parameters.ValueKind != JsonValueKind.Array) return;
        foreach (var p in parameters.EnumerateArray())
        {
            var name = p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var type = p.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            var def = p.TryGetProperty("default", out var dd) && dd.ValueKind == JsonValueKind.String
                ? dd.GetString() : null;
            var desc = p.TryGetProperty("description", out var de) && de.ValueKind == JsonValueKind.String
                ? de.GetString() : null;
            sb.AppendLine($"| {owner} | {EscapeMd(name)} | `{EscapeMd(type)}` | `{EscapeMd(def ?? "")}` | {EscapeMd(desc ?? "")} |");
        }
    }

    private static string EscapeMd(string s)
        => s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}
