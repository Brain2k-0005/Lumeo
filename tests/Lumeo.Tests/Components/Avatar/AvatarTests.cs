using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Avatar;

public class AvatarTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AvatarTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Avatar tests

    [Fact]
    public void Avatar_Renders_Div_With_Base_Classes()
    {
        var cut = _ctx.Render<L.Avatar>();

        var div = cut.Find("div");
        var cls = div.GetAttribute("class");
        Assert.Contains("relative", cls);
        Assert.Contains("flex", cls);
        Assert.Contains("shrink-0", cls);
        // overflow-hidden and rounded-full are on the inner div
        var inner = cut.FindAll("div")[1];
        var innerCls = inner.GetAttribute("class");
        Assert.Contains("overflow-hidden", innerCls);
        Assert.Contains("rounded-full", innerCls);
    }

    [Fact]
    public void Avatar_Renders_Default_Size()
    {
        var cut = _ctx.Render<L.Avatar>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("h-10", cls);
        Assert.Contains("w-10", cls);
    }

    [Theory]
    [InlineData(L.Avatar.AvatarSize.Sm, "h-8", "w-8")]
    [InlineData(L.Avatar.AvatarSize.Default, "h-10", "w-10")]
    [InlineData(L.Avatar.AvatarSize.Lg, "h-12", "w-12")]
    public void Avatar_Renders_Correct_Size(L.Avatar.AvatarSize size, string expectedH, string expectedW)
    {
        var cut = _ctx.Render<L.Avatar>(p => p
            .Add(a => a.Size, size));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains(expectedH, cls);
        Assert.Contains(expectedW, cls);
    }

    [Fact]
    public void Avatar_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.Avatar>(p => p
            .Add(a => a.Class, "my-avatar-class"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-avatar-class", cls);
        // rounded-full is on the inner div
        var inner = cut.FindAll("div")[1];
        Assert.Contains("rounded-full", inner.GetAttribute("class"));
    }

    [Fact]
    public void Avatar_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.Avatar>(p => p
            .Add(a => a.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-avatar"
            }));

        Assert.Equal("my-avatar", cut.Find("div").GetAttribute("data-testid"));
    }

    [Fact]
    public void Avatar_Renders_Child_Content()
    {
        var cut = _ctx.Render<L.Avatar>(p => p
            .AddChildContent("<span>JD</span>"));

        Assert.Contains("JD", cut.Markup);
    }

    // AvatarImage tests

    [Fact]
    public void AvatarImage_Renders_Img_Element()
    {
        var cut = _ctx.Render<L.AvatarImage>(p => p
            .Add(a => a.Src, "/avatar.png")
            .Add(a => a.Alt, "User avatar"));

        Assert.NotNull(cut.Find("img"));
    }

    [Fact]
    public void AvatarImage_Sets_Src_And_Alt()
    {
        var cut = _ctx.Render<L.AvatarImage>(p => p
            .Add(a => a.Src, "/avatar.png")
            .Add(a => a.Alt, "User avatar"));

        var img = cut.Find("img");
        Assert.Equal("/avatar.png", img.GetAttribute("src"));
        Assert.Equal("User avatar", img.GetAttribute("alt"));
    }

    [Fact]
    public void AvatarImage_Renders_With_Base_Classes()
    {
        var cut = _ctx.Render<L.AvatarImage>(p => p
            .Add(a => a.Src, "/avatar.png"));

        var cls = cut.Find("img").GetAttribute("class");
        Assert.Contains("aspect-square", cls);
        Assert.Contains("h-full", cls);
        Assert.Contains("w-full", cls);
        Assert.Contains("object-cover", cls);
    }

    [Fact]
    public void AvatarImage_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.AvatarImage>(p => p
            .Add(a => a.Src, "/avatar.png")
            .Add(a => a.Class, "my-image-class"));

        var cls = cut.Find("img").GetAttribute("class");
        Assert.Contains("my-image-class", cls);
        Assert.Contains("aspect-square", cls);
    }

    [Fact]
    public void AvatarImage_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.AvatarImage>(p => p
            .Add(a => a.Src, "/avatar.png")
            .Add(a => a.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "avatar-img"
            }));

        Assert.Equal("avatar-img", cut.Find("img").GetAttribute("data-testid"));
    }

    [Fact]
    public void AvatarImage_OnError_Fires()
    {
        var errorFired = false;
        var cut = _ctx.Render<L.AvatarImage>(p => p
            .Add(a => a.Src, "/avatar.png")
            .Add(a => a.OnError, Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, () => errorFired = true)));

        cut.Find("img").TriggerEvent("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());
        Assert.True(errorFired);
    }

    // AvatarFallback tests

    [Fact]
    public void AvatarFallback_Renders_Div_With_Base_Classes()
    {
        var cut = _ctx.Render<L.AvatarFallback>(p => p
            .AddChildContent("JD"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("h-full", cls);
        Assert.Contains("w-full", cls);
        Assert.Contains("items-center", cls);
        Assert.Contains("justify-center", cls);
        Assert.Contains("rounded-full", cls);
        Assert.Contains("bg-muted", cls);
    }

    [Fact]
    public void AvatarFallback_Renders_Child_Content()
    {
        var cut = _ctx.Render<L.AvatarFallback>(p => p
            .AddChildContent("JD"));

        Assert.Contains("JD", cut.Find("div").TextContent);
    }

    [Fact]
    public void AvatarFallback_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.AvatarFallback>(p => p
            .Add(a => a.Class, "my-fallback-class")
            .AddChildContent("AB"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-fallback-class", cls);
        Assert.Contains("rounded-full", cls);
    }

    [Fact]
    public void AvatarFallback_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.AvatarFallback>(p => p
            .Add(a => a.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "avatar-fallback"
            })
            .AddChildContent("AB"));

        Assert.Equal("avatar-fallback", cut.Find("div").GetAttribute("data-testid"));
    }

    // --- Shape: Square ---

    [Fact]
    public void Shape_Square_Applies_Rounded_Md_To_Inner_Div()
    {
        var cut = _ctx.Render<L.Avatar>(p => p
            .Add(a => a.Shape, L.Avatar.AvatarShape.Square));

        var inner = cut.FindAll("div")[1];
        Assert.Contains("rounded-md", inner.GetAttribute("class"));
    }

    [Fact]
    public void Shape_Circle_Default_Applies_Rounded_Full_To_Inner_Div()
    {
        var cut = _ctx.Render<L.Avatar>();

        var inner = cut.FindAll("div")[1];
        Assert.Contains("rounded-full", inner.GetAttribute("class"));
    }

    // --- Status ---

    [Fact]
    public void Status_Online_Renders_Green_Status_Dot()
    {
        var cut = _ctx.Render<L.Avatar>(p => p
            .Add(a => a.Status, L.Avatar.AvatarStatus.Online));

        var statusSpan = cut.FindAll("span").FirstOrDefault(s =>
            (s.GetAttribute("class") ?? "").Contains("bg-success"));
        Assert.NotNull(statusSpan);
    }

    [Fact]
    public void Status_None_Does_Not_Render_Status_Dot()
    {
        var cut = _ctx.Render<L.Avatar>(p => p
            .Add(a => a.Status, L.Avatar.AvatarStatus.None));

        // No status span should be present
        var statusSpans = cut.FindAll("span").Where(s =>
            (s.GetAttribute("class") ?? "").Contains("rounded-full") &&
            (s.GetAttribute("class") ?? "").Contains("ring-2")).ToList();
        Assert.Empty(statusSpans);
    }
}
