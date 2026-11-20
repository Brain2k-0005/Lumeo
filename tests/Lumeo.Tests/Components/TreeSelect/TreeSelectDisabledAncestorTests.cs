using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeSelect;

/// <summary>
/// Issue #556 — a Disabled parent must disable its whole subtree. Previously only the
/// item's own Disabled flag was consulted, so the children of a disabled header stayed
/// selectable.
/// </summary>
public class TreeSelectDisabledAncestorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeSelectDisabledAncestorTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.TreeSelect.TreeSelectItem> Tree() =>
    [
        new()
        {
            Label = "Locked", Value = "locked", Disabled = true,
            Children =
            [
                new() { Label = "Child", Value = "child", Children = [ new() { Label = "Grandchild", Value = "grand" } ] }
            ]
        },
        new()
        {
            Label = "Open", Value = "open",
            Children = [ new() { Label = "Free", Value = "free" } ]
        }
    ];

    private IRenderedComponent<L.TreeSelect> Open(bool multiple, List<string>? selectedChange = null, Action<string?>? onValue = null)
    {
        var cut = _ctx.Render<L.TreeSelect>((ComponentParameterCollectionBuilder<L.TreeSelect> p) =>
        {
            p.Add(c => c.Items, Tree());
            p.Add(c => c.Multiple, multiple);
            p.Add(c => c.ExpandAll, true);
            p.Add(c => c.ValueChanged, v => onValue?.Invoke(v));
            p.Add(c => c.ValuesChanged, v => { if (v is not null) selectedChange?.AddRange(v); });
        });
        cut.Find("button").Click();
        return cut;
    }

    private static void ExpandAll(IRenderedComponent<L.TreeSelect> cut) { } // tree opens fully expanded (ExpandAll=true)

    private static Task ClickRow(IRenderedComponent<L.TreeSelect> cut, string label)
        => cut.InvokeAsync(() => Row(cut, label).FirstElementChild!.Click());

    private static AngleSharp.Dom.IElement Row(IRenderedComponent<L.TreeSelect> cut, string label)
        => cut.FindAll("[role='treeitem']").First(t => t.QuerySelector("span.truncate")!.TextContent == label);

    [Fact]
    public void Descendants_of_a_disabled_item_render_aria_disabled_and_dimmed()
    {
        var cut = Open(multiple: false);
        ExpandAll(cut);

        foreach (var label in new[] { "Locked", "Child", "Grandchild" })
        {
            var row = Row(cut, label);
            Assert.Equal("true", row.GetAttribute("aria-disabled"));
            Assert.Contains("cursor-not-allowed", row.FirstElementChild!.ClassName);
        }

        var free = Row(cut, "Free");
        Assert.Null(free.GetAttribute("aria-disabled"));
        Assert.DoesNotContain("cursor-not-allowed", free.FirstElementChild!.ClassName);
    }

    [Fact]
    public async Task Clicking_a_descendant_of_a_disabled_item_does_not_select_in_single_mode()
    {
        string? picked = null;
        var cut = Open(multiple: false, onValue: v => picked = v);
        ExpandAll(cut);

        await ClickRow(cut, "Grandchild");

        Assert.Null(picked);
        // Control: an enabled leaf still selects.
        cut = Open(multiple: false, onValue: v => picked = v);
        ExpandAll(cut);
        await ClickRow(cut, "Free");
        Assert.Equal("free", picked);
    }

    [Fact]
    public async Task Clicking_a_descendant_of_a_disabled_item_does_not_select_in_multiple_mode()
    {
        var changed = new List<string>();
        var cut = Open(multiple: true, selectedChange: changed);
        ExpandAll(cut);

        await ClickRow(cut, "Child");
        await ClickRow(cut, "Grandchild");
        await ClickRow(cut, "Locked");
        Assert.Empty(changed);

        await ClickRow(cut, "Free");
        Assert.Equal(["free"], changed);
    }

    [Fact]
    public async Task Enter_on_a_descendant_of_a_disabled_item_does_not_select()
    {
        string? picked = null;
        var cut = Open(multiple: false, onValue: v => picked = v);
        ExpandAll(cut);

        // Roving focus: Down from "Locked" reaches "Child"; Enter must not select it.
        var panel = cut.Find("[role='tree']");
        await cut.InvokeAsync(() => panel.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));
        await cut.InvokeAsync(() => panel.KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        Assert.Null(picked);
    }
}
