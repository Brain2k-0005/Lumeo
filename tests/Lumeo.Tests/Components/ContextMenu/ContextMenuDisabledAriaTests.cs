using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// #174 (keyboard-a11y) — disabled menu items must expose <c>aria-disabled</c>
/// (not only the native <c>disabled</c> attribute) so assistive tech announces
/// the state, mirroring DropdownMenuTrigger's idiom. ContextMenuRadioItem had no
/// <c>Disabled</c> support at all: it could neither be skipped by the roving nav
/// (the JS <c>getMenuItems</c> filters on <c>:not([disabled])</c>) nor activate-
/// guarded. The fix adds <c>aria-disabled="@(Disabled ? "true" : null)"</c> to
/// ContextMenuItem / ContextMenuCheckboxItem / ContextMenuSubTrigger and gives
/// ContextMenuRadioItem a Disabled parameter that emits both attributes and gates
/// Select().
/// </summary>
public class ContextMenuDisabledAriaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ContextMenuDisabledAriaTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- ContextMenuItem ---

    [Fact]
    public void Item_Disabled_Emits_AriaDisabled_True()
    {
        var cut = _ctx.Render<L.ContextMenuItem>(p => p
            .Add(c => c.Disabled, true)
            .Add(c => c.ChildContent, (RenderFragment)(b => b.AddContent(0, "Delete"))));

        var button = cut.Find("button[role='menuitem']");
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
    }

    [Fact]
    public void Item_Enabled_Omits_AriaDisabled()
    {
        var cut = _ctx.Render<L.ContextMenuItem>(p => p
            .Add(c => c.Disabled, false)
            .Add(c => c.ChildContent, (RenderFragment)(b => b.AddContent(0, "Delete"))));

        var button = cut.Find("button[role='menuitem']");
        Assert.Null(button.GetAttribute("aria-disabled"));
    }

    // --- ContextMenuCheckboxItem ---

    [Fact]
    public void CheckboxItem_Disabled_Emits_AriaDisabled_True()
    {
        var cut = _ctx.Render<L.ContextMenuCheckboxItem>(p => p
            .Add(c => c.Disabled, true)
            .Add(c => c.ChildContent, (RenderFragment)(b => b.AddContent(0, "Show toolbar"))));

        var button = cut.Find("button[role='menuitemcheckbox']");
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
    }

    // --- ContextMenuRadioItem (new Disabled support) ---

    private IRenderedComponent<IComponent> RenderRadioItem(bool disabled, EventCallback<string> valueChanged)
    {
        // ContextMenuRadioItem has a non-nullable [CascadingParameter] context that
        // ContextMenuRadioGroup supplies via <CascadingValue>, so render it inside
        // its group (which also routes Select -> ValueChanged).
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenuRadioGroup>(0);
            builder.AddAttribute(1, "Value", "other");
            builder.AddAttribute(2, "ValueChanged", valueChanged);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(group =>
            {
                group.OpenComponent<L.ContextMenuRadioItem>(0);
                group.AddAttribute(1, "Value", "left");
                group.AddAttribute(2, "Disabled", disabled);
                group.AddAttribute(3, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Align left")));
                group.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void RadioItem_Disabled_Emits_AriaDisabled_And_Native_Disabled()
    {
        var cut = RenderRadioItem(disabled: true, EventCallback<string>.Empty);

        var button = cut.Find("button[role='menuitemradio']");
        // aria-disabled for AT...
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        // ...and the native disabled so the JS roving nav (:not([disabled])) skips it.
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void RadioItem_Enabled_Omits_AriaDisabled()
    {
        var cut = RenderRadioItem(disabled: false, EventCallback<string>.Empty);

        var button = cut.Find("button[role='menuitemradio']");
        Assert.Null(button.GetAttribute("aria-disabled"));
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public void RadioItem_Disabled_Click_Does_Not_Select()
    {
        var selected = false;
        var cb = EventCallback.Factory.Create<string>(this, (string _) => selected = true);
        var cut = RenderRadioItem(disabled: true, cb);

        // bUnit throws ArgumentException when clicking a native-disabled element;
        // before the fix the radio item had NO disabled attribute, so the click
        // dispatched and the ungated Select() fired. Either path must leave the
        // selection untouched, so swallow the disabled-dispatch exception.
        try { cut.Find("button[role='menuitemradio']").Click(); }
        catch (ArgumentException) { }

        // Select() is gated on Disabled (and the native disabled blocks dispatch),
        // so OnSelect/ValueChanged must not fire.
        Assert.False(selected);
    }
}
