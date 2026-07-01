using System.Reflection;
using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.SpeedDial;

/// <summary>
/// Regression tests for triage #193 (LIFECYCLE, low) —
/// "Open() seeds _focusedIndex only after awaiting GetMenuItemCount, so a fast
/// ArrowDown/ArrowUp right after open computes from the stale -1 and skips or
/// desyncs the first item".
///
/// Before the fix, <c>Open()</c> set <c>_focusedIndex = -1</c> synchronously and
/// only promoted it to <c>0</c> AFTER awaiting RegisterClickOutside +
/// GetMenuItemCount — and only when the reported item count was &gt; 0. Any arrow
/// key arriving inside that async window moved the cursor off the about-to-be
/// focused first item, desyncing the cursor from DOM focus.
///
/// The fix (a) seeds <c>_focusedIndex = 0</c> SYNCHRONOUSLY at the top of Open()
/// so the cursor always matches the focus Open is about to apply, and (b) gates
/// MoveFocus/FocusIndex behind an <c>_opening</c> flag so an arrow key that
/// arrives before the open-focus lands is ignored rather than desyncing.
/// </summary>
public class SpeedDialOpenFocusSeedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public SpeedDialOpenFocusSeedTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.SpeedDial.SpeedDialItem> TwoItems() => new()
    {
        new() { Label = "Share" },
        new() { Label = "Print" },
    };

    // The private cursor field. With the fix it is seeded to 0 synchronously at
    // the top of Open(); without the fix it is -1 until the post-await block runs
    // (and that block only runs when GetMenuItemCount() returns > 0).
    private static int FocusedIndex(IRenderedComponent<L.SpeedDial> cut)
    {
        var field = typeof(L.SpeedDial)
            .GetField("_focusedIndex", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (int)field.GetValue(cut.Instance)!;
    }

    /// <summary>
    /// THE DISCRIMINATOR. With GetMenuItemCount() reporting 0, Open()'s
    /// post-await <c>if (count > 0)</c> seed never runs — so the ONLY thing that
    /// can leave the cursor at 0 is the synchronous seed the fix added. Under the
    /// bug the cursor is still -1 after open; with the fix it is 0.
    /// </summary>
    [Fact]
    public void Open_Seeds_FocusedIndex_Synchronously_Even_When_ItemCount_Is_Reported_Zero()
    {
        _interop.MenuItemCount = 0; // post-await seed path is skipped entirely
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, TwoItems()));

        cut.Find("button[id^='speeddial-trigger-']").Click();

        // Menu is open...
        Assert.Equal("true", cut.Find("button[id^='speeddial-trigger-']").GetAttribute("aria-expanded"));
        // ...and the cursor is on the first action, seeded BEFORE any await — not
        // left at the stale -1 the old code would still hold here.
        Assert.Equal(0, FocusedIndex(cut));
    }

    /// <summary>
    /// Normal path preserved: when items exist the first action is focused on
    /// open (unchanged behaviour) and the cursor ends at 0.
    /// </summary>
    [Fact]
    public void Open_Still_Focuses_First_Action_When_Items_Exist()
    {
        _interop.MenuItemCount = 2;
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, TwoItems()));

        cut.Find("button[id^='speeddial-trigger-']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusMenuItemCalls, c => c.Index == 0));
        Assert.Equal(0, FocusedIndex(cut));
    }

    /// <summary>
    /// The cursor is consistent with DOM focus right after open: ArrowDown moves
    /// from the seeded index 0 to index 1 (not from a stale -1 that would re-land
    /// on 0). This is the user-visible symptom the seed fixes.
    /// </summary>
    [Fact]
    public void ArrowDown_After_Open_Lands_On_Second_Action_From_Seeded_Cursor()
    {
        _interop.MenuItemCount = 2;
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, TwoItems()));
        cut.Find("button[id^='speeddial-trigger-']").Click();

        // Open completes (focus index 0 landed); the cursor is now a real 0.
        cut.WaitForAssertion(() => Assert.Contains(_interop.FocusMenuItemCalls, c => c.Index == 0));

        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusMenuItemCalls, c => c.Index == 1));
        Assert.Equal(1, FocusedIndex(cut));
    }
}
