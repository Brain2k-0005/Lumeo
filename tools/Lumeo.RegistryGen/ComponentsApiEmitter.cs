using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lumeo.RegistryGen;

/// <summary>
/// Builds tools/lumeo-mcp/src/components-api.json by walking every component
/// directory under each UI root and parsing every .razor file with
/// <see cref="RazorParameterScanner"/>. Emits one entry per component directory
/// with the root component's parameters lifted to the top level and any
/// sibling files exposed under "subComponents".
/// </summary>
public static class ComponentsApiEmitter
{
    public sealed record ComponentMeta(
        string Name,
        string Category,
        string? Subcategory,
        string Description,
        string NugetPackage,
        string[] Files,
        string[] CssVars);

    public static int Emit(
        string outputPath,
        IEnumerable<string> componentDirs,
        IEnumerable<string> uiRoots,
        Func<string, ComponentMeta> metaResolver,
        TextWriter logger,
        string version,
        string? repoRoot = null)
    {
        var components = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var thinFallbacks = new List<(string Name, string Reason)>();
        int totalParams = 0, totalEnums = 0, totalRecords = 0;

        var roots = uiRoots.ToArray();

        // Service-layer types are scanned up-front (not just for the `services`
        // section below) so their enum names form the "already globally exposed"
        // set — we don't want to duplicate Size/Side/Align/… into every component
        // that merely takes one as a parameter; those are discoverable via
        // api.services. Empty when we don't know the repo root.
        var serviceTypes = repoRoot is not null
            ? ScanServices(repoRoot, logger)
            : (IReadOnlyList<ServiceApiScanner.ServiceType>)Array.Empty<ServiceApiScanner.ServiceType>();
        var globallyExposedEnums = new HashSet<string>(
            serviceTypes.Where(t => t.Kind == "enum").Select(t => t.Name), StringComparer.Ordinal);

        // Project-wide index of every PUBLIC enum declared in a standalone .cs file
        // across the scanned package src roots. The per-component enum discovery in
        // RazorParameterScanner only sees enums declared INSIDE a component's own
        // @code block; an enum a component references via a [Parameter] but whose
        // declaration lives in a separate root-level file (e.g. MenuItemVariant.cs at
        // the src/Lumeo root, shared by DropdownMenu/ContextMenu/Menubar) was
        // therefore invisible in api.enums. This index lets us resolve those.
        var enumIndex = BuildEnumIndex(roots, logger);

        foreach (var dir in componentDirs)
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(name)) continue;

            var packageRoot = roots.FirstOrDefault(r => dir.StartsWith(r, StringComparison.OrdinalIgnoreCase));
            if (packageRoot is null) continue;
            var packageSrcRoot = Path.GetDirectoryName(packageRoot)!;

            var razorFiles = Directory
                .EnumerateFiles(dir, "*.razor", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var meta = metaResolver(name);

            // Union of every PUBLIC enum declared anywhere in this component's file set (root +
            // sub-components), so a sub-component parameter typed through a sibling's nested
            // enum (e.g. DataTableSortableHeader.SortDirection, typed as
            // DataTable<object>.SortDirection and declared in the sibling DataTable.razor) can
            // still get its CLR-implicit default resolved — not just enums declared in the SAME
            // file as the parameter (Codex P2, PR #358 round 3). First-seen wins on a name
            // clash across files, matching CollectLocalPublicEnums' own TryAdd semantics.
            var componentEnumMembers = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var rf in razorFiles)
            {
                foreach (var (enumName, members) in RazorParameterScanner.CollectPublicEnums(rf))
                    componentEnumMembers.TryAdd(enumName, members);
            }

            // Parse each razor file
            var perFile = new Dictionary<string, RazorParameterScanner.RazorFileSchema>(StringComparer.Ordinal);
            foreach (var rf in razorFiles)
            {
                var schema = RazorParameterScanner.Scan(rf, componentEnumMembers);
                perFile[Path.GetFileNameWithoutExtension(rf)] = schema;
                if (schema.ParseFailed)
                {
                    logger.WriteLine($"[components-api] WARN: parse failed for {rf}: {schema.ParseError}");
                    thinFallbacks.Add((schema.ComponentName, schema.ParseError ?? "unknown"));
                }
            }

            // Pick the "root" file: same name as the directory if present, else the
            // first file alphabetically. Its parameters become the top-level schema.
            RazorParameterScanner.RazorFileSchema? root = null;
            if (perFile.TryGetValue(name, out var matchedRoot)) root = matchedRoot;
            else if (perFile.Count > 0) root = perFile.Values.OrderBy(s => s.ComponentName, StringComparer.Ordinal).First();

            // Aggregate sub-components (every razor file in the dir) keyed by file stem.
            var subEntries = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (stem, schema) in perFile)
            {
                if (root is not null && stem == root.ComponentName) continue;
                subEntries[stem] = SerializeSchema(schema);
            }

            var rootDict = root is null ? null : SerializeSchema(root);

            // Enums this component REFERENCES via a [Parameter] but does not declare
            // in any of its own @code blocks — resolved from the project-wide index
            // of standalone enum files (e.g. MenuItemVariant). Globally-exposed enums
            // (Size/Side/…) are skipped: they already live in api.services.
            var resolvedEnums = ResolveReferencedEnums(perFile, enumIndex, globallyExposedEnums);

            // Aggregate counts
            if (root is not null)
            {
                totalParams += root.Parameters.Length;
                totalEnums += root.Enums.Length;
                totalRecords += root.Records.Length;
            }
            totalEnums += resolvedEnums.Count;
            foreach (var s in subEntries.Values.OfType<Dictionary<string, object?>>())
            {
                if (s.TryGetValue("parameters", out var pp) && pp is Array pArr) totalParams += pArr.Length;
                if (s.TryGetValue("enums", out var ee) && ee is Array eArr) totalEnums += eArr.Length;
                if (s.TryGetValue("records", out var rr) && rr is Array rArr) totalRecords += rArr.Length;
            }

            var relativeFiles = meta.Files;

            var examples = repoRoot is not null
                ? ExampleExtractor.ForComponent(repoRoot, name)
                    .Select(e => (object)new Dictionary<string, object?> { ["title"] = e.Title, ["code"] = e.Code })
                    .ToArray()
                : Array.Empty<object>();

            var entry = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["category"] = meta.Category,
                ["subcategory"] = meta.Subcategory,
                ["description"] = meta.Description,
                ["nugetPackage"] = meta.NugetPackage,
                ["files"] = relativeFiles,
                ["namespace"] = root?.Namespace,
                ["inheritsFrom"] = root?.InheritsFrom,
                ["implements"] = root?.Implements ?? Array.Empty<string>(),
                ["parameters"] = root?.Parameters.Select(SerializeParam).ToArray() ?? Array.Empty<object>(),
                ["events"] = root?.Events.Select(SerializeEvent).ToArray() ?? Array.Empty<object>(),
                ["enums"] = (root?.Enums ?? Array.Empty<RazorParameterScanner.EnumInfo>())
                    .Concat(resolvedEnums).Select(SerializeEnum).ToArray(),
                ["records"] = root?.Records.Select(SerializeRecord).ToArray() ?? Array.Empty<object>(),
                ["gotchas"] = root?.Gotchas ?? Array.Empty<string>(),
                ["cssVars"] = meta.CssVars,
                ["examples"] = examples,
                ["subComponents"] = subEntries,
                ["a11y"] = ExtractA11y(razorFiles),
                ["parseFailed"] = root?.ParseFailed ?? false,
                ["parseError"] = root?.ParseError,
            };

            components[name] = entry;
        }

        // Service-layer API — the public consumer-facing surface that lives in
        // plain C# (OverlayService, ThemeBuilder, IResponsiveService, global
        // enums, …). Indexed so AI agents can discover it the same way they
        // discover .razor components. Only when we know where the repo is.
        object[] services = serviceTypes.Select(SerializeService).ToArray();
        int serviceCount = serviceTypes.Count;

        // Theme tokens + patterns — only when we know where the repo is.
        object[] themeTokens = Array.Empty<object>();
        object[] patterns = Array.Empty<object>();
        if (repoRoot is not null)
        {
            themeTokens = ExampleExtractor.ThemeTokens(repoRoot)
                .Select(t => (object)new Dictionary<string, object?> { ["token"] = t.Token, ["cssVar"] = t.CssVar })
                .ToArray();
            patterns = ExampleExtractor.Patterns(repoRoot)
                .Select(p => (object)new Dictionary<string, object?>
                {
                    ["title"] = p.Title,
                    ["route"] = p.Route,
                    ["description"] = p.Description,
                    ["examples"] = p.Examples.Select(e => (object)new Dictionary<string, object?> { ["title"] = e.Title, ["code"] = e.Code }).ToArray(),
                })
                .ToArray();
        }

        var rootJson = new Dictionary<string, object?>
        {
            ["$schema"] = "https://lumeo.nativ.sh/components-api-schema.json",
            ["version"] = version,
            ["generated"] = DateTime.UtcNow.ToString("O"),
            ["stats"] = new Dictionary<string, object?>
            {
                ["componentCount"] = components.Count,
                ["totalParameters"] = totalParams,
                ["totalEnums"] = totalEnums,
                ["totalRecords"] = totalRecords,
                ["serviceCount"] = serviceCount,
                ["thinFallbacks"] = thinFallbacks.Select(t => new { name = t.Name, reason = t.Reason }).ToArray(),
            },
            ["themeTokens"] = themeTokens,
            ["patterns"] = patterns,
            ["services"] = services,
            ["components"] = components,
        };

        var jsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        // Normalise CRLF→LF inside serialized values so components-api.json is
        // byte-identical across OSes (descriptions carry the host newline). Only
        // genuine CRLF escapes are touched — escaped backslash sequences are safe.
        var json = JsonSerializer.Serialize(rootJson, jsonOpts).Replace("\\r\\n", "\\n");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, json, new UTF8Encoding(false));

        logger.WriteLine($"[components-api] Wrote {components.Count} components, {totalParams} params, {totalEnums} enums, {totalRecords} records, {serviceCount} services, {thinFallbacks.Count} thin fallbacks → {outputPath}");
        return components.Count;
    }

    /// <summary>
    /// Scan the curated set of service-layer C# source files for their public,
    /// consumer-facing API. The file → allowed-type-name map is hand-maintained
    /// here because these live outside the auto-discovered UI directories. A
    /// missing file or a parse failure is logged and skipped (fail-soft), never
    /// fatal — matching the Razor-scan contract.
    /// </summary>
    private static IReadOnlyList<ServiceApiScanner.ServiceType> ScanServices(string repoRoot, TextWriter logger)
    {
        string Core(params string[] parts) => Path.Combine(new[] { repoRoot, "src", "Lumeo" }.Concat(parts).ToArray());

        // Each entry: (relative C# file, allowed type names | null = all public types).
        var sources = new (string Path, string[]? TypeFilter)[]
        {
            (Core("Services", "OverlayService.cs"), new[]
            {
                "OverlayService", "OverlayOptions", "SheetOverlayOptions",
                "DialogOverlayOptions", "DrawerOverlayOptions", "AlertDialogOptions",
                "OverlayResult", "OverlayParameters", "OverlayInstance", "OverlayType", "OverlaySize", "SheetSize",
            }),
            // IOverlayService lives in its own file, not OverlayService.cs.
            (Core("Services", "IOverlayService.cs"), new[]
            {
                "IOverlayService",
            }),
            (Core("Services", "IResponsiveService.cs"), new[]
            {
                "IResponsiveService", "Breakpoint", "ViewportInfo",
            }),
            (Core("Services", "ThemeService.cs"), new[]
            {
                "ThemeService", "ThemeMode", "ThemeSchemeInfo",
            }),
            (Core("Services", "IThemeService.cs"), new[]
            {
                "IThemeService", "LayoutDirection",
            }),
            (Core("Services", "Theming", "ThemeBuilder.cs"), new[]
            {
                "ThemeBuilder", "Theme",
            }),
            (Core("Services", "ComponentInteropService.cs"), new[]
            {
                "ComponentInteropService",
            }),
            (Core("Services", "IComponentInteropService.cs"), new[]
            {
                "IComponentInteropService",
            }),
            // Root global enums (each in its own src/Lumeo/*.cs).
            (Core("Size.cs"), new[] { "Size" }),
            (Core("Density.cs"), new[] { "Density" }),
            (Core("Side.cs"), new[] { "Side" }),
            (Core("Align.cs"), new[] { "Align" }),
            (Core("Orientation.cs"), new[] { "Orientation" }),
        };

        return ServiceApiScanner.ScanFiles(sources, logger);
    }

    /// <summary>
    /// Indexes every PUBLIC enum declared in a standalone <c>.cs</c> file across the
    /// scanned package src roots (the parent of each UI root, e.g. <c>src/Lumeo</c>),
    /// keyed by simple type name. First declaration wins. Skips <c>bin/</c>,
    /// <c>obj/</c> and generated <c>.g.cs</c> files, and cheaply pre-filters to files
    /// that actually contain an <c>enum</c> declaration so the Roslyn parse only runs
    /// where it can find something. Fail-soft: an unreadable file is skipped.
    /// </summary>
    private static Dictionary<string, ServiceApiScanner.ServiceType> BuildEnumIndex(
        IEnumerable<string> uiRoots, TextWriter logger)
    {
        var index = new Dictionary<string, ServiceApiScanner.ServiceType>(StringComparer.Ordinal);
        var srcRoots = uiRoots
            .Select(Path.GetDirectoryName)
            .Where(d => !string.IsNullOrEmpty(d))
            .Select(d => d!)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var srcRoot in srcRoots)
        {
            if (!Directory.Exists(srcRoot)) continue;
            foreach (var cs in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
            {
                var norm = cs.Replace('\\', '/');
                if (norm.Contains("/obj/", StringComparison.Ordinal)
                    || norm.Contains("/bin/", StringComparison.Ordinal)
                    || norm.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string text;
                try { text = File.ReadAllText(cs); }
                catch { continue; }
                if (!text.Contains("enum ", StringComparison.Ordinal)) continue;

                foreach (var t in ServiceApiScanner.Scan(cs, logger))
                {
                    if (t.Kind != "enum") continue;
                    index.TryAdd(t.Name, t); // first declaration wins; deterministic by enumeration order
                }
            }
        }
        return index;
    }

    /// <summary>
    /// Resolves the enums a component references through a <c>[Parameter]</c> (across
    /// its root + every sub-component) but does NOT declare in any of its own @code
    /// blocks — the gap that hid <c>MenuItemVariant</c> from the menu components'
    /// <c>api.enums</c>. Enums the component already declares nested, and enums
    /// already surfaced globally via <c>api.services</c> (Size/Side/Align/…), are
    /// skipped. Returns them sorted by name for a stable, byte-reproducible registry.
    /// </summary>
    private static List<RazorParameterScanner.EnumInfo> ResolveReferencedEnums(
        IReadOnlyDictionary<string, RazorParameterScanner.RazorFileSchema> perFile,
        IReadOnlyDictionary<string, ServiceApiScanner.ServiceType> enumIndex,
        IReadOnlySet<string> globallyExposed)
    {
        if (enumIndex.Count == 0 || perFile.Count == 0)
            return new List<RazorParameterScanner.EnumInfo>();

        // Enum names already declared nested in any of this component's files.
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var schema in perFile.Values)
            foreach (var en in schema.Enums)
                declared.Add(en.Name);

        var referenced = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var schema in perFile.Values)
            foreach (var p in schema.Parameters)
            {
                var name = SimpleTypeName(p.Type);
                if (name is null
                    || declared.Contains(name)
                    || globallyExposed.Contains(name)
                    || !enumIndex.ContainsKey(name))
                    continue;
                referenced.Add(name);
            }

        var result = new List<RazorParameterScanner.EnumInfo>(referenced.Count);
        foreach (var name in referenced)
        {
            var st = enumIndex[name];
            result.Add(new RazorParameterScanner.EnumInfo(
                st.Name,
                st.EnumValues.Select(v => v.Name).ToArray(),
                st.Summary));
        }
        return result;
    }

    /// <summary>
    /// Reduces a parameter type string to a bare (optionally namespace-qualified)
    /// identifier — stripping a trailing nullable <c>?</c> and any namespace prefix.
    /// Returns null for anything that is not a plain identifier (generics, arrays,
    /// tuples, …), since an enum-typed parameter is never one of those.
    /// </summary>
    private static string? SimpleTypeName(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return null;
        var t = type.Trim();
        if (t.EndsWith("?", StringComparison.Ordinal)) t = t[..^1];
        var lastDot = t.LastIndexOf('.');
        if (lastDot >= 0) t = t[(lastDot + 1)..];
        return Regex.IsMatch(t, "^[A-Za-z_][A-Za-z0-9_]*$") ? t : null;
    }

    private static Dictionary<string, object?> SerializeService(ServiceApiScanner.ServiceType s)
        => new()
        {
            ["name"] = s.Name,
            ["kind"] = s.Kind,
            ["namespace"] = s.Namespace,
            ["summary"] = s.Summary,
            ["properties"] = s.Properties.Select(p => (object)new
            {
                name = p.Name,
                type = p.Type,
                @default = p.Default,
                summary = p.Summary,
            }).ToArray(),
            ["methods"] = s.Methods.Select(m => (object)new
            {
                name = m.Name,
                returnType = m.ReturnType,
                signature = m.Signature,
                summary = m.Summary,
            }).ToArray(),
            ["events"] = s.Events.Select(e => (object)new
            {
                name = e.Name,
                type = e.Type,
                summary = e.Summary,
            }).ToArray(),
            ["enumValues"] = s.EnumValues.Select(e => (object)new
            {
                name = e.Name,
                summary = e.Summary,
            }).ToArray(),
        };

    private static Dictionary<string, object?> SerializeSchema(RazorParameterScanner.RazorFileSchema s)
        => new()
        {
            ["componentName"] = s.ComponentName,
            ["fileName"] = s.FileName,
            ["namespace"] = s.Namespace,
            ["inheritsFrom"] = s.InheritsFrom,
            ["implements"] = s.Implements,
            ["parameters"] = s.Parameters.Select(SerializeParam).ToArray(),
            ["events"] = s.Events.Select(SerializeEvent).ToArray(),
            ["enums"] = s.Enums.Select(SerializeEnum).ToArray(),
            ["records"] = s.Records.Select(SerializeRecord).ToArray(),
            ["gotchas"] = s.Gotchas,
            ["parseFailed"] = s.ParseFailed,
            ["parseError"] = s.ParseError,
        };

    // Known Blazor KeyboardEventArgs.Key values we report when a component handles
    // keydown — a precise whitelist so we don't pick up unrelated string literals.
    // Internal (not private): PerComponentEnricher's keyboardInteractions summary
    // reuses this SAME whitelist + KeyComparisonRegex below, so the two scanners
    // can never drift apart again — a switch/pattern key the a11y scanner credits
    // must show up in the human-readable summary too (PR #356 round-3, Codex P3:
    // dock.json's api.a11y.keys had ArrowLeft/ArrowRight/Home/End from Dock's
    // HandleKeyDown switch, but keyboardInteractions — built from a narrower
    // `.Key == "X"`-only regex — still said "no keyboard support").
    internal static readonly string[] KnownKeys =
    {
        "Enter", "Escape", "Tab", " ", "Spacebar", "ArrowUp", "ArrowDown", "ArrowLeft",
        "ArrowRight", "Home", "End", "PageUp", "PageDown", "Delete", "Backspace", "ContextMenu", "F10",
        // F2 is a real editing key here — DataGridCell enters edit mode on `e.Key == "F2"`
        // (Excel-style). Safe to list now that key detection is comparison-context-aware
        // below: a numeric `.ToString("F2", ...)` format specifier (e.g. Tour.razor) is NOT
        // a `.Key`/`case`/switch-arm comparison, so it is never miscounted as a handled key.
        "F2",
    };

    // A key literal only counts when it sits in an actual key-COMPARISON context:
    //   `case "X":` / `case "X" or "Y":`             — switch STATEMENT arms
    //   `.Key == "X"`                                 — plain equality
    //   `.Key is "X" or "Y" or "Z"`                   — C# pattern-match "or" chain
    //   `"X" => ...` / `(cond, "X") => ...`           — switch EXPRESSION arms, bare
    //                                                    or tuple-patterned, incl.
    //                                                    `"X" or "Y" => ...`
    // Previously any quoted string equal to a known key ANYWHERE in the file
    // counted — including plain string literals that happen to equal a key, e.g.
    // `string.Join(" ", ...)` in a CssClass helper — which is why Dock.razor's
    // registry entry advertised a Space interaction it doesn't actually have
    // (CodeRabbit/Codex P3, PR #356 round 1). The switch-expression-arm alternative
    // was added after a first tightening pass silently dropped REAL interactions
    // from every component using that idiom (Splitter/Resizable's tuple switch,
    // Kanban/RadioGroup/Segmented/QueryBuilder/Stepper's `"X" or "Y" => n`) — caught
    // by diffing every component's api.a11y.keys before/after the change. The
    // repeated named group "k" is valid in .NET regex: every alternative/repetition
    // that matches contributes its own capture, all readable via
    // Match.Groups["k"].Captures.
    internal static readonly Regex KeyComparisonRegex = new(
        "\\bcase\\s+\"(?<k>[^\"]*)\"(?:\\s*(?:or|,)\\s*\"(?<k>[^\"]*)\")*" +
        "|\\.Key\\s+is\\s+\"(?<k>[^\"]*)\"(?:\\s*(?:or|,)\\s*\"(?<k>[^\"]*)\")*" +
        "|\\.Key\\s*==\\s*\"(?<k>[^\"]*)\"",
        RegexOptions.Compiled);

    // The switch-EXPRESSION-arm alternative (`"X" => ...` / `"X" or "Y" => ...`), split
    // out of KeyComparisonRegex: unlike the three alternatives above, a bare `"X" => ...`
    // carries no `.Key`/`case` marker of its own, so matching it ANYWHERE in the file
    // (the original round-2 fix) also credits switch expressions that have nothing to do
    // with keyboard handling — e.g. Icon.razor's `name switch { "ArrowDown" => ...,
    // "Home" => ... }` icon-glyph lookup, or Stepper.razor's OWN `intent switch { ... }`
    // sitting right next to its real `e.Key switch { ... }` in the same file (PR #356
    // round-4, Codex P2). Only applied within a Key-switch body — see
    // <see cref="FindKeySwitchBodies"/> — never against the raw file text.
    private static readonly Regex SwitchArmKeyRegex = new(
        "\"(?<k>[^\"]*)\"(?:\\s*or\\s*\"(?<k>[^\"]*)\")*\\s*\\)?\\s*=>",
        RegexOptions.Compiled);

    // Every `switch {` (switch-EXPRESSION opener) in the file, so its governing
    // expression can be inspected for a `.Key`/`Key` mention.
    private static readonly Regex SwitchExpressionOpenerRegex = new(
        "switch\\s*\\{",
        RegexOptions.Compiled);

    // A whole-word `Key` occurrence (matches the property access in `e.Key`, `args.Key`,
    // or a bare `Key` local) — used to test whether a switch's governing expression is
    // actually keyboard-key-shaped.
    private static readonly Regex KeyWordRegex = new("\\bKey\\b", RegexOptions.Compiled);

    /// <summary>
    /// Finds the body span (start index + length, EXCLUDING the braces) of every switch
    /// EXPRESSION in <paramref name="text"/> whose governing expression mentions `.Key`/
    /// `Key` — the one shape every real keyboard switch in the codebase uses, whether
    /// bare (`e.Key switch { "Enter" => ... }`) or tuple-patterned (Splitter/Resizable's
    /// `(IsHorizontal, e.Key) switch { (true, "ArrowLeft") => ... }`). The governing
    /// expression is taken as the text back to the nearest statement/block boundary
    /// (`;`, `{`, or `}`) before "switch" — bounded, so an unrelated switch elsewhere in
    /// the same file (Stepper's OWN `intent switch { ... }`, Icon's `name switch { ... }`
    /// icon-glyph lookup) is correctly excluded since ITS governing expression has no
    /// `Key` in it. Nested braces (an arm's own object initializer, tuple, etc.) are
    /// balanced so the body span always ends at the switch's own matching close brace.
    /// </summary>
    private static IEnumerable<(int Start, int Length)> FindKeySwitchBodies(string text)
    {
        const int MaxLookback = 300; // generous for a one-line discriminant expression
        foreach (Match opener in SwitchExpressionOpenerRegex.Matches(text))
        {
            var switchStart = opener.Index; // 's' of "switch"
            var scanStart = switchStart;
            var floor = Math.Max(0, switchStart - MaxLookback);
            while (scanStart > floor && text[scanStart - 1] != ';' && text[scanStart - 1] != '{' && text[scanStart - 1] != '}')
                scanStart--;
            var discriminant = text.Substring(scanStart, switchStart - scanStart);
            if (!KeyWordRegex.IsMatch(discriminant)) continue;

            var bodyStart = opener.Index + opener.Length; // just past the opening '{'
            var depth = 1;
            var i = bodyStart;
            while (i < text.Length && depth > 0)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}') depth--;
                i++;
            }
            if (depth == 0) yield return (bodyStart, i - 1 - bodyStart);
        }
    }

    /// <summary>
    /// All key-literal occurrences in <paramref name="text"/> that sit in an actual
    /// key-comparison context — <see cref="KeyComparisonRegex"/>'s three alternatives,
    /// PLUS switch-expression arms but ONLY inside a genuine `.Key switch { }` block
    /// (see <see cref="FindKeySwitchBodies"/>). Returns raw (Index, Key) occurrences,
    /// unfiltered by <see cref="KnownKeys"/> — callers apply the whitelist themselves.
    /// Shared by <see cref="ExtractA11y"/> and PerComponentEnricher's keyboardInteractions
    /// summary so neither can drift from the other (PR #356 round-3, Codex P3).
    /// </summary>
    internal static IEnumerable<(int Index, string Key)> MatchKeyLiteralOccurrences(string text)
    {
        foreach (Match m in KeyComparisonRegex.Matches(text))
            foreach (Capture c in m.Groups["k"].Captures)
                yield return (c.Index, c.Value);

        foreach (var (start, length) in FindKeySwitchBodies(text))
        {
            var body = text.Substring(start, length);
            foreach (Match m in SwitchArmKeyRegex.Matches(body))
                foreach (Capture c in m.Groups["k"].Captures)
                    yield return (start + c.Index, c.Value);
        }
    }

    // One-hop local-variable key tracking, closing the gap the comment above USED to
    // document as accepted: Carousel/Stepper/TabsTrigger all pick their RTL-aware Arrow
    // key pair via the exact same idiom -
    //   var (back, forward) = isRtl ? ("ArrowRight", "ArrowLeft") : ("ArrowLeft", "ArrowRight");
    //   if (e.Key == back) ... else if (e.Key == forward) ...
    // - so KeyComparisonRegex (literal-only) silently dropped ArrowLeft/ArrowRight from
    // all three (CodeRabbit/Codex, PR #356 round-2). TupleKeyAssignRegex captures BOTH
    // branches' literals for each destructured variable name; LocalKeyUseRegex then finds
    // where that name is later compared via `.Key == <name>`, at which point every
    // literal ever assigned to it (from either branch - we don't evaluate the runtime
    // condition, just union both possibilities) is credited. Intentionally narrow (one
    // hop, this one tuple-ternary shape) rather than a general const/var resolver, to
    // avoid reopening the original over-matching bug this whole file's history is about.
    private static readonly Regex TupleKeyAssignRegex = new(
        "\\bvar\\s*\\(\\s*(?<v1>\\w+)\\s*,\\s*(?<v2>\\w+)\\s*\\)\\s*=\\s*[^;]*?" +
        "\\(\\s*\"(?<a1>[^\"]*)\"\\s*,\\s*\"(?<a2>[^\"]*)\"\\s*\\)\\s*:\\s*" +
        "\\(\\s*\"(?<b1>[^\"]*)\"\\s*,\\s*\"(?<b2>[^\"]*)\"\\s*\\)",
        RegexOptions.Compiled);

    private static readonly Regex LocalKeyUseRegex = new(
        "\\.Key\\s*==\\s*(?<v>[A-Za-z_]\\w*)\\b",
        RegexOptions.Compiled);

    // Razor `@* ... *@` comments (Singleline so `.` also matches the newlines inside
    // multi-line doc-style comment blocks, common throughout Lumeo's .razor files).
    private static readonly Regex RazorCommentRegex = new(
        "@\\*.*?\\*@", RegexOptions.Compiled | RegexOptions.Singleline);

    // C# `///` XML-doc lines (the only line-comment style Lumeo's @code blocks use).
    private static readonly Regex XmlDocLineRegex = new(
        "^[ \\t]*///.*$", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Strips every comment form Lumeo's .razor files actually use before
    /// ExtractA11y's signal regexes run over the raw source. Comments are prose, and
    /// prose routinely references the very tokens this scanner looks for — to explain
    /// why a component does or does NOT have a behaviour — not to declare markup: e.g.
    /// ScrollArea.razor's own AriaLabel doc comment argues FOR adding a tab stop by
    /// noting "ScrollArea had no tabindex/@onkeydown of its own", and a naive
    /// <see cref="string.Contains(string)"/> over the whole file reads that prose as
    /// if it were a live @onkeydown handler, mis-reporting keyboardInteractive: true
    /// for a component with no key handler at all (PR #356 round-7, Codex P2). Round-8
    /// (Codex P2) found the same false positive survives via a plain `//` line note —
    /// InputMask.razor's "previous component ... handled Backspace in @onkeydown"
    /// history comment — because only Razor `@* *@` and `///` were stripped; `//` and
    /// `/* */` are now stripped too, via <see cref="StripLineAndBlockComments"/>. This
    /// is a general class of false positive, not a one-off in ScrollArea/InputMask
    /// specifically, so it's fixed at the scanner rather than by only rewording those
    /// comments.
    /// </summary>
    internal static string StripCommentsForA11yScan(string text)
    {
        text = RazorCommentRegex.Replace(text, "");
        text = XmlDocLineRegex.Replace(text, "");
        text = StripLineAndBlockComments(text);
        return text;
    }

    /// <summary>
    /// Strips C#-style `//` line comments and `/* ... */` block comments, WITHOUT
    /// touching text inside a quoted string — a naive regex would treat the `//` in a
    /// URL like <c>"https://example.com"</c> (or an href in Razor/HTML markup, which
    /// uses the same <c>"..."</c> delimiter) as a comment start and delete the rest of
    /// the line (PR #356 round-8, Codex P2). This is a small character-scanning state
    /// machine, not a full C#/Razor tokenizer — this whole file is "best effort, not a
    /// parser" (see <see cref="ExtractA11y"/>) — but tracking one string state is
    /// enough to keep `//`/`/* */` recognition out of any quoted text, string or markup
    /// alike, since both use <c>"..."</c>.
    /// </summary>
    internal static string StripLineAndBlockComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        var inString = false;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];

            if (inString)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < text.Length)
                {
                    // Preserve the escaped char verbatim so `\"` doesn't end the string.
                    sb.Append(text[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == '"') inString = false;
                i++;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                sb.Append(c);
                i++;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                // Line comment: drop through to (but keep) the line break.
                i += 2;
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                // Block comment: drop through to the closing */ (or EOF if unterminated).
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i = Math.Min(i + 2, text.Length);
                continue;
            }

            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    // A `role="@(...)"` Razor expression (e.g. a ternary that picks role="img" based on
    // a computed property, as Chart's canvas host does). Regex can't evaluate the
    // expression, but the literal role token it ultimately assigns is almost always
    // written inline as a quoted string literal in a ternary/switch arm inside the
    // parens, so the expression body is captured and re-scanned for bare string
    // literals below (PR #356 round-8, Codex P2: comment-stripping removed the doc
    // comment that used to accidentally supply this token as prose).
    private static readonly Regex DynamicRoleExprRegex = new(
        "role=\"@\\((?<expr>.*?)\\)\"", RegexOptions.Compiled | RegexOptions.Singleline);

    // A `role="@PropertyName"` Razor binding (e.g. Result.razor's `role="@AriaRole"`,
    // Toast.razor's `role="@Role"`) — a bare member reference, not the `@(...)` expression
    // form above. The member itself lives somewhere in the component's file set and is
    // resolved via ExpressionBodiedMemberRegex below (PR #356 round-9, CodeRabbit).
    private static readonly Regex PropertyRoleRefRegex = new(
        "role=\"@(?<prop>[A-Za-z_][A-Za-z0-9_]*)\"", RegexOptions.Compiled);

    // Expression-bodied member declarations (`<modifiers> <type> Name => <body>;`), e.g.
    // `private string AriaRole => Status is ... ? "alert" : "status";`. Used to resolve a
    // `role="@PropertyName"` reference: the referenced member's body is re-scanned for bare
    // string literals, same one-hop, no-full-evaluation heuristic as DynamicRoleExprRegex —
    // block-bodied getters (`{ get { ... } }`) and further indirection (the property calling
    // another property) are NOT chased.
    private static readonly Regex ExpressionBodiedMemberRegex = new(
        @"(?:private|protected|internal|public)?\s*(?:static\s+)?[\w<>\[\],\.\?]+\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=>\s*(?<body>[^;]*);",
        RegexOptions.Compiled);

    /// <summary>
    /// Static a11y signal extraction from a component's .razor source: the ARIA roles and
    /// aria-* attributes it renders, the keyboard keys it handles, and whether it manages
    /// focus. Powers the MCP's a11y view so an agent can see a component's accessibility
    /// contract without reading the source. Regex over markup — best-effort, not a parser.
    /// </summary>
    internal static object ExtractA11y(IEnumerable<string> razorFiles)
    {
        var roles = new SortedSet<string>(StringComparer.Ordinal);
        var ariaAttrs = new SortedSet<string>(StringComparer.Ordinal);
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var keyboardInteractive = false;
        var focusManaged = false;

        // Pre-read + comment-strip every file once, and index expression-bodied member
        // bodies (name -> bodies) across the WHOLE component's file set up front, so a
        // `role="@PropertyName"` reference in one file (e.g. the root .razor) can resolve
        // to a property declared in a sibling file too, not just the same one.
        var texts = new List<(string File, string Text)>();
        var memberBodies = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in razorFiles)
        {
            string text;
            try { text = File.ReadAllText(file); } catch { continue; }
            text = StripCommentsForA11yScan(text);
            texts.Add((file, text));

            foreach (Match m in ExpressionBodiedMemberRegex.Matches(text))
            {
                var name = m.Groups["name"].Value;
                if (!memberBodies.TryGetValue(name, out var bodies))
                    memberBodies[name] = bodies = new List<string>();
                bodies.Add(m.Groups["body"].Value);
            }
        }

        foreach (var (_, text) in texts)
        {
            // Literal role="..." values.
            foreach (Match m in Regex.Matches(text, "role=\"([a-zA-Z]+)\"")) roles.Add(m.Groups[1].Value);
            // role assigned through a Dictionary<string, object> splat attribute (e.g.
            // Icon's `merged["role"] = "img";`), the pattern components use when the
            // role is one of several conditionally-merged attributes.
            foreach (Match m in Regex.Matches(text, "\\[\"role\"\\]\\s*=\\s*\"([a-zA-Z]+)\"")) roles.Add(m.Groups[1].Value);
            // role picked inside a Razor `role="@(...)"` expression (ternary/switch
            // arm) — pull any bare string-literal role token out of the expression.
            foreach (Match outer in DynamicRoleExprRegex.Matches(text))
                foreach (Match inner in Regex.Matches(outer.Groups["expr"].Value, "\"([a-zA-Z]+)\""))
                    roles.Add(inner.Groups[1].Value);
            // role picked via a bare property/field reference (`role="@PropertyName"`) —
            // resolve the member in the component's file set and harvest bare string
            // literals from its body.
            foreach (Match m in PropertyRoleRefRegex.Matches(text))
            {
                if (!memberBodies.TryGetValue(m.Groups["prop"].Value, out var bodies)) continue;
                foreach (var body in bodies)
                    foreach (Match inner in Regex.Matches(body, "\"([a-zA-Z]+)\""))
                        roles.Add(inner.Groups[1].Value);
            }
            // aria-* attribute NAMES (e.g. aria-expanded, aria-label), whether rendered
            // as a literal HTML attribute (aria-label="...") or as a Dictionary<string,
            // object> splat attribute key (Icon's `merged["aria-hidden"] = "true";`) —
            // the optional `"]` tolerates the dictionary-indexer syntax before the `=`.
            foreach (Match m in Regex.Matches(text, "(aria-[a-z]+)\"?\\]?\\s*=")) ariaAttrs.Add(m.Groups[1].Value);

            if (text.Contains("@onkeydown") || text.Contains("KeyboardEventArgs"))
            {
                keyboardInteractive = true;
                foreach (var (_, k) in MatchKeyLiteralOccurrences(text))
                {
                    if (!KnownKeys.Contains(k)) continue; // still whitelist-gated
                    keys.Add(k == " " ? "Space" : k);
                }

                // One-hop local-variable resolution (see TupleKeyAssignRegex's doc comment).
                var localKeyLiterals = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
                foreach (Match m in TupleKeyAssignRegex.Matches(text))
                {
                    void Track(string varName, string literal)
                    {
                        if (!localKeyLiterals.TryGetValue(varName, out var set))
                            localKeyLiterals[varName] = set = new HashSet<string>(StringComparer.Ordinal);
                        set.Add(literal);
                    }
                    Track(m.Groups["v1"].Value, m.Groups["a1"].Value);
                    Track(m.Groups["v1"].Value, m.Groups["b1"].Value);
                    Track(m.Groups["v2"].Value, m.Groups["a2"].Value);
                    Track(m.Groups["v2"].Value, m.Groups["b2"].Value);
                }
                if (localKeyLiterals.Count > 0)
                {
                    foreach (Match m in LocalKeyUseRegex.Matches(text))
                    {
                        if (!localKeyLiterals.TryGetValue(m.Groups["v"].Value, out var literals)) continue;
                        foreach (var lit in literals)
                        {
                            if (!KnownKeys.Contains(lit)) continue; // still whitelist-gated
                            keys.Add(lit == " " ? "Space" : lit);
                        }
                    }
                }
            }
            if (text.Contains("FocusAsync") || text.Contains("FocusElement") || text.Contains("tabindex"))
                focusManaged = true;
        }

        return new Dictionary<string, object?>
        {
            ["roles"] = roles.ToArray(),
            ["ariaAttributes"] = ariaAttrs.ToArray(),
            ["keys"] = keys.ToArray(),
            ["keyboardInteractive"] = keyboardInteractive,
            ["focusManaged"] = focusManaged,
        };
    }

    private static object SerializeParam(RazorParameterScanner.ParameterInfo p)
        => new
        {
            name = p.Name,
            type = p.Type,
            @default = p.Default,
            description = p.Description,
            isCascading = p.IsCascading,
            captureUnmatched = p.CaptureUnmatched,
            isEditorRequired = p.IsEditorRequired,
        };

    private static object SerializeEvent(RazorParameterScanner.EventInfo e)
        => new
        {
            name = e.Name,
            type = e.Type,
            description = e.Description,
        };

    private static object SerializeEnum(RazorParameterScanner.EnumInfo en)
        => new
        {
            name = en.Name,
            values = en.Values,
            description = en.Description,
        };

    private static object SerializeRecord(RazorParameterScanner.RecordInfo r)
        => new
        {
            name = r.Name,
            signature = r.Signature,
            description = r.Description,
        };
}
