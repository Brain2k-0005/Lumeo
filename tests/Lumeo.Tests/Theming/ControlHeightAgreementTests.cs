using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Theming;

/// <summary>
/// Every control that reads as "a field" has to be the height of a plain Input.
///
/// This is the defect that kept coming back through the 5.0 scale alignment: a component
/// spells the input box out literally instead of deriving it, the ladder moves, and the
/// literal stays behind. Three review rounds in a row found another one - Cascader,
/// TreeSelect, TagInput and ColorPicker were the last four. Per-component size tests do not
/// catch it, because each one passes against its OWN table; the mismatch only exists in the
/// relationship BETWEEN components, which is what this test asserts.
///
/// It reads Input's height at defaults rather than hard-coding h-8, so the next deliberate
/// move of the ladder updates the expectation for free and only the stragglers fail.
/// </summary>
public class ControlHeightAgreementTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ControlHeightAgreementTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>The h-* / min-h-* token on the element that carries the control's box.</summary>
    private static string HeightToken(string cssClass, string component)
    {
        var tokens = cssClass.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.StartsWith("h-", StringComparison.Ordinal)
                     || t.StartsWith("min-h-", StringComparison.Ordinal))
            .ToArray();

        Assert.True(tokens.Length == 1,
            $"{component}: expected exactly one height token on its control element, found "
            + (tokens.Length == 0 ? "none" : string.Join(", ", tokens))
            + $" in \"{cssClass}\"");

        // h-8 and min-h-8 are the same box as far as a row of fields is concerned - one is a
        // fixed height, the other a floor for a control that grows (TagInput wraps its tags).
        return tokens[0].Replace("min-h-", "h-");
    }

    private string InputHeight()
    {
        var input = _ctx.Render<L.Input>().Find("input");
        return HeightToken(input.GetAttribute("class") ?? "", "Input");
    }

    [Fact]
    public void Cascaders_Trigger_Is_The_Height_Of_An_Input()
    {
        var trigger = _ctx.Render<L.Cascader>().Find("button");
        Assert.Equal(InputHeight(), HeightToken(trigger.GetAttribute("class") ?? "", "Cascader"));
    }

    [Fact]
    public void TreeSelects_Trigger_Is_The_Height_Of_An_Input()
    {
        var trigger = _ctx.Render<L.TreeSelect>().Find("button");
        Assert.Equal(InputHeight(), HeightToken(trigger.GetAttribute("class") ?? "", "TreeSelect"));
    }

    [Fact]
    public void ColorPickers_Trigger_Is_The_Height_Of_An_Input()
    {
        var trigger = _ctx.Render<L.ColorPicker>().Find("button");
        Assert.Equal(InputHeight(), HeightToken(trigger.GetAttribute("class") ?? "", "ColorPicker"));
    }

    [Fact]
    public void TagInputs_Floor_Is_The_Height_Of_An_Input()
    {
        // The container, not the inner <input> - TagInput's box is the wrapper that holds
        // the tags, and the field inside it is unstyled.
        var container = _ctx.Render<L.TagInput<string>>().Find("div.min-h-8, div[class*='min-h-']");
        Assert.Equal(InputHeight(), HeightToken(container.GetAttribute("class") ?? "", "TagInput"));
    }

    [Fact]
    public void PasswordInputs_Wrapper_Is_The_Height_Of_An_Input()
    {
        var wrapper = _ctx.Render<L.PasswordInput>().Find("div[class*='overflow-hidden']");
        Assert.Equal(InputHeight(), HeightToken(wrapper.GetAttribute("class") ?? "", "PasswordInput"));
    }

    [Fact]
    public void NumberInputs_Field_Is_The_Height_Of_An_Input()
    {
        var field = _ctx.Render<L.NumberInput>().Find("input");
        Assert.Equal(InputHeight(), HeightToken(field.GetAttribute("class") ?? "", "NumberInput"));
    }
}

/// <summary>
/// The render-level agreement test above covers the six primitives it can construct at
/// defaults. It cannot cover a control buried three levels inside a Scheduler dialog or a
/// FileViewer error state - and those were exactly where the last stragglers hid, because a
/// literal box in a rarely-rendered branch is invisible to every per-component test.
///
/// So this scans the source instead, for one specific shape: a SPACE-DELIMITED h-9 sharing a
/// class list with `rounded` and either `border` or a background. That is a control box written
/// out by hand at the pre-5.0 rung, bordered or filled. The token match is deliberate -
/// `max-h-96` contains "h-9" as a substring and is not a control box.
///
/// The `border`-only form of this check shipped one round earlier and missed FileViewer's
/// download link, which is a filled `bg-primary` button with no border at all - so the shape
/// now covers both.
/// </summary>
public class LiteralControlBoxGuardTests
{
    /// <summary>
    /// Deliberate exceptions, each one a decision rather than an oversight.
    /// </summary>
    private static readonly (string File, string Why)[] Allowed =
    {
        ("UI/Menubar/Menubar.razor",
         "Menubar and Navigation-Menu are deferred to 5.1.0 - the owner scoped 5.0 to the "
         + "B2C input and display primitives, so the bar keeps its pre-5.0 height until the "
         + "component is aligned as a whole rather than having its height moved in isolation."),
        ("UI/NavigationMenu/NavigationMenuTrigger.razor",
         "Same deferral as Menubar above."),
        ("UI/Calendar/Calendar.razor",
         "Not a rung at all in shadcn: their day cell is size-(--cell-size), driven by a CSS "
         + "variable rather than a step on the control ladder. Matching that means giving "
         + "Calendar a cell-size token, which is a restructure like Card's --card-spacing and "
         + "is deferred to 5.1.0 with it. Moving the literal h-9 to h-8 in the meantime would "
         + "look like progress while leaving the actual divergence untouched."),
    };

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Lumeo.slnx")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return dir!;
    }

    [Fact]
    public void No_Component_Spells_Out_The_Pre_5_0_Control_Box()
    {
        var root = Path.Combine(RepoRoot(), "src");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (rel.Contains("/obj/") || Allowed.Any(a => rel.EndsWith(a.File, StringComparison.Ordinal)))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!line.Contains("rounded", StringComparison.Ordinal)
                    || (!line.Contains("border", StringComparison.Ordinal)
                        && !line.Contains("bg-", StringComparison.Ordinal)))
                    continue;

                var hasFixedH9 = line
                    .Split(new[] { ' ', '"', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(t => t == "h-9" || t == "min-h-9");

                if (hasFixedH9)
                    offenders.Add($"{rel}:{i + 1}");
            }
        }

        Assert.True(offenders.Count == 0,
            "These lines spell out the pre-5.0 control box (h-9 with rounded and a border "
            + "or background) instead "
            + "of deriving it. Either move them onto the current rung or add a documented entry "
            + "to the Allowed list above:\n  " + string.Join("\n  ", offenders));
    }
}
