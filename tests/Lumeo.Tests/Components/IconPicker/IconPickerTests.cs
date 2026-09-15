using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.IconPicker;

public class IconPickerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public IconPickerTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Minimal, pack-agnostic test fixture: real IconSource values (no icon pack
    // dependency needed — IconSource.Stroke is a plain factory), so the picker
    // exercises exactly what a real Lucide/Tabler/… list would give it.
    private static readonly IReadOnlyList<L.IconPickerItem> TestIcons = new List<L.IconPickerItem>
    {
        new("Home", L.IconSource.Stroke("<path d=\"M3 3\" />"), new[] { "house" }),
        new("Star", L.IconSource.Stroke("<path d=\"M3 3\" />")),
        new("Heart", L.IconSource.Stroke("<path d=\"M3 3\" />")),
        new("Settings", L.IconSource.Stroke("<path d=\"M3 3\" />")),
    };

    // --- Rendering ---

    [Fact]
    public void Renders_Trigger_Button()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p.Add(c => c.Icons, TestIcons));
        Assert.NotNull(cut.Find("button[type='button']"));
    }

    [Fact]
    public void Shows_Default_Placeholder_When_No_Value()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p.Add(c => c.Icons, TestIcons));
        Assert.Contains("Pick an icon", cut.Markup);
    }

    [Fact]
    public void Shows_Custom_Placeholder()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Placeholder, "Choose a category icon"));
        Assert.Contains("Choose a category icon", cut.Markup);
    }

    [Fact]
    public void Grid_Not_Rendered_When_Closed()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p.Add(c => c.Icons, TestIcons));
        Assert.Empty(cut.FindAll("[role='option']"));
    }

    // --- Opening ---

    [Fact]
    public void Opening_Shows_All_Icons()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true));

        var options = cut.FindAll("[role='option']");
        Assert.Equal(TestIcons.Count, options.Count);
        foreach (var icon in TestIcons)
        {
            Assert.Contains(icon.Name, cut.Markup);
        }
    }

    [Fact]
    public void Clicking_Trigger_Opens_Grid()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p.Add(c => c.Icons, TestIcons));
        Assert.Empty(cut.FindAll("[role='option']"));

        cut.Find("button[type='button']").Click();

        Assert.Equal(TestIcons.Count, cut.FindAll("[role='option']").Count);
    }

    // --- Search ---

    [Fact]
    public void Search_Narrows_Results_By_Name()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true));

        cut.Find("input[type='text']").Input("star");

        var options = cut.FindAll("[role='option']");
        Assert.Single(options);
        Assert.Contains("Star", cut.Markup);
        Assert.DoesNotContain("Heart", cut.Markup);
    }

    [Fact]
    public void Search_Matches_Keywords_Not_Just_Name()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true));

        // "house" is a Keyword on the "Home" item, not part of its Name.
        cut.Find("input[type='text']").Input("house");

        var options = cut.FindAll("[role='option']");
        Assert.Single(options);
        Assert.Contains("Home", cut.Markup);
    }

    [Fact]
    public void Search_With_No_Matches_Shows_Empty_State()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true));

        cut.Find("input[type='text']").Input("zzz-no-match");

        Assert.Empty(cut.FindAll("[role='option']"));
        Assert.Contains("No icons found", cut.Markup);
    }

    [Fact]
    public void Searchable_False_Renders_No_Search_Box()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true)
            .Add(c => c.Searchable, false));

        Assert.Empty(cut.FindAll("input[type='text']"));
        Assert.Equal(TestIcons.Count, cut.FindAll("[role='option']").Count);
    }

    // --- Selection ---

    [Fact]
    public void Clicking_An_Icon_Raises_ValueChanged_And_Closes()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true)
            .Add(c => c.ValueChanged, cb));

        var starOption = cut.FindAll("[role='option']").Single(o => o.GetAttribute("aria-label") == "Star");
        starOption.Click();

        Assert.Equal("Star", selected);
        Assert.Empty(cut.FindAll("[role='option']"));
    }

    [Fact]
    public void Selected_Icon_Renders_On_Trigger()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Value, "Heart")
            .Add(c => c.ShowLabel, true));

        Assert.Contains("Heart", cut.Markup);
    }

    // --- Clearable ---

    [Fact]
    public void Clearable_Clear_Button_Resets_Value_To_Null()
    {
        string? lastValue = "unset";
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => lastValue = v);
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Value, "Heart")
            .Add(c => c.Clearable, true)
            .Add(c => c.ValueChanged, cb));

        cut.Find("button[aria-label='Clear']").Click();

        Assert.Null(lastValue);
    }

    [Fact]
    public void Clear_Button_Not_Rendered_Without_Clearable()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Value, "Heart"));

        Assert.Empty(cut.FindAll("button[aria-label='Clear']"));
    }

    // --- Keyboard ---

    [Fact]
    public void ArrowRight_Then_Enter_Selects_First_Icon()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true)
            .Add(c => c.ValueChanged, cb));

        var search = cut.Find("input[type='text']");
        search.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        search.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(TestIcons[0].Name, selected);
    }

    [Fact]
    public void Escape_Closes_The_Popover()
    {
        bool? openValue = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openValue = v);
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true)
            .Add(c => c.OpenChanged, cb));

        var dialog = cut.Find("[role='dialog']");
        dialog.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(openValue);
    }

    // --- Disabled ---

    [Fact]
    public void Disabled_Trigger_Blocks_Open()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Disabled, true));

        var button = cut.Find("button[type='button']");
        Assert.True(button.HasAttribute("disabled"));

        var ex = Record.Exception(() => button.Click());
        // A disabled native button either no-ops the click or bUnit throws on the
        // attempt — either way, the grid must never appear.
        Assert.Empty(cut.FindAll("[role='option']"));
    }
}
