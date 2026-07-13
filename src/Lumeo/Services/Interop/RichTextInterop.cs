using Microsoft.JSInterop;

namespace Lumeo.Services.Interop;

/// <summary>
/// JS-isolation adapter for the RichTextEditor module (rich-text-editor.js).
/// The module is dynamically imported on first use so the ~100 kB of TipTap
/// code is never pulled down by apps that don't use the RichTextEditor.
/// </summary>
internal sealed class RichTextInterop
{
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> GetModuleAsync(IJSRuntime js)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Lumeo.Editor/js/rich-text-editor.js");
        return _module;
    }

    public async ValueTask<string> InitAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(
        IJSRuntime js,
        Microsoft.AspNetCore.Components.ElementReference elementRef,
        DotNetObjectReference<T> dotNetRef,
        object options)
        where T : class
    {
        try
        {
            var module = await GetModuleAsync(js);
            return await module.InvokeAsync<string>("rte.init", elementRef, dotNetRef, options);
        }
        catch (JSDisconnectedException)
        {
            return string.Empty;
        }
    }

    public async ValueTask SetContentAsync(IJSRuntime js, string id, string? html)
    {
        try
        {
            var module = await GetModuleAsync(js);
            await module.InvokeVoidAsync("rte.setContent", id, html ?? string.Empty);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask CommandAsync(IJSRuntime js, string id, string name, object?[]? args)
    {
        try
        {
            var module = await GetModuleAsync(js);
            // InvokeVoidAsync(string, params object?[]) — we want the js function
            // invoked with (id, name, ...args), so the array we pass IS that arg list.
            var payload = new object?[] { id, name };
            if (args is not null && args.Length > 0)
            {
                payload = payload.Concat(args).ToArray();
            }
            await module.InvokeVoidAsync("rte.command", args: payload);
        }
        catch (JSDisconnectedException) { }
    }

    // Trim safety: the deserializer constructs RichTextActiveState purely via reflection, which the
    // linker cannot see — without this the parameterless ctor/property setters get
    // trimmed and JSRuntime throws ConstructorContainsNullParameterNames at runtime.
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(RichTextActiveState))]
    public async ValueTask<RichTextActiveState?> GetActiveAsync(IJSRuntime js, string id)
    {
        try
        {
            var module = await GetModuleAsync(js);
            return await module.InvokeAsync<RichTextActiveState?>("rte.getActive", id);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    public async ValueTask SetDisabledAsync(IJSRuntime js, string id, bool disabled)
    {
        try
        {
            var module = await GetModuleAsync(js);
            await module.InvokeVoidAsync("rte.setDisabled", id, disabled);
        }
        catch (JSDisconnectedException) { }
    }

    /// <summary>
    /// Pushes updated <c>aria-invalid</c> and <c>aria-describedby</c> onto the editor's
    /// live contenteditable element. TipTap stamps these once at construction via
    /// <c>editorProps.attributes</c> and does not react to later changes, so explicit
    /// setAttribute calls are required when validation state changes post-mount.
    /// </summary>
    public async ValueTask SetAriaAttributesAsync(IJSRuntime js, string id, bool ariaInvalid, string? ariaDescribedBy)
    {
        try
        {
            var module = await GetModuleAsync(js);
            await module.InvokeVoidAsync("rte.setAriaAttributes", id, ariaInvalid, ariaDescribedBy);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask DestroyAsync(IJSRuntime js, string id)
    {
        try
        {
            var module = await GetModuleAsync(js);
            await module.InvokeVoidAsync("rte.destroy", id);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask<string?> PromptLinkAsync(IJSRuntime js, string? initial)
    {
        try
        {
            var module = await GetModuleAsync(js);
            return await module.InvokeAsync<string?>("rte.promptLink", initial);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
            _module = null;
        }
    }
}

/// <summary>
/// Snapshot of which TipTap marks/nodes are active at the current selection —
/// used by the RichTextEditor toolbar to render active states.
/// </summary>
public sealed record RichTextActiveState(
    bool Bold,
    bool Italic,
    bool Underline,
    bool Strike,
    bool Code,
    bool Paragraph,
    bool Heading1,
    bool Heading2,
    bool Heading3,
    bool BulletList,
    bool OrderedList,
    bool Blockquote,
    bool CodeBlock,
    bool Link,
    bool CanUndo,
    bool CanRedo)
{
    // Trim safety: JSRuntime's reflection-based serializer must never bind the positional
    // ctor — the trimmer strips its parameter names ("ConstructorContainsNullParameterNames",
    // crashes the component under a trimmed publish). With this parameterless ctor STJ
    // uses property-based (de)serialization instead. Do not remove.
    public RichTextActiveState() : this(false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false) { }
}
