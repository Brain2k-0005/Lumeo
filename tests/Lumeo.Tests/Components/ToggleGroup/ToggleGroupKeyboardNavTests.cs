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
}
