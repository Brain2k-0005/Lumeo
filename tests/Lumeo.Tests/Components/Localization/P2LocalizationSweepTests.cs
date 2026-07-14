using System.Globalization;
using Bunit;
using Microsoft.Extensions.Options;
using Xunit;
using Lumeo.Services.Localization;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Localization;

/// <summary>
/// Spot checks for the P2 localization sweep: previously-hardcoded English
/// strings (ConfirmButton / PickList / FileManager / AudioPlayer /
/// ThemeSwitcher / Stepper "Optional" / BreadcrumbEllipsis "More") now resolve
/// through ILumeoLocalizer — en renders unchanged, and a non-English UI
/// culture renders its translation. Key-completeness is asserted for all 14
/// shipped locales so the en-fallback never silently papers over a miss.
/// </summary>
public class P2LocalizationSweepTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public P2LocalizationSweepTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void WithUICulture(string culture, Action body)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            body();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    // --- Component spot checks: en + de ---

    [Fact]
    public void BreadcrumbEllipsis_SrOnly_Is_Localized()
    {
        WithUICulture("en-US", () =>
        {
            var cut = _ctx.Render<Lumeo.BreadcrumbEllipsis>();
            Assert.Equal("More", cut.Find("span.sr-only").TextContent);
        });
        WithUICulture("de-DE", () =>
        {
            var cut = _ctx.Render<Lumeo.BreadcrumbEllipsis>();
            Assert.Equal("Mehr", cut.Find("span.sr-only").TextContent);
        });
    }

    [Fact]
    public void ThemeSwitcher_Section_Labels_Are_Localized()
    {
        WithUICulture("en-US", () =>
        {
            var cut = _ctx.Render<Lumeo.ThemeSwitcher>();
            Assert.Contains("Color", cut.Markup);
            Assert.Contains("Mode", cut.Markup);
        });
        WithUICulture("de-DE", () =>
        {
            var cut = _ctx.Render<Lumeo.ThemeSwitcher>();
            Assert.Contains("Farbe", cut.Markup);
            Assert.Contains("Modus", cut.Markup);
        });
    }

    [Fact]
    public void PickList_Defaults_Are_Localized()
    {
        WithUICulture("de-DE", () =>
        {
            var cut = _ctx.Render<Lumeo.PickList<string>>(p => p
                .Add(l => l.Items, new[] { "x" })
                .Add(l => l.SelectedItems, Array.Empty<string>()));

            Assert.Contains("Verfügbar", cut.Markup);
            Assert.Contains("Ausgewählt", cut.Markup);
            Assert.Contains("Keine Einträge", cut.Markup); // empty target panel
        });
    }

    [Fact]
    public void AudioPlayer_Aria_Labels_Are_Localized()
    {
        WithUICulture("en-US", () =>
        {
            var cut = _ctx.Render<Lumeo.AudioPlayer>(p => p.Add(a => a.Src, "test.mp3"));
            Assert.NotNull(cut.Find("[aria-label='Audio player']"));
            Assert.NotNull(cut.Find("[aria-label='Play']"));
            Assert.NotNull(cut.Find("[aria-label='Seek']"));
        });
        WithUICulture("de-DE", () =>
        {
            var cut = _ctx.Render<Lumeo.AudioPlayer>(p => p.Add(a => a.Src, "test.mp3"));
            Assert.NotNull(cut.Find("[aria-label='Audio-Player']"));
            Assert.NotNull(cut.Find("[aria-label='Abspielen']"));
        });
    }

    [Fact]
    public void Stepper_Optional_Label_Is_Localized()
    {
        WithUICulture("de-DE", () =>
        {
            var cut = _ctx.Render<Lumeo.Stepper>(p => p
                .Add(s => s.ActiveStep, 0)
                .AddChildContent(b =>
                {
                    b.OpenComponent<Lumeo.StepperStep>(0);
                    b.AddAttribute(1, "Title", "Schritt 1");
                    b.AddAttribute(2, "Optional", true);
                    b.CloseComponent();
                }));

            // Steps register during a hidden first pass; re-render so the
            // header reflects the registered steps.
            cut.Render(p => p.Add(s => s.ActiveStep, 0));

            Assert.Contains("Optional", cut.Markup); // de happens to match en
        });
    }

    // --- Key completeness: every new key exists in every shipped locale ---

    private static readonly string[] NewKeys =
    {
        "ConfirmButton.Title", "ConfirmButton.Confirm", "ConfirmButton.Cancel",
        "PickList.SourceHeader", "PickList.TargetHeader", "PickList.NoItems",
        "FileManager.Open", "FileManager.Rename", "FileManager.Delete",
        "FileManager.Name", "FileManager.Size", "FileManager.Modified", "FileManager.Loading",
        "AudioPlayer.Label", "AudioPlayer.Play", "AudioPlayer.Pause", "AudioPlayer.Seek",
        "AudioPlayer.Mute", "AudioPlayer.Unmute", "AudioPlayer.Download",
        "Theme.Color", "Theme.Mode",
        "DataGrid.ColumnMovedAnnouncement",
    };

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("it")]
    [InlineData("pt")]
    [InlineData("nl")]
    [InlineData("pl")]
    [InlineData("ja")]
    [InlineData("zh")]
    [InlineData("ko")]
    [InlineData("ar")]
    [InlineData("ru")]
    [InlineData("tr")]
    public void New_Keys_Exist_In_Locale(string culture)
    {
        var options = new LumeoLocalizationOptions();
        LumeoDefaultStrings.ApplyDefaults(options);

        Assert.True(options.Translations.TryGetValue(culture, out var bucket),
            $"culture bucket '{culture}' missing");
        foreach (var key in NewKeys)
        {
            Assert.True(bucket!.ContainsKey(key), $"'{key}' missing in '{culture}'");
        }
    }

    [Fact]
    public void ConfirmButton_Defaults_Resolve_Via_Localizer()
    {
        var options = new LumeoLocalizationOptions();
        LumeoDefaultStrings.ApplyDefaults(options);
        var localizer = new LumeoLocalizer(Options.Create(options));

        WithUICulture("en-US", () =>
        {
            Assert.Equal("Are you sure?", localizer["ConfirmButton.Title"]);
            Assert.Equal("Continue", localizer["ConfirmButton.Confirm"]);
            Assert.Equal("Cancel", localizer["ConfirmButton.Cancel"]);
        });
        WithUICulture("de-DE", () =>
        {
            Assert.Equal("Sind Sie sicher?", localizer["ConfirmButton.Title"]);
            Assert.Equal("Fortfahren", localizer["ConfirmButton.Confirm"]);
        });
    }

    [Fact]
    public void FileManager_Strings_Resolve_Via_Localizer()
    {
        var options = new LumeoLocalizationOptions();
        LumeoDefaultStrings.ApplyDefaults(options);
        var localizer = new LumeoLocalizer(Options.Create(options));

        WithUICulture("en-US", () =>
        {
            Assert.Equal("Open", localizer["FileManager.Open"]);
            Assert.Equal("Name", localizer["FileManager.Name"]);
            Assert.Equal("Loading…", localizer["FileManager.Loading"]);
        });
        WithUICulture("fr-FR", () =>
        {
            Assert.Equal("Ouvrir", localizer["FileManager.Open"]);
        });
    }
}
