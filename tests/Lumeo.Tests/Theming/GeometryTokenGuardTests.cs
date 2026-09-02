using System.Text.RegularExpressions;
using Xunit;

namespace Lumeo.Tests.Theming;

/// <summary>#434: the geometry tokens are a contract. Every var(--lumeo-…, fallback) a component
/// reads must be declared in lumeo.css with that same fallback as its default, so a consumer
/// who sets the token and one who does not see the same geometry, and every component keeps
/// its data-slot, the selector overrides are written against.</summary>
public class GeometryTokenGuardTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lumeo.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Lumeo.slnx not found above " + AppContext.BaseDirectory);
    }

    private static IEnumerable<string> RazorFiles(string root) =>
        new[] { "src/Lumeo/UI", "src/Lumeo.DataGrid/UI" }
            .Select(p => Path.Combine(root, p)).Where(Directory.Exists)
            .SelectMany(p => Directory.EnumerateFiles(p, "*.razor", SearchOption.AllDirectories));

    [Fact]
    public void Every_Consumed_Token_Is_Declared_With_The_Same_Default()
    {
        var root = RepoRoot();
        var css = File.ReadAllText(Path.Combine(root, "src/Lumeo/wwwroot/css/lumeo.css"));
        var declared = Regex.Matches(css, @"(--lumeo-[a-z0-9-]+):\s*([^;]+);")
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => Regex.Replace(g.First().Groups[2].Value, @"\s+", ""));
        var consumed = new Dictionary<string, HashSet<string>>();
        foreach (var file in RazorFiles(root))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"var\((--lumeo-[a-z0-9-]+),((?:[^()]|\((?:[^()]|\([^()]*\))*\))*)\)"))
            {
                if (!consumed.TryGetValue(m.Groups[1].Value, out var set)) consumed[m.Groups[1].Value] = set = new();
                set.Add(Regex.Replace(m.Groups[2].Value, @"\s+", ""));
            }
        }
        Assert.NotEmpty(consumed);
        // Component-local variables (--lumeo-arrow-y, --lumeo-beam-size, ...) are set inline by
        // the component itself and are not tokens; only what lumeo.css declares is a contract.
        var geometry = new[]
        {
            "--lumeo-control-h-xs", "--lumeo-control-h-sm", "--lumeo-control-h", "--lumeo-control-h-lg", "--lumeo-icon-size",
            "--lumeo-sidebar-item-h-sm", "--lumeo-sidebar-item-h", "--lumeo-sidebar-item-h-lg",
            "--lumeo-table-head-h", "--lumeo-table-cell-p", "--lumeo-grid-cell-px", "--lumeo-grid-cell-py",
        };
        foreach (var token in geometry)
        {
            Assert.True(declared.ContainsKey(token), $"{token} is not declared in lumeo.css");
            Assert.True(consumed.ContainsKey(token), $"{token} is declared but no component reads it");
        }
        foreach (var (token, fallbacks) in consumed)
        {
            if (!declared.TryGetValue(token, out var value)) continue;
            foreach (var fb in fallbacks)
                Assert.True(value == fb, $"{token}: components fall back to '{fb}' but lumeo.css declares '{value}'");
        }
    }

    [Fact]
    public void The_Field_Family_And_Button_Share_The_Control_Height_Token()
    {
        var root = RepoRoot();
        foreach (var c in new[] { "Button/Button", "Input/Input", "Select/SelectTrigger", "DatePicker/DatePicker", "TimePicker/TimePicker", "Cascader/Cascader", "TreeSelect/TreeSelect", "ColorPicker/ColorPicker", "TagInput/TagInput", "NumberInput/NumberInput", "PasswordInput/PasswordInput" })
            Assert.Contains("var(--lumeo-control-h,", File.ReadAllText(Path.Combine(root, "src/Lumeo/UI", c + ".razor")));
    }

    /// <summary>The ten components that render no element of their own delegate the slot to
    /// what they compose; everything else must mark its root.</summary>
    private static readonly HashSet<string> SlotLess = new(StringComparer.OrdinalIgnoreCase)
    {
        "AlertDialog/AlertDialog.razor", "ConfirmButton/ConfirmButton.razor", "DatePicker/DateRangePicker.razor",
        "DensityScope/DensityScope.razor", "Dialog/Dialog.razor", "Drawer/Drawer.razor", "DropdownButton/DropdownButton.razor",
        "Icon/Icon.razor", "Sheet/Sheet.razor", "Stepper/StepperStep.razor",
    };

    [Fact]
    public void Every_Component_Carries_A_Data_Slot()
    {
        var root = RepoRoot();
        var ui = Path.Combine(root, "src/Lumeo/UI");
        var missing = Directory.EnumerateFiles(ui, "*.razor", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(ui, f).Replace('\\', '/'))
            .Where(rel => !SlotLess.Contains(rel))
            .Where(rel => !File.ReadAllText(Path.Combine(ui, rel)).Contains("data-slot="))
            .ToList();
        Assert.True(missing.Count == 0, "components without a data-slot on their root: " + string.Join(", ", missing));
    }
}
