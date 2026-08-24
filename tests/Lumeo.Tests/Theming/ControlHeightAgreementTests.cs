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
