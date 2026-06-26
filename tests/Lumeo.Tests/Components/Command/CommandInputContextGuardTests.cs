using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Command;

// Regression for battle-wave2 #173 (edge-data, low): CommandInput dereferenced a
// non-null `= default!` Context with no null guard, so rendering it OUTSIDE a
// Command (or before the cascade resolves) threw NullReferenceException — unlike
// every other sub-part (CommandList/CommandItem/CommandGroup/CommandEmpty) which
// treats Context as nullable and guards usage. The fix makes Context nullable and
// short-circuits the markup with `@if (Context is not null)`.
public class CommandInputContextGuardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CommandInputContextGuardTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Edge input: render CommandInput with no cascading Command.CommandContext
    // present (the input is misplaced outside a Command). Pre-fix this threw an
    // NRE on Context.ListId during render; post-fix it must render without
    // throwing — mirroring CommandEmpty_Renders_Default_Message which already
    // renders a sub-part standalone.
    [Fact]
    public void CommandInput_Without_Context_Does_Not_Throw()
    {
        var ex = Record.Exception(() => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.CommandInput>(0);
            builder.AddAttribute(1, "Placeholder", "Search commands...");
            builder.CloseComponent();
        }));

        Assert.Null(ex);
    }

    // With no Context, the guarded markup short-circuits: no <input> is rendered
    // (and crucially, no crash) instead of dereferencing a null Context.
    [Fact]
    public void CommandInput_Without_Context_Renders_No_Input()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.CommandInput>(0);
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll("input"));
    }

    // The normal path is unchanged: inside a Command the cascade resolves, so the
    // input renders and is wired to the context (aria-controls present).
    [Fact]
    public void CommandInput_Inside_Command_Still_Renders_Input()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                b.AddAttribute(1, "Placeholder", "Search commands...");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var input = cut.Find("input[type='text']");
        Assert.Equal("Search commands...", input.GetAttribute("placeholder"));
        Assert.False(string.IsNullOrEmpty(input.GetAttribute("aria-controls")));
    }
}
