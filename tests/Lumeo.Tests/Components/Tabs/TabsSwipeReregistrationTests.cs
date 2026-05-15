using Bunit;
using Lumeo.Services;
using Lumeo.Services.Localization;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tabs;

/// <summary>
/// Regression tests for Bug A: after a swipe fires and the active tab changes,
/// returning to the original tab must re-attach JS pointer listeners so that
/// subsequent swipes still work.
///
/// Strategy: render two TabsContent panels with SwipeEnabled=true, click the
/// second tab trigger (panel "one" removed from DOM), click back to "one"
/// (panel "one" recreated in DOM) and assert RegisterTabSwipe was called twice.
/// </summary>
public class TabsSwipeReregistrationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _trackingInterop = new();

    public TabsSwipeReregistrationTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo/js/components.js");
        module.Mode = JSRuntimeMode.Loose;

        // Wire TrackingInteropService so we can observe RegisterTabSwipe calls.
        _ctx.Services.AddSingleton<IComponentInteropService>(_trackingInterop);
        _ctx.Services.AddScoped<ComponentInteropService>();
        _ctx.Services.AddScoped<ToastService>();
        _ctx.Services.AddScoped<IToastService>(sp => sp.GetRequiredService<ToastService>());
        _ctx.Services.AddScoped<OverlayService>();
        _ctx.Services.AddScoped<IOverlayService>(sp => sp.GetRequiredService<OverlayService>());
        _ctx.Services.AddScoped<ThemeService>();
        _ctx.Services.AddScoped<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
        _ctx.Services.AddScoped<KeyboardShortcutService>();
        _ctx.Services.AddScoped<IKeyboardShortcutService>(sp => sp.GetRequiredService<KeyboardShortcutService>());
        _ctx.Services.AddScoped<IDataGridExportService, Lumeo.Services.DataGridExportService>();
        _ctx.Services.AddScoped<HapticsService>();
        _ctx.Services.AddSingleton<IOptions<LumeoLocalizationOptions>>(_ =>
        {
            var options = new LumeoLocalizationOptions();
            LumeoDefaultStrings.ApplyDefaults(options);
            return Options.Create(options);
        });
        _ctx.Services.AddScoped<ILumeoLocalizer, LumeoLocalizer>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderTabsWithTriggers()
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tabs>(0);
            builder.AddAttribute(1, "ActiveValue", "one");
            builder.AddAttribute(2, "SwipeEnabled", true);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TabsList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.TabsTrigger>(0);
                    inner.AddAttribute(1, "Value", "one");
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(t => t.AddContent(0, "One")));
                    inner.CloseComponent();

                    inner.OpenComponent<L.TabsTrigger>(2);
                    inner.AddAttribute(3, "Value", "two");
                    inner.AddAttribute(4, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Two")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();

                b.OpenComponent<L.TabsContent>(2);
                b.AddAttribute(3, "Value", "one");
                b.AddAttribute(4, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Panel One")));
                b.CloseComponent();

                b.OpenComponent<L.TabsContent>(5);
                b.AddAttribute(6, "Value", "two");
                b.AddAttribute(7, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Panel Two")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void TabsContent_SwipeReregisters_AfterReturningToActiveTab()
    {
        // Render with panel "one" active — RegisterTabSwipe fires once (first render).
        var cut = RenderTabsWithTriggers();
        cut.WaitForAssertion(() => Assert.Equal(1, _trackingInterop.RegisterTabSwipeCallCount));

        // Click the second trigger — panel "one" leaves the DOM (Active render mode).
        // UnregisterTabSwipe should fire for panel "one"; RegisterTabSwipe for "two".
        var triggers = cut.FindAll("[role='tab']");
        triggers[1].Click();

        cut.WaitForAssertion(() => Assert.Equal(1, _trackingInterop.UnregisterTabSwipeCallCount));
        // "two" is now active — its RegisterTabSwipe fires.
        cut.WaitForAssertion(() => Assert.Equal(2, _trackingInterop.RegisterTabSwipeCallCount));

        // Click back to the first trigger — panel "one" returns to the DOM.
        // RegisterTabSwipe must fire AGAIN for panel "one" (third total call).
        triggers = cut.FindAll("[role='tab']");
        triggers[0].Click();

        cut.WaitForAssertion(() => Assert.Equal(3, _trackingInterop.RegisterTabSwipeCallCount));
    }
}
