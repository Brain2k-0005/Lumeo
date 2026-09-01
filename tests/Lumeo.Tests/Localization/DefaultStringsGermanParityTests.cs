using Xunit;
using Lumeo.Services.Localization;

namespace Lumeo.Tests.Localization;

/// <summary>
/// Field report 5.3. Six keys had an English value and no German one, so a German UI fell
/// back to English on exactly those strings: the Expand/Collapse labels, the Gantt keyboard
/// announcements and the Kanban card role description. Nothing flagged it, because a missing
/// key silently resolves to the fallback culture.
///
/// German is Lumeo's second first-class language (the docs and the field report are both
/// German), so it is held to full parity with English. The other bundled languages are not
/// covered here on purpose: they were never complete, and a guard that fails from day one
/// guards nothing.
/// </summary>
public class DefaultStringsGermanParityTests
{
    [Fact]
    public void German_Covers_Every_English_Key()
    {
        var missing = LumeoDefaultStrings.En.Keys
            .Where(k => !LumeoDefaultStrings.De.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "English keys with no German value (add them to LumeoDefaultStrings.De): "
            + string.Join(", ", missing));
    }

    [Fact]
    public void German_Has_No_Key_English_Lacks()
    {
        // A German-only key is a typo in one of the two tables, not a translation.
        var orphaned = LumeoDefaultStrings.De.Keys
            .Where(k => !LumeoDefaultStrings.En.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(orphaned.Count == 0, "German keys with no English counterpart: " + string.Join(", ", orphaned));
    }

    [Theory]
    [InlineData("Gantt.KeyboardTaskMoved")]
    [InlineData("Gantt.KeyboardTaskResized")]
    public void Format_Placeholders_Match_Between_The_Two_Languages(string key)
    {
        // The translated string is fed to string.Format with the same arguments; a dropped
        // or renumbered placeholder throws at runtime, in the screen reader announcement.
        static IEnumerable<string> Placeholders(string s) =>
            System.Text.RegularExpressions.Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).OrderBy(v => v);

        Assert.Equal(Placeholders(LumeoDefaultStrings.En[key]), Placeholders(LumeoDefaultStrings.De[key]));
    }
}
