using Microsoft.JSInterop;
using Lumeo.Services;

namespace Lumeo.Services.Interop;

internal sealed class UtilityInterop
{
    private readonly Dictionary<string, Func<string, Task>> _otpPasteHandlers = new();

    // --- OTP Paste ---

    public async ValueTask RegisterOtpPaste(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string baseId,
        int length,
        Func<string, Task> handler)
    {
        _otpPasteHandlers[baseId] = handler;
        await module.InvokeVoidAsync("registerOtpPaste", baseId, length, selfRef);
    }

    public async ValueTask UnregisterOtpPaste(IJSObjectReference module, string baseId, int length)
    {
        _otpPasteHandlers.Remove(baseId);
        await module.InvokeVoidAsync("unregisterOtpPaste", baseId, length);
    }

    public async Task OnOtpPaste(string baseId, string digits)
    {
        if (_otpPasteHandlers.TryGetValue(baseId, out var handler))
        {
            await handler(digits);
        }
    }

    // --- Selective keydown preventDefault ---

    public async ValueTask RegisterPreventDefaultKeys(
        IJSObjectReference module,
        string elementId,
        IReadOnlyList<PreventDefaultKeyRule> rules)
    {
        // Trim safety: serialize the rules as dictionaries, not as the record type.
        // Under a trimmed publish the linker BOTH strips constructor parameter names
        // (WASM metadata trimming — even on kept ctors) AND removes the record's
        // unused parameterless ctor (reflection use is invisible to the linker), so
        // JSRuntime's reflection serializer throws ConstructorContainsNullParameterNames
        // at runtime — this crashed nearly every docs page live (keyboard scroll
        // suppression runs on ~60 components). Dictionaries serialize to the identical
        // JSON array-of-objects; the JS side is unchanged.
        var payload = new List<Dictionary<string, object?>>(rules.Count);
        foreach (var r in rules)
        {
            payload.Add(new Dictionary<string, object?>
            {
                ["key"] = r.Key,
                ["requireNoModifiers"] = r.RequireNoModifiers,
                ["skipComposing"] = r.SkipComposing,
                ["skipEditable"] = r.SkipEditable,
            });
        }
        await module.InvokeVoidAsync("registerPreventDefaultKeys", elementId, payload);
    }

    public async ValueTask UnregisterPreventDefaultKeys(IJSObjectReference module, string elementId)
    {
        await module.InvokeVoidAsync("unregisterPreventDefaultKeys", elementId);
    }

    // --- Auto Resize ---

    public async ValueTask SetupAutoResize(IJSObjectReference module, string elementId, int maxRows)
    {
        await module.InvokeVoidAsync("setupAutoResize", elementId, maxRows);
    }

    public async ValueTask UnregisterAutoResize(IJSObjectReference module, string elementId)
    {
        await module.InvokeVoidAsync("unregisterAutoResize", elementId);
    }

    // --- File Download ---

    public async ValueTask DownloadFile(
        IJSObjectReference module,
        string fileName,
        string contentBase64,
        string mimeType = "application/octet-stream")
    {
        await module.InvokeVoidAsync("downloadFile", fileName, contentBase64, mimeType);
    }

    // --- Clipboard ---

    public async ValueTask CopyToClipboard(IJSObjectReference module, string text)
    {
        await module.InvokeVoidAsync("copyToClipboard", text);
    }

    // --- LocalStorage ---

    public async ValueTask SaveToLocalStorage(IJSObjectReference module, string key, string value)
    {
        await module.InvokeVoidAsync("saveToLocalStorage", key, value);
    }

    public async ValueTask<string?> LoadFromLocalStorage(IJSObjectReference module, string key)
    {
        return await module.InvokeAsync<string?>("loadFromLocalStorage", key);
    }

    public async ValueTask RemoveFromLocalStorage(IJSObjectReference module, string key)
    {
        await module.InvokeVoidAsync("removeFromLocalStorage", key);
    }

    // --- Mention: Textarea Caret Position ---

    // Trim safety: the deserializer constructs TextareaCaretInfo purely via reflection, which the
    // linker cannot see — without this the parameterless ctor/property setters get
    // trimmed and JSRuntime throws ConstructorContainsNullParameterNames at runtime.
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(ComponentInteropService.TextareaCaretInfo))]
    public async ValueTask<ComponentInteropService.TextareaCaretInfo> GetTextareaCaretPosition(
        IJSObjectReference module,
        string elementId)
    {
        return await module.InvokeAsync<ComponentInteropService.TextareaCaretInfo>("getTextareaCaretPosition", elementId);
    }

    public void Clear() => _otpPasteHandlers.Clear();
}
