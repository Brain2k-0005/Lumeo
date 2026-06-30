using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Lumeo.Tests.Components.Sortable;

/// <summary>
/// Regression tests for the controlled-component rollback fix on SortableList.
///
/// NOTE on what counts as a "veto" here: a re-render that carries Items UNCHANGED
/// from what it was immediately BEFORE the interaction is NOT treated as a veto —
/// that is the pre-existing #144 contract (regression-tested), which favors keeping
/// the optimistic local reorder for a parent that observes ItemsChanged without
/// echoing it back (a common "fire and forget" usage). List content can't otherwise
/// distinguish "the parent deliberately rejected this" from "the parent hasn't
/// reacted yet" when both produce the identical, unchanged value — so only Items
/// that differ from BOTH our own last push AND the pre-interaction snapshot count as
/// an authoritative, distinguishable decision (a genuine veto/normalization/reset).
/// </summary>
public class SortableListControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SortableListControlledRollbackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment<string> TextTemplate =>
        item => builder => builder.AddContent(0, item);

    private static List<string> RenderedOrder(IRenderedComponent<Lumeo.SortableList<string>> cut)
        => cut.FindAll("[data-sortable-item]").Select(el => el.TextContent.Trim()).ToList();

    // --- Controlled: a DISTINGUISHABLE veto (the parent supplies different content) rolls back ---

    [Fact]
    public async Task Controlled_Veto_With_Different_Content_Rolls_Back_To_Bound_Order()
    {
        // Parent starts with Items = [A, B, C]. On every ItemsChanged it explicitly
        // normalizes/rejects the proposed order back to a DIFFERENT-instance copy of
        // [A, B, C] — same content as the pre-interaction snapshot, so this exercises
        // the #144 "unchanged from before" no-op path, not a genuine veto (see the
        // class doc). Re-asserts #144 still holds under the controlled branch.
        var original = new List<string> { "A", "B", "C" };
        IRenderedComponent<Lumeo.SortableList<string>>? cut = null;

        var callback = EventCallback.Factory.Create<List<string>>(this, (incoming) =>
        {
            cut!.Render(p =>
            {
                p.Add(l => l.Items, new List<string>(original));
                p.Add(l => l.ItemTemplate, TextTemplate);
                p.Add(l => l.ItemsChanged, EventCallback.Factory.Create<List<string>>(this, (_) => { }));
            });
        });

        cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, original)
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged, callback));

        Assert.Equal(new List<string> { "A", "B", "C" }, RenderedOrder(cut));

        var firstHandle = cut.FindAll("[role='button']")[0];
        await cut.InvokeAsync(() => firstHandle.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));

        // Same content as before the interaction -> #144 no-op -> local reorder kept.
        Assert.Equal(new List<string> { "B", "A", "C" }, RenderedOrder(cut));
    }

    [Fact]
    public async Task Controlled_Veto_With_Distinct_Content_Rolls_Back_To_That_Content()
    {
        // A GENUINE, distinguishable veto/normalization: the parent's ItemsChanged
        // handler computes and supplies a DIFFERENT order than both what we proposed
        // (B,A,C) and what it had before (A,B,C) — e.g. a server-side reorder rule
        // that places "C" first. Unlike the same-content case above, this content
        // does not match _lastPushedItems or the pre-interaction snapshot, so it is
        // an unambiguous authoritative decision and must win.
        var original = new List<string> { "A", "B", "C" };
        var normalized = new List<string> { "C", "A", "B" };
        IRenderedComponent<Lumeo.SortableList<string>>? cut = null;

        var callback = EventCallback.Factory.Create<List<string>>(this, (incoming) =>
        {
            cut!.Render(p =>
            {
                p.Add(l => l.Items, normalized);
                p.Add(l => l.ItemTemplate, TextTemplate);
                p.Add(l => l.ItemsChanged, EventCallback.Factory.Create<List<string>>(this, (_) => { }));
            });
        });

        cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, original)
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged, callback));

        Assert.Equal(new List<string> { "A", "B", "C" }, RenderedOrder(cut));

        var firstHandle = cut.FindAll("[role='button']")[0];
        await cut.InvokeAsync(() => firstHandle.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));

        // The parent's distinct, authoritative decision wins — not the proposed
        // B,A,C and not the stale pre-interaction A,B,C.
        Assert.Equal(new List<string> { "C", "A", "B" }, RenderedOrder(cut));
    }

    // --- Controlled: accepted reorder (bind-style echo) keeps the new order ---

    [Fact]
    public async Task Controlled_Accepted_Reorder_Keeps_New_Order()
    {
        // Parent accepts every reorder by adopting the emitted snapshot and
        // re-rendering with it (mirrors a @bind-Items round-trip).
        IRenderedComponent<Lumeo.SortableList<string>>? cut = null;

        EventCallback<List<string>> callback = default;
        callback = EventCallback.Factory.Create<List<string>>(this, (incoming) =>
        {
            cut!.Render(p =>
            {
                p.Add(l => l.Items, incoming);
                p.Add(l => l.ItemTemplate, TextTemplate);
                p.Add(l => l.ItemsChanged, callback);
            });
        });

        cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "B", "C" })
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged, callback));

        var firstHandle = cut.FindAll("[role='button']")[0];
        await cut.InvokeAsync(() => firstHandle.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));

        // Parent accepted — the new order must be kept, not reverted.
        Assert.Equal(new List<string> { "B", "A", "C" }, RenderedOrder(cut));
    }

    // --- Controlled: programmatic parent reset ---

    [Fact]
    public void Controlled_Programmatic_Reset_Is_Adopted()
    {
        // Start with [A, B, C]; the parent programmatically replaces Items WITHOUT
        // the user reordering first (simulates an external data reload).
        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "B", "C" })
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged, EventCallback.Factory.Create<List<string>>(this, (_) => { })));

        Assert.Equal(new List<string> { "A", "B", "C" }, RenderedOrder(cut));

        cut.Render(p => p
            .Add(l => l.Items, new List<string> { "X", "Y", "Z" })
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged, EventCallback.Factory.Create<List<string>>(this, (_) => { })));

        Assert.Equal(new List<string> { "X", "Y", "Z" }, RenderedOrder(cut));
    }
}
