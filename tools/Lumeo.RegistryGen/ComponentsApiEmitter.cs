using System.Text;
using System.Text.Json;

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
        string version)
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
                ["cssVars"] = meta.CssVars,
                ["subComponents"] = subEntries,
                ["parseFailed"] = root?.ParseFailed ?? false,
                ["parseError"] = root?.ParseError,
            };

            components[name] = entry;
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
                ["thinFallbacks"] = thinFallbacks.Select(t => new { name = t.Name, reason = t.Reason }).ToArray(),
            },
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

        logger.WriteLine($"[components-api] Wrote {components.Count} components, {totalParams} params, {totalEnums} enums, {totalRecords} records, {thinFallbacks.Count} thin fallbacks → {outputPath}");
        return components.Count;
    }

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
            ["parseFailed"] = s.ParseFailed,
            ["parseError"] = s.ParseError,
        };

    private static object SerializeParam(RazorParameterScanner.ParameterInfo p)
        => new
        {
            name = p.Name,
            type = p.Type,
            @default = p.Default,
            description = p.Description,
            isCascading = p.IsCascading,
            captureUnmatched = p.CaptureUnmatched,
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
