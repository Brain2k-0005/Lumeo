using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InputMask;

/// <summary>
/// Coverage for a MIXED mask — one whose slots interleave letter tokens ('A')
/// and digit tokens ('#') around literals (e.g. a license plate "AA-####").
/// The mask token vocabulary is: '#' = digit (char.IsDigit), 'A' = letter
/// (char.IsLetter), '*' = any, everything else = literal. Typed input flows
/// through ExtractRaw (which drops chars that don't match the slot under the
/// mask cursor, so letters can't fall into digit slots and vice-versa) and then
/// ApplyMask (which re-inserts the literals). ValueChanged carries the RAW value
/// (significant chars only); the input's value attribute is the masked display.
/// </summary>
public class InputMaskMixedMaskTests : IAsyncLifetime
{
    private const string PlateMask = "AA-####"; // 2 letters, literal '-', 4 digits

    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public InputMaskMixedMaskTests()
    {
        _ctx.AddLumeoServices();
        // InputMask reads the caret via interop inside HandleInput; the tracking
        // service answers it (and records SetInputCaret) so the default strict
        // JSInterop never throws on the mixed-mask edits below.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Letters_And_Digits_Land_In_Their_Own_Slots_And_Literal_Auto_Inserts()
    {
        string? raw = null;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, PlateMask)
            .Add(c => c.ValueChanged, v => raw = v));

        // User types the fully-formed plate (including the separator).
        cut.Find("input").Input("AB-1234");

        // RAW drops the literal '-'; display re-inserts it.
        Assert.Equal("AB1234", raw);
        Assert.Equal("AB-1234", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Separator_Is_Auto_Inserted_When_User_Omits_It()
    {
        string? raw = null;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, PlateMask)
            .Add(c => c.ValueChanged, v => raw = v));

        // No '-' typed: ApplyMask still places the literal between slots 2 and 3.
        cut.Find("input").Input("CD9876");

        Assert.Equal("CD9876", raw);
        Assert.Equal("CD-9876", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Letters_Typed_Into_Digit_Slots_Are_Rejected()
    {
        string? raw = null;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, PlateMask)
            .Add(c => c.ValueChanged, v => raw = v));

        // After the two letters, 'x' and 'y' hit the '#' digit slots and are
        // dropped; only the digits 1 and 2 survive.
        cut.Find("input").Input("ABxy12");

        Assert.Equal("AB12", raw);
        Assert.Equal("AB-12", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Digits_Typed_Into_Leading_Letter_Slots_Are_Rejected()
    {
        string? raw = null;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, PlateMask)
            .Add(c => c.ValueChanged, v => raw = v));

        // The leading "12" can't fill the 'A' letter slots and is rejected; the
        // mask cursor stays on slot 0 until the letters 'A','B' arrive, then the
        // trailing digits fill the '#' slots.
        cut.Find("input").Input("12-AB34");

        Assert.Equal("AB34", raw);
        Assert.Equal("AB-34", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Wildcard_Slot_Accepts_Both_Letters_And_Digits()
    {
        // A different mixed mask: '*' (any) bracketed by a digit and a letter
        // around a '/' literal, proving '*' is the permissive slot while '#'/'A'
        // still gate their own kinds.
        const string mask = "#*/A"; // digit, any, literal '/', letter
        string? raw = null;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, mask)
            .Add(c => c.ValueChanged, v => raw = v));

        // '9' -> '#', 'x' -> '*' (any accepts the letter), '/' literal skipped as
        // raw, 'Q' -> 'A'. A stray digit in the letter slot would be rejected, so
        // a clean trace keeps exactly these three significant chars.
        cut.Find("input").Input("9x/Q");

        Assert.Equal("9xQ", raw);
        Assert.Equal("9x/Q", cut.Find("input").GetAttribute("value"));
    }
}
