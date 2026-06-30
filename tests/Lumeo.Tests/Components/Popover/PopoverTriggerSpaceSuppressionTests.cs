using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Popover;

/// <summary>
/// Codex P3 — the Space-default suppression (battle-wave2 #88) on the fallback
/// role=button trigger must register against the id that ACTUALLY renders, not the
/// internal fallback id. A consumer-splatted id (via AdditionalAttributes) renders
/// AFTER the explicit id= and wins in the DOM, so registering against the raw
/// fallback id targets an element no longer in the document — Space silently stops
/// being suppressed (the popover toggles AND the page scrolls). Mirrors
/// DialogTrigger/SheetTrigger's EffectiveId pattern.
/// </summary>
public class PopoverTriggerSpaceSuppressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public PopoverTriggerSpaceSuppressionTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderTrigger(string? consumerId = null)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Popover>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PopoverTrigger>(0);
                if (consumerId is not null)
                    b.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object> { ["id"] = consumerId });
                b.AddAttribute(2, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Open")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Consumer_Splatted_Id_Wins_In_The_Dom()
    {
        var cut = RenderTrigger(consumerId: "my-trigger");
        Assert.Equal("my-trigger", cut.Find("[role='button']").GetAttribute("id"));
    }

    [Fact]
    public void Space_Suppression_Registers_Against_The_Consumer_Splatted_Id()
    {
        // Without the fix this registers the internal fallback id, which no element in the DOM
        // carries (the consumer's "my-trigger" rendered instead) — the suppression is inert.
        var cut = RenderTrigger(consumerId: "my-trigger");
        cut.WaitForAssertion(() => Assert.Contains("my-trigger", _interop.RegisterPreventDefaultKeysElementIds));
    }

    [Fact]
    public void Without_A_Consumer_Id_Suppression_Registers_Against_The_Rendered_Fallback_Id()
    {
        var cut = RenderTrigger();
        var renderedId = cut.Find("[role='button']").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(renderedId));
        cut.WaitForAssertion(() => Assert.Contains(renderedId, _interop.RegisterPreventDefaultKeysElementIds));
    }
}
