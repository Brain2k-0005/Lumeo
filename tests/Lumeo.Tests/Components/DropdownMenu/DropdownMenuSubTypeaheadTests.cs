using System.Linq;
using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// Round-6/7 (Codex) — DropdownMenuSubContent typeahead gap. The round-2 fix that
/// scopes submenu keydown with stopPropagation stopped printable keys reaching the
/// root DropdownMenuContent typeahead — but unlike ContextMenuSubContent, the
/// dropdown submenu had NO local typeahead branch of its own, so type-to-focus was
/// dead inside dropdown submenus (keyboard users could no longer jump to an item by
/// name). The fix adds the scoped submenu typeahead (MenuTypeahead +
/// FocusMenuItemByTypeahead), mirroring ContextMenuSubContent.
///
/// Discriminating repro: press a printable key on the OPEN sub content.
///   • Pre-fix: no default branch → NO FocusMenuItemByTypeahead call at all.
///   • Fixed:   exactly one typeahead call, against the SUB container id.
/// </summary>
public class DropdownMenuSubTypeaheadTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DropdownMenuSubTypeaheadTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private RenderFragment Child => b =>
    {
        b.OpenComponent<L.DropdownMenuTrigger>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Menu")));
        b.CloseComponent();
        b.OpenComponent<L.DropdownMenuContent>(2);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(content =>
        {
            content.OpenComponent<L.DropdownMenuSub>(0);
            content.AddAttribute(1, "ChildContent", (RenderFragment)(sub =>
            {
                sub.OpenComponent<L.DropdownMenuSubTrigger>(0);
                sub.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "More")));
                sub.CloseComponent();
                sub.OpenComponent<L.DropdownMenuSubContent>(2);
                sub.AddAttribute(3, "ChildContent", (RenderFragment)(sc =>
                {
                    sc.OpenComponent<L.DropdownMenuItem>(0);
                    sc.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
                    sc.CloseComponent();
                }));
                sub.CloseComponent();
            }));
            content.CloseComponent();
        }));
        b.CloseComponent();
    };

    [Fact]
    public void Printable_Key_In_Open_Submenu_Triggers_Scoped_Typeahead()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p.Add(m => m.Open, true).Add(m => m.ChildContent, Child));

        // Open the submenu via its trigger, then locate the sub content panel.
        cut.Find("button[aria-haspopup='menu']").Click();
        var subContent = cut.Find("[id*='dropdown-sub-content']");

        subContent.KeyDown(new KeyboardEventArgs { Key = "b" });

        // Exactly one typeahead call, scoped to the SUB container. Pre-fix there was
        // no local typeahead branch, so this list was empty.
        var call = Assert.Single(_interop.TypeaheadCalls);
        Assert.Equal(subContent.Id, call.ContainerId);
        Assert.Equal("b", call.Query);
    }

    [Fact]
    public void Modifier_Combo_In_Submenu_Does_Not_Trigger_Typeahead()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p.Add(m => m.Open, true).Add(m => m.ChildContent, Child));
        cut.Find("button[aria-haspopup='menu']").Click();
        var subContent = cut.Find("[id*='dropdown-sub-content']");

        // Ctrl+b is a modifier combo, not type-to-focus: it must fall through so
        // native shortcuts keep working.
        subContent.KeyDown(new KeyboardEventArgs { Key = "b", CtrlKey = true });

        Assert.Empty(_interop.TypeaheadCalls);
    }
}
