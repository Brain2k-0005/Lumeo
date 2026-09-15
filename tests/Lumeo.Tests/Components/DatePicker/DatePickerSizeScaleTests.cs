using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// Field report #464 (carried): DatePicker/DateRangePicker had no Size lever at all —
/// the parameter list was character-identical between 4.3.3, 5.0.0 and 5.9.1. This pins
/// the new <c>Size</c> parameter's per-rung height/text/padding on the trigger, its
/// cascading <c>Density</c> inheritance (mirroring <see cref="L.Input"/>), and parity
/// with <see cref="L.SelectTrigger"/> at the one rung Select itself currently supports
/// (it has no Size lever of its own yet — see DatePickerSelectParityTests's own remarks).
///
/// Height/padding-per-rung values are the SAME ladder pinned for Input in
/// InputSizeScaleTests.cs (h-5/px-1 .. h-[60px]/px-6) — reused deliberately rather than
/// inventing a new table.
/// </summary>
public class DatePickerSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DatePickerSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertHasClass(string? cls, string token)
    {
        var tokens = (cls ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(token, tokens);
    }

    // AllowKeyboardInput=false forces the plain <button> trigger (Single mode, no
    // custom TriggerContent still renders the typeable <input> by default — the button
    // path is simplest for pinning CssClass directly without also touching the input's
    // own class computation, which is covered separately below).
    private IRenderedComponent<L.DatePicker> RenderButtonTrigger(L.Size size, L.Density? density = null)
        => _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(d => d.AllowKeyboardInput, false);
            p.Add(d => d.Size, size);
            if (density.HasValue) p.Add(d => d.Density, density.Value);
        });

    // ============================== Height + padding per rung ==============================

    [Theory]
    // Comfortable (default density)
    [InlineData(L.Size.Xxs, L.Density.Comfortable, "h-6", "px-1.5")]
    [InlineData(L.Size.Xs, L.Density.Comfortable, "h-7", "px-2")]
    [InlineData(L.Size.Sm, L.Density.Comfortable, "h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]", "px-2.5")]
    [InlineData(L.Size.Md, L.Density.Comfortable, "h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]", "px-2.5")]
    [InlineData(L.Size.Lg, L.Density.Comfortable, "h-11", "px-4")]
    [InlineData(L.Size.Xl, L.Density.Comfortable, "h-[52px]", "px-5")]
    [InlineData(L.Size.Xxl, L.Density.Comfortable, "h-[60px]", "px-6")]
    // Compact
    [InlineData(L.Size.Xxs, L.Density.Compact, "h-5", "px-1")]
    [InlineData(L.Size.Xs, L.Density.Compact, "h-6", "px-1.5")]
    [InlineData(L.Size.Sm, L.Density.Compact, "h-7", "px-2")]
    [InlineData(L.Size.Md, L.Density.Compact, "h-7", "px-2")]
    [InlineData(L.Size.Lg, L.Density.Compact, "h-10", "px-3")]
    [InlineData(L.Size.Xl, L.Density.Compact, "h-12", "px-3.5")]
    [InlineData(L.Size.Xxl, L.Density.Compact, "h-14", "px-4")]
    // Spacious
    [InlineData(L.Size.Xxs, L.Density.Spacious, "h-7", "px-2")]
    [InlineData(L.Size.Xs, L.Density.Spacious, "h-8", "px-2")]
    [InlineData(L.Size.Sm, L.Density.Spacious, "h-9", "px-3")]
    [InlineData(L.Size.Md, L.Density.Spacious, "h-9", "px-3")]
    [InlineData(L.Size.Lg, L.Density.Spacious, "h-12", "px-5")]
    [InlineData(L.Size.Xl, L.Density.Spacious, "h-14", "px-6")]
    [InlineData(L.Size.Xxl, L.Density.Spacious, "h-16", "px-7")]
    public void Trigger_Height_And_Padding_Per_Rung(L.Size size, L.Density density, string h, string px)
    {
        var cut = RenderButtonTrigger(size, density);
        var cls = cut.Find("button").GetAttribute("class");
        AssertHasClass(cls, h);
        AssertHasClass(cls, px);
    }

    [Fact]
    public void Default_Size_Is_Md()
    {
        var cut = _ctx.Render<L.DatePicker>(p => p.Add(d => d.AllowKeyboardInput, false));
        var cls = cut.Find("button").GetAttribute("class");
        AssertHasClass(cls, "h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]");
        AssertHasClass(cls, "px-2.5");
        AssertHasClass(cls, "text-sm");
    }

    // ============================== Text size per rung ==============================

    [Theory]
    [InlineData(L.Size.Xxs, "text-[8px]")]
    [InlineData(L.Size.Xs, "text-[10px]")]
    [InlineData(L.Size.Sm, "text-xs")]
    [InlineData(L.Size.Md, "text-sm")]
    [InlineData(L.Size.Lg, "text-base")]
    [InlineData(L.Size.Xl, "text-lg")]
    [InlineData(L.Size.Xxl, "text-xl")]
    public void Trigger_Text_Size_Per_Rung(L.Size size, string textClass)
    {
        var cut = RenderButtonTrigger(size);
        AssertHasClass(cut.Find("button").GetAttribute("class"), textClass);
    }

    [Theory]
    [InlineData(L.Size.Xs, "text-[10px]")]
    [InlineData(L.Size.Lg, "text-base")]
    public void Typeable_Input_Text_Size_Follows_Size(L.Size size, string textClass)
    {
        // Default AllowKeyboardInput=true path — the inner <input> must track the same
        // TextSizeClasses as the button-trigger CssClass, not a hardcoded text-sm.
        var cut = _ctx.Render<L.DatePicker>(p => p.Add(d => d.Size, size));
        AssertHasClass(cut.Find("input").GetAttribute("class"), textClass);
    }

    // ============================== Cascading Density inheritance ==============================
    // Mirrors Input.Density: an ambient DensityScope sets the trigger's height/padding,
    // and an explicit Density parameter on DatePicker itself overrides the cascade.

    private IRenderedComponent<IComponent> RenderUnderDensityScope(L.Density scopeDensity, L.Density? explicitDensity = null)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DensityScope>(0);
            builder.AddAttribute(1, "Value", scopeDensity);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.DatePicker>(0);
                inner.AddAttribute(1, "AllowKeyboardInput", false);
                if (explicitDensity.HasValue) inner.AddAttribute(2, "Density", explicitDensity.Value);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Inherits_Density_From_Ambient_DensityScope()
    {
        var cut = RenderUnderDensityScope(L.Density.Compact);
        var cls = cut.Find("button").GetAttribute("class");
        AssertHasClass(cls, "h-7");
        AssertHasClass(cls, "px-2");
    }

    [Fact]
    public void Explicit_Density_Overrides_Ambient_DensityScope()
    {
        var cut = RenderUnderDensityScope(L.Density.Compact, explicitDensity: L.Density.Spacious);
        var cls = cut.Find("button").GetAttribute("class");
        AssertHasClass(cls, "h-9");
        AssertHasClass(cls, "px-3");
    }

    [Fact]
    public void No_DensityScope_And_No_Explicit_Density_Falls_Back_To_Comfortable()
    {
        var cut = _ctx.Render<L.DatePicker>(p => p.Add(d => d.AllowKeyboardInput, false));
        var cls = cut.Find("button").GetAttribute("class");
        AssertHasClass(cls, "h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]");
        AssertHasClass(cls, "px-2.5");
    }

    // ============================== Select trigger parity ==============================
    // Select has no Size parameter of its own (only Density) — so parity is asserted at
    // the DatePicker's default Md rung, across all 3 densities, which is the one point
    // where Select's own trigger sizing actually exists to compare against. At Md the
    // DatePicker/Select height+padding tokens must be byte-identical.

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
        var datePicker = RenderUnderDensityScope(density);

        var selectCls = select.Find("button[role='combobox']").GetAttribute("class") ?? "";
        var dpCls = datePicker.Find("button").GetAttribute("class") ?? "";

        var selectTokens = selectCls.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hToken = Assert.Single(selectTokens, t => t.StartsWith("h-", StringComparison.Ordinal));
        var pxToken = Assert.Single(selectTokens, t => t.StartsWith("px-", StringComparison.Ordinal));

        AssertHasClass(dpCls, hToken);
        AssertHasClass(dpCls, pxToken);
    }
}
