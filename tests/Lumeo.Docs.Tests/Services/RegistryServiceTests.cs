using System.Net;
using System.Text;
using Bunit;
using Lumeo.Docs.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Docs.Tests.Services;

public class RegistryServiceTests
{
    [Fact]
    public async Task GroupsByCategory_returns_grouped_components()
    {
        var json = """
        {
          "components": {
            "input":   { "name": "Input",   "category": "Forms",        "subcategory": "Inputs",   "description": "An input.",   "thumbnail": "/preview-cards/input.png",   "nugetPackage": "Lumeo", "hasDocsPage": true },
            "select":  { "name": "Select",  "category": "Forms",        "subcategory": "Selection","description": "A select.",   "thumbnail": "/preview-cards/select.png",  "nugetPackage": "Lumeo", "hasDocsPage": true },
            "table":   { "name": "Table",   "category": "Data Display", "subcategory": "Tables",   "description": "A table.",    "thumbnail": "/preview-cards/table.png",   "nugetPackage": "Lumeo", "hasDocsPage": true }
          }
        }
        """;
        var http = new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("https://test/") };
        // Non-in-process JS runtime: RegistryService skips the inline-registry
        // path and exercises the HTTP fetch path under test.
        var svc = new RegistryService(http, new StubJsRuntime());

        var groups = await svc.GroupsByCategoryAsync();

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups["Forms"].Count);
        Assert.Single(groups["Data Display"]);
    }

    [Fact]
    public async Task GetComponentAsync_dedupes_concurrent_loads_for_the_same_slug()
    {
        // Mirrors a page rendering several sibling <PropsTable> instances (root +
        // sub-components) for the SAME slug in one render pass: each one calls
        // GetComponentAsync before the first has populated _detailCache. Before the
        // fix, that fired one registry/{slug}.json fetch PER caller instead of sharing
        // the single in-flight load (Codex P2, PR #358 round 3).
        var json = """{ "name": "Button", "category": "Forms", "description": "A button.", "nugetPackage": "Lumeo" }""";
        var handler = new CountingDelayedHandler(json);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test/") };
        var svc = new RegistryService(http, new StubJsRuntime());

        // Two callers ask for the same slug back-to-back, before either await yields —
        // the exact shape of two <PropsTable Slug="button" .../> instances rendering
        // in the same synchronous pass.
        var t1 = svc.GetComponentAsync("button");
        var t2 = svc.GetComponentAsync("button");

        Assert.Equal(1, handler.RequestCount); // only ONE HTTP request in flight
        handler.Release();
        var r1 = await t1;
        var r2 = await t2;

        Assert.Equal(1, handler.RequestCount); // still one, after both callers resolved
        Assert.NotNull(r1);
        Assert.Same(r1, r2); // both callers observe the same cached detail instance
    }

    [Fact]
    public async Task GetComponentAsync_retries_after_a_synchronously_failing_load()
    {
        // Codex P2, PR #358 round 4: when LoadComponentAsync completes synchronously
        // (e.g. a handler that resolves without ever yielding, as a real 404/invalid-JSON
        // response can), its own `finally` tries to evict `slug` from _detailLoads BEFORE
        // GetComponentAsync has stored the entry — a no-op. Without the fix, the
        // already-completed failed task then gets stored and NEVER removed, so every
        // later call for that slug returns the stale null forever instead of retrying.
        var handler = new SyncFailingThenSucceedingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test/") };
        var svc = new RegistryService(http, new StubJsRuntime());

        var first = await svc.GetComponentAsync("button");
        Assert.Null(first); // first load failed synchronously

        var second = await svc.GetComponentAsync("button");

        Assert.Equal(2, handler.RequestCount); // second call issued a FRESH request, not a cached failure
        Assert.NotNull(second);
        Assert.Equal("Button", second!.Name);
    }

    // Fails the first request synchronously (no await ever reached), succeeds on the second —
    // reproduces a handler that resolves without yielding, so LoadComponentAsync runs to
    // completion before GetComponentAsync's caller regains control.
    private sealed class SyncFailingThenSucceedingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            RequestCount++;
            if (RequestCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
            var json = """{ "name": "Button", "category": "Forms", "description": "A button.", "nugetPackage": "Lumeo" }""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    // Counts requests and blocks each one on a gate so a test can assert only ONE
    // HTTP request was issued while two callers are still awaiting the same load.
    private sealed class CountingDelayedHandler(string json) : HttpMessageHandler
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            RequestCount++;
            await _gate.Task;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        public void Release() => _gate.TrySetResult();
    }

    // Minimal IJSRuntime that is NOT IJSInProcessRuntime, so RegistryService's
    // synchronous inline-registry read is skipped and the fetch path is tested.
    private sealed class StubJsRuntime : Microsoft.JSInterop.IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => default;
    }
}
