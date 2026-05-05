using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lumeo.Cli;

/// <summary>
/// Handles <c>lumeo apply [preset]</c> — decodes a short base62 preset produced by
/// the /themes customizer page (falling back to the Cloudflare Worker at
/// <see cref="LumeoPresetApi.BaseUrl"/> for server-stored IDs) and writes the
/// resolved configuration into <c>lumeo.json</c> and <c>wwwroot/lumeo-theme.json</c>
/// so build-time consumers pick it up.
/// </summary>
public static class ThemeCommands
{
    /// <summary>Encodes named options into the 6-char client-side preset code.
    /// Unknown option values error out with the list of valid choices so typos
    /// are caught before the code is printed.</summary>
    public static Task Encode(
        string? theme, string? style, string? baseColor, string? radius,
        string? font, string? icons, string? menuColor, string? menuAccent,
        bool dark, bool commandOnly)
    {
        int? Idx(string[] valid, string? value, string label, int defaultIdx)
        {
            if (string.IsNullOrWhiteSpace(value)) return defaultIdx;
            var i = Array.FindIndex(valid, v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));
            if (i < 0)
            {
                Console.Error.WriteLine(Ansi.Red($"Invalid --{label} '{value}'. Valid: {string.Join(", ", valid.Where(v => v.Length > 0))}"));
                Environment.ExitCode = 2;
                return null;
            }
            return i;
        }

        var themeIdx = Idx(LumeoPresetOptions.Themes, theme, "theme", 0);
        var styleIdx = Idx(LumeoPresetOptions.Styles, style, "style", 0);
        var baseIdx = Idx(LumeoPresetOptions.BaseColors, baseColor, "base", 0);
        var radiusIdx = Idx(LumeoPresetOptions.Radii, radius, "radius", 2);
        var fontIdx = Idx(LumeoPresetOptions.Fonts, font, "font", 0);
        var iconsIdx = Idx(LumeoPresetOptions.IconLibraries, icons, "icons", 0);
        var menuColorIdx = Idx(LumeoPresetOptions.MenuColors, menuColor, "menu-color", 0);
        var menuAccentIdx = Idx(LumeoPresetOptions.MenuAccents, menuAccent, "menu-accent", 0);
        if (themeIdx is null || styleIdx is null || baseIdx is null || radiusIdx is null
            || fontIdx is null || iconsIdx is null || menuColorIdx is null || menuAccentIdx is null)
        {
            return Task.CompletedTask; // exit code already set
        }

        var preset = new LumeoPreset(
            Theme: themeIdx.Value, Style: styleIdx.Value, BaseColor: baseIdx.Value,
            Radius: radiusIdx.Value, Font: fontIdx.Value, IconLibrary: iconsIdx.Value,
            MenuColor: menuColorIdx.Value, MenuAccent: menuAccentIdx.Value,
            Dark: dark ? 1 : 0);

        var code = LumeoPresetCodec.Encode(preset);
        if (commandOnly)
        {
            Console.WriteLine($"lumeo apply --preset {code}");
        }
        else
        {
            Console.WriteLine(code);
        }
        return Task.CompletedTask;
    }


    // All theme keys that <c>apply</c> understands. Used for --only filtering
    // and to ensure lumeo-theme.json has a stable, known shape.
    private static readonly string[] AllParts =
        { "theme", "style", "baseColor", "radius", "font", "iconLibrary", "menuColor", "menuAccent", "dark" };

    // Aliases so users don't need to type "iconLibrary" or "baseColor" for --only.
    private static readonly Dictionary<string, string[]> PartAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["theme"] = new[] { "theme" },
        ["style"] = new[] { "style" },
        ["basecolor"] = new[] { "baseColor" },
        ["base"] = new[] { "baseColor" },
        ["radius"] = new[] { "radius" },
        ["font"] = new[] { "font" },
        ["icons"] = new[] { "iconLibrary" },
        ["iconlibrary"] = new[] { "iconLibrary" },
        ["menu"] = new[] { "menuColor", "menuAccent" },
        ["menucolor"] = new[] { "menuColor" },
        ["menuaccent"] = new[] { "menuAccent" },
        ["dark"] = new[] { "dark" },
    };

    public static async Task Apply(string preset, string? only, bool dryRun, bool yes, bool silent)
    {
        void Info(string line) { if (!silent) Console.WriteLine(line); }
        void InfoBlank() { if (!silent) Console.WriteLine(); }

        if (string.IsNullOrWhiteSpace(preset))
        {
            Console.Error.WriteLine(Ansi.Red("Missing preset. Usage: lumeo apply <preset>  (or --preset <id>)"));
            Console.Error.WriteLine(Ansi.Dim("Example: lumeo apply b4Ndd7"));
            Environment.ExitCode = 2;
            return;
        }

        // Resolve --only into a concrete allow-list of normalized keys, or null for "apply all".
        HashSet<string>? allowed = null;
        if (!string.IsNullOrWhiteSpace(only))
        {
            allowed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!PartAliases.TryGetValue(raw, out var expanded))
                {
                    Console.Error.WriteLine(Ansi.Red($"Unknown --only part '{raw}'. Valid: {string.Join(", ", PartAliases.Keys)}"));
                    Environment.ExitCode = 2;
                    return;
                }
                foreach (var k in expanded) allowed.Add(k);
            }
        }

        // Step 1: decode. Client-side first; fall back to Worker for server-stored IDs.
        LumeoThemeConfig? resolved;
        string source;
        if (LumeoPresetCodec.TryDecode(preset, out var decoded))
        {
            source = "client-side";
            resolved = ResolveFromDecoded(decoded);
        }
        else
        {
            source = "server";
            resolved = await TryFetchFromWorker(preset);
            if (resolved is null) return; // Error already printed + exit code set.
        }

        // Step 2: surface what was resolved.
        InfoBlank();
        Info(Ansi.Bold($"Preset {preset} decoded") + Ansi.Dim($" (via {source})") + ":");
        Row("theme", resolved.Theme, silent);
        Row("style", resolved.Style, silent);
        Row("baseColor", resolved.BaseColor, silent);
        Row("radius", resolved.Radius, silent);
        Row("font", resolved.Font, silent);
        Row("iconLibrary", resolved.IconLibrary, silent);
        Row("menuColor", resolved.MenuColor, silent);
        Row("menuAccent", resolved.MenuAccent, silent);
        Row("dark", resolved.Dark?.ToString(), silent);
        InfoBlank();

        if (allowed is not null)
            Info(Ansi.Dim($"--only filter: {string.Join(",", allowed.OrderBy(s => s))}"));

        if (dryRun)
        {
            Info(Ansi.Dim("--dry-run: nothing was written."));
            return;
        }

        // Step 3: locate lumeo.json. shadcn-parity: require init first.
        var cfg = ConfigIO.TryLoad();
        if (cfg is null)
        {
            Console.Error.WriteLine(Ansi.Red("No lumeo.json found. Run `lumeo init` first, then re-run `lumeo apply`."));
            Environment.ExitCode = 1;
            return;
        }

        // Step 4: confirmation gate (skipped by --yes or --silent).
        if (!yes && !silent && Prompts.Interactive)
        {
            if (!Prompts.Confirm("Write changes to lumeo.json + wwwroot/lumeo-theme.json?", defaultYes: true))
            {
                Console.WriteLine(Ansi.Yellow("Aborted — no files written."));
                return;
            }
        }

        // Step 5: back up lumeo.json before mutating.
        var configPath = Path.Combine(Environment.CurrentDirectory, Paths.ConfigFile);
        var backupPath = configPath + ".bak";
        try
        {
            File.Copy(configPath, backupPath, overwrite: true);
            Info(Ansi.Dim($"  backup     → {Paths.ConfigFile}.bak"));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(Ansi.Yellow($"! Could not back up {Paths.ConfigFile}: {ex.Message}"));
        }

        // Step 6: merge into cfg.Theme (only the allowed keys).
        cfg.Theme ??= new LumeoThemeConfig();
        MergeInto(cfg.Theme, resolved, allowed);
        ConfigIO.Save(cfg);
        Info(Ansi.Green("  write      ") + Paths.ConfigFile + Ansi.Dim(" (theme section)"));

        // Step 7: write wwwroot/lumeo-theme.json (merge-preserving unknown keys).
        var themeJsonRel = Path.Combine("wwwroot", "lumeo-theme.json");
        var themeJsonPath = Path.Combine(Environment.CurrentDirectory, themeJsonRel);
        var wwwroot = Path.Combine(Environment.CurrentDirectory, "wwwroot");
        if (!Directory.Exists(wwwroot))
        {
            Console.Error.WriteLine(Ansi.Yellow($"! wwwroot/ not found — skipping {themeJsonRel}."));
            Console.Error.WriteLine(Ansi.Dim("  (run `lumeo init --with-css` or create wwwroot/ manually if you want runtime theme defaults)"));
        }
        else
        {
            WriteOrMergeThemeJson(themeJsonPath, resolved, allowed);
            Info(Ansi.Green("  write      ") + themeJsonRel);
        }

        // Step 8: install the icon-library NuGet package if the preset requires one
        // the consumer doesn't already have. Blazicons are compile-time — theme.js
        // can't switch them at runtime — so this is the only way to keep the CLI
        // apply step 1:1 with the customizer's icon selection.
        if ((allowed is null || allowed.Contains("iconLibrary")) && !string.IsNullOrEmpty(resolved.IconLibrary))
        {
            await MaybeInstallIconPackageAsync(resolved.IconLibrary, yes, silent);
        }

        // Step 9: self-host the font (shadcn / next.js style) so the consumer doesn't
        // depend on fonts.googleapis.com at runtime. theme.js reads the local path
        // from lumeo-theme.json and injects a <link> to it on page load.
        if ((allowed is null || allowed.Contains("font"))
            && !string.IsNullOrEmpty(resolved.Font) && resolved.Font != "system"
            && Directory.Exists(wwwroot))
        {
            using var http = new HttpClient();
            var localPath = await FontInstaller.InstallAsync(resolved.Font, wwwroot, http, silent);
            if (!string.IsNullOrEmpty(localPath))
            {
                // Write the local path into lumeo-theme.json under a well-known key
                // so theme.js can pick it up. Preserves any other keys (like a
                // consumer-added override) that live in the same file.
                try
                {
                    var themeJson = Path.Combine(wwwroot, "lumeo-theme.json");
                    var node = File.Exists(themeJson)
                        ? System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(themeJson)) as System.Text.Json.Nodes.JsonObject
                        : new System.Text.Json.Nodes.JsonObject();
                    node!["fontLocalPath"] = localPath;
                    await File.WriteAllTextAsync(themeJson, node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(Ansi.Yellow($"! Could not persist fontLocalPath to lumeo-theme.json: {ex.Message}"));
                }
            }
        }

        InfoBlank();
        Info(Ansi.Green("OK ") + $"Applied preset {Ansi.Bold(preset)}.");
    }

    // Maps the customizer's icon library id to the corresponding Blazicons NuGet id.
    // Keep in sync with LumeoPresetOptions.IconLibraries + the docs customizer.
    private static readonly Dictionary<string, string> IconLibraryPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lucide"] = "Blazicons.Lucide",
        ["bootstrap"] = "Blazicons.Bootstrap",
        ["fluentui"] = "Blazicons.FluentUI",
        ["font-awesome"] = "Blazicons.FontAwesome",
        ["google-material"] = "Blazicons.GoogleMaterialDesign",
        ["material-design"] = "Blazicons.MaterialDesignIcons",
        ["ionicons"] = "Blazicons.Ionicons",
        ["devicon"] = "Blazicons.Devicon",
        ["flag-icons"] = "Blazicons.FlagIcons",
    };

    private static async Task MaybeInstallIconPackageAsync(string iconLib, bool yes, bool silent)
    {
        void Info(string line) { if (!silent) Console.WriteLine(line); }

        if (!IconLibraryPackages.TryGetValue(iconLib, out var packageId))
        {
            Console.Error.WriteLine(Ansi.Yellow($"! Unknown icon library '{iconLib}' — no NuGet package mapped. Skipping install."));
            return;
        }

        var csproj = Directory.EnumerateFiles(Environment.CurrentDirectory, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (csproj is null)
        {
            Console.Error.WriteLine(Ansi.Yellow($"! No .csproj in current directory — skipping {packageId} install. Run `dotnet add package {packageId}` manually."));
            return;
        }

        // Cheap check: scan the csproj for an existing PackageReference before shelling out.
        try
        {
            var contents = await File.ReadAllTextAsync(csproj);
            if (contents.Contains($"\"{packageId}\"", StringComparison.OrdinalIgnoreCase))
            {
                Info(Ansi.Dim($"  icons      {packageId} already referenced — skipped."));
                return;
            }
        }
        catch { /* fall through and let dotnet handle it */ }

        if (!yes && Prompts.Interactive)
        {
            if (!Prompts.Confirm($"Install {Ansi.Cyan(packageId)} NuGet package?", defaultYes: true))
            {
                Info(Ansi.Yellow($"  icons      skipped — add with `dotnet add package {packageId}` when ready."));
                return;
            }
        }

        Info(Ansi.Dim($"  icons      installing {packageId} …"));
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"add \"{csproj}\" package {packageId}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) throw new InvalidOperationException("failed to start dotnet");
            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0)
            {
                var stderr = await proc.StandardError.ReadToEndAsync();
                Console.Error.WriteLine(Ansi.Yellow($"! `dotnet add package {packageId}` exited {proc.ExitCode}: {stderr.Trim()}"));
            }
            else
            {
                Info(Ansi.Green("  icons      ") + $"{packageId} installed.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(Ansi.Yellow($"! Failed to install {packageId}: {ex.Message}"));
        }
    }

    // Resolve the numeric indices from the codec to human-readable string values.
    private static LumeoThemeConfig ResolveFromDecoded(LumeoPreset decoded) => new()
    {
        Theme = NullIfEmpty(LumeoPresetOptions.At(LumeoPresetOptions.Themes, decoded.Theme)),
        Style = LumeoPresetOptions.At(LumeoPresetOptions.Styles, decoded.Style, "default"),
        BaseColor = LumeoPresetOptions.At(LumeoPresetOptions.BaseColors, decoded.BaseColor, "slate"),
        Radius = LumeoPresetOptions.At(LumeoPresetOptions.Radii, decoded.Radius, "0.5"),
        Font = LumeoPresetOptions.At(LumeoPresetOptions.Fonts, decoded.Font, "system"),
        IconLibrary = LumeoPresetOptions.At(LumeoPresetOptions.IconLibraries, decoded.IconLibrary, "lucide"),
        MenuColor = LumeoPresetOptions.At(LumeoPresetOptions.MenuColors, decoded.MenuColor, "default"),
        MenuAccent = LumeoPresetOptions.At(LumeoPresetOptions.MenuAccents, decoded.MenuAccent, "subtle"),
        Dark = decoded.Dark == 1,
    };

    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

    // Fetch a server-stored preset from the Cloudflare Worker. Returns null (and
    // sets ExitCode + prints a useful error) on any failure.
    private static async Task<LumeoThemeConfig?> TryFetchFromWorker(string id)
    {
        var url = $"{LumeoPresetApi.BaseUrl}/preset/{Uri.EscapeDataString(id)}";
        HttpResponseMessage resp;
        try
        {
            resp = await RegistryLoader.s_http.GetAsync(url);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(Ansi.Red(
                $"Failed to reach preset API ({url}): {ex.Message}"));
            Console.Error.WriteLine(Ansi.Dim("Check your network connection or pass a valid 6-char client-side code."));
            Environment.ExitCode = 1;
            return null;
        }

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Console.Error.WriteLine(Ansi.Red(
                $"Preset '{id}' not found on server (404) and isn't a valid client-side code."));
            Console.Error.WriteLine(Ansi.Dim("Verify the code or check connectivity."));
            Environment.ExitCode = 1;
            return null;
        }
        if (!resp.IsSuccessStatusCode)
        {
            Console.Error.WriteLine(Ansi.Red(
                $"Preset API returned {(int)resp.StatusCode} {resp.ReasonPhrase} for '{id}'."));
            Environment.ExitCode = 1;
            return null;
        }

        string body;
        try { body = await resp.Content.ReadAsStringAsync(); }
        catch (Exception ex)
        {
            Console.Error.WriteLine(Ansi.Red($"Failed to read preset payload: {ex.Message}"));
            Environment.ExitCode = 1;
            return null;
        }

        try
        {
            var cfg = JsonSerializer.Deserialize<LumeoThemeConfig>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (cfg is null)
            {
                Console.Error.WriteLine(Ansi.Red($"Preset '{id}' returned an empty or invalid payload."));
                Environment.ExitCode = 1;
                return null;
            }
            return cfg;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(Ansi.Red($"Preset '{id}' payload is not valid JSON: {ex.Message}"));
            Environment.ExitCode = 1;
            return null;
        }
    }

    // Merge `incoming` into `target`. If `allowed` is non-null, only those keys are copied.
    private static void MergeInto(LumeoThemeConfig target, LumeoThemeConfig incoming, HashSet<string>? allowed)
    {
        bool Ok(string key) => allowed is null || allowed.Contains(key);
        if (Ok("theme") && incoming.Theme is not null) target.Theme = incoming.Theme;
        if (Ok("style") && incoming.Style is not null) target.Style = incoming.Style;
        if (Ok("baseColor") && incoming.BaseColor is not null) target.BaseColor = incoming.BaseColor;
        if (Ok("radius") && incoming.Radius is not null) target.Radius = incoming.Radius;
        if (Ok("font") && incoming.Font is not null) target.Font = incoming.Font;
        if (Ok("iconLibrary") && incoming.IconLibrary is not null) target.IconLibrary = incoming.IconLibrary;
        if (Ok("menuColor") && incoming.MenuColor is not null) target.MenuColor = incoming.MenuColor;
        if (Ok("menuAccent") && incoming.MenuAccent is not null) target.MenuAccent = incoming.MenuAccent;
        if (Ok("dark") && incoming.Dark is not null) target.Dark = incoming.Dark;
    }

    // Write or merge lumeo-theme.json, preserving keys the user added manually that we don't know about.
    private static void WriteOrMergeThemeJson(string path, LumeoThemeConfig resolved, HashSet<string>? allowed)
    {
        JsonObject root;
        if (File.Exists(path))
        {
            try
            {
                var parsed = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
                root = parsed ?? new JsonObject();
            }
            catch
            {
                // Malformed — start fresh but preserve old file as .bak.
                try { File.Copy(path, path + ".bak", overwrite: true); } catch { /* ignore */ }
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        void Set(string key, JsonNode? value)
        {
            if (allowed is not null && !allowed.Contains(key)) return;
            if (value is null) return;
            root[key] = value;
        }

        Set("theme", resolved.Theme is null ? null : JsonValue.Create(resolved.Theme));
        Set("style", resolved.Style is null ? null : JsonValue.Create(resolved.Style));
        Set("baseColor", resolved.BaseColor is null ? null : JsonValue.Create(resolved.BaseColor));
        Set("radius", resolved.Radius is null ? null : JsonValue.Create(resolved.Radius));
        Set("font", resolved.Font is null ? null : JsonValue.Create(resolved.Font));
        Set("iconLibrary", resolved.IconLibrary is null ? null : JsonValue.Create(resolved.IconLibrary));
        Set("menuColor", resolved.MenuColor is null ? null : JsonValue.Create(resolved.MenuColor));
        Set("menuAccent", resolved.MenuAccent is null ? null : JsonValue.Create(resolved.MenuAccent));
        Set("dark", resolved.Dark is null ? null : JsonValue.Create(resolved.Dark));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
    }

    private static void Row(string label, string? value, bool silent)
    {
        if (silent) return;
        Console.WriteLine($"  {Ansi.Dim(label.PadRight(14))} {value ?? Ansi.Dim("(unset)")}");
    }
}
