using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Lumeo.RegistryGen.Tests;

/// <summary>
/// PR #356 round-8 (Codex P2) — <see cref="ComponentsApiEmitter.ExtractA11y"/>'s signal
/// regexes only ever matched LITERAL markup (<c>role="..."</c>, <c>aria-*=...</c>). Before
/// round-7/8's comment-stripping fixes, some components' real (but dynamic) a11y state
/// leaked into the scan only because a doc comment happened to mention the token as prose
/// — e.g. Chart.razor's XML doc mentioning <c>role="img"</c>, or Icon.razor's doc mentioning
/// <c>aria-hidden="true"</c>. Once comments are correctly stripped, that accidental source
/// disappears, so the scan itself must ALSO recognise the two real dynamic shapes Lumeo
/// components use: a Razor <c>role="@(...)"</c> ternary/switch expression (Chart's canvas
/// host), and a <c>Dictionary&lt;string, object&gt;</c> splat-attribute assignment like
/// <c>merged["role"] = "img";</c> / <c>merged["aria-hidden"] = "true";</c> (Icon's
/// A11yAttributes). This class exercises <see cref="ComponentsApiEmitter.ExtractA11y"/>
/// directly against small fixture files reproducing those two exact shapes.
/// </summary>
public class ComponentsApiEmitterDynamicA11yTests
{
    private static Dictionary<string, object?> ExtractFromSource(string source)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lumeo-a11y-test-{Path.GetRandomFileName()}.razor");
        File.WriteAllText(path, source);
        try
        {
            return (Dictionary<string, object?>)ComponentsApiEmitter.ExtractA11y(new[] { path });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Picks_Up_A_Role_Chosen_Inside_A_Dynamic_Razor_Expression()
    {
        // Mirrors Chart.razor's canvas host exactly.
        var source = """
            <div id="@_chartId"
                 role="@(string.IsNullOrEmpty(HostAriaLabel) ? null : "img")" aria-label="@HostAriaLabel"
                 tabindex="@HostTabIndex"></div>
            """;

        var a11y = ExtractFromSource(source);

        var roles = Assert.IsType<string[]>(a11y["roles"]);
        Assert.Contains("img", roles);
        var ariaAttrs = Assert.IsType<string[]>(a11y["ariaAttributes"]);
        Assert.Contains("aria-label", ariaAttrs);
    }

    [Fact]
    public void Picks_Up_Role_And_Aria_Attributes_Assigned_Via_A_Dictionary_Splat()
    {
        // Mirrors Icon.razor's A11yAttributes: role/aria-label/aria-hidden are all
        // assigned as Dictionary<string, object> entries, not literal HTML attributes.
        var source = """
            @code {
                private IReadOnlyDictionary<string, object>? A11yAttributes
                {
                    get
                    {
                        var merged = new Dictionary<string, object>();
                        if (!string.IsNullOrWhiteSpace(Title))
                        {
                            merged["role"] = "img";
                            merged["aria-label"] = Title;
                        }
                        else
                        {
                            merged["aria-hidden"] = "true";
                        }
                        return merged;
                    }
                }
            }
            """;

        var a11y = ExtractFromSource(source);

        var roles = Assert.IsType<string[]>(a11y["roles"]);
        Assert.Contains("img", roles);
        var ariaAttrs = Assert.IsType<string[]>(a11y["ariaAttributes"]);
        Assert.Contains("aria-label", ariaAttrs);
        Assert.Contains("aria-hidden", ariaAttrs);
    }

    [Fact]
    public void Still_Ignores_Aria_Attribute_Names_Only_Checked_Via_ContainsKey()
    {
        // Icon.razor also has `AdditionalAttributes.ContainsKey("aria-label")` checks
        // (is the CONSUMER overriding it?) right next to the real assignment — those
        // must not be double-counted or misread; a plain ContainsKey("aria-xxx") with
        // no trailing `=` is not itself an assignment and shouldn't crash the scan.
        var source = """
            @code {
                private bool ConsumerSetsAria =>
                    AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("aria-label");
            }
            """;

        var a11y = ExtractFromSource(source);

        // No crash, and no false role/aria signal from a bare ContainsKey check.
        var ariaAttrs = Assert.IsType<string[]>(a11y["ariaAttributes"]);
        Assert.Empty(ariaAttrs);
    }

    [Fact]
    public void Still_Matches_Plain_Literal_Role_And_Aria_Markup()
    {
        // Backward-compat pin: the original literal-markup case still works unchanged.
        var source = "<div role=\"toolbar\" aria-expanded=\"true\"></div>";

        var a11y = ExtractFromSource(source);

        var roles = Assert.IsType<string[]>(a11y["roles"]);
        Assert.Contains("toolbar", roles);
        var ariaAttrs = Assert.IsType<string[]>(a11y["ariaAttributes"]);
        Assert.Contains("aria-expanded", ariaAttrs);
    }
}
