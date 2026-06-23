using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Combobox;

/// <summary>
/// B9 — the Combobox input must suppress the browser default for the listbox
/// navigation keys, so ArrowUp/Down move the focused option (not the text caret)
/// and a plain Enter selects the active option instead of submitting a form.
/// Same registration set as Command, registered ON the editable input itself.
/// </summary>
public class ComboboxKeyboardPreventDefaultTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ComboboxKeyboardPreventDefaultTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> Render()
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Items", new object[] { "apple", "banana" });
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();
                b.OpenComponent<L.ComboboxContent>(2);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Input_Registers_PreventDefault_For_Arrow_And_Enter_Keys()
    {
        var cut = Render();
        var inputId = cut.Find("input").GetAttribute("id");

        var reg = Assert.Single(_ctx.JSInterop.Invocations,
            i => i.Identifier == "registerPreventDefaultKeys" && (i.Arguments[0] as string) == inputId);

        var rules = (IReadOnlyList<Lumeo.Services.PreventDefaultKeyRule>)reg.Arguments[1]!;
        var keys = rules.Select(r => r.Key).ToList();
        Assert.Contains("ArrowDown", keys);
        Assert.Contains("ArrowUp", keys);
        Assert.Contains("Enter", keys);
        // Must NOT skip editable targets — the keydown fires on the input itself.
        Assert.All(rules, r => Assert.False(r.SkipEditable));
    }
}
