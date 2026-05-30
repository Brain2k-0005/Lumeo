using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// Tests for composition-mode grouping (<see cref="L.SelectGroup"/>): labelled headers,
/// nested (multi-level) groups, collapsible behaviour, group-level selection in multi-select,
/// and filtering that hides groups with no matching descendants. Mirrors ComboboxGroupTests
/// so both pickers stay behaviour-compatible.
/// </summary>
public class SelectGroupTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectGroupTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Renders an open Select with two groups (Fruits: apple/banana, Vegetables: carrot)
    // and the toggles the caller needs.
    private IRenderedComponent<IComponent> RenderGrouped(
        bool searchable = false,
        bool collapsible = false,
        bool defaultExpanded = true,
        bool multiple = false,
        bool groupSelect = false,
        List<string>? values = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Searchable", searchable);
            builder.AddAttribute(3, "Multiple", multiple);
            if (values is not null) builder.AddAttribute(4, "Values", values);
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    Group(c, 0, "Fruits", grp =>
                    {
                        Item(grp, 0, "apple", "Apple");
                        Item(grp, 1, "banana", "Banana");
                    }, collapsible, defaultExpanded, groupSelect);

                    Group(c, 10, "Vegetables", grp =>
                    {
                        Item(grp, 0, "carrot", "Carrot");
                    }, collapsible, defaultExpanded, groupSelect);
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        void Group(RenderTreeBuilder rb, int seq, string label, RenderFragment children,
                   bool col, bool def, bool gs)
        {
            rb.OpenComponent<L.SelectGroup>(seq);
            rb.AddAttribute(seq + 1, "Label", label);
            rb.AddAttribute(seq + 2, "Collapsible", col);
            rb.AddAttribute(seq + 3, "DefaultExpanded", def);
            rb.AddAttribute(seq + 4, "GroupSelect", gs);
            rb.AddAttribute(seq + 5, "ChildContent", children);
            rb.CloseComponent();
        }

        void Item(RenderTreeBuilder rb, int seq, string value, string label)
        {
            rb.OpenComponent<L.SelectItem>(seq);
            rb.AddAttribute(seq + 1, "Value", value);
            rb.AddAttribute(seq + 2, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
            rb.CloseComponent();
        }
    }

    [Fact]
    public void Renders_Group_Headers_With_Labels()
    {
        var cut = RenderGrouped();
        Assert.Equal(2, cut.FindAll("[role='group']").Count);
        Assert.Contains("Fruits", cut.Markup);
        Assert.Contains("Vegetables", cut.Markup);
    }

    [Fact]
    public void Group_Header_Is_Not_An_Option()
    {
        var cut = RenderGrouped();
        // Only the 3 items are options; headers are not role=option.
        Assert.Equal(3, cut.FindAll("[role='option']").Count);
    }

    [Fact]
    public void Filter_Hides_Group_With_No_Matching_Items()
    {
        var cut = RenderGrouped(searchable: true);

        // Drive the search via the public OnSearch callback on the context — Searchable
        // tests in this project don't always have a textbox in scope.
        // Instead, render again with a Select that has a search input. Reuse SelectTrigger
        // typing path by setting Searchable=true and using the Select's input.
        // Simpler: use the public component param via a re-render with a search value.
        var input = cut.Find("input"); // SearchableSelect renders a search box
        input.Input("carrot");

        var groups = cut.FindAll("[role='group']");
        var fruits = groups.First(g => g.TextContent.Contains("Fruits"));
        var veg = groups.First(g => g.TextContent.Contains("Vegetables"));
        Assert.Contains("hidden", fruits.GetAttribute("class") ?? "");
        Assert.DoesNotContain("hidden", veg.GetAttribute("class") ?? "");
        Assert.Single(cut.FindAll("[role='option']"));
    }

    [Fact]
    public void Collapsible_Group_Hides_Items_When_Collapsed()
    {
        var cut = RenderGrouped(collapsible: true, defaultExpanded: false);
        Assert.Empty(cut.FindAll("[role='option']"));

        var fruitsHeader = cut.FindAll("[role='button']").First(h => h.TextContent.Contains("Fruits"));
        fruitsHeader.Click();

        Assert.Equal(2, cut.FindAll("[role='option']").Count);
    }

    [Fact]
    public void Active_Search_Force_Expands_Collapsed_Group()
    {
        var cut = RenderGrouped(searchable: true, collapsible: true, defaultExpanded: false);
        Assert.Empty(cut.FindAll("[role='option']"));

        cut.Find("input").Input("apple");

        var options = cut.FindAll("[role='option']");
        Assert.Single(options);
        Assert.Contains("Apple", options[0].TextContent);
    }

    [Fact]
    public void GroupSelect_Checkbox_Selects_All_Descendants()
    {
        var selected = new List<string>();
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Multiple", true);
            builder.AddAttribute(3, "Values", selected);
            builder.AddAttribute(4, "ValuesChanged",
                EventCallback.Factory.Create<List<string>?>(this, v => selected = v ?? new List<string>()));
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectGroup>(0);
                    c.AddAttribute(1, "Label", "Fruits");
                    c.AddAttribute(2, "GroupSelect", true);
                    c.AddAttribute(3, "ChildContent", (RenderFragment)(g =>
                    {
                        g.OpenComponent<L.SelectItem>(0);
                        g.AddAttribute(1, "Value", "apple");
                        g.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                        g.CloseComponent();
                        g.OpenComponent<L.SelectItem>(3);
                        g.AddAttribute(1, "Value", "banana");
                        g.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
                        g.CloseComponent();
                    }));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var checkbox = cut.Find("[role='checkbox']");
        checkbox.Click();

        Assert.Contains("apple", selected);
        Assert.Contains("banana", selected);
    }

    [Fact]
    public void Collapsible_Header_Toggles_With_Enter_Key()
    {
        var cut = RenderGrouped(collapsible: true, defaultExpanded: false);
        Assert.Empty(cut.FindAll("[role='option']"));

        var fruitsHeader = cut.FindAll("[role='button']").First(h => h.TextContent.Contains("Fruits"));
        Assert.Equal("0", fruitsHeader.GetAttribute("tabindex"));
        fruitsHeader.KeyDown("Enter");

        Assert.Equal(2, cut.FindAll("[role='option']").Count);
    }

    [Fact]
    public void GroupSelect_Checkbox_Is_Keyboard_Focusable_And_Toggles_With_Space()
    {
        var selected = new List<string>();
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Multiple", true);
            builder.AddAttribute(3, "Values", selected);
            builder.AddAttribute(4, "ValuesChanged",
                EventCallback.Factory.Create<List<string>?>(this, v => selected = v ?? new List<string>()));
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectGroup>(0);
                    c.AddAttribute(1, "Label", "Fruits");
                    c.AddAttribute(2, "GroupSelect", true);
                    c.AddAttribute(3, "ChildContent", (RenderFragment)(g =>
                    {
                        g.OpenComponent<L.SelectItem>(0);
                        g.AddAttribute(1, "Value", "apple");
                        g.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                        g.CloseComponent();
                    }));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var checkbox = cut.Find("[role='checkbox']");
        Assert.Equal("0", checkbox.GetAttribute("tabindex"));
        checkbox.KeyDown(" ");

        Assert.Contains("apple", selected);
    }

    [Fact]
    public void GroupSelect_Picks_Up_Descendants_Of_Collapsed_Group()
    {
        // Codex finding: when Collapsible+GroupSelect+DefaultExpanded=false, descendants
        // must still register so the group-select checkbox can toggle them while collapsed.
        var selected = new List<string>();
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Multiple", true);
            builder.AddAttribute(3, "Values", selected);
            builder.AddAttribute(4, "ValuesChanged",
                EventCallback.Factory.Create<List<string>?>(this, v => selected = v ?? new List<string>()));
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectGroup>(0);
                    c.AddAttribute(1, "Label", "Editors");
                    c.AddAttribute(2, "Collapsible", true);
                    c.AddAttribute(3, "DefaultExpanded", false);
                    c.AddAttribute(4, "GroupSelect", true);
                    c.AddAttribute(5, "ChildContent", (RenderFragment)(g =>
                    {
                        g.OpenComponent<L.SelectItem>(0);
                        g.AddAttribute(1, "Value", "vscode");
                        g.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "VS Code")));
                        g.CloseComponent();
                        g.OpenComponent<L.SelectItem>(3);
                        g.AddAttribute(1, "Value", "rider");
                        g.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Rider")));
                        g.CloseComponent();
                    }));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Items don't appear as navigable options while collapsed...
        Assert.Empty(cut.FindAll("[role='option']"));

        // ...but the group-select checkbox still selects them.
        cut.Find("[role='checkbox']").Click();
        Assert.Contains("vscode", selected);
        Assert.Contains("rider", selected);
    }

    [Fact]
    public void GroupSelect_Skips_Disabled_Items()
    {
        // Codex finding: a disabled SelectItem inside a GroupSelect-enabled group must
        // not be auto-selected by the header checkbox.
        var selected = new List<string>();
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Multiple", true);
            builder.AddAttribute(3, "Values", selected);
            builder.AddAttribute(4, "ValuesChanged",
                EventCallback.Factory.Create<List<string>?>(this, v => selected = v ?? new List<string>()));
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectGroup>(0);
                    c.AddAttribute(1, "Label", "Fruits");
                    c.AddAttribute(2, "GroupSelect", true);
                    c.AddAttribute(3, "ChildContent", (RenderFragment)(g =>
                    {
                        g.OpenComponent<L.SelectItem>(0);
                        g.AddAttribute(1, "Value", "apple");
                        g.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                        g.CloseComponent();
                        g.OpenComponent<L.SelectItem>(3);
                        g.AddAttribute(1, "Value", "banana");
                        g.AddAttribute(2, "Disabled", true);
                        g.AddAttribute(3, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
                        g.CloseComponent();
                    }));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("[role='checkbox']").Click();

        Assert.Contains("apple", selected);
        Assert.DoesNotContain("banana", selected); // disabled — skipped
    }

    [Fact]
    public void Collapsed_Outer_Group_Hides_Inner_Items_Even_When_Inner_Is_Expanded()
    {
        // Codex finding: SelectItem must consult EffectiveIsExpanded (own + ancestors),
        // not just the nearest group's IsExpanded — otherwise an inner expanded group
        // inside a collapsed outer leaks navigable items the user can't see.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectGroup>(0);
                    c.AddAttribute(1, "Label", "Outer");
                    c.AddAttribute(2, "Collapsible", true);
                    c.AddAttribute(3, "DefaultExpanded", false);
                    c.AddAttribute(4, "ChildContent", (RenderFragment)(outer =>
                    {
                        outer.OpenComponent<L.SelectGroup>(0);
                        outer.AddAttribute(1, "Label", "Inner");
                        outer.AddAttribute(2, "Collapsible", true);
                        outer.AddAttribute(3, "DefaultExpanded", true); // locally expanded
                        outer.AddAttribute(4, "ChildContent", (RenderFragment)(inner =>
                        {
                            inner.OpenComponent<L.SelectItem>(0);
                            inner.AddAttribute(1, "Value", "apple");
                            inner.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                            inner.CloseComponent();
                        }));
                        outer.CloseComponent();
                    }));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Outer is collapsed → no item is navigable even though Inner is locally expanded.
        Assert.Empty(cut.FindAll("[role='option']"));
    }

    [Fact]
    public void GroupSelect_Toggle_Scopes_To_Search_Visible_Matches()
    {
        // Codex finding: when a search filter is active, GroupSelect should toggle only
        // the visible matches — not silently select hidden items.
        var selected = new List<string>();
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Multiple", true);
            builder.AddAttribute(3, "Searchable", true);
            builder.AddAttribute(4, "Values", selected);
            builder.AddAttribute(5, "ValuesChanged",
                EventCallback.Factory.Create<List<string>?>(this, v => selected = v ?? new List<string>()));
            builder.AddAttribute(6, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectGroup>(0);
                    c.AddAttribute(1, "Label", "Fruits");
                    c.AddAttribute(2, "GroupSelect", true);
                    c.AddAttribute(3, "ChildContent", (RenderFragment)(g =>
                    {
                        g.OpenComponent<L.SelectItem>(0);
                        g.AddAttribute(1, "Value", "apple");
                        g.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                        g.CloseComponent();
                        g.OpenComponent<L.SelectItem>(3);
                        g.AddAttribute(1, "Value", "banana");
                        g.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
                        g.CloseComponent();
                    }));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Type a query that only matches "apple"
        cut.Find("input").Input("app");

        // Click the group-select checkbox
        cut.Find("[role='checkbox']").Click();

        Assert.Contains("apple", selected);
        Assert.DoesNotContain("banana", selected); // filtered out, must NOT be selected
    }

    [Fact]
    public void Nested_Groups_Render_Multi_Level()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectGroup>(0);
                    c.AddAttribute(1, "Label", "Food");
                    c.AddAttribute(2, "ChildContent", (RenderFragment)(outer =>
                    {
                        outer.OpenComponent<L.SelectGroup>(0);
                        outer.AddAttribute(1, "Label", "Fruits");
                        outer.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                        {
                            inner.OpenComponent<L.SelectItem>(0);
                            inner.AddAttribute(1, "Value", "apple");
                            inner.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                            inner.CloseComponent();
                        }));
                        outer.CloseComponent();
                    }));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Equal(2, cut.FindAll("[role='group']").Count);
        Assert.Single(cut.FindAll("[role='option']"));
        Assert.Contains("Food", cut.Markup);
        Assert.Contains("Fruits", cut.Markup);
    }
}
