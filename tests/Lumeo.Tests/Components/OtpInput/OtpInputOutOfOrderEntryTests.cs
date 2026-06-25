using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.OtpInput;

/// <summary>
/// Battle-wave-2 triage edge-data bug for OtpInput (#44):
///
/// When the bound Value is shorter than the box being typed into, typing into a
/// NON-LEADING empty box left-collapses the char (BuildValue drops interior
/// empties), so the typed character lands in the box at Value.Length-1 — and the
/// component used to move focus to the literal typed `index + 1`, which SKIPS
/// the genuinely-next-empty box and lands focus on the wrong cell.
///
/// The fix focuses the next genuinely-empty box (Value.Length) after accepting a
/// char instead of the raw `index + 1`. bUnit cannot move real DOM focus, so the
/// test asserts the OBSERVABLE focus move the component performs through interop
/// (TrackingInteropService.FocusElementCalls) plus the rendered cell value. For
/// ordinary sequential typing the two targets coincide, so the contrast test
/// guards against a regression that would break normal entry.
/// </summary>
public class OtpInputOutOfOrderEntryTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OtpInputOutOfOrderEntryTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ──────────────────────────────────────────────────────────────────────────
    // Typing into a non-leading empty box (Value="12", type into box 3) collapses
    // the char to box 2, and focus must land on the next genuinely-empty box 3 —
    // NOT the raw index+1 (box 4), which pre-fix skipped the real next-empty box.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Typing_Into_NonLeading_Empty_Box_Focuses_Next_Genuinely_Empty_Box()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        string? changed = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 6)
            .Add(c => c.Value, "12")
            .Add(c => c.ValueChanged, v => changed = v));

        var inputs = cut.FindAll("input");
        var wrongTargetId = inputs[4].GetAttribute("id"); // raw index+1, pre-fix
        var correctTargetId = inputs[3].GetAttribute("id"); // next genuinely empty

        // Type "5" into the empty box at index 3 (Value is only "12").
        inputs[3].Input(new ChangeEventArgs { Value = "5" });

        // The value collapses gap-free to "125" (no interior whitespace).
        Assert.Equal("125", changed);

        // Focus moved to the next genuinely-empty box (index 3), and NOT to the
        // pre-fix raw index+1 box (index 4) which skipped a real empty cell.
        Assert.Contains(correctTargetId!, interop.FocusElementCalls);
        Assert.DoesNotContain(wrongTargetId!, interop.FocusElementCalls);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // The collapsed char renders in the box at Value.Length-1 (box 2), confirming
    // the left-collapse and that the rendered markup is sensible after the edit.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Typing_Into_NonLeading_Empty_Box_Renders_Char_At_Collapsed_Position()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 6)
            .Add(c => c.Value, "12"));

        cut.FindAll("input")[3].Input(new ChangeEventArgs { Value = "5" });

        var inputs = cut.FindAll("input");
        Assert.Equal("1", inputs[0].GetAttribute("value"));
        Assert.Equal("2", inputs[1].GetAttribute("value"));
        Assert.Equal("5", inputs[2].GetAttribute("value")); // collapsed to box 2
        Assert.Equal("", inputs[3].GetAttribute("value") ?? "");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Contrast / regression guard: ordinary SEQUENTIAL typing (into the first
    // empty box) still advances focus to index+1, which equals Value.Length here.
    // This proves the fix preserves the normal-path focus advance.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sequential_Typing_Still_Advances_Focus_To_Next_Box()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        string? changed = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 6)
            .Add(c => c.Value, "123")
            .Add(c => c.ValueChanged, v => changed = v));

        var inputs = cut.FindAll("input");
        var nextBoxId = inputs[4].GetAttribute("id");

        // Box 3 is the first empty box; typing there is normal sequential entry.
        inputs[3].Input(new ChangeEventArgs { Value = "4" });

        Assert.Equal("1234", changed);
        Assert.Contains(nextBoxId!, interop.FocusElementCalls);
    }
}
