using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InputMask;

/// <summary>
/// InputMask has no @onkeydown (the historical Backspace handler was removed — see the
/// source comment — because it double-fired ValueChanged and always end-truncated
/// regardless of the caret). All typing/deletion now flows through the single native
/// @oninput handler, so the Lumeo-owned keyboard surface is that mechanism's correctness,
/// not a custom key handler. InputMaskCaretTests.cs already pins caret-aware deletion in
/// the MIDDLE and at the END of the raw value; this file adds the START-of-string case
/// (the specific scenario the old bug's blanket <c>_rawValue[..^1]</c> truncation would
/// have gotten wrong) and confirms a non-matching keystroke (a letter typed into a
/// digit-only "#" slot) never reaches the raw value.
/// </summary>
public class InputMaskKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public InputMaskKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Backspace_At_The_Start_Removes_The_First_Character_Not_The_Last()
    {
        string? raw = null;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###")
            .Add(c => c.Value, "123456")
            .Add(c => c.ValueChanged, v => raw = v));

        // Caret was at the very start; deleting the first digit yields the browser
        // string "23-456". The old handler always dropped the LAST raw char
        // ("12345") regardless of where the caret actually was — the fix must
        // instead produce "23456".
        cut.Find("input").Input("23-456");

        Assert.Equal("23456", raw);
    }

    [Fact]
    public void Typing_A_NonMatching_Character_Never_Reaches_The_Raw_Value()
    {
        string? raw = null;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "#####")
            .Add(c => c.ValueChanged, v => raw = v));

        // A letter typed into an all-digit "#####" mask: ExtractRaw must skip it
        // (not insert it, and not let it block subsequent digits from matching).
        cut.Find("input").Input("12a3");

        Assert.Equal("123", raw);
    }
}
