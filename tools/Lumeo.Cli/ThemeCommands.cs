namespace Lumeo.Cli;

/// <summary>
/// Handles <c>lumeo theme apply --preset &lt;code&gt;</c> — decodes a short base62 preset
/// produced by the /themes customizer page and prints the resolved configuration plus
/// a ready-to-paste CSS snippet. Writing to a specific file is a future enhancement;
/// for now we keep the flow safe and explicit (user decides where the CSS lands).
/// </summary>
public static class ThemeCommands
{
    public static Task Apply(string preset, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            Console.Error.WriteLine(Ansi.Red("Missing --preset <code>. Example: lumeo theme apply --preset b4Ndd7"));
            Environment.ExitCode = 2;
            return Task.CompletedTask;
        }

        if (!LumeoPresetCodec.TryDecode(preset, out var decoded))
        {
            Console.Error.WriteLine(Ansi.Red($"Invalid preset code '{preset}'. Expected 6 base62 characters from the Lumeo /themes customizer."));
            Environment.ExitCode = 2;
            return Task.CompletedTask;
        }

        var theme = LumeoPresetOptions.At(LumeoPresetOptions.Themes, decoded.Theme);
        var style = LumeoPresetOptions.At(LumeoPresetOptions.Styles, decoded.Style, "default");
        var baseColor = LumeoPresetOptions.At(LumeoPresetOptions.BaseColors, decoded.BaseColor, "slate");
        var radius = LumeoPresetOptions.At(LumeoPresetOptions.Radii, decoded.Radius, "0.5");
        var font = LumeoPresetOptions.At(LumeoPresetOptions.Fonts, decoded.Font, "system");
        var iconLib = LumeoPresetOptions.At(LumeoPresetOptions.IconLibraries, decoded.IconLibrary, "lucide");
        var menuColor = LumeoPresetOptions.At(LumeoPresetOptions.MenuColors, decoded.MenuColor, "default");
        var menuAccent = LumeoPresetOptions.At(LumeoPresetOptions.MenuAccents, decoded.MenuAccent, "subtle");
        var dark = decoded.Dark == 1;

        Console.WriteLine();
        Console.WriteLine(Ansi.Bold($"Preset {preset} decoded:"));
        Row("Theme",         string.IsNullOrEmpty(theme) ? "default" : theme);
        Row("Style",         style);
        Row("Base Color",    baseColor);
        Row("Radius",        $"{radius}rem");
        Row("Font",          font);
        Row("Icon Library",  iconLib);
        Row("Menu Color",    menuColor);
        Row("Menu Accent",   menuAccent);
        Row("Dark Mode",     dark ? "on" : "off");
        Console.WriteLine();

        // The Lumeo theme system applies these via JS (themeManager.*) AND CSS files.
        // The runtime theme-switcher handles everything at runtime; for a build-time
        // setup we print the equivalent data-attributes / <html class> so the consumer
        // can wire them into their layout or startup JS.
        var fontFamily = FontFamily(font);
        Console.WriteLine(Ansi.Dim("// Apply at runtime (paste into your startup JS / _Host.cshtml):"));
        Console.WriteLine($"  document.documentElement.classList.toggle('dark', {(dark ? "true" : "false")});");
        if (!string.IsNullOrEmpty(theme))
            Console.WriteLine($"  document.documentElement.setAttribute('data-theme', '{theme}');");
        Console.WriteLine($"  document.documentElement.setAttribute('data-style', '{style}');");
        Console.WriteLine($"  document.documentElement.setAttribute('data-base', '{baseColor}');");
        Console.WriteLine($"  document.documentElement.setAttribute('data-menu', '{menuColor}');");
        Console.WriteLine($"  document.documentElement.setAttribute('data-menu-accent', '{menuAccent}');");
        Console.WriteLine($"  document.documentElement.style.setProperty('--radius', '{radius}rem');");
        Console.WriteLine($"  document.documentElement.style.setProperty('--font-sans', \"{fontFamily}\");");
        Console.WriteLine();

        if (dryRun)
        {
            Console.WriteLine(Ansi.Dim("--dry-run: nothing was written."));
        }
        else
        {
            Console.WriteLine(Ansi.Dim("If you use the Lumeo theme-switcher component at runtime, the above is automatic."));
            Console.WriteLine(Ansi.Dim("Write operations into a specific theme.css will ship in a follow-up."));
        }

        return Task.CompletedTask;
    }

    private static string FontFamily(string id) => id switch
    {
        "inter" => "'Inter', sans-serif",
        "geist" => "'Geist', sans-serif",
        "ibm-plex-sans" => "'IBM Plex Sans', sans-serif",
        "jetbrains-mono" => "'JetBrains Mono', monospace",
        "fira-code" => "'Fira Code', monospace",
        _ => "system-ui, -apple-system, sans-serif",
    };

    private static void Row(string label, string value)
    {
        Console.WriteLine($"  {Ansi.Dim(label.PadRight(14))} {value}");
    }
}
