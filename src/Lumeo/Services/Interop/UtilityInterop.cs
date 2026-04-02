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

    // --- Auto Resize ---

    public async ValueTask SetupAutoResize(IJSObjectReference module, string elementId, int maxRows)
    {
        await module.InvokeVoidAsync("setupAutoResize", elementId, maxRows);
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

    public async ValueTask<ComponentInteropService.TextareaCaretInfo> GetTextareaCaretPosition(
        IJSObjectReference module,
        string elementId)
    {
        return await module.InvokeAsync<ComponentInteropService.TextareaCaretInfo>("getTextareaCaretPosition", elementId);
    }

    public void Clear() => _otpPasteHandlers.Clear();
}
