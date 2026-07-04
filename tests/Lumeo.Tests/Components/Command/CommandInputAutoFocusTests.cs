using Bunit;
using Lumeo.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Command;

/// <summary>
/// Regression: CommandInput gains an opt-in <c>AutoFocus</c> parameter that focuses
/// the search input on first render via the existing focus interop (the HTML
/// autofocus attribute is a no-op in Blazor WASM). Default is false, matching cmdk's
/// bare Command.Input.
/// </summary>
public class CommandInputAutoFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public CommandInputAutoFocusTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderCommandInput(bool? autoFocus) =>
        _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                if (autoFocus is { } af) b.AddAttribute(1, "AutoFocus", af);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void AutoFocus_Focuses_Input_On_First_Render()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = RenderCommandInput(autoFocus: true);

        var inputId = cut.Find("input").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(inputId));
        cut.WaitForAssertion(() => Assert.Contains(inputId!, interop.FocusElementCalls));
    }

    [Fact]
    public void Default_Does_Not_AutoFocus_Input()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = RenderCommandInput(autoFocus: null); // default (false)

        var inputId = cut.Find("input").GetAttribute("id");
        // Only RegisterPreventDefaultKeys targets the input id; FocusElement never does.
        Assert.DoesNotContain(inputId!, interop.FocusElementCalls);
    }
}
