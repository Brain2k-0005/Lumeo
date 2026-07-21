namespace Lumeo.Services.Localization;

internal static partial class LumeoDefaultStrings
{
    internal static readonly IReadOnlyDictionary<string, string> Nl = new Dictionary<string, string>
    {
        // DataGrid
        ["DataGrid.NoData"] = "Geen gegevens",
        ["DataGrid.Loading"] = "Laden…",
        ["DataGrid.SearchPlaceholder"] = "Zoeken…",
        ["DataGrid.ClearSearch"] = "Zoekopdracht wissen",
        ["DataGrid.Columns"] = "Kolommen",
        ["DataGrid.ToggleColumns"] = "Kolommen weergeven/verbergen",
        ["DataGrid.ExportCsv"] = "CSV exporteren",
        ["DataGrid.ExportExcel"] = "Excel exporteren",
        ["DataGrid.ExportJson"] = "JSON exporteren",
        ["DataGrid.Export"] = "Exporteren",
        ["DataGrid.Filter"] = "Filter",
        ["DataGrid.Filters"] = "Filters",
        ["DataGrid.ClearFilters"] = "Filters wissen",
        ["DataGrid.ResetLayout"] = "Lay-out herstellen",
        ["DataGrid.SaveLayout"] = "Lay-out opslaan",
        ["DataGrid.Layouts"] = "Lay-outs",
        ["DataGrid.NewLayout"] = "Nieuwe lay-out",
        ["DataGrid.Personal"] = "Persoonlijk",
        ["DataGrid.Global"] = "Globaal",
        ["DataGrid.SystemDefault"] = "Systeemstandaard",
        ["DataGrid.LayoutName"] = "Lay-outnaam",
        ["DataGrid.Save"] = "Opslaan",
        ["DataGrid.Cancel"] = "Annuleren",
        ["DataGrid.Delete"] = "Verwijderen",
        ["DataGrid.Rename"] = "Hernoemen",
        ["DataGrid.PinLeft"] = "Links vastmaken",
        ["DataGrid.PinRight"] = "Rechts vastmaken",
        ["DataGrid.Unpin"] = "Losmaken",
        ["DataGrid.PinColumn"] = "Kolom vastmaken",
        ["DataGrid.ResizeColumn"] = "Kolombreedte aanpassen (gebruik de pijltjestoetsen, dubbelklik om automatisch aan te passen)",
        ["DataGrid.DragToReorder"] = "Sleep om kolom te herschikken",
        ["DataGrid.ColumnMovedAnnouncement"] = "{0} verplaatst naar positie {1} van {2}",
        ["DataGrid.FilterColumn"] = "{0} filteren",
        ["DataGrid.SelectRow"] = "Rij {0} selecteren",
        ["DataGrid.SelectAllRows"] = "Alle rijen selecteren",
        ["DataGrid.ExpandRow"] = "Rij {0} uitklappen",
        ["DataGrid.CollapseRow"] = "Rij {0} inklappen",
        ["DataGrid.ExpandGroup"] = "Groep {0} uitklappen",
        ["DataGrid.CollapseGroup"] = "Groep {0} inklappen",
        ["DataGrid.Hide"] = "Verbergen",
        ["DataGrid.Show"] = "Weergeven",
        ["DataGrid.SortAscending"] = "Oplopend sorteren",
        ["DataGrid.SortDescending"] = "Aflopend sorteren",
        ["DataGrid.ClearSort"] = "Sortering wissen",
        ["DataGrid.Edit"] = "Bewerken",
        ["DataGrid.CommitEdit"] = "Opslaan",
        ["DataGrid.CancelEdit"] = "Annuleren",
        ["DataGrid.AggregateSum"] = "Som",
        ["DataGrid.AggregateAvg"] = "Gem.",
        ["DataGrid.AggregateCount"] = "Aantal",
        ["DataGrid.AggregateRow"] = "Aggregatierij",
        ["DataGrid.AggregateMin"] = "Min",
        ["DataGrid.AggregateMax"] = "Max",
        ["DataGrid.Items"] = "items",
        ["DataGrid.ItemsCount"] = "{0} items",
        ["DataGrid.ItemsCount.One"] = "{0} item",
        ["DataGrid.ItemsCount.Other"] = "{0} items",
        ["DataGrid.CopySelected"] = "Kopiëren ({0})",
        ["DataGrid.ApplyLayout"] = "Lay-out toepassen",
        ["DataGrid.NoSavedLayouts"] = "Nog geen opgeslagen lay-outs.",
        ["DataGrid.SaveCurrentLayout"] = "Huidige lay-out opslaan…",
        ["DataGrid.Default"] = "Standaard",
        ["DataGrid.MoveUp"] = "Omhoog",
        ["DataGrid.MoveDown"] = "Omlaag",
        ["DataGrid.Retry"] = "Opnieuw proberen",
        ["DataGrid.ErrorLoadingData"] = "Laden mislukt: {0}",
        ["DataGrid.ExpandFullscreen"] = "Volledig scherm openen",
        ["DataGrid.ExitFullscreen"] = "Volledig scherm sluiten",
        ["DataGrid.AddGroupLevel"] = "+ Groepsniveau toevoegen",
        ["DataGrid.GroupPanelPlaceholder"] = "Sleep een groepeerbare kolomkop hierheen of gebruik de vervolgkeuzelijst",
        ["DataGrid.DragToGroup"] = "Sleep om op deze kolom te groeperen",
        ["DataGrid.RemoveGrouping"] = "Groepering verwijderen",
        ["DataGrid.ClearAllGrouping"] = "Alle groepering wissen",
        ["DataGrid.DragToReorderRow"] = "Sleep om rij te herschikken",
        ["DataGrid.RowReorderUnavailable"] = "Rijen herschikken is niet mogelijk tijdens groeperen of virtualisatie",
        ["Filter.FilterTitle"] = "Filter: {0}",

        // Pagination
        ["Pagination.Previous"] = "Vorige",
        ["Pagination.Next"] = "Volgende",
        ["Pagination.First"] = "Eerste",
        ["Pagination.Last"] = "Laatste",
        ["Pagination.Page"] = "Pagina",
        ["Pagination.MorePages"] = "Meer pagina's",
        ["Pagination.GoToPage"] = "Ga naar pagina {0}",
        ["Pagination.RowsPerPage"] = "Rijen per pagina",
        ["Pagination.RangeOfTotal"] = "{0}–{1} van {2}",

        // Filter operators
        ["Filter.Contains"] = "bevat",
        ["Filter.DoesNotContain"] = "bevat niet",
        ["Filter.Equals"] = "gelijk aan",
        ["Filter.NotEquals"] = "niet gelijk aan",
        ["Filter.StartsWith"] = "begint met",
        ["Filter.EndsWith"] = "eindigt op",
        ["Filter.GreaterThan"] = "groter dan",
        ["Filter.LessThan"] = "kleiner dan",
        ["Filter.GreaterThanOrEqual"] = "groter of gelijk",
        ["Filter.LessThanOrEqual"] = "kleiner of gelijk",
        ["Filter.Between"] = "tussen",
        ["Filter.IsEmpty"] = "is leeg",
        ["Filter.IsNotEmpty"] = "is niet leeg",
        ["Filter.Apply"] = "Toepassen",
        ["Filter.Clear"] = "Wissen",
        ["Filter.Value"] = "Waarde",
        ["Filter.SelectAll"] = "Alle",
        ["Filter.ValuePlaceholder"] = "Waarde…",
        ["Filter.ToValuePlaceholder"] = "Tot…",

        // ColorPicker
        ["ColorPicker.PickColor"] = "Kies een kleur",
        ["ColorPicker.Hue"] = "Tint",
        ["ColorPicker.Saturation"] = "Verzadiging",
        ["ColorPicker.Lightness"] = "Helderheid",
        ["ColorPicker.Value"] = "Waarde",
        ["ColorPicker.Opacity"] = "Dekking",
        ["ColorPicker.Hex"] = "Hex",
        ["ColorPicker.HexValue"] = "Hex-waarde",
        ["ColorPicker.Red"] = "Rood",
        ["ColorPicker.Green"] = "Groen",
        ["ColorPicker.Blue"] = "Blauw",
        ["ColorPicker.Presets"] = "Voorkeuzes",

        // PasswordInput
        ["Password.Placeholder"] = "Wachtwoord invoeren",
        ["Password.Toggle"] = "Wachtwoord tonen/verbergen",
        ["Password.Weak"] = "Zwak",
        ["Password.Fair"] = "Redelijk",
        ["Password.Good"] = "Goed",
        ["Password.Strong"] = "Sterk",

        // FileUpload
        ["FileUpload.DragDrop"] = "Sleep bestanden hierheen",
        ["FileUpload.Or"] = "of",
        ["FileUpload.Browse"] = "Bladeren",
        ["FileUpload.MaxSize"] = "Max. grootte: {0}",
        ["FileUpload.Accepted"] = "Toegestaan: {0}",
        ["FileUpload.Remove"] = "Verwijderen",
        ["FileUpload.Uploading"] = "Uploaden…",
        ["FileUpload.Uploaded"] = "Geüpload",
        ["FileUpload.Failed"] = "Mislukt",
        ["FileUpload.Retry"] = "Opnieuw proberen",
        ["FileUpload.TooLarge"] = "Bestand te groot",
        ["FileUpload.TypeNotAllowed"] = "Bestandstype niet toegestaan",
        ["FileUpload.ChooseFile"] = "Bestand kiezen",
        ["FileUpload.ClickToUpload"] = "Klik om te uploaden of sleep hierheen",

        // Overlays
        ["Dialog.Close"] = "Sluiten",
        ["Dialog.Confirm"] = "Bevestigen",
        ["Dialog.Cancel"] = "Annuleren",
        ["Dialog.Ok"] = "OK",
        ["Dialog.Yes"] = "Ja",
        ["Dialog.No"] = "Nee",
        ["Toast.Close"] = "Sluiten",
        ["Toast.Dismiss"] = "Sluiten",
        ["AlertDialog.Delete"] = "Verwijderen",
        ["AlertDialog.Continue"] = "Doorgaan",

        // Combobox / Command / Select
        ["Combobox.Placeholder"] = "Selecteren…",
        ["Combobox.SearchPlaceholder"] = "Zoeken…",
        ["Combobox.NoResults"] = "Geen resultaten",
        ["Combobox.Loading"] = "Laden…",
        ["Combobox.Clear"] = "Wissen",
        ["Combobox.Create"] = "\"{0}\" aanmaken",
        ["Command.Placeholder"] = "Voer een opdracht in of zoek…",
        ["Command.NoResults"] = "Geen resultaten",
        ["Select.Placeholder"] = "Selecteer een optie",

        // Calendar / DatePicker
        ["Calendar.Today"] = "Vandaag",
        ["Calendar.Clear"] = "Wissen",
        ["Calendar.PrevMonth"] = "Vorige maand",
        ["Calendar.NextMonth"] = "Volgende maand",
        ["Calendar.PrevYear"] = "Vorig jaar",
        ["Calendar.NextYear"] = "Volgend jaar",
        ["DatePicker.Placeholder"] = "Kies een datum",
        ["DateRange.Placeholder"] = "Kies een periode",
        ["DateRange.From"] = "Van",
        ["DateRange.To"] = "Tot",
        ["DateTimePicker.Placeholder"] = "Kies datum en tijd",
        ["DateTimePicker.TimeLabel"] = "Tijd",
        ["TimePicker.Placeholder"] = "Kies tijd",

        // Tour
        ["Tour.Skip"] = "Overslaan",
        ["Tour.Previous"] = "Vorige",
        ["Tour.Next"] = "Volgende",
        ["Tour.Finish"] = "Voltooien",

        // PopConfirm
        ["PopConfirm.Title"] = "Weet je het zeker?",
        ["PopConfirm.Confirm"] = "Ja",
        ["PopConfirm.Cancel"] = "Nee",

        // Misc
        ["Common.Search"] = "Zoeken",
        ["Common.Clear"] = "Wissen",
        ["Common.ClearAll"] = "Alles wissen",
        ["Common.Close"] = "Sluiten",
        ["Common.Loading"] = "Laden…",
        ["Common.NoResults"] = "Geen resultaten",
        ["Common.Apply"] = "Toepassen",
        ["Common.Reset"] = "Herstellen",
        ["Common.Save"] = "Opslaan",
        ["Common.Cancel"] = "Annuleren",
        ["Common.Copy"] = "Kopiëren",
        ["Common.Copied"] = "Gekopieerd",
        ["Common.More"] = "Meer",
        ["Common.Back"] = "Terug",
        ["Common.Next"] = "Volgende",
        ["Common.ShowMore"] = "Meer tonen",
        ["Common.ShowLess"] = "Minder tonen",

        // Transfer / TreeSelect / TagInput
        ["Transfer.SourceHeader"] = "Beschikbaar",
        ["Transfer.TargetHeader"] = "Geselecteerd",
        ["Transfer.MoveRight"] = "Naar rechts",
        ["Transfer.MoveLeft"] = "Naar links",
        ["Transfer.NoItems"] = "Geen items",
        ["TreeSelect.Placeholder"] = "Selecteren…",
        ["TreeSelect.NoResults"] = "Geen resultaten",
        ["TagInput.Placeholder"] = "Tag toevoegen…",

        // Cascader
        ["Cascader.Placeholder"] = "Selecteren…",

        // Rating / OTP
        ["Rating.Rate"] = "Beoordelen",
        ["Rating.RateOf"] = "Beoordeling {0} van {1}",
        ["Otp.Placeholder"] = "Code invoeren",

        // Empty
        ["Empty.Title"] = "Nog niets hier",
        ["Empty.Description"] = "Er zijn geen gegevens om weer te geven.",

        // Kanban
        ["Kanban.AddCard"] = "Kaart toevoegen",

        // Carousel
        ["Carousel.PreviousSlide"] = "Vorige dia",
        ["Carousel.NextSlide"] = "Volgende dia",
        ["Carousel.SlideXofY"] = "Dia {0} van {1}",

        // Stepper
        ["Stepper.Back"] = "Terug",
        ["Stepper.Next"] = "Volgende",
        ["Stepper.Finish"] = "Voltooien",
        ["Stepper.Optional"] = "Optioneel",
        ["Stepper.Skip"] = "Overslaan",

        // Window
        ["Window.Close"] = "Sluiten",
        ["Window.Minimize"] = "Minimaliseren",
        ["Window.Maximize"] = "Maximaliseren",
        ["Window.Restore"] = "Herstellen",

        // NumberInput
        ["NumberInput.Decrease"] = "Verlagen",
        ["NumberInput.Increase"] = "Verhogen",

        // DateTimePicker
        ["DateTimePicker.ClearDate"] = "Datum wissen",

        // Slider
        ["Slider.End"] = "einde",

        // FileManager
        ["FileManager.EmptyTitle"] = "Deze map is leeg",
        ["FileManager.EmptyState"] = "Geen bestanden of mappen hier.",
        ["FileManager.MoreActions"] = "Meer acties",
        ["FileManager.MoreActionsForName"] = "Meer acties voor {0}",

        // FileUpload (parameterised)
        ["FileUpload.ExceedsMaxSize"] = "Bestand \"{0}\" overschrijdt de maximale grootte van {1}.",

        // ConfirmButton
        ["ConfirmButton.Title"] = "Weet je het zeker?",
        ["ConfirmButton.Confirm"] = "Doorgaan",
        ["ConfirmButton.Cancel"] = "Annuleren",

        // PickList (panels)
        ["PickList.SourceHeader"] = "Beschikbaar",
        ["PickList.TargetHeader"] = "Geselecteerd",
        ["PickList.NoItems"] = "Geen items",

        // FileManager (context menu / list view)
        ["FileManager.Open"] = "Openen",
        ["FileManager.Rename"] = "Hernoemen",
        ["FileManager.Delete"] = "Verwijderen",
        ["FileManager.Name"] = "Naam",
        ["FileManager.Size"] = "Grootte",
        ["FileManager.Modified"] = "Gewijzigd",
        ["FileManager.Loading"] = "Laden…",

        // AudioPlayer
        ["AudioPlayer.Label"] = "Audiospeler",
        ["AudioPlayer.Play"] = "Afspelen",
        ["AudioPlayer.Pause"] = "Pauzeren",
        ["AudioPlayer.Seek"] = "Afspeelpositie",
        ["AudioPlayer.Mute"] = "Dempen",
        ["AudioPlayer.Unmute"] = "Dempen opheffen",
        ["AudioPlayer.Download"] = "Audio downloaden",

        // ThemeSwitcher
        ["Theme.Color"] = "Kleur",
        ["Theme.Mode"] = "Modus",

        // Gantt (Codex round 4, P2 #6 — locale completeness: previously only en/de had any Gantt.* keys)
        ["Gantt.Day"] = "Dag",
        ["Gantt.Week"] = "Week",
        ["Gantt.Month"] = "Maand",
        ["Gantt.Year"] = "Jaar",
        ["Gantt.Today"] = "Vandaag",
        ["Gantt.PreviousPeriod"] = "Vorige periode",
        ["Gantt.NextPeriod"] = "Volgende periode",
        ["Gantt.ExpandRow"] = "{0} uitklappen",
        ["Gantt.CollapseRow"] = "{0} inklappen",
        ["Gantt.NoTasksToDisplay"] = "Geen taken om weer te geven",
        ["Gantt.TaskAriaLabel"] = "{0}, {1} tot {2}",
    };
}
