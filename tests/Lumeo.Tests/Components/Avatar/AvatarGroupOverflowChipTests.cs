using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Avatar;

/// <summary>
/// The overflow chip has two shapes and the count decides which. A single-digit count fits
/// inside the avatar's own box, so the chip keeps that exact width and reads as the circle
/// its border-2 ring promises. Two digits fit at no rung - at Xxs the box leaves 12px of
/// inner width after the borders - so there it has to widen.
///
/// Both halves matter, and each one was got wrong in turn: a fixed width let "+10" spill
/// onto the avatars underneath, and then unconditional padding widened "+9" past its own
/// height and turned every count into a pill (Codex review of PR #430, two rounds running).
/// </summary>
public class AvatarGroupOverflowChipTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AvatarGroupOverflowChipTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>Renders a group of <paramref name="members"/> avatars capped at 1.</summary>
    private string[] ChipTokens(int members, Lumeo.Size size)
    {
        var cut = _ctx.Render<Lumeo.AvatarGroup>(p =>
        {
            p.Add(g => g.Size, size).Add(g => g.Max, 1);
            for (var i = 0; i < members; i++)
                p.AddChildContent<Lumeo.Avatar>(_ => { });
        });

        // The chip is the last shrink-0 box in the row - the avatars come before it.
        var chips = cut.FindAll("[class*='shrink-0']");
        Assert.NotEmpty(chips);
        return (chips[^1].GetAttribute("class") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "w-4")]
    [InlineData(Lumeo.Size.Xs, "w-5")]
    [InlineData(Lumeo.Size.Sm, "w-6")]
    [InlineData(Lumeo.Size.Md, "w-8")]
    [InlineData(Lumeo.Size.Lg, "w-10")]
    [InlineData(Lumeo.Size.Xl, "w-12")]
    [InlineData(Lumeo.Size.Xxl, "w-16")]
    public void A_Single_Digit_Count_Keeps_The_Avatars_Exact_Width(Lumeo.Size size, string width)
    {
        // 10 members, 1 shown => "+9": the widest single-digit count there is.
        var tokens = ChipTokens(members: 10, size);

        Assert.Contains(width, tokens);
        Assert.DoesNotContain(tokens, t => t.StartsWith("px-", StringComparison.Ordinal));
        Assert.DoesNotContain(tokens, t => t.StartsWith("min-w-", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "min-w-4")]
    [InlineData(Lumeo.Size.Xs, "min-w-5")]
    [InlineData(Lumeo.Size.Sm, "min-w-6")]
    [InlineData(Lumeo.Size.Md, "min-w-8")]
    [InlineData(Lumeo.Size.Lg, "min-w-10")]
    [InlineData(Lumeo.Size.Xl, "min-w-12")]
    [InlineData(Lumeo.Size.Xxl, "min-w-16")]
    public void A_Two_Digit_Count_Widens_Into_A_Pill(Lumeo.Size size, string floor)
    {
        // 11 members, 1 shown => "+10": the first count that does not fit the square.
        var tokens = ChipTokens(members: 11, size);

        Assert.Contains(floor, tokens);
        Assert.Contains(tokens, t => t.StartsWith("px-", StringComparison.Ordinal));

        // The floor replaces the fixed width rather than joining it - a w-* alongside min-w-*
        // would pin the box shut again and the padding would have nothing to do.
        Assert.DoesNotContain(tokens, t => t.StartsWith("w-", StringComparison.Ordinal));
    }
}
