using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Overlays;

/// <summary>Field report 4.14 / #444: a click on a modal backdrop used to move focus out of the
/// dialog (to body, or to an outer dialog when nested), so the next Escape hit nothing or the
/// wrong dialog. The backdrop now cancels the mousedown default, so focus stays inside.</summary>
public class BackdropKeepsFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BackdropKeepsFocusTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertBackdropCancelsMousedown<T>(IRenderedComponent<T> cut, string wrapperSelector) where T : Microsoft.AspNetCore.Components.IComponent
    {
        var backdrop = cut.Find(wrapperSelector + " > div");
        Assert.True(backdrop.Attributes.Any(a => a.Name.Contains("preventdefault", StringComparison.OrdinalIgnoreCase) && a.Name.Contains("mousedown", StringComparison.OrdinalIgnoreCase)),
            $"the backdrop under {wrapperSelector} must cancel mousedown so it never takes focus; attributes: " + string.Join(" ", backdrop.Attributes.Select(a => a.Name)));
    }

    [Fact]
    public void Dialog_Backdrop_Cancels_Mousedown()
    {
        var cut = _ctx.Render<L.Dialog>(p => p.Add(d => d.Open, true).AddChildContent<L.DialogContent>(c => c.AddChildContent("body")));
        AssertBackdropCancelsMousedown(cut, "[data-slot=dialog-content]");
    }

    [Fact]
    public void AlertDialog_Backdrop_Cancels_Mousedown()
    {
        var cut = _ctx.Render<L.AlertDialog>(p => p.Add(d => d.Open, true).AddChildContent<L.AlertDialogContent>(c => c.AddChildContent("body")));
        AssertBackdropCancelsMousedown(cut, "[data-slot=alert-dialog-content]");
    }

    [Fact]
    public void Sheet_Backdrop_Cancels_Mousedown()
    {
        var cut = _ctx.Render<L.Sheet>(p => p.Add(d => d.Open, true).AddChildContent<L.SheetContent>(c => c.AddChildContent("body")));
        AssertBackdropCancelsMousedown(cut, "[data-slot=sheet-content]");
    }
}
