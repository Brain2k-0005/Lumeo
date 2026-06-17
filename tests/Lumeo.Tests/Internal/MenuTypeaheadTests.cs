using System.Threading;
using Lumeo.Services;
using Xunit;

namespace Lumeo.Tests.Internal;

/// <summary>
/// Unit tests for the shared menu typeahead buffer (#222/#225/#226). Covers the
/// printable-key classification and the append-vs-reset buffering that backs
/// DropdownMenu / Menubar / MegaMenu type-to-focus.
/// </summary>
public class MenuTypeaheadTests
{
    [Theory]
    [InlineData("a", true)]
    [InlineData("Z", true)]
    [InlineData("5", true)]
    [InlineData("/", true)]
    [InlineData(" ", false)]      // Space activates the focused item
    [InlineData("Enter", false)]
    [InlineData("ArrowDown", false)]
    [InlineData("Escape", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsTypeaheadKey_Classifies_Printable_Single_Chars(string? key, bool expected)
    {
        Assert.Equal(expected, MenuTypeahead.IsTypeaheadKey(key));
    }

    [Fact]
    public void Push_Accumulates_Within_Reset_Window()
    {
        var ta = new MenuTypeahead();
        Assert.Equal("s", ta.Push("s"));
        Assert.Equal("se", ta.Push("e"));
        Assert.Equal("set", ta.Push("t"));
        Assert.Equal("set", ta.Buffer);
    }

    [Fact]
    public void Reset_Clears_The_Buffer()
    {
        var ta = new MenuTypeahead();
        ta.Push("a");
        ta.Push("b");
        ta.Reset();
        Assert.Equal(string.Empty, ta.Buffer);
        // After reset the next key starts a fresh single-char query.
        Assert.Equal("c", ta.Push("c"));
    }

    [Fact]
    public void Push_Starts_New_Query_After_Idle_Gap()
    {
        var ta = new MenuTypeahead();
        ta.Push("a");
        // The reset window is 1s; sleeping just over it makes the next key start
        // a new query rather than appending. Kept short to not slow the suite.
        Thread.Sleep(1100);
        Assert.Equal("b", ta.Push("b"));
    }
}
