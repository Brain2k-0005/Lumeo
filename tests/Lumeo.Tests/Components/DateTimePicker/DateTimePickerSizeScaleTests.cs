using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

/// <summary>
/// Field report #464 (carried), extended to DateTimePicker: it shares the DatePicker/
/// TimePicker/Select trigger shape (button + icon + text) but previously had NEITHER a
/// Size NOR a Density lever — its trigger was a hardcoded <c>h-8</c>. This pins the new
/// <c>Size</c> parameter's per-rung height/text/padding, the new cascading <c>Density</c>
/// inheritance (mirroring Input/DatePicker/TimePicker), and parity with SelectTrigger at
/// the one rung Select itself supports. See DatePickerSizeScaleTests.cs for the shared
/// height/padding ladder this reuses.
/// </summary>
public class DateTimePickerSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DateTimePickerSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertHasClass(string? cls, string token)
    {
        var tokens = (cls ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(token, tokens);
    }

    [Theory]
    [InlineData(L.Size.Xxs, L.Density.Comfortable, "h-6", "px-1.5")]
    [InlineData(L.Size.Xs, L.Density.Comfortable, "h-7", "px-2")]
    [InlineData(L.Size.Sm, L.Density.Comfortable, "h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]", "px-2.5")]
    [InlineData(L.Size.Md, L.Density.Comfortable, "h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]", "px-2.5")]
    [InlineData(L.Size.Lg, L.Density.Comfortable, "h-11", "px-4")]
    [InlineData(L.Size.Xl, L.Density.Comfortable, "h-[52px]", "px-5")]
    [InlineData(L.Size.Xxl, L.Density.Comfortable, "h-[60px]", "px-6")]
    [InlineData(L.Size.Xxs, L.Density.Compact, "h-5", "px-1")]
    [InlineData(L.Size.Md, L.Density.Compact, "h-7", "px-2")]
    [InlineData(L.Size.Lg, L.Density.Compact, "h-10", "px-3")]
    [InlineData(L.Size.Xxs, L.Density.Spacious, "h-7", "px-2")]
    [InlineData(L.Size.Md, L.Density.Spacious, "h-9", "px-3")]
    [InlineData(L.Size.Xxl, L.Density.Spacious, "h-16", "px-7")]
    public void Trigger_Height_And_Padding_Per_Rung(L.Size size, L.Density density, string h, string px)
    {
        var cut = _ctx.Render<L.DateTimePicker>(p => p.Add(d => d.Size, size).Add(d => d.Density, density));
        var cls = cut.Find("button").GetAttribute("class");
        AssertHasClass(cls, h);
        AssertHasClass(cls, px);
    }

    [Fact]
    public void Default_Size_Is_Md_And_Default_Density_Is_Comfortable()
    {
        // Previously a hardcoded h-8 regardless of any density/size — now derives from
        // the same (Size, Density) table as DatePicker/TimePicker/Select at their defaults.
        var cut = _ctx.Render<L.DateTimePicker>();
        var cls = cut.Find("button").GetAttribute("class");
        AssertHasClass(cls, "h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]");
        AssertHasClass(cls, "px-2.5");
        AssertHasClass(cls, "text-sm");
    }

    [Theory]
    [InlineData(L.Size.Xxs, "text-[8px]")]
    [InlineData(L.Size.Xs, "text-[10px]")]
    [InlineData(L.Size.Lg, "text-base")]
    [InlineData(L.Size.Xxl, "text-xl")]
    public void Trigger_Text_Size_Per_Rung(L.Size size, string textClass)
    {
        var cut = _ctx.Render<L.DateTimePicker>(p => p.Add(d => d.Size, size));
        AssertHasClass(cut.Find("button").GetAttribute("class"), textClass);
    }

    [Fact]
    public void Inherits_Density_From_Ambient_DensityScope()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DensityScope>(0);
            builder.AddAttribute(1, "Value", L.Density.Spacious);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.DateTimePicker>(0);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });
        var cls = cut.Find("button").GetAttribute("class");
        AssertHasClass(cls, "h-9");
        AssertHasClass(cls, "px-3");
    }

    [Fact]
    public void Explicit_Density_Overrides_Ambient_DensityScope()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DensityScope>(0);
            builder.AddAttribute(1, "Value", L.Density.Spacious);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.DateTimePicker>(0);
                inner.AddAttribute(1, "Density", L.Density.Compact);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });
        var cls = cut.Find("button").GetAttribute("class");
        AssertHasClass(cls, "h-7");
        AssertHasClass(cls, "px-2");
    }

    private static RenderFragment SelectChild => b =>
    {
        b.OpenComponent<L.SelectTrigger>(0);
        b.CloseComponent();
        b.OpenComponent<L.SelectContent>(1);
        b.CloseComponent();
    };

    [Theory]
    [InlineData(L.Density.Compact)]
    [InlineData(L.Density.Comfortable)]
    [InlineData(L.Density.Spacious)]
    public void Md_Rung_Trigger_Matches_SelectTrigger_Height_And_Padding(L.Density density)
    {
        var select = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DensityScope>(0);
            builder.AddAttribute(1, "Value", density);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.Select>(0);
                inner.AddAttribute(1, "ChildContent", SelectChild);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });
        var dtp = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DensityScope>(0);
            builder.AddAttribute(1, "Value", density);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.DateTimePicker>(0);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var selectTokens = (select.Find("button[role='combobox']").GetAttribute("class") ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hToken = Assert.Single(selectTokens, t => t.StartsWith("h-", StringComparison.Ordinal));
        var pxToken = Assert.Single(selectTokens, t => t.StartsWith("px-", StringComparison.Ordinal));

        var dtpCls = dtp.Find("button").GetAttribute("class");
        AssertHasClass(dtpCls, hToken);
        AssertHasClass(dtpCls, pxToken);
    }
}
