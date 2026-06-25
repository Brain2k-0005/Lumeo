using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.OtpInput;

/// <summary>
/// Battle-wave-2 triage keyboard-a11y bug for OtpInput (#43):
///
/// The Delete key fell through the shared `case "Backspace": case "Delete":`
/// block and behaved identically to Backspace — clearing the current cell AND
/// collapsing focus to the PREVIOUS box (and, on an empty cell, deleting the
/// previous cell). Delete is a FORWARD delete: it must clear only the current
/// cell, keep focus on that same cell, and do nothing on an already-empty cell.
///
/// bUnit cannot move real DOM focus, so these tests assert the OBSERVABLE
/// MECHANISMS: the emitted value after the keydown, the rendered cell value,
/// and the focus moves the component performs through interop
/// (TrackingInteropService.FocusElementCalls). Backspace's backward focus move
/// is the discriminator — Delete must NOT issue it.
/// </summary>
public class OtpInputDeleteKeyTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OtpInputDeleteKeyTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ──────────────────────────────────────────────────────────────────────────
    // Delete on a filled cell clears ONLY that cell and keeps focus on it —
    // it must NOT move focus to the previous box the way Backspace does.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_On_Filled_Cell_Clears_It_Without_Moving_Focus_Backward()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        string? changed = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "12")
            .Add(c => c.ValueChanged, v => changed = v));

        // The id of the previous box (index 0) — Backspace would focus this,
        // Delete must not.
        var inputs = cut.FindAll("input");
        var prevId = inputs[0].GetAttribute("id");

        // Delete on the filled cell at index 1.
        inputs[1].KeyDown(new KeyboardEventArgs { Key = "Delete" });

        // The cell was cleared (only index 1 removed → "1").
        Assert.Equal("1", changed);

        // And focus did NOT collapse to the previous cell — pre-fix Delete fell
        // through Backspace's block and focused index 0.
        Assert.DoesNotContain(prevId!, interop.FocusElementCalls);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Contrast: Backspace on the SAME filled cell DOES move focus backward, so
    // the previous assertion is genuinely testing Delete-specific behaviour and
    // not a no-op interop.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Backspace_On_Filled_Cell_Moves_Focus_Backward()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "12"));

        var inputs = cut.FindAll("input");
        var prevId = inputs[0].GetAttribute("id");

        inputs[1].KeyDown(new KeyboardEventArgs { Key = "Backspace" });

        Assert.Contains(prevId!, interop.FocusElementCalls);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Delete on an EMPTY cell does nothing: no value change and no backward
    // delete of the previous cell (pre-fix it deleted the previous cell).
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_On_Empty_Cell_Does_Nothing()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        string? changed = "sentinel";
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "12")
            .Add(c => c.ValueChanged, v => changed = v));

        // Index 2 is the first empty cell. Pre-fix Delete here fell through to
        // Backspace's empty-cell branch and deleted the previous (index 1) cell,
        // firing ValueChanged. Delete must instead be a no-op.
        cut.FindAll("input")[2].KeyDown(new KeyboardEventArgs { Key = "Delete" });

        Assert.Equal("sentinel", changed);
    }
}
