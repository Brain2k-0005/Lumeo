using System;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// P2 (Codex/CodeRabbit) — submenu teardown on parent exit. When the root menu closes
/// while a submenu is open, the sub's SubContext.IsOpen used to stay true, leaving the
/// submenu mounted with data-state="open" under a data-state="closed" parent until the
/// whole tree unmounted (orphaned registration + visual desync). The fix makes
/// DropdownMenuSub observe the cascaded root open flag and reset its own state so the
/// submenu exits/unmounts WITH the tree.
/// </summary>
public class DropdownMenuSubTeardownTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DropdownMenuSubTeardownTests() => _ctx.AddLumeoServices();
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
                    sc.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Sub Item")));
                    sc.CloseComponent();
                }));
                sub.CloseComponent();
            }));
            content.CloseComponent();
        }));
        b.CloseComponent();
    };

    private IRenderedComponent<L.DropdownMenu> RenderMenu(bool open)
        => _ctx.Render<L.DropdownMenu>(p => p.Add(m => m.Open, open).Add(m => m.ChildContent, Child));

    [Fact]
    public void Closing_Root_While_Submenu_Open_Dismisses_The_Submenu()
    {
        var cut = RenderMenu(open: true);

        // Open the submenu via its trigger.
        cut.Find("button[aria-haspopup='menu']").Click();
        Assert.Equal("open", cut.Find("[id*='sub-content']").GetAttribute("data-state"));

        // Close the root while the submenu is still open.
        cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, Child));

        // The submenu must exit WITH the tree: its data-state flips to closed (not
        // stranded open under a closed parent) and the fading surface is inert.
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

        // Both exit windows elapse and the whole tree unmounts — no orphaned submenu.
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[id*='sub-content']"));
            Assert.Empty(cut.FindAll("[role='menu']"));
        }, timeout: TimeSpan.FromSeconds(5));
    }
}
