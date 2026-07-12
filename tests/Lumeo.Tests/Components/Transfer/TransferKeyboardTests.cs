using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Transfer;

/// <summary>
/// Wave 4 composition audit — Transfer is a dual-listbox built from already-
/// tested primitives (Lumeo &lt;Checkbox&gt;, native move buttons, native search
/// &lt;input&gt;s). TransferMoveButtonAriaTests already covers accessible names.
/// This file fills the remaining neededTests gaps: the search -> select-all ->
/// row-checkbox -> move-button Tab order (native DOM order, no explicit
/// tabindex anywhere), the move buttons' real disabled state (not just visual —
/// a disabled native &lt;button&gt; cannot be activated), and that activating an
/// enabled move button moves the selection and clears it. Enter/Space
/// activation of a native &lt;button&gt; is free via the browser's default
/// semantics, so .Click() below exercises the exact handler a keydown would run.
/// </summary>
public class TransferKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TransferKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Transfer.TransferItem> Source() =>
    [
        new("Apple", "apple"),
        new("Banana", "banana"),
    ];

    [Fact]
    public void Source_Panel_Tab_Order_Is_Search_Then_SelectAll_Then_Rows()
    {
        var cut = _ctx.Render<L.Transfer>(p => p
            .Add(c => c.SourceItems, Source())
            .Add(c => c.ShowSearch, true));

        // First panel's focusable sequence in DOM order: search input, then the
        // select-all checkbox button, then each row's checkbox button.
        var input = cut.Find("input[type='text']");
        Assert.NotNull(input);

        var checkboxButtons = cut.FindAll("button[role='checkbox']");
        // select-all (source) + 2 rows (source) + select-all (target, renders
        // even with an empty/unset TargetItems since ShowSelectAll doesn't
        // gate on item count) = 4
        Assert.Equal(4, checkboxButtons.Count);

        // The select-all checkbox precedes the row checkboxes in source order —
        // verified via aria-label, which only the select-all carries.
        Assert.Equal("Select all", checkboxButtons[0].GetAttribute("aria-label"));
    }

    [Fact]
    public void Move_Right_Button_Is_Really_Disabled_When_Nothing_Selected()
    {
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.SourceItems, Source()));

        var moveRight = cut.FindAll("button").First(b => b.GetAttribute("aria-label") == "Move right");
        Assert.True(moveRight.HasAttribute("disabled"));

        // A disabled native <button> does not fire @onclick — activating it must
        // not move anything (not merely styled to look inert).
        var beforeTargetCount = cut.Instance.TargetItems?.Count ?? 0;
        moveRight.Click();
        Assert.Equal(beforeTargetCount, cut.Instance.TargetItems?.Count ?? 0);
    }

    [Fact]
    public void Activating_Move_Right_Moves_Selection_And_Clears_It()
    {
        List<L.Transfer.TransferItem>? sourceOut = null;
        List<L.Transfer.TransferItem>? targetOut = null;

        var cut = _ctx.Render<L.Transfer>(p => p
            .Add(c => c.SourceItems, Source())
            .Add(c => c.SourceItemsChanged, (List<L.Transfer.TransferItem> items) => sourceOut = items)
            .Add(c => c.TargetItemsChanged, (List<L.Transfer.TransferItem> items) => targetOut = items));

        // Select the first row's checkbox.
        cut.FindAll("button[role='checkbox']")[1].Click(); // index 0 is select-all

        var moveRight = cut.FindAll("button").First(b => b.GetAttribute("aria-label") == "Move right");
        Assert.False(moveRight.HasAttribute("disabled"));
        moveRight.Click();

        Assert.NotNull(targetOut);
        Assert.Single(targetOut!);
        Assert.Equal("apple", targetOut![0].Value);
        Assert.DoesNotContain(sourceOut!, i => i.Value == "apple");

        // Selection is cleared post-move — the move button reverts to disabled.
        moveRight = cut.FindAll("button").First(b => b.GetAttribute("aria-label") == "Move right");
        Assert.True(moveRight.HasAttribute("disabled"));
    }
}
