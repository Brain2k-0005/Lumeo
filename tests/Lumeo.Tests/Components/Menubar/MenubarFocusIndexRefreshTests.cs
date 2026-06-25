using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// Battle-test #77 (state-on-data-change): MenubarContent / MenubarSubContent
/// track keyboard focus by a positional <c>_focusedIndex</c>. When the menu's
/// item set changes WHILE the menu stays open (async refill, sort, add/remove),
/// a stale index silently retargets a DIFFERENT item on the next Arrow/Home/End
/// move — e.g. End on a 5-item menu parks _focusedIndex at 4, then the list
/// shrinks to 3, and the next ArrowDown computes (4+1)%3 = 2 instead of 0.
///
/// The fix re-validates _focusedIndex against the live item count on every
/// post-open render and resets it to -1 when it no longer maps to a real item,
/// so navigation re-seeds from the top of the refreshed list.
/// </summary>
public class MenubarFocusIndexRefreshTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public MenubarFocusIndexRefreshTests()
    {
        _ctx.AddLumeoServices();
        // Last registration wins: route component interop through the tracker.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>
    /// Host that renders a Menubar whose single menu's content holds
    /// <see cref="ItemCount"/> MenubarItems. Re-rendering the host with a new
    /// ItemCount re-renders the open MenubarContent (firing its
    /// OnAfterRenderAsync) so the index-revalidation path runs — without
    /// re-providing any cascading value through cut.Render.
    /// </summary>
    private sealed class MenubarItemCountProbe : ComponentBase
    {
        [Parameter] public int ItemCount { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<L.Menubar>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(bar =>
            {
                bar.OpenComponent<L.MenubarMenu>(0);
                bar.AddAttribute(1, "ChildContent", (RenderFragment)(menu =>
                {
                    menu.OpenComponent<L.MenubarTrigger>(0);
                    menu.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "File")));
                    menu.CloseComponent();

                    menu.OpenComponent<L.MenubarContent>(1);
                    menu.AddAttribute(2, "ChildContent", (RenderFragment)(content =>
                    {
                        for (var i = 0; i < ItemCount; i++)
                        {
                            var label = $"Item {i}";
                            content.OpenComponent<L.MenubarItem>(i);
                            content.AddAttribute(i + 1, "ChildContent", (RenderFragment)(c => c.AddContent(0, label)));
                            content.CloseComponent();
                        }
                    }));
                    menu.CloseComponent();
                }));
                bar.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    [Fact]
    public void FocusedIndex_Is_Reset_When_Items_Shrink_While_Menu_Stays_Open()
    {
        // Start with 5 items; the interop's reported count mirrors the rendered set.
        _interop.MenuItemCount = 5;
        var cut = _ctx.Render<MenubarItemCountProbe>(p => p.Add(x => x.ItemCount, 5));

        cut.Find("button").Click(); // open the File menu

        var content = cut.Find("[role='menu']");
        // End parks focus on the last item (index 4).
        content.KeyDown(new KeyboardEventArgs { Key = "End" });
        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusMenuItemCalls, c => c.Index == 4));

        // The list refills with fewer items (e.g. async data settles) while the
        // menu remains open. Lower the reported count and re-render the host, which
        // re-renders the open content and runs the index-revalidation path.
        _interop.MenuItemCount = 3;
        cut.Render(p => p.Add(x => x.ItemCount, 3));

        var indexCountBefore = _interop.FocusMenuItemCalls.Count;

        // Next ArrowDown must re-seed from the TOP of the shrunk list (index 0),
        // not land on a stale modulo target. Without the fix _focusedIndex is
        // still 4, so ArrowDown computes (4+1)%3 = 2 — a different item.
        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        var lastCall = _interop.FocusMenuItemCalls[^1];
        Assert.True(_interop.FocusMenuItemCalls.Count > indexCountBefore,
            "ArrowDown should have issued a focus-by-index call.");
        Assert.Equal(0, lastCall.Index);
    }
}
