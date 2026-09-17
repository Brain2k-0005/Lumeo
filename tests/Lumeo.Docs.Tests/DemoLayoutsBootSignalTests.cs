using Bunit;
using Lumeo;
using Lumeo.Docs.Pages.Demos;
using Lumeo.Docs.Tests.Helpers;
using Xunit;

namespace Lumeo.Docs.Tests;

// index.html paints a full-screen boot splash over every prerendered route and
// only js/docs.js `lumeo.signalBlazorReady` removes it. MainLayout sends that
// signal on its first render — but both demo layouts bypass MainLayout, so a
// direct load or hard reload of /demos/enterprise or /demos/saas/* sat behind
// the splash until index.html's 15-second safety-net timeout (owner report,
// 2026-09-17). Each demo layout must send the signal itself.
public class DemoLayoutsBootSignalTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DemoLayoutsBootSignalTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo/js/components.js");
        module.Mode = JSRuntimeMode.Loose;
        _ctx.AddDocsServices();
        _ctx.Services.AddLumeo();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void EnterpriseDemoLayout_Signals_Blazor_Ready_On_First_Render()
    {
        var ready = _ctx.JSInterop.SetupVoid("lumeo.signalBlazorReady");
        ready.SetVoidResult();

        _ctx.Render<EnterpriseDemoLayout>();

        Assert.Single(ready.Invocations);
    }

    [Fact]
    public void SaasDemoLayout_Signals_Blazor_Ready_On_First_Render()
    {
        var ready = _ctx.JSInterop.SetupVoid("lumeo.signalBlazorReady");
        ready.SetVoidResult();

        _ctx.Render<SaasDemoLayout>();

        Assert.Single(ready.Invocations);
    }
}
