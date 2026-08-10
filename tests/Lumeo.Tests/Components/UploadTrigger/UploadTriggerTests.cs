using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.UploadTrigger;

public class UploadTriggerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public UploadTriggerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_A_Hidden_File_Input_Inside_A_Label()
    {
        var cut = _ctx.Render<L.UploadTrigger>();
        Assert.NotNull(cut.Find("label"));
        var input = cut.Find("input[type='file']");
        Assert.Contains("sr-only", input.GetAttribute("class"));
    }

    [Fact]
    public void Multiple_Is_Forwarded_To_The_Input()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p.Add(u => u.Multiple, true));
        Assert.True(cut.Find("input[type='file']").HasAttribute("multiple"));
    }

    [Fact]
    public void Accept_Filter_Is_Forwarded_To_The_Input()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p.Add(u => u.Accept, "image/*"));
        Assert.Equal("image/*", cut.Find("input[type='file']").GetAttribute("accept"));
    }

    [Fact]
    public void Disabled_Is_Forwarded_To_The_Input()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p.Add(u => u.Disabled, true));
        Assert.True(cut.Find("input[type='file']").HasAttribute("disabled"));
    }

    [Fact]
    public void Sm_Size_Renders_TextSm_Not_TextXs()
    {
        // Wave-0 fix (C3): unlike Button, UploadTrigger's LabelClass merges
        // BASE-first then SizeClass, so SizeClass's text-xs (the LAST font-size
        // utility in source order) used to win the conflict and render 12px
        // instead of the correct 14px. Deleting the stray text-xs from Sm's
        // SizeClass lets the base text-sm survive uncontested.
        var cut = _ctx.Render<L.UploadTrigger>(p => p.Add(u => u.Size, Lumeo.Button.ButtonSize.Sm));

        var cls = cut.Find("label").GetAttribute("class") ?? "";
        Assert.Contains("text-sm", cls);
        Assert.DoesNotContain("text-xs", cls);
    }

    [Fact]
    public void Xs_Size_Renders_Button_Xs_Parity_Classes()
    {
        // SizeClass's own comment says it "mirrors Button's variant + size class
        // composition ... pixel-for-pixel" and must be "kept in sync if Button's
        // mapping ever changes" — this is that sync for the new ButtonSize.Xs value.
        var cut = _ctx.Render<L.UploadTrigger>(p => p.Add(u => u.Size, Lumeo.Button.ButtonSize.Xs));

        var cls = cut.Find("label").GetAttribute("class") ?? "";
        Assert.Contains("h-6", cls);
        Assert.Contains("px-2", cls);
        Assert.Contains("gap-1", cls);
        Assert.Contains("text-xs", cls);
    }

    [Fact]
    public void Lg_Size_Padding_Is_Synced_With_Buttons_Px6()
    {
        // Sync check for the Wave-0b Button.Lg px-8 -> px-6 change (the delta the
        // repo owner spotted comparing Lumeo's docs to shadcn's side by side).
        var cut = _ctx.Render<L.UploadTrigger>(p => p.Add(u => u.Size, Lumeo.Button.ButtonSize.Lg));

        var cls = cut.Find("label").GetAttribute("class") ?? "";
        Assert.Contains("px-6", cls);
        Assert.DoesNotContain("px-8", cls);
    }

    [Fact]
    public void Outline_Variant_Is_Synced_With_Buttons_ShadowXs()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p.Add(u => u.Variant, Lumeo.Button.ButtonVariant.Outline));

        Assert.Contains("shadow-xs", cut.Find("label").GetAttribute("class"));
    }
}
