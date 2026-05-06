using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.TagInput;

/// <summary>
/// Tests for the generic <c>TagInput&lt;TItem&gt;</c> API: custom item types,
/// templates, multi-separator splitting, validation, and MaxTags helper text.
/// </summary>
public class TagInputGenericTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TagInputGenericTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed record User(string Name, string Email);

    [Fact]
    public void String_TItem_works_without_GetTagText()
    {
        // Backward-compat: <TagInput TItem="string"> with the existing API still works.
        var cut = _ctx.Render<Lumeo.TagInput<string>>(p => p
            .Add(t => t.Tags, new List<string> { "alpha", "beta" }));

        Assert.Contains("alpha", cut.Markup);
        Assert.Contains("beta", cut.Markup);
        var spans = cut.FindAll("span.inline-flex");
        Assert.Equal(2, spans.Count);
    }

    [Fact]
    public void Custom_TItem_uses_GetTagText_for_display()
    {
        var users = new List<User>
        {
            new("Ada Lovelace", "ada@example.com"),
            new("Alan Turing",  "alan@example.com"),
        };

        var cut = _ctx.Render<Lumeo.TagInput<User>>(p => p
            .Add(t => t.Tags, users)
            .Add(t => t.GetTagText, u => u.Name));

        Assert.Contains("Ada Lovelace", cut.Markup);
        Assert.Contains("Alan Turing", cut.Markup);
        // ToString of the record (which would include "User { ... }") must NOT leak through
        Assert.DoesNotContain("ada@example.com", cut.Markup);
    }

    [Fact]
    public void TagTemplate_renders_custom_fragment_per_tag()
    {
        var users = new List<User>
        {
            new("Ada Lovelace", "ada@example.com"),
        };

        RenderFragment<User> tpl = u => builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "user-chip");
            builder.AddAttribute(2, "data-email", u.Email);
            builder.AddContent(3, u.Name);
            builder.CloseElement();
        };

        var cut = _ctx.Render<Lumeo.TagInput<User>>(p => p
            .Add(t => t.Tags, users)
            .Add(t => t.GetTagText, u => u.Name)
            .Add(t => t.TagTemplate, tpl));

        var chip = cut.Find("div.user-chip");
        Assert.NotNull(chip);
        Assert.Equal("ada@example.com", chip.GetAttribute("data-email"));
        Assert.Contains("Ada Lovelace", chip.TextContent);
    }

    [Fact]
    public async Task Multi_separator_splits_pasted_input()
    {
        var capturedTags = new List<string>();

        var cut = _ctx.Render<Lumeo.TagInput<string>>(p => p
            .Add(t => t.Tags, new List<string>())
            .Add(t => t.Separators, new[] { ",", ";", "\n", "\t" })
            .Add(t => t.TagsChanged,
                EventCallback.Factory.Create<List<string>>(this, list => capturedTags = list)));

        // Simulate a paste of "a,b;c\nd" via the input handler — Razor Bunit raises @oninput from Change
        var input = cut.Find("input[type='text']");
        await cut.InvokeAsync(() => input.Input("a,b;c\nd"));

        Assert.Equal(new[] { "a", "b", "c", "d" }, capturedTags.ToArray());
    }

    [Fact]
    public async Task ValidateInput_blocks_invalid_tag()
    {
        var capturedTags = new List<string> { /* starts empty */ };

        var cut = _ctx.Render<Lumeo.TagInput<string>>(p => p
            .Add(t => t.Tags, new List<string>())
            .Add(t => t.ValidateInput, (Func<string, string?>)(s =>
                s.Contains('@') ? null : "Must contain @"))
            .Add(t => t.TagsChanged,
                EventCallback.Factory.Create<List<string>>(this, list => capturedTags = list)));

        var input = cut.Find("input[type='text']");
        await cut.InvokeAsync(() => input.Input("notanemail"));
        await cut.InvokeAsync(() => input.KeyDown("Enter"));

        // No tag was added.
        Assert.Empty(capturedTags);

        // Inline error is rendered below the field.
        Assert.Contains("Must contain @", cut.Markup);

        // Now type a valid value — error clears, tag is added.
        await cut.InvokeAsync(() => input.Input("user@example.com"));
        await cut.InvokeAsync(() => input.KeyDown("Enter"));
        Assert.Single(capturedTags);
        Assert.Equal("user@example.com", capturedTags[0]);
    }

    [Fact]
    public void MaxTags_shows_helper_text()
    {
        var cut = _ctx.Render<Lumeo.TagInput<string>>(p => p
            .Add(t => t.Tags, new List<string> { "a", "b" })
            .Add(t => t.MaxTags, 2)
            .Add(t => t.MaxTagsHelperText, "You hit the limit."));

        Assert.Contains("You hit the limit.", cut.Markup);

        // Input must be disabled at max.
        var input = cut.Find("input[type='text']");
        Assert.True(input.HasAttribute("disabled"));
    }
}
