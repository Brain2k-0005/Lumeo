using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using TagInputCmp = global::Lumeo.TagInput<string>;

namespace Lumeo.Tests.Components.TagInput;

/// <summary>
/// Regression: Enter added a tag AND submitted an enclosing form. The input
/// now registers a native JS prevent-default rule for "Enter" (with
/// SkipComposing for IME safety) via RegisterPreventDefaultKeys.
/// </summary>
public class TagInputPreventDefaultTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        // Mirror ComponentInteropService's versioned module URL so VerifyInvoke
        // sees the actual import (same pattern as StrictInteropTests).
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        _module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        _module.Mode = JSRuntimeMode.Loose;
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Registers_Enter_PreventDefault_On_Input()
    {
        var cut = _ctx.Render<TagInputCmp>();
        var inputId = cut.Find("input[type='text']").GetAttribute("id");

        var invocation = _module.VerifyInvoke("registerPreventDefaultKeys");
        Assert.Equal(inputId, invocation.Arguments[0]);
        var rules = Assert.IsAssignableFrom<IReadOnlyList<PreventDefaultKeyRule>>(invocation.Arguments[1]);
        var rule = Assert.Single(rules);
        Assert.Equal("Enter", rule.Key);
        Assert.True(rule.SkipComposing); // IME safety
    }

    [Fact]
    public void Enter_Still_Adds_Tag()
    {
        List<string>? tags = null;
        var cut = _ctx.Render<TagInputCmp>(p => p
            .Add(t => t.TagsChanged, v => tags = v));

        var input = cut.Find("input[type='text']");
        input.Input("hello");
        input.KeyDown("Enter");

        Assert.NotNull(tags);
        Assert.Equal(new List<string> { "hello" }, tags);
    }
}
