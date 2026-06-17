using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Combobox;

/// <summary>
/// Regression tests for issue #198:
/// 1. Data-bound option ids must be collision-proof. They were derived from
///    <c>value.GetHashCode()</c>, so two values whose hashes collided produced duplicate
///    DOM ids — aria-activedescendant / focus highlight and Enter-select landed on the
///    wrong row. Ids are now the item's position in the rendered (filtered/grouped)
///    sequence, kept in sync between ComboboxContent and Combobox.
/// 2. Data-bound disabled rows must announce aria-disabled and render disabled styling
///    (mirroring Select), and must not be selectable.
/// 3. role="listbox" must carry aria-multiselectable in Multiple mode.
/// </summary>
public class ComboboxBoundIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ComboboxBoundIdTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderDataBound(
        IEnumerable<object> items,
        Func<object, string>? itemValue = null,
        Func<object, string?>? itemGroup = null,
        Func<object, bool>? itemDisabled = null,
        bool multiple = false,
        EventCallback<string>? valueChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Items", items);
            if (itemValue != null)
                builder.AddAttribute(3, "ItemValue", itemValue);
            if (itemGroup != null)
                builder.AddAttribute(4, "ItemGroup", itemGroup);
            if (itemDisabled != null)
                builder.AddAttribute(5, "ItemDisabled", itemDisabled);
            if (multiple)
                builder.AddAttribute(6, "Multiple", true);
            if (valueChanged.HasValue)
                builder.AddAttribute(7, "ValueChanged", valueChanged.Value);
            builder.AddAttribute(8, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();

                b.OpenComponent<L.ComboboxContent>(2);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<IComponent> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    [Fact]
    public void BoundOptions_Have_Distinct_Ids()
    {
        var items = new object[] { "apple", "banana", "cherry", "date", "elderberry" };
        var cut = RenderDataBound(items);

        var ids = cut.FindAll("button[role='option']")
            .Select(o => o.GetAttribute("id"))
            .ToList();

        Assert.Equal(items.Length, ids.Count);
        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void BoundOptions_Two_Hash_Colliding_Values_Get_Different_Ids()
    {
        // Two distinct list positions sharing the SAME logical value: the OLD hash-derived
        // id scheme emitted one id for both (a real collision); the position-index scheme
        // must keep them distinct.
        var items = new object[] { "x", "x" };

        var cut = RenderDataBound(items, itemValue: o => (string)o);

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
        listbox.KeyDown("ArrowDown"); // index 0
        listbox.KeyDown("ArrowDown"); // index 1
        listbox.KeyDown("ArrowDown"); // index 2

        var options = cut.FindAll("button[role='option']");
        var highlighted = options.Where(o => o.ClassList.Contains("bg-accent")).ToList();
        Assert.Single(highlighted);
        Assert.Same(options[2], highlighted[0]);
        Assert.Contains("cherry", highlighted[0].TextContent);
    }

    // --- Data-bound disabled rows (#198 a11y a) ---

    [Fact]
    public void Disabled_Bound_Row_Has_AriaDisabled_And_Disabled_Styling()
    {
        var cut = RenderDataBound(
            new object[] { "apple", "banana" },
            itemDisabled: it => (string)it == "banana");

        var banana = FindOption(cut, "banana")!;
        Assert.Equal("true", banana.GetAttribute("aria-disabled"));
        Assert.Contains("cursor-not-allowed", banana.ClassList);
        Assert.Contains("opacity-50", banana.ClassList);

        var apple = FindOption(cut, "apple")!;
        Assert.Equal("false", apple.GetAttribute("aria-disabled"));
        Assert.DoesNotContain("cursor-not-allowed", apple.ClassList);
    }

    [Fact]
    public void Clicking_Disabled_Bound_Row_Does_Not_Select()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string>(_ctx, (string v) => selected = v);
        var cut = RenderDataBound(
            new object[] { "apple", "banana" },
            itemDisabled: it => (string)it == "banana",
            valueChanged: cb);

        var banana = FindOption(cut, "banana")!;
        // Disabled bound rows omit the onclick handler entirely (mirrors Select), so a click
        // can't dispatch a selection. bUnit surfaces the missing handler as an exception —
        // swallow it; the point is that no selection occurs.
        try { banana.Click(); }
        catch (Bunit.MissingEventHandlerException) { }
        catch (ArgumentException) { }

        Assert.Null(selected);
    }

    [Fact]
    public void Disabled_Bound_Row_Has_No_Onclick_Handler()
    {
        var cut = RenderDataBound(
            new object[] { "apple", "banana" },
            itemDisabled: it => (string)it == "banana");

        // The disabled row renders no click handler at all (the enabled one does).
        var banana = FindOption(cut, "banana")!;
        Assert.Throws<Bunit.MissingEventHandlerException>(() => banana.Click());
    }

    // --- aria-multiselectable (#198 a11y b) ---

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
