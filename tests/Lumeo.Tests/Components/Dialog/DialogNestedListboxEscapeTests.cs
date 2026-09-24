using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Dialog;

/// <summary>
/// Field report (present since 5.10.0): Escape pressed inside a non-searchable Select
/// nested in a Dialog closed the list AND the dialog in the same keystroke — the list's
/// content root had no <c>@onkeydown:stopPropagation</c>, unlike Dialog/Sheet/Drawer/
/// AlertDialog's own content root (see <see cref="DialogFocusManagementTests"/> for that
/// existing coverage). Same class of bug audited and fixed with the same mechanism across
/// Select, Combobox, DropdownMenu, ContextMenu, Popover (which also covers DatePicker/
/// TimePicker, both hosted in a PopoverContent), Cascader and TreeSelect.
///
/// Mirrors <see cref="NestedOverlayHost{TInnerRoot,TInnerContent}"/>'s Dialog-in-Dialog
/// pattern for the components sharing its Open/OpenChanged/ChildContent shape (Select,
/// DropdownMenu, Popover); Combobox gets a dedicated host below because its trigger is a
/// plain input living outside its content component, unlike the others.
/// </summary>
public class DialogNestedListboxEscapeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DialogNestedListboxEscapeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Escape_On_A_Select_Listbox_Inside_A_Dialog_Closes_Only_The_Select()
    {
        var cut = _ctx.Render<NestedOverlayHost<L.Select, L.SelectContent>>();

        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(cut.Instance.InnerOpen);
        Assert.True(cut.Instance.OuterOpen);

        // A second Escape, now on the dialog itself, closes it.
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.False(cut.Instance.OuterOpen);
    }

    [Fact]
    public void Escape_On_A_DropdownMenu_Inside_A_Dialog_Closes_Only_The_Menu()
    {
        var cut = _ctx.Render<NestedOverlayHost<L.DropdownMenu, L.DropdownMenuContent>>();

        var menu = cut.Find("[role='menu']");
        menu.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(cut.Instance.InnerOpen);
        Assert.True(cut.Instance.OuterOpen);

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.False(cut.Instance.OuterOpen);
    }

    [Fact]
    public void Escape_On_A_Popover_Inside_A_Dialog_Closes_Only_The_Popover()
    {
        var cut = _ctx.Render<NestedOverlayHost<L.Popover, L.PopoverContent>>();

        // PopoverContent also renders role="dialog" — the inner one is last in document order.
        var dialogs = cut.FindAll("[role='dialog']");
        Assert.Equal(2, dialogs.Count);
        dialogs[^1].KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(cut.Instance.InnerOpen);
        Assert.True(cut.Instance.OuterOpen);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role='dialog']")));
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.False(cut.Instance.OuterOpen);
    }

    // --- Combobox: trigger is a plain input OUTSIDE ComboboxContent, so it needs its own host ---

    private sealed class ComboboxInDialogHost : ComponentBase
    {
        public bool DialogOpen { get; private set; } = true;
        public bool ComboboxOpen { get; private set; } = true;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "Open", DialogOpen);
            builder.AddAttribute(2, "OpenChanged", EventCallback.Factory.Create<bool>(this, v => DialogOpen = v));
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(mid =>
                {
                    mid.OpenComponent<L.Combobox>(0);
                    mid.AddAttribute(1, "Open", ComboboxOpen);
                    mid.AddAttribute(2, "OpenChanged", EventCallback.Factory.Create<bool>(this, v => ComboboxOpen = v));
                    mid.AddAttribute(3, "ChildContent", (RenderFragment)(ib =>
                    {
                        ib.OpenComponent<L.ComboboxInput>(0);
                        ib.CloseComponent();
                        ib.OpenComponent<L.ComboboxContent>(1);
                        ib.AddAttribute(2, "ChildContent", (RenderFragment)(c =>
                        {
                            c.OpenComponent<L.ComboboxItem>(0);
                            c.AddAttribute(1, "Value", "one");
                            c.AddAttribute(2, "ChildContent", (RenderFragment)(t => t.AddContent(0, "One")));
                            c.CloseComponent();
                        }));
                        ib.CloseComponent();
                    }));
                    mid.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    [Fact]
    public void Escape_On_The_Combobox_Input_Inside_A_Dialog_Closes_Only_The_Combobox()
    {
        var cut = _ctx.Render<ComboboxInDialogHost>();

        var input = cut.Find("input[role='combobox']");
        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(cut.Instance.ComboboxOpen);
        Assert.True(cut.Instance.DialogOpen);

        // ComboboxInput's @onkeydown:stopPropagation is a STATIC "true" (see the
        // ComboboxInput.razor comment above the input): a per-render expression gated
        // on Context.IsOpen looked right and passed in bUnit, but did NOT reliably stop
        // the event reaching the Dialog's own Escape handler in a real browser — bUnit's
        // simulated dispatch does not reproduce that discrepancy, which is why this
        // exact scenario needs the E2E spec (DialogNestedListboxEscapeE2ETests), not
        // just this bUnit test, to be trustworthy. The accepted trade-off of the static
        // fix: Escape on a closed-but-focused Combobox input no longer bubbles to
        // close an ancestor Dialog in one press.
        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.True(cut.Instance.DialogOpen);
    }
}
