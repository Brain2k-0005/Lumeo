using System;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// P2 (Codex/CodeRabbit) — submenu teardown on parent exit, ContextMenu variant. When
/// the root context menu closes while a submenu is open, the submenu used to stay
/// mounted with data-state="open" under a data-state="closed" parent. The fix makes
/// ContextMenuSub observe the cascaded root open flag and reset its own state so the
/// submenu exits/unmounts WITH the tree.
/// </summary>
public class ContextMenuSubTeardownTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ContextMenuSubTeardownTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private RenderFragment Child => b =>
    {
        b.OpenComponent<L.ContextMenuContent>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(content =>
        {
            content.OpenComponent<L.ContextMenuSub>(0);
            content.AddAttribute(1, "ChildContent", (RenderFragment)(sub =>
            {
                sub.OpenComponent<L.ContextMenuSubTrigger>(0);
                sub.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "More")));
                sub.CloseComponent();
                sub.OpenComponent<L.ContextMenuSubContent>(2);
                sub.AddAttribute(3, "ChildContent", (RenderFragment)(sc =>
                {
                    sc.OpenComponent<L.ContextMenuItem>(0);
                    sc.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Sub Item")));
                    sc.CloseComponent();
                }));
                sub.CloseComponent();
            }));
            content.CloseComponent();
        }));
        b.CloseComponent();
    };

    private IRenderedComponent<L.ContextMenu> RenderMenu(bool open)
        => _ctx.Render<L.ContextMenu>(p => p.Add(m => m.Open, open).Add(m => m.ChildContent, Child));

    [Fact]
    public void Closing_Root_While_Submenu_Open_Dismisses_The_Submenu()
    {
        var cut = RenderMenu(open: true);

        cut.Find("button[aria-haspopup='menu']").Click();
        Assert.Equal("open", cut.Find("[id*='sub-content']").GetAttribute("data-state"));

        cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, Child));

        var sub = cut.Find("[id*='sub-content']");
        Assert.Equal("closed", sub.GetAttribute("data-state"));
        Assert.Contains("pointer-events-none", sub.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Closing_Root_Eventually_Unmounts_Both_Menu_And_Submenu()
    {
        var cut = RenderMenu(open: true);
        cut.Find("button[aria-haspopup='menu']").Click();
        Assert.NotEmpty(cut.FindAll("[id*='sub-content']"));

        cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, Child));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[id*='sub-content']"));
            Assert.Empty(cut.FindAll("[role='menu']"));
        }, timeout: TimeSpan.FromSeconds(5));
    }
}
