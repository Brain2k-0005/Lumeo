using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PickList;

/// <summary>
/// Regression tests for the controlled-component rollback fix on PickList's
/// SelectedItems/SelectedItemsChanged pair. When SelectedItemsChanged is bound
/// (controlled) and the parent vetoes a move or a within-target reorder by
/// re-rendering with the SelectedItems it had before the interaction, the
/// component's optimistic local _targetItems must roll back to that bound
/// value rather than keeping the user's in-flight change. This specifically
/// covers the reorder case: the pre-fix code only re-seeded _targetItems when
/// the incoming *set* differed from the current one, so a same-set reorder
/// veto was never honoured.
/// </summary>
public class PickListControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PickListControlledRollbackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // The source ("Available") listbox holds items that are NOT selected;
    // the target ("Selected") listbox holds the SelectedItems.
    private static IElement SourceListbox(IRenderedComponent<L.PickList<string>> cut)
        => cut.FindAll("[role='listbox']").First(e => e.GetAttribute("aria-label") == "Available");

    private static IElement TargetListbox(IRenderedComponent<L.PickList<string>> cut)
        => cut.FindAll("[role='listbox']").First(e => e.GetAttribute("aria-label") == "Selected");

    private static IReadOnlyList<IElement> Options(IElement listbox)
        => listbox.QuerySelectorAll("button[role='option']").ToList();

    private static IElement SourceOption(IRenderedComponent<L.PickList<string>> cut, string text)
        => Options(SourceListbox(cut)).First(b => b.TextContent.Trim() == text);

    private static IReadOnlyList<string> TargetOrder(IRenderedComponent<L.PickList<string>> cut)
        => Options(TargetListbox(cut)).Select(b => b.TextContent.Trim()).ToList();

    // --- Controlled: veto of a within-target reorder rolls back the order ---

    [Fact]
    public async Task Controlled_Veto_Reorder_Rolls_Back_To_Bound_Order()
    {
        var items = new List<string> { "Alpha", "Beta", "Gamma", "Delta" };
        // Parent always re-renders with this exact (unchanged) order, vetoing
        // every reorder the user attempts.
        var parentSelected = new List<string> { "Alpha", "Beta", "Gamma" };
        IRenderedComponent<L.PickList<string>>? cut = null;

        var callback = EventCallback.Factory.Create<IEnumerable<string>>(_ctx, (IEnumerable<string> _) =>
        {
            // Veto: do NOT adopt the emitted order; re-render with the original.
            cut!.Render(p =>
            {
                p.Add(c => c.Items, items);
                p.Add(c => c.SelectedItems, parentSelected);
                p.Add(c => c.SelectedItemsChanged, EventCallback.Factory.Create<IEnumerable<string>>(_ctx, (_2) => { }));
            });
        });

        cut = _ctx.Render<L.PickList<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.SelectedItems, parentSelected)
            .Add(c => c.SelectedItemsChanged, callback));

        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, TargetOrder(cut));

        // Select "Gamma" in the target, then Alt+ArrowUp to reorder it up one
        // slot -> Alpha, Gamma, Beta (the optimistic local mutation).
        var gamma = Options(TargetListbox(cut)).First(b => b.TextContent.Trim() == "Gamma");
        gamma.Click();
        await cut.InvokeAsync(() => Options(TargetListbox(cut))
            .First(b => b.TextContent.Trim() == "Gamma")
            .KeyDown(new KeyboardEventArgs { Key = "ArrowUp", AltKey = true }));

        // The parent vetoed the reorder (re-rendered with the original order) —
        // the UI must roll back, not keep showing the rejected reorder.
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, TargetOrder(cut));
    }

    // --- Controlled: accepted reorder keeps the new order ---

    [Fact]
    public async Task Controlled_Accepted_Reorder_Keeps_New_Order()
    {
        var items = new List<string> { "Alpha", "Beta", "Gamma", "Delta" };
        var parentSelected = new List<string> { "Alpha", "Beta", "Gamma" };
        IRenderedComponent<L.PickList<string>>? cut = null;

        EventCallback<IEnumerable<string>> callback = default;
        callback = EventCallback.Factory.Create<IEnumerable<string>>(_ctx, (IEnumerable<string> incoming) =>
        {
            parentSelected = incoming.ToList();
            cut!.Render(p =>
            {
                p.Add(c => c.Items, items);
                p.Add(c => c.SelectedItems, parentSelected);
                p.Add(c => c.SelectedItemsChanged, callback);
            });
        });

        cut = _ctx.Render<L.PickList<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.SelectedItems, parentSelected)
            .Add(c => c.SelectedItemsChanged, callback));

        var gamma = Options(TargetListbox(cut)).First(b => b.TextContent.Trim() == "Gamma");
        gamma.Click();
        await cut.InvokeAsync(() => Options(TargetListbox(cut))
            .First(b => b.TextContent.Trim() == "Gamma")
            .KeyDown(new KeyboardEventArgs { Key = "ArrowUp", AltKey = true }));

        // Parent accepted — the reordered order must stick.
        Assert.Equal(new[] { "Alpha", "Gamma", "Beta" }, TargetOrder(cut));
    }

    // --- Controlled: veto of a move rolls back the target list ---

    [Fact]
    public async Task Controlled_Veto_Move_Selected_Rolls_Back_Target()
    {
        var items = new List<string> { "Alpha", "Beta", "Gamma" };
        var parentSelected = new List<string> { "Alpha" };
        IRenderedComponent<L.PickList<string>>? cut = null;

        var callback = EventCallback.Factory.Create<IEnumerable<string>>(_ctx, (IEnumerable<string> _) =>
        {
            // Veto: do NOT adopt the move; re-render with the original SelectedItems.
            cut!.Render(p =>
            {
                p.Add(c => c.Items, items);
                p.Add(c => c.SelectedItems, parentSelected);
                p.Add(c => c.SelectedItemsChanged, EventCallback.Factory.Create<IEnumerable<string>>(_ctx, (_2) => { }));
            });
        });

        cut = _ctx.Render<L.PickList<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.SelectedItems, parentSelected)
            .Add(c => c.SelectedItemsChanged, callback));

        Assert.Equal(new[] { "Alpha" }, TargetOrder(cut));

        // Select "Beta" in the source panel, then move it to the target — the
        // optimistic local mutation the pre-fix code never rolled back unless
        // the resulting count happened to mismatch.
        SourceOption(cut, "Beta").Click();
        var moveSelected = cut.FindAll("button").First(b => b.GetAttribute("aria-label") == "Move selected");
        await moveSelected.ClickAsync(new MouseEventArgs());

        // The parent vetoed the move — target must roll back to just "Alpha".
        Assert.Equal(new[] { "Alpha" }, TargetOrder(cut));
    }

    // --- Controlled: a genuine external reorder (not an echo of our push) is adopted ---

    [Fact]
    public void Controlled_Programmatic_External_Reorder_Is_Adopted()
    {
        var items = new List<string> { "Alpha", "Beta", "Gamma" };
        var cut = _ctx.Render<L.PickList<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.SelectedItems, new List<string> { "Alpha", "Beta", "Gamma" })
            .Add(c => c.SelectedItemsChanged, EventCallback.Factory.Create<IEnumerable<string>>(_ctx, (_) => { })));

        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, TargetOrder(cut));

        // Parent reorders externally WITHOUT the user interacting first (e.g. a
        // server-driven sort). Since this isn't an echo of anything the
        // component pushed, it must be adopted even though the set is the same.
        cut.Render(p => p
            .Add(c => c.Items, items)
            .Add(c => c.SelectedItems, new List<string> { "Gamma", "Beta", "Alpha" })
            .Add(c => c.SelectedItemsChanged, EventCallback.Factory.Create<IEnumerable<string>>(_ctx, (_) => { })));

        Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, TargetOrder(cut));
    }
}
