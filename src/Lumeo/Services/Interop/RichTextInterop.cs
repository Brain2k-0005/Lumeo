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
            "import", "./_content/Lumeo/js/rich-text-editor.js");
        return _module;
    }

    public async ValueTask<string> InitAsync<T>(
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
    bool CanRedo);
