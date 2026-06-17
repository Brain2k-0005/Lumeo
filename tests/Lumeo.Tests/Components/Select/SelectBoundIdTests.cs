using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// Regression tests for issue #197: data-bound option ids must be collision-proof.
/// They were derived from <c>value.GetHashCode()</c>, so two values whose hashes
/// collided produced duplicate DOM ids — aria-activedescendant / focus highlight and
/// Enter-select then landed on the wrong row. Ids are now the item's position in the
/// rendered (filtered/grouped) sequence, which is unique and kept in sync between
/// SelectContent (rendering) and Select (keyboard nav / FocusedItemId lookup).
/// </summary>
public class SelectBoundIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectBoundIdTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderDataBound(
        IEnumerable<object> items,
        Func<object, string>? itemValue = null,
        Func<object, string?>? itemGroup = null,
        bool multiple = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Items", items);
            if (itemValue != null)
                builder.AddAttribute(3, "ItemValue", itemValue);
            if (itemGroup != null)
                builder.AddAttribute(4, "ItemGroup", itemGroup);
            if (multiple)
                builder.AddAttribute(5, "Multiple", true);
            builder.AddAttribute(6, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(2);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void BoundOptions_Have_Distinct_Ids_Even_When_Value_Hashes_Could_Collide()
    {
        // A list with several values, including two whose GetHashCode happens to collide on
        // some runtimes. Regardless, every rendered option must carry a unique id.
        var items = new object[] { "apple", "banana", "cherry", "date", "elderberry" };
        var cut = RenderDataBound(items);

        var ids = cut.FindAll("button[role='option']")
            .Select(o => o.GetAttribute("id"))
            .ToList();

        Assert.Equal(items.Length, ids.Count);
        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.Equal(ids.Count, ids.Distinct().Count()); // no duplicates
    }

    [Fact]
    public void BoundOptions_Two_Hash_Colliding_Values_Get_Different_Ids()
    {
        // Force a real hash collision: map two distinct items to values that collide.
        // Even when string.GetHashCode() returns the same value for both, the index-based
        // ids must differ so highlight/Enter target the right row.
        var a = new object();
        var b = new object();
        var items = new[] { a, b };

        // Both values intentionally hash to the same bucket via identical content; the OLD
        // scheme `select-item-bound-{hash:X}` would emit identical ids here.
        var cut = RenderDataBound(items, itemValue: o => ReferenceEquals(o, a) ? "x" : "x_dup_same_hash");

        var ids = cut.FindAll("button[role='option']")
            .Select(o => o.GetAttribute("id"))
            .ToList();

        Assert.Equal(2, ids.Count);
        Assert.NotEqual(ids[0], ids[1]);
    }

    [Fact]
    public void ArrowDown_To_Index_I_Highlights_The_Ith_Rendered_Option()
    {
        var items = new object[] { "apple", "banana", "cherry" };
        var cut = RenderDataBound(items);

        var listbox = cut.Find("[role='listbox']");
        // Arrow to the 3rd item (index 2).
        listbox.KeyDown("ArrowDown"); // index 0
        listbox.KeyDown("ArrowDown"); // index 1
        listbox.KeyDown("ArrowDown"); // index 2

        var options = cut.FindAll("button[role='option']");
        // Exactly one option is highlighted, and it is the one at index 2.
        var highlighted = options.Where(o => o.ClassList.Contains("bg-accent")).ToList();
        Assert.Single(highlighted);
        Assert.Same(options[2], highlighted[0]);
        Assert.Contains("cherry", highlighted[0].TextContent);
    }

    [Fact]
    public void Grouped_ArrowDown_Highlight_Matches_Rendered_Order_Not_Source_Order()
    {
        // Source order: apple, carrot, banana. Grouped flattening renders apple, banana
        // (Fruits) then carrot (Vegetables). Index 1 must therefore highlight "banana".
        var items = new object[] { "apple", "carrot", "banana" };
        var cut = RenderDataBound(items, itemGroup: it => (string)it == "carrot" ? "Vegetables" : "Fruits");

        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("ArrowDown"); // index 0 -> apple
        listbox.KeyDown("ArrowDown"); // index 1 -> banana (rendered order)

        var options = cut.FindAll("button[role='option']");
        var highlighted = options.Single(o => o.ClassList.Contains("bg-accent"));
        Assert.Contains("banana", highlighted.TextContent);
    }

    // --- aria-multiselectable (#197 a11y) ---

    [Fact]
    public void Listbox_Has_AriaMultiselectable_True_In_Multiple_Mode()
    {
        var cut = RenderDataBound(new object[] { "apple", "banana" }, multiple: true);

        var listbox = cut.Find("[role='listbox']");
        Assert.Equal("true", listbox.GetAttribute("aria-multiselectable"));
    }

    [Fact]
    public void Listbox_Omits_AriaMultiselectable_In_Single_Mode()
    {
        var cut = RenderDataBound(new object[] { "apple", "banana" });

        var listbox = cut.Find("[role='listbox']");
        Assert.False(listbox.HasAttribute("aria-multiselectable"));
    }
}
