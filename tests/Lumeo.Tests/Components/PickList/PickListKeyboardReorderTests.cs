using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.PickList;

/// <summary>
/// #203 — the PickList now exposes WAI-ARIA listbox semantics, supports keyboard
/// operation (arrow keys move a roving focus, Space/Enter toggle selection) and
/// within-list reorder of the target (header up/down buttons + Alt+ArrowUp/Down).
/// </summary>
public class PickListKeyboardReorderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PickListKeyboardReorderTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<Lumeo.PickList<string>> Render(
        IEnumerable<string>? items = null,
        IEnumerable<string>? selected = null,
        EventCallback<IEnumerable<string>>? onChange = null,
        Action<ComponentParameterCollectionBuilder<Lumeo.PickList<string>>>? extra = null)
        => _ctx.Render<Lumeo.PickList<string>>(p =>
        {
            p.Add(l => l.Items, items ?? new[] { "Alpha", "Bravo", "Charlie" });
            p.Add(l => l.SelectedItems, selected ?? Array.Empty<string>());
            p.Add(l => l.ShowSearch, false);
            if (onChange.HasValue) p.Add(l => l.SelectedItemsChanged, onChange.Value);
            extra?.Invoke(p);
        });

    private static AngleSharp.Dom.IElement SourceList(IRenderedComponent<Lumeo.PickList<string>> cut)
        => cut.FindAll("[role='listbox']")[0];
    private static AngleSharp.Dom.IElement TargetList(IRenderedComponent<Lumeo.PickList<string>> cut)
        => cut.FindAll("[role='listbox']")[1];

    private static AngleSharp.Dom.IElement Option(AngleSharp.Dom.IElement list, string text)
        => list.QuerySelectorAll("button[role='option']").First(b => b.TextContent.Trim() == text);

    // --- ARIA ---

    [Fact]
    public void Both_Panels_Are_Multiselect_Listboxes()
    {
        var cut = Render(selected: new[] { "Bravo" });
        var listboxes = cut.FindAll("[role='listbox']");
        Assert.Equal(2, listboxes.Count);
        Assert.All(listboxes, lb => Assert.Equal("true", lb.GetAttribute("aria-multiselectable")));
        Assert.NotEmpty(cut.FindAll("button[role='option']"));
    }

    [Fact]
    public void Selected_Source_Item_Is_Aria_Selected()
    {
        var cut = Render();
        Option(SourceList(cut), "Alpha").Click();
        Assert.Equal("true", Option(SourceList(cut), "Alpha").GetAttribute("aria-selected"));
        Assert.Equal("false", Option(SourceList(cut), "Bravo").GetAttribute("aria-selected"));
    }

    [Fact]
    public void Exactly_One_Option_Per_List_Is_Tabbable()
    {
        var cut = Render(selected: new[] { "Bravo" });
        // Source has Alpha + Charlie; target has Bravo.
        Assert.Single(SourceList(cut).QuerySelectorAll("button[role='option'][tabindex='0']"));
        Assert.Single(TargetList(cut).QuerySelectorAll("button[role='option'][tabindex='0']"));
    }

    // --- Keyboard operation ---

    [Fact]
    public void Space_Toggles_Selection_On_Focused_Source_Item()
    {
        var cut = Render();
        // The option is a native <button>; Space/Enter activate it through the
        // browser's native click (not a Blazor @onkeydown), which bUnit models
        // with Click(). Handling keydown here too would double-toggle in a real browser.
        Option(SourceList(cut), "Alpha").Click();
        Assert.Equal("true", Option(SourceList(cut), "Alpha").GetAttribute("aria-selected"));
    }

    [Fact]
    public void ArrowDown_Moves_Roving_Focus_Via_Interop()
    {
        var tracking = new TrackingInteropService();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(tracking);
        var cut = Render();

        Option(SourceList(cut), "Alpha").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        // Focus moved to the second source option (Bravo).
        var bravoId = Option(SourceList(cut), "Bravo").GetAttribute("id");
        Assert.Contains(bravoId, tracking.FocusedElementIds);
    }

    // --- Within-list reorder ---

    [Fact]
    public void Reorder_Buttons_Render_When_Reorderable()
    {
        var cut = Render(selected: new[] { "Alpha", "Bravo" });
        Assert.NotEmpty(cut.FindAll("button[title='Move up']"));
        Assert.NotEmpty(cut.FindAll("button[title='Move down']"));
    }

    [Fact]
    public void MoveDown_Reorders_Selected_Target_Item_And_Emits()
    {
        IEnumerable<string>? captured = null;
        var cb = EventCallback.Factory.Create<IEnumerable<string>>(this, v => captured = v);
        var cut = Render(
            items: new[] { "Alpha", "Bravo", "Charlie" },
            selected: new[] { "Alpha", "Bravo", "Charlie" }, // all in target, order A,B,C
            onChange: cb);

        // Select "Alpha" in the target, then move it down one slot → B,A,C.
        Option(TargetList(cut), "Alpha").Click();
        cut.Find("button[title='Move down']").Click();

        Assert.NotNull(captured);
        Assert.Equal(new[] { "Bravo", "Alpha", "Charlie" }, captured!);
    }

    [Fact]
    public void MoveUp_Reorders_Selected_Target_Item_And_Emits()
    {
        IEnumerable<string>? captured = null;
        var cb = EventCallback.Factory.Create<IEnumerable<string>>(this, v => captured = v);
        var cut = Render(
            items: new[] { "Alpha", "Bravo", "Charlie" },
            selected: new[] { "Alpha", "Bravo", "Charlie" },
            onChange: cb);

        // Select "Charlie" and move it up → A,C,B.
        Option(TargetList(cut), "Charlie").Click();
        cut.Find("button[title='Move up']").Click();

        Assert.NotNull(captured);
        Assert.Equal(new[] { "Alpha", "Charlie", "Bravo" }, captured!);
    }

    [Fact]
    public void Alt_ArrowDown_On_Target_Item_Reorders()
    {
        IEnumerable<string>? captured = null;
        var cb = EventCallback.Factory.Create<IEnumerable<string>>(this, v => captured = v);
        var cut = Render(
            items: new[] { "Alpha", "Bravo", "Charlie" },
            selected: new[] { "Alpha", "Bravo", "Charlie" },
            onChange: cb);

        Option(TargetList(cut), "Alpha").Click(); // select Alpha
        Option(TargetList(cut), "Alpha").KeyDown(new KeyboardEventArgs { Key = "ArrowDown", AltKey = true });

        Assert.NotNull(captured);
        Assert.Equal(new[] { "Bravo", "Alpha", "Charlie" }, captured!);
    }

    [Fact]
    public void Reorderable_False_Hides_The_Reorder_Buttons()
    {
        var cut = Render(selected: new[] { "Alpha" }, extra: p => p.Add(l => l.Reorderable, false));
        Assert.Empty(cut.FindAll("button[title='Move up']"));
        Assert.Empty(cut.FindAll("button[title='Move down']"));
    }
}
