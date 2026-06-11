using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Command;

/// <summary>
/// Regression tests for the Command palette's keyboard interaction:
/// 1. CommandItem.Disabled was completely dead — the button never rendered the
///    disabled attribute and OnSelect was not gated, so disabled items were
///    fully clickable.
/// 2. There was no keyboard navigation at all (cmdk's core feature): the search
///    input now drives a highlighted item (ArrowDown/Up with wrap, Home/End,
///    Enter selects, disabled items skipped) following the WAI-ARIA
///    combobox-listbox pattern (role=combobox + aria-activedescendant on the
///    input, role=listbox on the list, role=option on items).
/// 3. The highlight must reset to the first visible item when the filter text
///    changes, and items hidden by the filter must leave the nav list.
/// 4. A CommandGroup whose items are all filtered out hides itself (heading
///    included), like cmdk's empty groups.
/// </summary>
public class CommandKeyboardNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CommandKeyboardNavTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed record ItemSpec(string Label, string? FilterValue = null, bool Disabled = false, Action? OnSelect = null);

    private IRenderedComponent<IComponent> RenderPalette(params ItemSpec[] items)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                b.CloseComponent();

                b.OpenComponent<L.CommandList>(1);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(list =>
                {
                    var seq = 0;
                    foreach (var item in items)
                    {
                        list.OpenComponent<L.CommandItem>(seq++);
                        if (item.FilterValue is not null)
                            list.AddAttribute(seq++, "FilterValue", item.FilterValue);
                        if (item.Disabled)
                            list.AddAttribute(seq++, "Disabled", true);
                        if (item.OnSelect is not null)
                            list.AddAttribute(seq++, "OnSelect", EventCallback.Factory.Create(_ctx, item.OnSelect));
                        var label = item.Label;
                        list.AddAttribute(seq++, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
                        list.CloseComponent();
                    }
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<IComponent> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    // --- ARIA wiring (combobox-listbox pattern) ---

    [Fact]
    public void Input_Has_Combobox_Aria_Wiring_Pointing_At_The_Listbox()
    {
        var cut = RenderPalette(new ItemSpec("Save"), new ItemSpec("Open"));

        var input = cut.Find("input");
        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("true", input.GetAttribute("aria-expanded"));

        var listbox = cut.Find("[role='listbox']");
        var listId = listbox.GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(listId));
        Assert.Equal(listId, input.GetAttribute("aria-controls"));
    }

    [Fact]
    public void Items_Render_As_Options_With_Aria_Disabled()
    {
        var cut = RenderPalette(new ItemSpec("Save"), new ItemSpec("Open", Disabled: true));

        var save = FindOption(cut, "Save");
        var open = FindOption(cut, "Open");
        Assert.NotNull(save);
        Assert.NotNull(open);
        Assert.Equal("false", save!.GetAttribute("aria-disabled"));
        Assert.Equal("true", open!.GetAttribute("aria-disabled"));
    }

    // --- Disabled items (previously fully clickable) ---

    [Fact]
    public void Disabled_Item_Renders_Disabled_Attribute()
    {
        var cut = RenderPalette(new ItemSpec("Save", Disabled: true));

        var button = cut.Find("button[role='option']");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void Disabled_Item_Click_Does_Not_Fire_OnSelect()
    {
        var called = false;
        var cut = RenderPalette(new ItemSpec("Save", Disabled: true, OnSelect: () => called = true));

        cut.Find("button[role='option']").Click();

        Assert.False(called);
    }

    [Fact]
    public void Enabled_Item_Click_Still_Fires_OnSelect()
    {
        var called = false;
        var cut = RenderPalette(new ItemSpec("Save", OnSelect: () => called = true));

        cut.Find("button[role='option']").Click();

        Assert.True(called);
    }

    // --- Keyboard navigation from the search input ---

    [Fact]
    public void ArrowDown_Then_Enter_Selects_First_Item()
    {
        string? selected = null;
        var cut = RenderPalette(
            new ItemSpec("Save", OnSelect: () => selected = "Save"),
            new ItemSpec("Open", OnSelect: () => selected = "Open"));

        var input = cut.Find("input");
        input.KeyDown("ArrowDown");
        input.KeyDown("Enter");

        Assert.Equal("Save", selected);
    }

    [Fact]
    public void ArrowDown_Sets_ActiveDescendant_To_Highlighted_Item()
    {
        var cut = RenderPalette(new ItemSpec("Save"), new ItemSpec("Open"));

        var input = cut.Find("input");
        Assert.True(string.IsNullOrEmpty(input.GetAttribute("aria-activedescendant")));

        input.KeyDown("ArrowDown");
        var save = FindOption(cut, "Save");
        Assert.Equal(save!.GetAttribute("id"), cut.Find("input").GetAttribute("aria-activedescendant"));
        Assert.Equal("true", save.GetAttribute("aria-selected"));
        Assert.Contains("bg-accent", save.ClassList);

        input.KeyDown("ArrowDown");
        var open = FindOption(cut, "Open");
        Assert.Equal(open!.GetAttribute("id"), cut.Find("input").GetAttribute("aria-activedescendant"));
        Assert.Equal("false", FindOption(cut, "Save")!.GetAttribute("aria-selected"));
    }

    [Fact]
    public void Arrow_Navigation_Wraps_At_Both_Ends()
    {
        var cut = RenderPalette(new ItemSpec("Save"), new ItemSpec("Open"), new ItemSpec("Quit"));

        var input = cut.Find("input");
        // ArrowUp with no highlight wraps to the last item.
        input.KeyDown("ArrowUp");
        Assert.Equal("true", FindOption(cut, "Quit")!.GetAttribute("aria-selected"));

        // ArrowDown from the last item wraps back to the first.
        input.KeyDown("ArrowDown");
        Assert.Equal("true", FindOption(cut, "Save")!.GetAttribute("aria-selected"));
    }

    [Fact]
    public void Home_And_End_Jump_To_First_And_Last_Item()
    {
        var cut = RenderPalette(new ItemSpec("Save"), new ItemSpec("Open"), new ItemSpec("Quit"));

        var input = cut.Find("input");
        input.KeyDown("End");
        Assert.Equal("true", FindOption(cut, "Quit")!.GetAttribute("aria-selected"));

        input.KeyDown("Home");
        Assert.Equal("true", FindOption(cut, "Save")!.GetAttribute("aria-selected"));
    }

    [Fact]
    public void Arrow_Navigation_Skips_Disabled_Items()
    {
        string? selected = null;
        var cut = RenderPalette(
            new ItemSpec("Save", Disabled: true, OnSelect: () => selected = "Save"),
            new ItemSpec("Open", OnSelect: () => selected = "Open"));

        var input = cut.Find("input");
        // First item is disabled — ArrowDown must land on "Open" directly.
        input.KeyDown("ArrowDown");
        Assert.Equal("true", FindOption(cut, "Open")!.GetAttribute("aria-selected"));
        Assert.Equal("false", FindOption(cut, "Save")!.GetAttribute("aria-selected"));

        input.KeyDown("Enter");
        Assert.Equal("Open", selected);
    }

    [Fact]
    public void Enter_Without_Highlight_Is_A_Noop()
    {
        var called = false;
        var cut = RenderPalette(new ItemSpec("Save", OnSelect: () => called = true));

        cut.Find("input").KeyDown("Enter");

        Assert.False(called);
    }

    // --- Filtering interaction ---

    [Fact]
    public void Highlight_Resets_To_First_Visible_Item_When_Filter_Changes()
    {
        var cut = RenderPalette(
            new ItemSpec("Apple", FilterValue: "apple"),
            new ItemSpec("Apricot", FilterValue: "apricot"),
            new ItemSpec("Banana", FilterValue: "banana"));

        var input = cut.Find("input");
        input.KeyDown("ArrowDown");
        input.KeyDown("ArrowDown");
        input.KeyDown("ArrowDown");
        Assert.Equal("true", FindOption(cut, "Banana")!.GetAttribute("aria-selected"));

        // Typing a filter must reset the highlight to the first VISIBLE item.
        cut.Find("input").Input("ap");

        var apple = FindOption(cut, "Apple");
        Assert.NotNull(apple);
        Assert.Equal("true", apple!.GetAttribute("aria-selected"));
        Assert.Equal(apple.GetAttribute("id"), cut.Find("input").GetAttribute("aria-activedescendant"));
        Assert.Null(FindOption(cut, "Banana")); // filtered out of the DOM entirely
    }

    [Fact]
    public void Navigation_Only_Walks_Items_Visible_Under_The_Current_Filter()
    {
        string? selected = null;
        var cut = RenderPalette(
            new ItemSpec("Save", FilterValue: "save", OnSelect: () => selected = "Save"),
            new ItemSpec("Open", FilterValue: "open", OnSelect: () => selected = "Open"),
            new ItemSpec("Settings", FilterValue: "settings", OnSelect: () => selected = "Settings"));

        cut.Find("input").Input("se");
        var input = cut.Find("input");
        // Only "Settings" matches "se"; the highlight reset already points at it.
        // ArrowDown wraps within the single visible item and Enter selects it —
        // never "Save" or "Open".
        input.KeyDown("ArrowDown");
        input.KeyDown("Enter");

        Assert.Equal("Settings", selected);
    }

    [Fact]
    public void Enter_With_Everything_Filtered_Out_Is_A_Noop()
    {
        var called = false;
        var cut = RenderPalette(new ItemSpec("Save", FilterValue: "save", OnSelect: () => called = true));

        cut.Find("input").Input("zzz");
        var input = cut.Find("input");
        input.KeyDown("ArrowDown");
        input.KeyDown("Enter");

        Assert.False(called);
        Assert.True(string.IsNullOrEmpty(input.GetAttribute("aria-activedescendant")));
    }

    // --- Empty groups hide (cmdk behavior) ---

    private IRenderedComponent<IComponent> RenderGroupedPalette()
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                b.CloseComponent();

                b.OpenComponent<L.CommandList>(1);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.CommandGroup>(0);
                    list.AddAttribute(1, "Heading", "Files");
                    list.AddAttribute(2, "ChildContent", (RenderFragment)(grp =>
                    {
                        grp.OpenComponent<L.CommandItem>(0);
                        grp.AddAttribute(1, "FilterValue", "save");
                        grp.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Save")));
                        grp.CloseComponent();
                    }));
                    list.CloseComponent();

                    list.OpenComponent<L.CommandGroup>(3);
                    list.AddAttribute(4, "Heading", "System");
                    list.AddAttribute(5, "ChildContent", (RenderFragment)(grp =>
                    {
                        grp.OpenComponent<L.CommandItem>(0);
                        grp.AddAttribute(1, "FilterValue", "quit");
                        grp.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Quit")));
                        grp.CloseComponent();
                    }));
                    list.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Group_With_All_Items_Filtered_Out_Is_Hidden()
    {
        var cut = RenderGroupedPalette();

        cut.Find("input").Input("save");

        // The group root is the only div carrying the "p-1" group class.
        var filesGroup = cut.FindAll("div").First(d => d.TextContent.Contains("Files") && (d.GetAttribute("class") ?? "").Contains("p-1"));
        var systemGroup = cut.FindAll("div").First(d => d.TextContent.Contains("System") && (d.GetAttribute("class") ?? "").Contains("p-1"));

        Assert.False(filesGroup.HasAttribute("hidden"));
        Assert.True(systemGroup.HasAttribute("hidden"));
    }

    [Fact]
    public void Group_Reappears_When_Filter_Matches_Again()
    {
        var cut = RenderGroupedPalette();

        cut.Find("input").Input("save");
        cut.Find("input").Input("");

        var filesGroup = cut.FindAll("div").First(d => d.TextContent.Contains("Files") && (d.GetAttribute("class") ?? "").Contains("p-1"));
        var systemGroup = cut.FindAll("div").First(d => d.TextContent.Contains("Quit") && (d.GetAttribute("class") ?? "").Contains("p-1"));

        Assert.False(filesGroup.HasAttribute("hidden"));
        Assert.False(systemGroup.HasAttribute("hidden"));
    }
}
