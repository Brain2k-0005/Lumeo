using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ToggleGroup;

/// <summary>
/// #184: ToggleGroup had no roving tabindex or arrow-key navigation. Radix model:
/// the group is one tab stop, arrows move FOCUS between items (Enter/Space, the
/// native button activation, toggles). We drive a TrackingInteropService so we
/// can assert which element id focus moved to.
/// </summary>
public class ToggleGroupKeyboardNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ToggleGroupKeyboardNavTests()
    {
        _ctx.AddLumeoServices();
        // Replace the real interop with the tracking one so FocusElement calls
        // (the arrow-key focus movement) are recorded.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.ToggleGroup> RenderGroup(
        L.ToggleGroup.ToggleGroupType type = L.ToggleGroup.ToggleGroupType.Single,
        string? value = null,
        bool disableB = false)
    {
        return _ctx.Render<L.ToggleGroup>(builder =>
        {
            builder.OpenComponent<L.ToggleGroup>(0);
            builder.AddAttribute(1, "Type", type);
            if (value is not null) builder.AddAttribute(2, "Value", value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ToggleGroupItem>(0);
                b.AddAttribute(1, "Value", "a");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                b.CloseComponent();

                b.OpenComponent<L.ToggleGroupItem>(1);
                b.AddAttribute(1, "Value", "b");
                b.AddAttribute(2, "Disabled", disableB);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "B")));
                b.CloseComponent();

                b.OpenComponent<L.ToggleGroupItem>(2);
                b.AddAttribute(1, "Value", "c");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "C")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Roving_Tabindex_First_Item_When_Nothing_Selected()
    {
        var cut = RenderGroup();
        var buttons = cut.FindAll("button");
        Assert.Equal("0", buttons[0].GetAttribute("tabindex"));
        Assert.Equal("-1", buttons[1].GetAttribute("tabindex"));
        Assert.Equal("-1", buttons[2].GetAttribute("tabindex"));
    }

    [Fact]
    public void Roving_Tabindex_Selected_Item_Is_Tab_Stop()
    {
        var cut = RenderGroup(value: "c");
        var buttons = cut.FindAll("button");
        Assert.Equal("-1", buttons[0].GetAttribute("tabindex"));
        Assert.Equal("0", buttons[2].GetAttribute("tabindex"));
    }

    [Fact]
    public void ArrowRight_Moves_Focus_To_Next_Item()
    {
        var cut = RenderGroup();
        var buttons = cut.FindAll("button");

        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // Focus should have moved to item "b" (second button).
        Assert.Contains(buttons[1].GetAttribute("id"), _interop.FocusedElementIds);
    }

    [Fact]
    public void ArrowRight_Does_Not_Toggle()
    {
        var cut = RenderGroup();

        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // Arrow only moves focus; nothing is pressed.
        Assert.All(cut.FindAll("button"), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }

    [Fact]
    public void ArrowLeft_Wraps_To_Last_Item()
    {
        var cut = RenderGroup();
        var buttons = cut.FindAll("button");

        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Contains(buttons[2].GetAttribute("id"), _interop.FocusedElementIds);
    }

    [Fact]
    public void Arrow_Skips_Disabled_Item()
    {
        var cut = RenderGroup(disableB: true);
        var buttons = cut.FindAll("button");

        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // b is disabled, so focus jumps to c.
        Assert.Contains(buttons[2].GetAttribute("id"), _interop.FocusedElementIds);
        Assert.DoesNotContain(buttons[1].GetAttribute("id"), _interop.FocusedElementIds);
    }

    [Fact]
    public void Successive_Arrows_Chain_Across_Items()
    {
        var cut = RenderGroup();
        var buttons = cut.FindAll("button");
        var group = cut.Find("[role='group']");

        group.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }); // a -> b
        group.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }); // b -> c

        Assert.Equal(buttons[2].GetAttribute("id"), _interop.FocusedElementIds[^1]);
    }

    [Fact]
    public void End_Focuses_Last_Home_Focuses_First()
    {
        var cut = RenderGroup();
        var buttons = cut.FindAll("button");
        var group = cut.Find("[role='group']");

        group.KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Equal(buttons[2].GetAttribute("id"), _interop.FocusedElementIds[^1]);

        group.KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal(buttons[0].GetAttribute("id"), _interop.FocusedElementIds[^1]);
    }

    // ----------------------------------------------------------------- #69 ----
    // The item registry must track DECLARATION order, not !Contains-append order.
    // Builds a ChildContent fragment from a value list so a re-render can insert /
    // rename items; assertions read the COMMITTED DOM (button order + tabindex),
    // which reflects the final render regardless of how Blazor reuses instances.

    private static RenderFragment Items(params string[] values) => b =>
    {
        var seq = 0;
        foreach (var v in values)
        {
            var value = v;
            b.OpenComponent<L.ToggleGroupItem>(seq++);
            b.AddAttribute(seq++, "Value", value);
            b.AddAttribute(seq++, "ChildContent",
                (RenderFragment)(c => c.AddContent(0, value.ToUpperInvariant())));
            b.CloseComponent();
        }
    };

    private IRenderedComponent<L.ToggleGroup> RenderWith(params string[] values) =>
        _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.ChildContent, Items(values)));

    [Fact]
    public void Insert_At_Front_Keeps_Registry_In_Declaration_Order()
    {
        // Start with b, c — then re-render with a inserted at the FRONT.
        var cut = RenderWith("b", "c");
        cut.Render(p => p.Add(g => g.ChildContent, Items("a", "b", "c")));

        var buttons = cut.FindAll("button");
        // The new first DOM button ("a") must be the single tab stop. With the old
        // !Contains-append registry this was [b, c, a], leaving b as the "0" stop
        // and a parked at "-1".
        Assert.Equal("a", buttons[0].TextContent.Trim().ToLowerInvariant());
        Assert.Equal("0", buttons[0].GetAttribute("tabindex"));
        Assert.Equal("-1", buttons[1].GetAttribute("tabindex"));
        Assert.Equal("-1", buttons[2].GetAttribute("tabindex"));

        // End must record the LAST DOM button's id. The old tail-append order made
        // End resolve to "a" (the appended tail), not the real last item "c".
        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Equal(buttons[2].GetAttribute("id"), _interop.FocusedElementIds.Last());
    }

    [Fact]
    public void Renaming_A_Middle_Value_Keeps_It_In_DOM_Position()
    {
        // Rename the MIDDLE item's Value (b -> bb). It must stay the middle slot,
        // so ArrowRight from the first item lands on it (not skip past it).
        var cut = RenderWith("a", "b", "c");
        cut.Render(p => p.Add(g => g.ChildContent, Items("a", "bb", "c")));

        var buttons = cut.FindAll("button");
        Assert.Equal("bb", buttons[1].TextContent.Trim().ToLowerInvariant());

        // First item ("a") is the tab stop; ArrowRight must move focus to the
        // renamed middle button in its DOM position.
        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(buttons[1].GetAttribute("id"), _interop.FocusedElementIds.Last());
    }

    // ---------------------------------------------------------------- #169 ----
    // Arrow navigation must start from the item the user is REALLY on (focused via
    // mouse/Tab), not a stale arrow-chain anchor. Focusing "c" then pressing
    // ArrowRight must move to c's neighbour, not the first-enabled anchor's.

    [Fact]
    public void Arrow_Starts_From_Mouse_Focused_Item_Not_Stale_Anchor()
    {
        var cut = RenderGroup(); // a, b, c — nothing selected
        var buttons = cut.FindAll("button");

        // User focuses the LAST item with the mouse/Tab (fires @onfocus ->
        // SetFocusValue). No arrow key has run, so without the #169 sync the group
        // would still anchor on the first-enabled item "a".
        buttons[2].Focus();

        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // From "c" (index 2) ArrowRight wraps to "a" (index 0) — c's real neighbour.
        // The stale anchor would have moved a -> b instead.
        Assert.Equal(buttons[0].GetAttribute("id"), _interop.FocusedElementIds.Last());
        Assert.NotEqual(buttons[1].GetAttribute("id"), _interop.FocusedElementIds.Last());
    }

    [Fact]
    public void Click_Syncs_Focus_Anchor_For_Subsequent_Arrow()
    {
        var cut = RenderGroup(type: L.ToggleGroup.ToggleGroupType.Multiple); // a, b, c
        var buttons = cut.FindAll("button");

        // Click "b" twice (Multiple mode): select then DESELECT, so nothing is
        // selected but the user's focus is on "b" (Toggle's SetFocusValue). This
        // separates focus (b) from the selection-anchor (none -> first-enabled a).
        buttons[1].Click();
        buttons[1].Click();

        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // With the #169 sync, ArrowRight starts from the focused "b" -> "c". Without
        // it the group would anchor on first-enabled "a" -> "b".
        Assert.Equal(buttons[2].GetAttribute("id"), _interop.FocusedElementIds.Last());
        Assert.NotEqual(buttons[1].GetAttribute("id"), _interop.FocusedElementIds.Last());
    }
}
