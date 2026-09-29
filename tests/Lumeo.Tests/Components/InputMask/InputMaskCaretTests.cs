using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InputMask;

/// <summary>
/// #177: Backspace truncated from the end (ignored the caret), ValueChanged fired
/// twice (keydown + input), and the caret was never restored after re-masking.
/// Deletion now flows through the single native input handler (caret-correct),
/// fires ValueChanged once, and the caret is repositioned via SetInputCaret.
///
/// The caret is read and restored only when the masked display differs from what the
/// browser produced (a rejected char, an inserted literal): only then is the field
/// rewritten, and only a rewrite moves the caret. When the display equals the typed text
/// the browser's caret is already right, and a restore computed for an older keystroke
/// would land after the user typed further under network latency, inserting the next
/// characters mid-text (InputLatencyTests, E2E).
/// </summary>
public class InputMaskCaretTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public InputMaskCaretTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Typing_Fires_ValueChanged_Once()
    {
        var count = 0;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###")
            .Add(c => c.ValueChanged, _ => count++));

        cut.Find("input").Input("123");

        Assert.Equal(1, count);
    }

    [Fact]
    public void Deleting_Fires_ValueChanged_Once()
    {
        var count = 0;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###")
            .Add(c => c.Value, "123456")
            .Add(c => c.ValueChanged, _ => count++));

        // A native input event (what Backspace produces) with one char removed.
        cut.Find("input").Input("12345");

        // Exactly one — previously keydown + input double-fired.
        Assert.Equal(1, count);
    }

    [Fact]
    public void Deleting_Middle_Char_Is_Caret_Aware_Not_End_Truncation()
    {
        string? raw = null;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###")
            .Add(c => c.Value, "123456")
            .Add(c => c.ValueChanged, v => raw = v));

        // Caret was after "2"; deleting yields the browser string "13-456".
        // The raw value must become "13456" (caret-aware), NOT "12345"
        // (the old _rawValue[..^1] end-truncation).
        cut.Find("input").Input("13-456");

        Assert.Equal("13456", raw);
    }

    [Fact]
    public void Input_Restores_The_Caret()
    {
        _interop.InputCaret = 4; // browser caret after typing "1234"
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###"));

        cut.Find("input").Input("1234");

        // The masked display is "123-4": the separator is inserted, so the field is
        // rewritten and the caret lands after the fourth digit, index 5.
        Assert.NotEmpty(_interop.SetInputCaretCalls);
        Assert.Equal(5, _interop.SetInputCaretCalls[^1].Position);
    }

    [Fact]
    public void Display_Equal_To_Typed_Text_Neither_Reads_Nor_Restores_The_Caret()
    {
        _interop.InputCaret = 1; // anywhere: it must not be used
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###"));

        // "123" masks to "123" (no dangling separator until the next slot is entered):
        // nothing is rewritten, so the browser's caret stays where the user put it.
        cut.Find("input").Input("123");

        Assert.Equal(0, _interop.GetInputCaretCallCount);
        Assert.Empty(_interop.SetInputCaretCalls);
        Assert.Equal("123", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Caret_Maps_To_End_Of_Filled_Significant_Slots()
    {
        // Two digits and a rejected letter typed; the display is "12" (the separator
        // only appears once the third slot is entered), so the caret lands at index 2.
        _interop.InputCaret = 3;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "##/##"));

        cut.Find("input").Input("12x");

        Assert.Equal(2, _interop.SetInputCaretCalls[^1].Position);
    }

    [Fact]
    public void Caret_Maps_Past_Interior_Literal_When_More_Follows()
    {
        // Caret is between the digits "12|3456" of a re-masked "12-3456"-style
        // value. Two significant chars precede it, and a literal sits right after
        // the second slot, so the caret skips past the separator to index 3.
        _interop.InputCaret = 2;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "##-###")
            .Add(c => c.Value, "12345"));

        // Browser string without the separator (e.g. pasted) and the caret after "12";
        // ApplyMask re-inserts the separator, so the field is rewritten.
        cut.Find("input").Input("12345");

        // Display "12-345": after 2 filled slots the '-' literal is skipped → 3.
        Assert.Equal(3, _interop.SetInputCaretCalls[^1].Position);
    }
}
