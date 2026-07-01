using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Input;

/// <summary>
/// An &lt;input type="file"&gt; must NOT receive a <c>value</c> attribute: in a real browser
/// Blazor throws InvalidStateError ("may only be programmatically set to the empty string")
/// the moment a file is picked and the two-way binding pushes the value back. Input has no
/// typed Type param, so <c>type="file"</c> arrives via the attribute splat; the value binding
/// must drop out for it. (Repro: the Input page's "File Input" demo crashed on file select.)
/// </summary>
public class InputFileTypeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public InputFileTypeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void File_Input_Omits_Value_Attribute_Even_When_Value_Is_Set()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, "C:\\fakepath\\leak.txt")
            .AddUnmatched("type", "file"));

        var input = cut.Find("input");
        Assert.Equal("file", input.GetAttribute("type"));
        Assert.Null(input.GetAttribute("value")); // omitted — never set on a file input
    }

    [Fact]
    public void Non_File_Input_Still_Renders_Its_Value()
    {
        var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Value, "hello"));
        Assert.Equal("hello", cut.Find("input").GetAttribute("value"));
    }
}
