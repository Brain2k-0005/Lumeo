using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// Base class for the Gantt v2/v3 parity harness (feat/gantt-v3, T4). Unlike
/// every other spec in this project — which drives the docs WASM site
/// (<see cref="PlaywrightTestBase"/>'s default base URL, port 5287/5290) —
/// these specs drive <c>tests/Lumeo.Tests.ServerHost</c>'s NEW <c>/e2e/gantt-v2</c>
/// / <c>/e2e/gantt-v3</c> / <c>/e2e/gantt-v3-tree</c> pages (a Blazor SERVER host,
/// real SignalR circuit, no WASM boot). <see cref="PlaywrightTestBase.BaseUrl"/>
/// is a non-virtual get-only property seeded once from <c>LUMEO_E2E_BASE_URL</c>
/// in its own field initializer, so it can't be overridden by a derived class —
/// this base therefore ignores it entirely and resolves its own base URL from a
/// SEPARATE env var, <see cref="GanttHostBaseUrl"/>, defaulting to a port
/// (5299) that doesn't collide with the docs dev-server (5287 local / 5290 CI)
/// or the server-leg harness (dynamic port, scripts/server-leg/run.mjs).
///
/// Running locally:
/// <code>
/// dotnet run --project tests/Lumeo.Tests.ServerHost/Lumeo.Tests.ServerHost.csproj --urls http://localhost:5299
/// # in another terminal:
/// dotnet test tests/Lumeo.Tests.E2E/Lumeo.Tests.E2E.csproj --filter "FullyQualifiedName~Gantt"
/// </code>
/// </summary>
[Collection(GanttSequentialCollection.Name)]
public abstract class GanttParityTestBase : PlaywrightTestBase
{
    protected static string GanttHostBaseUrl { get; } =
        Environment.GetEnvironmentVariable("LUMEO_GANTT_E2E_BASE_URL")
        ?? "http://localhost:5299";

    /// <summary>Navigates to <paramref name="path"/> relative to <see cref="GanttHostBaseUrl"/> (NOT the docs-site <c>BaseUrl</c>).</summary>
    protected Task<IResponse?> GotoHost(string path) => Page.GotoAsync($"{GanttHostBaseUrl}{path}");
}

/// <summary>
/// Forces every Gantt parity spec onto xUnit's single-threaded "same collection"
/// lane. All of them drive the SAME live <c>Lumeo.Tests.ServerHost</c> process —
/// a single Kestrel instance juggling one real SignalR circuit per test's
/// browser page — and xUnit parallelizes across test CLASSES by default. Under
/// that concurrent circuit load, several otherwise-correct assertions
/// (Milestone_diamond_bounding_box_matches_between_v2_and_v3, the
/// Arrow_endpoints theories) intermittently exceeded their timeouts purely from
/// resource contention, not a real rendering bug — see the T4 report's gate
/// section. <c>[Collection]</c> on the shared base is inherited by every
/// subclass, so this is the ONE place that needs it.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class GanttSequentialCollection : ICollectionFixture<object>
{
    public const string Name = "Gantt E2E (sequential — shared ServerHost circuit)";
}
