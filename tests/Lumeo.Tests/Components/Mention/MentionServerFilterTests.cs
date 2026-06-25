using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Mention;

/// <summary>
/// Battle-test #32 (state-on-data-change): when OnSearch is supplied (server-driven
/// mode) the parent returns the already-filtered Options for the search term. The
/// component used to re-apply its own client-side substring filter on top, silently
/// dropping any server-supplied option whose Label/Value did not literally contain
/// the typed _searchTerm (e.g. fuzzy / remote matches). The fix bypasses the client
/// filter whenever OnSearch.HasDelegate so server results render verbatim.
/// </summary>
public class MentionServerFilterTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MentionServerFilterTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        // The dropdown opens only when GetTextareaCaretPosition returns non-null,
        // so stub the versioned components.js module to return a caret record.
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        var module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        module.Mode = JSRuntimeMode.Loose;
        // Caret at selectionStart=4 (after typing "@xyz") with a small offset, so
        // _searchTerm becomes "xyz" — a term none of the server options contain.
        module.Setup<ComponentInteropService.TextareaCaretInfo>("getTextareaCaretPosition", _ => true)
            .SetResult(new ComponentInteropService.TextareaCaretInfo(0, 0, 4, OffsetTop: 4, OffsetLeft: 6, LineHeight: 18));
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Server-returned options whose Label/Value do NOT contain the typed term "xyz"
    // (simulating a fuzzy / remote match the server decided was relevant).
    private static List<L.Mention.MentionOption> ServerResults() =>
    [
        new("Alice", "alice"),
        new("Bob", "bob"),
    ];

    [Fact]
    public void Server_Supplied_Options_Are_Not_Re_Filtered_By_SearchTerm()
    {
        var cut = _ctx.Render<L.Mention>(p =>
        {
            p.Add(c => c.Options, ServerResults());
            // Supplying OnSearch puts the component in server-driven mode.
            p.Add(c => c.OnSearch, EventCallback.Factory.Create<string>(this, _ => { }));
        });

        // Type a trigger + a term that none of the server options literally contain.
        // HandleInput sets _searchTerm = "xyz" and opens the dropdown.
        cut.Find("textarea").Input("@xyz");

        // Before the fix the client substring filter dropped every option (none
        // contains "xyz"), so the dropdown rendered zero options (or didn't render).
        // After the fix the server-supplied Options render verbatim.
        var options = cut.FindAll("[role='option']");
        Assert.Equal(2, options.Count);
        Assert.Equal("true", cut.Find("textarea").GetAttribute("aria-expanded"));
    }
}
