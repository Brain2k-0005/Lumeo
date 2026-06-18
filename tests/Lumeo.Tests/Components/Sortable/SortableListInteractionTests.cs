using Bunit;
using Lumeo.Services;
using Lumeo.Services.Localization;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sortable;

/// <summary>
/// Regression tests for the #204 SortableList fixes:
///   1. Touch registration is reconciled against the CURRENT Disabled state
///      (mounted disabled then enabled must register; re-disabling unregisters)
///      rather than being gated on the first render only.
///   2. Keyboard reordering on the drag handle (ArrowUp/Down/Home/End) mutates
///      the list and fires ItemsChanged, and is a no-op while Disabled.
///   3. The Group parameter is emitted as data-sortable-group on the container.
///   4. The supplementary OnReorder callback carries old/new index + item.
/// </summary>
public class SortableListInteractionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _tracking = new();

    public SortableListInteractionTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo/js/components.js");
        module.Mode = JSRuntimeMode.Loose;

        // Swap in TrackingInteropService so we can observe Register/Unregister
        // SortableTouch calls.
        _ctx.Services.AddSingleton<IComponentInteropService>(_tracking);
        _ctx.Services.AddScoped<ComponentInteropService>();
        _ctx.Services.AddScoped<ToastService>();
        _ctx.Services.AddScoped<IToastService>(sp => sp.GetRequiredService<ToastService>());
        _ctx.Services.AddScoped<OverlayService>();
        _ctx.Services.AddScoped<IOverlayService>(sp => sp.GetRequiredService<OverlayService>());
        _ctx.Services.AddScoped<ThemeService>();
        _ctx.Services.AddScoped<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
        _ctx.Services.AddScoped<KeyboardShortcutService>();
        _ctx.Services.AddScoped<IKeyboardShortcutService>(sp => sp.GetRequiredService<KeyboardShortcutService>());
        _ctx.Services.AddScoped<IDataGridExportService, Lumeo.Services.DataGridExportService>();
        _ctx.Services.AddScoped<HapticsService>();
        _ctx.Services.AddSingleton<IOptions<LumeoLocalizationOptions>>(_ =>
        {
            var options = new LumeoLocalizationOptions();
            LumeoDefaultStrings.ApplyDefaults(options);
            return Options.Create(options);
        });
        _ctx.Services.AddScoped<ILumeoLocalizer, LumeoLocalizer>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment<string> TextTemplate =>
        item => builder => builder.AddContent(0, item);

    // ---- Bug 1: touch (re)registration vs Disabled state ----

    [Fact]
    public void Touch_Registers_When_Enabled_After_Mounting_Disabled()
    {
        var items = new List<string> { "A", "B", "C" };
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, items)
            .Add(l => l.Disabled, true)
            .Add(l => l.ItemTemplate, TextTemplate));

        // Mounted disabled — no touch handler should have been wired up.
        Assert.Equal(0, _tracking.RegisterSortableTouchCallCount);

        // Enable it: the touch handler must now register even though the first
        // render is long past.
        cut.Render(p => p
            .Add(l => l.Items, items)
            .Add(l => l.Disabled, false)
            .Add(l => l.ItemTemplate, TextTemplate));
        Assert.Equal(1, _tracking.RegisterSortableTouchCallCount);
        Assert.Equal(0, _tracking.UnregisterSortableTouchCallCount);

        // Disable again: the live touch handler must be torn down.
        cut.Render(p => p
            .Add(l => l.Items, items)
            .Add(l => l.Disabled, true)
            .Add(l => l.ItemTemplate, TextTemplate));
        Assert.Equal(1, _tracking.UnregisterSortableTouchCallCount);
    }

    [Fact]
    public void Touch_Registers_Once_When_Enabled_From_Start()
    {
        var items = new List<string> { "A", "B" };
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, items)
            .Add(l => l.ItemTemplate, TextTemplate));

        // First render with an enabled list registers exactly once and does not
        // re-register on subsequent renders.
        Assert.Equal(1, _tracking.RegisterSortableTouchCallCount);
        cut.Render(p => p
            .Add(l => l.Items, items)
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.Class, "extra"));
        Assert.Equal(1, _tracking.RegisterSortableTouchCallCount);
    }

    // ---- a11y: keyboard reordering ----

    [Fact]
    public async Task ArrowDown_On_Handle_Moves_Item_Down_And_Fires_ItemsChanged()
    {
        List<string>? changed = null;
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "B", "C" })
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged, EventCallback.Factory.Create<List<string>>(this, v => changed = v)));

        var firstHandle = cut.FindAll("[role='button']")[0];
        await cut.InvokeAsync(() => firstHandle.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));

        Assert.NotNull(changed);
        Assert.Equal(new List<string> { "B", "A", "C" }, changed);
    }

    [Fact]
    public async Task ArrowUp_On_Handle_Moves_Item_Up_And_Fires_ItemsChanged()
    {
        List<string>? changed = null;
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "B", "C" })
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged, EventCallback.Factory.Create<List<string>>(this, v => changed = v)));

        // Handle of the second item ("B") moves it up past "A".
        var secondHandle = cut.FindAll("[role='button']")[1];
        await cut.InvokeAsync(() => secondHandle.KeyDown(new KeyboardEventArgs { Key = "ArrowUp" }));

        Assert.NotNull(changed);
        Assert.Equal(new List<string> { "B", "A", "C" }, changed);
    }

    [Fact]
    public async Task End_On_Handle_Moves_Item_To_Bottom()
    {
        List<string>? changed = null;
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "B", "C" })
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged, EventCallback.Factory.Create<List<string>>(this, v => changed = v)));

        var firstHandle = cut.FindAll("[role='button']")[0];
        await cut.InvokeAsync(() => firstHandle.KeyDown(new KeyboardEventArgs { Key = "End" }));

        Assert.Equal(new List<string> { "B", "C", "A" }, changed);
    }

    [Fact]
    public async Task ArrowUp_At_Top_Is_A_NoOp()
    {
        var fired = false;
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "B" })
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged, EventCallback.Factory.Create<List<string>>(this, _ => fired = true)));

        var firstHandle = cut.FindAll("[role='button']")[0];
        await cut.InvokeAsync(() => firstHandle.KeyDown(new KeyboardEventArgs { Key = "ArrowUp" }));

        Assert.False(fired);
    }

    [Fact]
    public async Task Keyboard_Reorder_Is_NoOp_When_Disabled()
    {
        var fired = false;
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "B", "C" })
            .Add(l => l.Disabled, true)
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged, EventCallback.Factory.Create<List<string>>(this, _ => fired = true)));

        // No handles are rendered when Disabled, so the row itself is the only
        // focusable surface — assert no handle exists and nothing fires.
        Assert.Empty(cut.FindAll("[role='button']"));
        Assert.False(fired);
    }

    // ---- Bug 2: Group attribute is wired into the DOM ----

    [Fact]
    public void Group_Is_Emitted_As_Data_Attribute()
    {
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A" })
            .Add(l => l.Group, "tasks")
            .Add(l => l.ItemTemplate, TextTemplate));

        var container = cut.Find("[data-sortable-group]");
        Assert.Equal("tasks", container.GetAttribute("data-sortable-group"));
    }

    [Fact]
    public void Group_Attribute_Absent_When_Not_Set()
    {
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A" })
            .Add(l => l.ItemTemplate, TextTemplate));

        Assert.Empty(cut.FindAll("[data-sortable-group]"));
    }

    // ---- Supplementary: OnReorder payload ----

    [Fact]
    public async Task OnReorder_Carries_Old_And_New_Index_And_Item()
    {
        L.SortableList<string>.SortableMoved? moved = null;
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "B", "C" })
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.OnReorder, EventCallback.Factory.Create<L.SortableList<string>.SortableMoved>(this, m => moved = m)));

        var firstHandle = cut.FindAll("[role='button']")[0];
        await cut.InvokeAsync(() => firstHandle.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));

        Assert.NotNull(moved);
        Assert.Equal("A", moved!.Item);
        Assert.Equal(0, moved.OldIndex);
        Assert.Equal(1, moved.NewIndex);
    }
}
