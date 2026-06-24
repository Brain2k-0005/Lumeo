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

            // Parse each razor file
            var perFile = new Dictionary<string, RazorParameterScanner.RazorFileSchema>(StringComparer.Ordinal);
            foreach (var rf in razorFiles)
            {
                var schema = RazorParameterScanner.Scan(rf);
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

            // Aggregate counts
            if (root is not null)
            {
                totalParams += root.Parameters.Length;
                totalEnums += root.Enums.Length;
                totalRecords += root.Records.Length;
            }
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
                ["enums"] = root?.Enums.Select(SerializeEnum).ToArray() ?? Array.Empty<object>(),
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
        object[] services = Array.Empty<object>();
        int serviceCount = 0;
        if (repoRoot is not null)
        {
            var serviceTypes = ScanServices(repoRoot, logger);
            serviceCount = serviceTypes.Count;
            services = serviceTypes.Select(SerializeService).ToArray();
        }

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
        var json = JsonSerializer.Serialize(rootJson, jsonOpts);
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
    private static readonly string[] KnownKeys =
    {
        "Enter", "Escape", "Tab", " ", "Spacebar", "ArrowUp", "ArrowDown", "ArrowLeft",
        "ArrowRight", "Home", "End", "PageUp", "PageDown", "Delete", "Backspace", "ContextMenu", "F10",
    };

    /// <summary>
    /// Static a11y signal extraction from a component's .razor source: the ARIA roles and
    /// aria-* attributes it renders, the keyboard keys it handles, and whether it manages
    /// focus. Powers the MCP's a11y view so an agent can see a component's accessibility
    /// contract without reading the source. Regex over markup — best-effort, not a parser.
    /// </summary>
    private static object ExtractA11y(IEnumerable<string> razorFiles)
    {
        var roles = new SortedSet<string>(StringComparer.Ordinal);
        var ariaAttrs = new SortedSet<string>(StringComparer.Ordinal);
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var keyboardInteractive = false;
        var focusManaged = false;

        foreach (var file in razorFiles)
        {
            string text;
            try { text = File.ReadAllText(file); } catch { continue; }

            // Literal role="..." values (skip dynamic role="@(...)").
            foreach (Match m in Regex.Matches(text, "role=\"([a-zA-Z]+)\"")) roles.Add(m.Groups[1].Value);
            // aria-* attribute NAMES (e.g. aria-expanded, aria-label).
            foreach (Match m in Regex.Matches(text, "(aria-[a-z]+)\\s*=")) ariaAttrs.Add(m.Groups[1].Value);

            if (text.Contains("@onkeydown") || text.Contains("KeyboardEventArgs"))
            {
                keyboardInteractive = true;
                foreach (var k in KnownKeys)
                    if (text.Contains("\"" + k + "\"")) keys.Add(k == " " ? "Space" : k);
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
