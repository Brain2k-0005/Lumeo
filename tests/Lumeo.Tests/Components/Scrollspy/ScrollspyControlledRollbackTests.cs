using Bunit;
using Lumeo.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scrollspy;

/// <summary>
/// Regression tests for the controlled-component rollback fix on Scrollspy.
/// When Scrollspy is used in controlled mode (ActiveIdChanged bound) and the
/// parent vetoes a click-driven active-section change by re-rendering with the
/// ORIGINAL ActiveId value, the UI must roll back to that bound value rather
/// than keeping the optimistic click-driven update.
/// </summary>
public class ScrollspyControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ScrollspyControlledRollbackTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment LinksAndSections() => builder =>
    {
        builder.OpenComponent<L.ScrollspyLink>(0);
        builder.AddAttribute(1, "Target", "intro");
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Intro")));
        builder.CloseComponent();

        builder.OpenComponent<L.ScrollspyLink>(3);
        builder.AddAttribute(4, "Target", "features");
        builder.AddAttribute(5, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Features")));
        builder.CloseComponent();

        builder.OpenComponent<L.ScrollspySection>(6);
        builder.AddAttribute(7, "Id", "intro");
        builder.AddAttribute(8, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Intro body")));
        builder.CloseComponent();

        builder.OpenComponent<L.ScrollspySection>(9);
        builder.AddAttribute(10, "Id", "features");
        builder.AddAttribute(11, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Features body")));
        builder.CloseComponent();
    };

    // --- Controlled: veto rolls back ---

    [Fact]
    public void Controlled_Veto_Click_Rolls_Back_Active_Link()
    {
        // Parent starts with ActiveId="intro" and vetoes every click by
        // re-rendering with the SAME ActiveId it had before (never advances).
        string parentState = "intro";
        IRenderedComponent<L.Scrollspy>? cut = null;

        var callback = EventCallback.Factory.Create<string>(_ctx, (string incoming) =>
        {
            // Veto: do NOT update parentState; re-render with the original value.
            cut!.Render(p =>
            {
                p.Add(b => b.ActiveId, parentState);   // still "intro"
                p.Add(b => b.ActiveIdChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { }));
                p.Add(b => b.ChildContent, LinksAndSections());
            });
        });

        cut = _ctx.Render<L.Scrollspy>(p => p
            .Add(b => b.ActiveId, "intro")
            .Add(b => b.ActiveIdChanged, callback)
            .Add(b => b.ChildContent, LinksAndSections()));

        // Initially "Intro" is active.
        var introLink = cut.FindAll("[data-slot='scrollspy-link']").First(l => l.TextContent.Trim() == "Intro");
        Assert.Equal("true", introLink.GetAttribute("data-active"));

        // Click "Features" — OnNavigate optimistically sets the active section to
        // "features" and fires ActiveIdChanged; the parent vetoes and re-renders
        // with ActiveId="intro" (unchanged from before the click).
        cut.FindAll("[data-slot='scrollspy-link']").First(l => l.TextContent.Trim() == "Features").Click();

        // After the veto the active link must have rolled back to "Intro", not
        // stayed on the optimistically-clicked "Features".
        var active = cut.FindAll("[data-slot='scrollspy-link']")
            .Where(l => l.GetAttribute("data-active") == "true")
            .ToList();
        Assert.Single(active);
        Assert.Equal("Intro", active[0].TextContent.Trim());
    }

    // --- Controlled: accepted click keeps new value ---

    [Fact]
    public void Controlled_Accepted_Click_Keeps_New_Active_Link()
    {
        // Parent accepts every click by updating its own state and re-rendering.
        string parentState = "intro";
        IRenderedComponent<L.Scrollspy>? cut = null;

        EventCallback<string> callback = default;
        callback = EventCallback.Factory.Create<string>(_ctx, (string incoming) =>
        {
            parentState = incoming;
            cut!.Render(p =>
            {
                p.Add(b => b.ActiveId, parentState);
                p.Add(b => b.ActiveIdChanged, callback);
                p.Add(b => b.ChildContent, LinksAndSections());
            });
        });

        cut = _ctx.Render<L.Scrollspy>(p => p
            .Add(b => b.ActiveId, "intro")
            .Add(b => b.ActiveIdChanged, callback)
            .Add(b => b.ChildContent, LinksAndSections()));

        cut.FindAll("[data-slot='scrollspy-link']").First(l => l.TextContent.Trim() == "Features").Click();

        // Parent accepted — "Features" should now be the only active link.
        var active = cut.FindAll("[data-slot='scrollspy-link']")
            .Where(l => l.GetAttribute("data-active") == "true")
            .ToList();
        Assert.Single(active);
        Assert.Equal("Features", active[0].TextContent.Trim());
    }

    // --- Controlled: veto also rolls back the programmatic scroll ---

    [Fact]
    public void Controlled_Veto_Rolls_Back_Scroll_Position()
    {
        // Validates that the rollback isn't just cosmetic (data-active): the
        // component re-issues a programmatic scroll back to the vetoed
        // (authoritative) section.
        string parentState = "intro";
        IRenderedComponent<L.Scrollspy>? cut = null;

        var callback = EventCallback.Factory.Create<string>(_ctx, (_) =>
        {
            cut!.Render(p =>
            {
                p.Add(b => b.ActiveId, parentState);
                p.Add(b => b.ActiveIdChanged, EventCallback.Factory.Create<string>(_ctx, (_2) => { }));
                p.Add(b => b.ChildContent, LinksAndSections());
            });
        });

        cut = _ctx.Render<L.Scrollspy>(p => p
            .Add(b => b.ActiveId, "intro")
            .Add(b => b.ActiveIdChanged, callback)
            .Add(b => b.ChildContent, LinksAndSections()));

        cut.FindAll("[data-slot='scrollspy-link']").First(l => l.TextContent.Trim() == "Features").Click();

        // First scroll is the click itself (to "features"); the veto must trigger
        // a follow-up rollback scroll back to "intro".
        var calls = _interop.ScrollspyScrollToCalls;
        Assert.True(calls.Count >= 2, $"Expected at least 2 scroll calls, got {calls.Count}");
        Assert.Equal("features", calls[0].SectionId);
        Assert.Equal("intro", calls[^1].SectionId);
    }

    // --- Controlled: programmatic parent reset (no prior click) ---

    [Fact]
    public void Controlled_Programmatic_Reset_Is_Adopted()
    {
        // Start with ActiveId="intro"; the parent programmatically resets to
        // "features" WITHOUT a user click first (external data-driven set).
        var cut = _ctx.Render<L.Scrollspy>(p => p
            .Add(b => b.ActiveId, "intro")
            .Add(b => b.ActiveIdChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { }))
            .Add(b => b.ChildContent, LinksAndSections()));

        Assert.Equal("true", cut.FindAll("[data-slot='scrollspy-link']")
            .First(l => l.TextContent.Trim() == "Intro").GetAttribute("data-active"));

        cut.Render(p =>
        {
            p.Add(b => b.ActiveId, "features");
            p.Add(b => b.ActiveIdChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { }));
            p.Add(b => b.ChildContent, LinksAndSections());
        });

        var active = cut.FindAll("[data-slot='scrollspy-link']")
            .Where(l => l.GetAttribute("data-active") == "true")
            .ToList();
        Assert.Single(active);
        Assert.Equal("Features", active[0].TextContent.Trim());
    }

    // --- Controlled: a genuine empty/null clear-veto rolls back to NOTHING active (round-16 Codex
    //     finding — only updating _currentActiveId when ActiveId was non-empty meant an authoritative
    //     reset to "" couldn't clear the optimistically-active link) ---

    [Fact]
    public void Controlled_Veto_With_Empty_ActiveId_Clears_The_Active_Link()
    {
        // Parent starts with ActiveId="intro" and its handler explicitly CLEARS to "" — a real,
        // distinguishable decision (ActiveId demonstrably HAD a value and now demonstrably doesn't).
        IRenderedComponent<L.Scrollspy>? cut = null;

        var callback = EventCallback.Factory.Create<string>(_ctx, (string _) =>
        {
            cut!.Render(p =>
            {
                p.Add(b => b.ActiveId, "");
                p.Add(b => b.ActiveIdChanged, EventCallback.Factory.Create<string>(_ctx, (_2) => { }));
                p.Add(b => b.ChildContent, LinksAndSections());
            });
        });

        cut = _ctx.Render<L.Scrollspy>(p => p
            .Add(b => b.ActiveId, "intro")
            .Add(b => b.ActiveIdChanged, callback)
            .Add(b => b.ChildContent, LinksAndSections()));

        Assert.Equal("true", cut.FindAll("[data-slot='scrollspy-link']")
            .First(l => l.TextContent.Trim() == "Intro").GetAttribute("data-active"));

        // Click "Features" — optimistically activates it; the parent's genuine clear-veto fires.
        cut.FindAll("[data-slot='scrollspy-link']").First(l => l.TextContent.Trim() == "Features").Click();

        // No link may remain active — the clear must be honoured, not silently ignored.
        var active = cut.FindAll("[data-slot='scrollspy-link']")
            .Where(l => l.GetAttribute("data-active") == "true")
            .ToList();
        Assert.Empty(active);
    }

    // --- Controlled: clearing ActiveId must never leave a stale pending scroll that later fires
    //     for the empty id (round-17 Codex P3 follow-up) ---

    [Fact]
    public void Clearing_ActiveId_Never_Triggers_A_Scroll_To_The_Empty_Id()
    {
        // Real section -> empty, in successive renders (the closest bUnit can model the "a clear
        // arrives before OnAfterRenderAsync consumes the prior pending scroll" race — bUnit's Render
        // flushes OnAfterRenderAsync synchronously per call, so this asserts the durable guarantee:
        // across the WHOLE sequence, a ScrollspyScrollTo call for an empty/null section id never
        // happens, regardless of how _pendingProgrammaticScroll's lifecycle is implemented internally.
        var cut = _ctx.Render<L.Scrollspy>(p => p
            .Add(b => b.ActiveId, "intro")
            .Add(b => b.ActiveIdChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { }))
            .Add(b => b.ChildContent, LinksAndSections()));

        cut.Render(p =>
        {
            p.Add(b => b.ActiveId, "features");
            p.Add(b => b.ActiveIdChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { }));
            p.Add(b => b.ChildContent, LinksAndSections());
        });

        cut.Render(p =>
        {
            p.Add(b => b.ActiveId, "");
            p.Add(b => b.ActiveIdChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { }));
            p.Add(b => b.ChildContent, LinksAndSections());
        });

        Assert.DoesNotContain(_interop.ScrollspyScrollToCalls, c => string.IsNullOrEmpty(c.SectionId));
    }
}
