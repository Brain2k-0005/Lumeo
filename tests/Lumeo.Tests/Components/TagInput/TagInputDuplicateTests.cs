using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using TagInputCmp = global::Lumeo.TagInput<string>;

namespace Lumeo.Tests.Components.TagInput;

/// <summary>
/// Battle-test wave 2, finding #7 (high): with <c>AllowDuplicates=true</c> two
/// equal tags used to share an identical <c>@key="tag"</c>, which throws at
/// render time, and <c>RemoveTag</c> used a value-equality <c>Where</c> that
/// stripped EVERY equal tag instead of the one whose X was clicked. The fix keys
/// the loop by <c>(tag, index)</c> and removes a single tag by position.
/// </summary>
public class TagInputDuplicateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TagInputDuplicateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Duplicate_tags_render_without_throwing_on_duplicate_keys()
    {
        // Two equal values with AllowDuplicates=true. Keying the loop by the value
        // alone produced a duplicate @key and threw "Duplicate @key" during the
        // render diff; keying by (tag, index) gives each chip a distinct key.
        var cut = _ctx.Render<TagInputCmp>(p => p
            .Add(t => t.AllowDuplicates, true)
            .Add(t => t.Tags, new List<string> { "alpha", "alpha", "beta" }));

        var spans = cut.FindAll("span.inline-flex");
        Assert.Equal(3, spans.Count);
    }

    [Fact]
    public void Re_render_after_adding_a_duplicate_tag_does_not_throw()
    {
        // Reproduce the exact state sequence: render -> change the Tags param to a
        // list that now contains a duplicate -> assert the duplicate survived and
        // the re-render (diff against the prior keyed list) did not throw.
        var cut = _ctx.Render<TagInputCmp>(p => p
            .Add(t => t.AllowDuplicates, true)
            .Add(t => t.Tags, new List<string> { "alpha" }));

        Assert.Single(cut.FindAll("span.inline-flex"));

        cut.Render(p => p
            .Add(t => t.AllowDuplicates, true)
            .Add(t => t.Tags, new List<string> { "alpha", "alpha" }));

        Assert.Equal(2, cut.FindAll("span.inline-flex").Count);
    }

    [Fact]
    public void Removing_one_duplicate_chip_removes_only_that_occurrence()
    {
        List<string>? captured = null;
        var cut = _ctx.Render<TagInputCmp>(p => p
            .Add(t => t.AllowDuplicates, true)
            .Add(t => t.Tags, new List<string> { "alpha", "alpha", "beta" })
            .Add(t => t.TagsChanged,
                EventCallback.Factory.Create<List<string>>(this, list => captured = list)));

        // Click the first chip's remove button. With the value-equality Where the
        // old code removed BOTH "alpha" tags; the position-based RemoveTagAt removes
        // exactly one occurrence, leaving "alpha" + "beta". The catch mirrors the
        // Combobox tests — the click can trigger interop the test renderer lacks,
        // but TagsChanged has already fired synchronously by then.
        var removeButtons = cut.FindAll("button[aria-label^='Remove ']");
        try { removeButtons[0].Click(); } catch (ArgumentException) { }

        Assert.NotNull(captured);
        Assert.Equal(new List<string> { "alpha", "beta" }, captured);
    }

    [Fact]
    public void Removing_a_middle_duplicate_keeps_the_other_occurrences()
    {
        List<string>? captured = null;
        var cut = _ctx.Render<TagInputCmp>(p => p
            .Add(t => t.AllowDuplicates, true)
            .Add(t => t.Tags, new List<string> { "x", "x", "x" })
            .Add(t => t.TagsChanged,
                EventCallback.Factory.Create<List<string>>(this, list => captured = list)));

        // Remove the middle chip; two equal "x" tags must remain.
        var removeButtons = cut.FindAll("button[aria-label^='Remove ']");
        try { removeButtons[1].Click(); } catch (ArgumentException) { }

        Assert.NotNull(captured);
        Assert.Equal(new List<string> { "x", "x" }, captured);
    }
}
