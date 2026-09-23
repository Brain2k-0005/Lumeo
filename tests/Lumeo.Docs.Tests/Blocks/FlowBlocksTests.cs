using Bunit;
using Lumeo;
using Lumeo.Docs.Pages.Patterns;
using Lumeo.Docs.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Docs.Tests.Blocks;

// Phase 3b: the four /blocks/flow-* pages (Pages/Patterns/Flow*Pattern.razor). Each test
// renders the real page (same DI as the docs WASM app, Loose JSInterop — mirrors
// SaasDemoTests/AllShowcasesRenderTests) and checks one behavioural invariant the task spec
// calls out: automation's add-step button, the agent tree's add-tool + Tidy pass, the
// pipeline's undeletable gates + cycle rejection, and the impact map's per-initiative
// inspector.
public class FlowBlocksTests
{
    private static BunitContext NewContext(IResponsiveService? responsive = null)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddDocsServices();
        if (responsive is not null) ctx.Services.AddSingleton(responsive);
        return ctx;
    }

    // ---- Automation workflow ----

    [Fact]
    public async Task Automation_page_renders_its_steps_and_labeled_branches()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<FlowAutomationPattern>();

        Assert.Contains("New order", cut.Markup);
        Assert.Contains("In stock?", cut.Markup);
        Assert.Contains("Reserve stock", cut.Markup);
        Assert.Contains("Ship order", cut.Markup);
        // The Yes/No condition-branch labels.
        Assert.Contains(">Yes<", cut.Markup);
        Assert.Contains(">No<", cut.Markup);
    }

    [Fact]
    public async Task Automation_add_step_appends_a_new_node_chained_off_the_trigger()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<FlowAutomationPattern>();
        var canvas = cut.FindComponent<FlowCanvas>();
        var before = canvas.Instance.CurrentNodes.Count;

        var addStep = cut.FindAll("button").First(b => b.TextContent.Contains("Add step"));
        await cut.InvokeAsync(() => addStep.Click());

        var after = cut.FindComponent<FlowCanvas>().Instance.CurrentNodes.Count;
        Assert.Equal(before + 1, after);
    }

    // ---- AI agent tree ----

    [Fact]
    public async Task Agent_tree_page_renders_the_orchestrator_and_its_sub_agents()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<FlowAgentTreePattern>();

        Assert.Contains("Orchestrator", cut.Markup);
        Assert.Contains("Research agent", cut.Markup);
        Assert.Contains("Coding agent", cut.Markup);
        Assert.Contains("delegates", cut.Markup);
        Assert.Contains("GPT-5.1", cut.Markup);
    }

    [Fact]
    public async Task Agent_tree_add_tool_on_a_leaf_node_adds_a_child_with_a_uses_edge()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<FlowAgentTreePattern>();
        var canvas = cut.FindComponent<FlowCanvas>();
        var before = canvas.Instance.CurrentNodes.Count;

        // "research-tool"/"coding-tool" are leaves (no outgoing tree edge) and each renders an
        // inline "Add tool" button; the two agent/root nodes above them already have a child, so
        // they don't.
        var addToolButtons = cut.FindAll("button").Where(b => b.TextContent.Contains("Add tool")).ToList();
        Assert.NotEmpty(addToolButtons);
        await cut.InvokeAsync(() => addToolButtons[0].Click());

        var after = cut.FindComponent<FlowCanvas>().Instance.CurrentNodes.Count;
        Assert.Equal(before + 1, after);
    }

    [Fact]
    public async Task Agent_tree_tidy_button_recomputes_positions_without_throwing()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<FlowAgentTreePattern>();

        var tidy = cut.FindAll("button").First(b => b.TextContent.Contains("Tidy"));
        // Recomputing layout must not throw and must leave every node in place (none lost).
        var before = cut.FindComponent<FlowCanvas>().Instance.CurrentNodes.Count;
        await cut.InvokeAsync(() => tidy.Click());
        var after = cut.FindComponent<FlowCanvas>().Instance.CurrentNodes.Count;
        Assert.Equal(before, after);
    }

    // ---- Data pipeline ----

    [Fact]
    public async Task Pipeline_page_renders_dependency_ordered_stages()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<FlowPipelinePattern>();

        Assert.Contains("Ingest", cut.Markup);
        Assert.Contains("Clean", cut.Markup);
        Assert.Contains("QA sign-off", cut.Markup);
        Assert.Contains("Transform", cut.Markup);
        Assert.Contains("Release sign-off", cut.Markup);
        Assert.Contains("Publish", cut.Markup);
    }

    [Fact]
    public async Task Pipeline_approval_gates_are_marked_not_deletable_and_show_a_locked_badge()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<FlowPipelinePattern>();
        var canvas = cut.FindComponent<FlowCanvas>();

        var gateQa = canvas.Instance.CurrentNodes.Single(n => n.Id == "gate-qa");
        var gateRelease = canvas.Instance.CurrentNodes.Single(n => n.Id == "gate-release");
        Assert.False(gateQa.Deletable);
        Assert.False(gateRelease.Deletable);

        var ingest = canvas.Instance.CurrentNodes.Single(n => n.Id == "ingest");
        Assert.True(ingest.Deletable);

        // The lock badge is icon-only (review round 1 shrank it to fit "Release sign-off" on
        // one line without truncating) - its accessible name lives in the title attribute.
        Assert.Contains("This gate can't be deleted", cut.Markup);
    }

    [Fact]
    public async Task Pipeline_rejects_a_connection_that_would_create_a_cycle()
    {
        var toast = new RecordingToastService();
        await using var ctx = NewContext();
        ctx.Services.AddSingleton<IToastService>(toast);
        var cut = ctx.Render<FlowPipelinePattern>();
        var canvas = cut.FindComponent<FlowCanvas>();

        // "publish" is downstream of "ingest" in the fixed chain — connecting publish -> ingest
        // would close a cycle. CommitConnect is the same [JSInvokable] entry point flow.js calls
        // on a real pointer-drop connect gesture; invoked through the renderer's dispatcher
        // (InvokeAsync) since it triggers a re-render, same as a real JS-originated call would.
        var accepted = await cut.InvokeAsync(() => canvas.Instance.CommitConnect("publish", null, "ingest", null));

        Assert.False(accepted);
        Assert.Contains(toast.Errors, e => (e.Description ?? "").Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Pipeline_accepts_a_connection_that_does_not_create_a_cycle()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<FlowPipelinePattern>();
        var canvas = cut.FindComponent<FlowCanvas>();

        // "ingest" -> "transform" skips ahead in the same DAG direction — no cycle.
        var accepted = await cut.InvokeAsync(() => canvas.Instance.CommitConnect("ingest", null, "transform", null));

        Assert.True(accepted);
    }

    // ---- Growth impact map ----

    [Fact]
    public async Task Impact_map_page_renders_initiatives_metrics_and_the_north_star()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<FlowImpactMapPattern>();

        Assert.Contains("Referral program", cut.Markup);
        Assert.Contains("Activation rate", cut.Markup);
        Assert.Contains("Weekly Active Teams", cut.Markup);
        // Correlation-score edge labels.
        Assert.Contains("r=0.61", cut.Markup);
        Assert.Contains("32%", cut.Markup);
    }

    [Fact]
    public async Task Impact_map_selecting_an_initiative_shows_its_correlated_metrics_in_the_inspector()
    {
        var responsive = new FakeResponsiveService();
        await using var ctx = NewContext(responsive);
        var cut = ctx.Render<FlowImpactMapPattern>();
        var canvas = cut.FindComponent<FlowCanvas>();

        Assert.Contains("Select an initiative", cut.Markup);

        await cut.InvokeAsync(() => canvas.Instance.SelectAsync(new[] { "initiative-onboarding" }));

        var after = cut.Markup;
        Assert.Contains("Onboarding redesign", after);
        Assert.Contains("r=0.74", after);
        Assert.Contains("r=0.55", after);
    }

    private sealed class FakeResponsiveService : IResponsiveService
    {
        public double Width { get; } = 1920;
        public double Height { get; } = 1080;
        public Breakpoint Current { get; } = Breakpoint.Xl;
        public bool IsMobile => false;
        public bool IsTablet => false;
        public bool IsDesktop => true;
        public event Action<ViewportInfo>? ViewportChanged { add { } remove { } }
        public ValueTask EnsureInitialisedAsync() => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingToastService : IToastService
    {
        public List<(string Title, string? Description)> Errors { get; } = new();
        // Explicit (non-field-like) event accessors: this fake never raises them, and a plain
        // field-like `event Action? OnShow;` declaration trips CS0067 ("event never used") under
        // -warnaserror in CI. Empty add/remove bodies satisfy the interface without a backing
        // field for the compiler to complain is unused.
        public event Action<ToastMessage>? OnShow { add { } remove { } }
        public event Action<string>? OnDismiss { add { } remove { } }
        public event Action<string, ToastOptions>? OnUpdate { add { } remove { } }
        public string Show(string title, string? description = null, ToastVariant variant = ToastVariant.Default) => Guid.NewGuid().ToString();
        public string Show(ToastOptions options) => Guid.NewGuid().ToString();
        public string Show(Microsoft.AspNetCore.Components.RenderFragment content, ToastVariant variant = ToastVariant.Default, int? duration = null) => Guid.NewGuid().ToString();
        public string Success(string title, string? description = null) => Guid.NewGuid().ToString();
        public string Error(string title, string? description = null)
        {
            Errors.Add((title, description));
            return Guid.NewGuid().ToString();
        }
        public string Warning(string title, string? description = null) => Guid.NewGuid().ToString();
        public string Info(string title, string? description = null) => Guid.NewGuid().ToString();
        public void Dismiss(string toastId) { }
        public void DismissAll() { }
        public void Update(string toastId, ToastOptions options) { }
        public Task<string> Promise<T>(Func<Task<T>> action, ToastOptions loading, Func<T, ToastOptions> success, Func<Exception, ToastOptions> error)
            => Task.FromResult(Guid.NewGuid().ToString());
    }
}
