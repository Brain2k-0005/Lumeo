using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Transfer;

/// <summary>
/// Battle-test #36 (keyboard-a11y) — the two icon-only move-direction buttons
/// (ChevronRight / ChevronLeft) carried no accessible name, so a screen reader
/// announced them as unlabeled buttons. They must expose an aria-label (reusing
/// the existing Transfer.MoveRight / Transfer.MoveLeft localization keys),
/// mirroring how the select-all checkboxes are labelled.
/// </summary>
public class TransferMoveButtonAriaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TransferMoveButtonAriaTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Transfer.TransferItem> Source() =>
    [
        new("Apple", "apple"),
        new("Banana", "banana"),
    ];

    // The two move buttons are the icon-only h-8 w-8 buttons between the panels.
    private static IReadOnlyList<AngleSharp.Dom.IElement> MoveButtons(IRenderedComponent<L.Transfer> cut)
        => cut.FindAll("button").Where(b => (b.GetAttribute("class") ?? "").Contains("h-8 w-8")).ToList();

    [Fact]
    public void Move_To_Target_Button_Has_Accessible_Name()
    {
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.SourceItems, Source()));
        // First move button = ChevronRight (move to target).
        var moveRight = MoveButtons(cut)[0];
        Assert.Equal("Move right", moveRight.GetAttribute("aria-label"));
    }

    [Fact]
    public void Move_To_Source_Button_Has_Accessible_Name()
    {
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.SourceItems, Source()));
        // Second move button = ChevronLeft (move to source).
        var moveLeft = MoveButtons(cut)[1];
        Assert.Equal("Move left", moveLeft.GetAttribute("aria-label"));
    }

    [Fact]
    public void Both_Move_Buttons_Expose_A_NonEmpty_AriaLabel()
    {
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.SourceItems, Source()));
        var buttons = MoveButtons(cut);
        Assert.Equal(2, buttons.Count);
        Assert.All(buttons, b => Assert.False(string.IsNullOrWhiteSpace(b.GetAttribute("aria-label"))));
    }
}
