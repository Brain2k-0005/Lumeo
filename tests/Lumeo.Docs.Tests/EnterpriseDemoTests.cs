using Bunit;
using Lumeo;
using Lumeo.Docs.Pages.Demos;
using Lumeo.Docs.Tests.Helpers;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Lumeo.Docs.Tests;

// Meridian Ops (enterprise demo) polish pass — regression coverage for the two
// behavioural findings from the audit: approving/rejecting a request now visibly
// marks it resolved (and disables further actions on it), and the Audit log's
// facet counts recompute against the current filter instead of staying pinned to
// the full-dataset totals.
public class EnterpriseDemoTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public EnterpriseDemoTests()
    {
        // Loose mode: ComponentInteropService / OverlayProvider make real
        // _content/Lumeo/js/components.js interop calls (focus trap, scroll lock,
        // exit-animation wait) that bUnit has no browser to satisfy.
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo/js/components.js");
        module.Mode = JSRuntimeMode.Loose;

        // Icon/nav/registry services the demo's <DynamicIcon> needs, plus the real
        // Lumeo service set (Toast/Overlay/Theme/…) so ConfirmButton's AlertDialog
        // flow — driven through a real <OverlayProvider />, not an internal hook —
        // renders and resolves exactly as it does in the app.
        _ctx.AddDocsServices();
        _ctx.Services.AddLumeo();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Renders the Approvals view next to a real OverlayProvider, the way
    // EnterpriseDemoLayout does, so ConfirmButton's confirm dialog actually mounts.
    private static void ApprovalsWithOverlayHost(RenderTreeBuilder builder)
    {
        builder.OpenComponent<EnterpriseApprovalsView>(0);
        builder.CloseComponent();
        builder.OpenComponent<OverlayProvider>(1);
        builder.CloseComponent();
    }

    [Fact]
    public void Approving_a_request_marks_it_resolved_and_disables_further_actions()
    {
        var cut = _ctx.Render(ApprovalsWithOverlayHost);

        // REQ-4471 is selected by default. Trigger its Approve action.
        var approveTrigger = cut.FindAll("button").First(b => b.TextContent.Trim() == "Approve");
        approveTrigger.Click();

        // The ConfirmButton opens a real AlertDialog through OverlayProvider.
        cut.WaitForState(() => cut.FindAll("[role='alertdialog']").Count > 0, TimeSpan.FromSeconds(5));
        var dialog = cut.Find("[role='alertdialog']");
        var confirm = dialog.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Approve");
        confirm.Click();

        // Once resolved: no live Approve/Reject action remains for this request —
        // it's replaced by a disabled "Resolved" button — and the master-list card
        // and detail header both surface a "Resolved" badge instead of looking
        // identical to an untouched request.
        cut.WaitForState(() => cut.FindAll("button").Count(b => b.TextContent.Trim() == "Approve") == 0, TimeSpan.FromSeconds(5));

        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() is "Approve" or "Reject");
        var resolvedButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Resolved");
        Assert.True(resolvedButton.HasAttribute("disabled"));
        Assert.Contains("Resolved", cut.Markup);
    }

    [Fact]
    public void Audit_facet_counts_recompute_under_a_category_filter()
    {
        var cut = _ctx.Render<EnterpriseAuditView>();

        string SeverityCount(string severity)
        {
            var label = cut.FindAll("aside label").First(l => l.TextContent.Contains(severity));
            var spans = label.QuerySelectorAll("span");
            return spans[^1].TextContent.Trim();
        }

        // Baseline (no filters): full-dataset totals from MeridianData.Audit.
        Assert.Equal("4", SeverityCount("Warning"));
        Assert.Equal("3", SeverityCount("Critical"));

        // Tick the "Auth" category facet — narrows the timeline to 3 events
        // (2 Warning, 1 Notice, 0 Info, 0 Critical).
        var authLabel = cut.FindAll("aside label").First(l => l.TextContent.Contains("Auth"));
        authLabel.QuerySelector("[role='checkbox']")!.Click();

        cut.WaitForState(() => SeverityCount("Warning") == "2", TimeSpan.FromSeconds(5));

        // The severity facet must reflect the Auth-only subset, not the stale
        // full-dataset totals it used to show once a category was checked.
        Assert.Equal("2", SeverityCount("Warning"));
        Assert.Equal("1", SeverityCount("Notice"));
        Assert.Equal("0", SeverityCount("Critical"));
        Assert.Equal("0", SeverityCount("Info"));
    }
}
