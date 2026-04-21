namespace Lumeo.Services.Localization;

/// <summary>
/// Built-in translations for Lumeo component UI text. Ships with EN, DE, ES, FR,
/// IT, PT, NL, PL, JA, ZH-Hans (Simplified Chinese), KO, AR, RU, TR out of the box.
/// Applied by <see cref="LumeoServiceExtensions.AddLumeo"/> before any consumer
/// override callback runs — so consumers can replace individual keys or add whole
/// cultures without having to redefine everything.
/// Keys are namespaced by component: "DataGrid.NoData", "Pagination.Previous", etc.
/// Use {0}, {1}... for formatted values. Per-locale dictionaries live as partial
/// types under <c>Services/Localization/Locales/</c>.
/// </summary>
internal static partial class LumeoDefaultStrings
{
    internal static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        // ── DataGrid ────────────────────────────────────────────────
        ["DataGrid.NoData"]              = "No data available",
        ["DataGrid.Loading"]             = "Loading…",
        ["DataGrid.SearchPlaceholder"]   = "Search…",
        ["DataGrid.ClearSearch"]         = "Clear search",
        ["DataGrid.Columns"]             = "Columns",
        ["DataGrid.ToggleColumns"]       = "Toggle columns",
        ["DataGrid.ExportCsv"]           = "Export CSV",
        ["DataGrid.ExportExcel"]         = "Export Excel",
        ["DataGrid.ExportJson"]          = "Export JSON",
        ["DataGrid.Export"]              = "Export",
        ["DataGrid.Filter"]              = "Filter",
        ["DataGrid.Filters"]             = "Filters",
        ["DataGrid.ClearFilters"]        = "Clear filters",
        ["DataGrid.ResetLayout"]         = "Reset layout",
        ["DataGrid.SaveLayout"]          = "Save layout",
        ["DataGrid.Layouts"]             = "Layouts",
        ["DataGrid.NewLayout"]           = "New layout",
        ["DataGrid.Personal"]            = "Personal",
        ["DataGrid.Global"]              = "Global",
        ["DataGrid.SystemDefault"]       = "System default",
        ["DataGrid.LayoutName"]          = "Layout name",
        ["DataGrid.Save"]                = "Save",
        ["DataGrid.Cancel"]              = "Cancel",
        ["DataGrid.Delete"]              = "Delete",
        ["DataGrid.Rename"]              = "Rename",
        ["DataGrid.PinLeft"]             = "Pin to left",
        ["DataGrid.PinRight"]            = "Pin to right",
        ["DataGrid.Unpin"]               = "Unpin",
        ["DataGrid.Hide"]                = "Hide",
        ["DataGrid.Show"]                = "Show",
        ["DataGrid.SortAscending"]       = "Sort ascending",
        ["DataGrid.SortDescending"]      = "Sort descending",
        ["DataGrid.ClearSort"]           = "Clear sort",
        ["DataGrid.Edit"]                = "Edit",
        ["DataGrid.CommitEdit"]          = "Save",
        ["DataGrid.CancelEdit"]          = "Cancel",
        ["DataGrid.AggregateSum"]        = "Sum",
        ["DataGrid.AggregateAvg"]        = "Avg",
        ["DataGrid.AggregateCount"]      = "Count",
        ["DataGrid.AggregateMin"]        = "Min",
        ["DataGrid.AggregateMax"]        = "Max",
        ["DataGrid.Items"]               = "items",
        ["DataGrid.ItemsCount"]          = "{0} items",
        ["DataGrid.CopySelected"]        = "Copy ({0})",
        ["DataGrid.ApplyLayout"]         = "Apply layout",
        ["DataGrid.NoSavedLayouts"]      = "No saved layouts yet.",
        ["DataGrid.SaveCurrentLayout"]   = "Save current layout…",
        ["DataGrid.Default"]             = "Default",
        ["DataGrid.MoveUp"]              = "Move up",
        ["DataGrid.MoveDown"]            = "Move down",
        ["DataGrid.Retry"]               = "Retry",
        ["DataGrid.ErrorLoadingData"]    = "Failed to load data: {0}",
        ["DataGrid.ExpandFullscreen"]    = "Expand to fullscreen",
        ["DataGrid.ExitFullscreen"]      = "Exit fullscreen",
        ["Filter.FilterTitle"]           = "Filter: {0}",

        // ── Pagination ──────────────────────────────────────────────
        ["Pagination.Previous"]          = "Previous",
        ["Pagination.Next"]              = "Next",
        ["Pagination.First"]             = "First",
        ["Pagination.Last"]              = "Last",
        ["Pagination.Page"]              = "Page",
        ["Pagination.MorePages"]         = "More pages",
        ["Pagination.GoToPage"]          = "Go to page {0}",
        ["Pagination.RowsPerPage"]       = "Rows per page",
        ["Pagination.RangeOfTotal"]      = "{0}–{1} of {2}",

        // ── Filter operators ────────────────────────────────────────
        ["Filter.Contains"]              = "contains",
        ["Filter.DoesNotContain"]        = "does not contain",
        ["Filter.Equals"]                = "equals",
        ["Filter.NotEquals"]             = "not equals",
        ["Filter.StartsWith"]            = "starts with",
        ["Filter.EndsWith"]              = "ends with",
        ["Filter.GreaterThan"]           = "greater than",
        ["Filter.LessThan"]              = "less than",
        ["Filter.GreaterThanOrEqual"]    = "greater or equal",
        ["Filter.LessThanOrEqual"]       = "less or equal",
        ["Filter.Between"]               = "between",
        ["Filter.IsEmpty"]               = "is empty",
        ["Filter.IsNotEmpty"]            = "is not empty",
        ["Filter.Apply"]                 = "Apply",
        ["Filter.Clear"]                 = "Clear",
        ["Filter.Value"]                 = "Value",
        ["Filter.SelectAll"]             = "All",
        ["Filter.ValuePlaceholder"]      = "Value…",
        ["Filter.ToValuePlaceholder"]    = "To value…",

        // ── ColorPicker ─────────────────────────────────────────────
        ["ColorPicker.PickColor"]        = "Pick a color",
        ["ColorPicker.Hue"]              = "Hue",
        ["ColorPicker.Saturation"]       = "Saturation",
        ["ColorPicker.Lightness"]        = "Lightness",
        ["ColorPicker.Value"]            = "Value",
        ["ColorPicker.Opacity"]          = "Opacity",
        ["ColorPicker.Hex"]              = "Hex",
        ["ColorPicker.HexValue"]         = "Hex value",
        ["ColorPicker.Red"]              = "Red",
        ["ColorPicker.Green"]            = "Green",
        ["ColorPicker.Blue"]             = "Blue",
        ["ColorPicker.Presets"]          = "Presets",

        // ── PasswordInput ───────────────────────────────────────────
        ["Password.Placeholder"]         = "Enter password",
        ["Password.Toggle"]              = "Toggle password visibility",
        ["Password.Weak"]                = "Weak",
        ["Password.Fair"]                = "Fair",
        ["Password.Good"]                = "Good",
        ["Password.Strong"]              = "Strong",

        // ── FileUpload ──────────────────────────────────────────────
        ["FileUpload.DragDrop"]          = "Drag & drop files here",
        ["FileUpload.Or"]                = "or",
        ["FileUpload.Browse"]            = "Browse",
        ["FileUpload.MaxSize"]           = "Max size: {0}",
        ["FileUpload.Accepted"]          = "Accepted: {0}",
        ["FileUpload.Remove"]            = "Remove",
        ["FileUpload.Uploading"]         = "Uploading…",
        ["FileUpload.Uploaded"]          = "Uploaded",
        ["FileUpload.Failed"]            = "Failed",
        ["FileUpload.Retry"]             = "Retry",
        ["FileUpload.TooLarge"]          = "File too large",
        ["FileUpload.TypeNotAllowed"]    = "File type not allowed",
        ["FileUpload.ChooseFile"]        = "Choose file",
        ["FileUpload.ClickToUpload"]     = "Click to upload or drag and drop",

        // ── Overlays & dialogs ──────────────────────────────────────
        ["Dialog.Close"]                 = "Close",
        ["Dialog.Confirm"]               = "Confirm",
        ["Dialog.Cancel"]                = "Cancel",
        ["Dialog.Ok"]                    = "OK",
        ["Dialog.Yes"]                   = "Yes",
        ["Dialog.No"]                    = "No",
        ["Toast.Close"]                  = "Close",
        ["Toast.Dismiss"]                = "Dismiss",
        ["AlertDialog.Delete"]           = "Delete",
        ["AlertDialog.Continue"]         = "Continue",

        // ── Combobox / Command / Select ─────────────────────────────
        ["Combobox.Placeholder"]         = "Select…",
        ["Combobox.SearchPlaceholder"]   = "Search…",
        ["Combobox.NoResults"]           = "No results found",
        ["Combobox.Loading"]             = "Loading…",
        ["Combobox.Clear"]               = "Clear",
        ["Combobox.Create"]              = "Create \"{0}\"",
        ["Command.Placeholder"]          = "Type a command or search…",
        ["Command.NoResults"]            = "No results found",
        ["Select.Placeholder"]           = "Select an option",

        // ── Calendar / DatePicker ───────────────────────────────────
        ["Calendar.Today"]               = "Today",
        ["Calendar.Clear"]               = "Clear",
        ["Calendar.PrevMonth"]           = "Previous month",
        ["Calendar.NextMonth"]           = "Next month",
        ["Calendar.PrevYear"]            = "Previous year",
        ["Calendar.NextYear"]            = "Next year",
        ["DatePicker.Placeholder"]       = "Pick a date",
        ["DateRange.Placeholder"]        = "Pick a date range",
        ["DateRange.From"]               = "From",
        ["DateRange.To"]                 = "To",
        ["DateTimePicker.Placeholder"]   = "Select date and time",
        ["DateTimePicker.TimeLabel"]     = "Time",
        ["TimePicker.Placeholder"]       = "Select time",

        // ── Tour ────────────────────────────────────────────────────
        ["Tour.Skip"]                    = "Skip",
        ["Tour.Previous"]                = "Previous",
        ["Tour.Next"]                    = "Next",
        ["Tour.Finish"]                  = "Finish",

        // ── PopConfirm ──────────────────────────────────────────────
        ["PopConfirm.Title"]             = "Are you sure?",
        ["PopConfirm.Confirm"]           = "Yes",
        ["PopConfirm.Cancel"]            = "No",

        // ── Misc ────────────────────────────────────────────────────
        ["Common.Search"]                = "Search",
        ["Common.Clear"]                 = "Clear",
        ["Common.ClearAll"]              = "Clear all",
        ["Common.Close"]                 = "Close",
        ["Common.Loading"]               = "Loading…",
        ["Common.NoResults"]             = "No results",
        ["Common.Apply"]                 = "Apply",
        ["Common.Reset"]                 = "Reset",
        ["Common.Save"]                  = "Save",
        ["Common.Cancel"]                = "Cancel",
        ["Common.Copy"]                  = "Copy",
        ["Common.Copied"]                = "Copied",
        ["Common.More"]                  = "More",
        ["Common.Back"]                  = "Back",
        ["Common.Next"]                  = "Next",
        ["Common.ShowMore"]              = "Show more",
        ["Common.ShowLess"]              = "Show less",

        // ── Transfer / TreeSelect / TagInput ────────────────────────
        ["Transfer.SourceHeader"]        = "Available",
        ["Transfer.TargetHeader"]        = "Selected",
        ["Transfer.MoveRight"]           = "Move right",
        ["Transfer.MoveLeft"]            = "Move left",
        ["Transfer.NoItems"]             = "No items",
        ["TreeSelect.Placeholder"]       = "Select…",
        ["TreeSelect.NoResults"]         = "No results",
        ["TagInput.Placeholder"]         = "Add tag…",

        // ── Cascader ────────────────────────────────────────────────
        ["Cascader.Placeholder"]         = "Select…",

        // ── Rating / OTP ────────────────────────────────────────────
        ["Rating.Rate"]                  = "Rate",
        ["Rating.RateOf"]                = "Rate {0} out of {1}",
        ["Otp.Placeholder"]              = "Enter code",

        // ── Empty state defaults ────────────────────────────────────
        ["Empty.Title"]                  = "Nothing here yet",
        ["Empty.Description"]            = "There's no data to display.",
    };

    internal static readonly IReadOnlyDictionary<string, string> De = new Dictionary<string, string>
    {
        // ── DataGrid ────────────────────────────────────────────────
        ["DataGrid.NoData"]              = "Keine Daten vorhanden",
        ["DataGrid.Loading"]             = "Wird geladen…",
        ["DataGrid.SearchPlaceholder"]   = "Suchen…",
        ["DataGrid.ClearSearch"]         = "Suche löschen",
        ["DataGrid.Columns"]             = "Spalten",
        ["DataGrid.ToggleColumns"]       = "Spalten ein-/ausblenden",
        ["DataGrid.ExportCsv"]           = "CSV exportieren",
        ["DataGrid.ExportExcel"]         = "Excel exportieren",
        ["DataGrid.ExportJson"]          = "JSON exportieren",
        ["DataGrid.Export"]              = "Exportieren",
        ["DataGrid.Filter"]              = "Filter",
        ["DataGrid.Filters"]             = "Filter",
        ["DataGrid.ClearFilters"]        = "Filter zurücksetzen",
        ["DataGrid.ResetLayout"]         = "Layout zurücksetzen",
        ["DataGrid.SaveLayout"]          = "Layout speichern",
        ["DataGrid.Layouts"]             = "Layouts",
        ["DataGrid.NewLayout"]           = "Neues Layout",
        ["DataGrid.Personal"]            = "Persönlich",
        ["DataGrid.Global"]              = "Global",
        ["DataGrid.SystemDefault"]       = "Systemstandard",
        ["DataGrid.LayoutName"]          = "Layoutname",
        ["DataGrid.Save"]                = "Speichern",
        ["DataGrid.Cancel"]              = "Abbrechen",
        ["DataGrid.Delete"]              = "Löschen",
        ["DataGrid.Rename"]              = "Umbenennen",
        ["DataGrid.PinLeft"]             = "Links anheften",
        ["DataGrid.PinRight"]            = "Rechts anheften",
        ["DataGrid.Unpin"]               = "Lösen",
        ["DataGrid.Hide"]                = "Ausblenden",
        ["DataGrid.Show"]                = "Einblenden",
        ["DataGrid.SortAscending"]       = "Aufsteigend sortieren",
        ["DataGrid.SortDescending"]      = "Absteigend sortieren",
        ["DataGrid.ClearSort"]           = "Sortierung entfernen",
        ["DataGrid.Edit"]                = "Bearbeiten",
        ["DataGrid.CommitEdit"]          = "Speichern",
        ["DataGrid.CancelEdit"]          = "Abbrechen",
        ["DataGrid.AggregateSum"]        = "Summe",
        ["DataGrid.AggregateAvg"]        = "Ø",
        ["DataGrid.AggregateCount"]      = "Anzahl",
        ["DataGrid.AggregateMin"]        = "Min",
        ["DataGrid.AggregateMax"]        = "Max",
        ["DataGrid.Items"]               = "Einträge",
        ["DataGrid.ItemsCount"]          = "{0} Einträge",
        ["DataGrid.CopySelected"]        = "Kopieren ({0})",
        ["DataGrid.ApplyLayout"]         = "Layout anwenden",
        ["DataGrid.NoSavedLayouts"]      = "Noch keine gespeicherten Layouts.",
        ["DataGrid.SaveCurrentLayout"]   = "Aktuelles Layout speichern…",
        ["DataGrid.Default"]             = "Standard",
        ["DataGrid.MoveUp"]              = "Nach oben",
        ["DataGrid.MoveDown"]            = "Nach unten",
        ["DataGrid.Retry"]               = "Erneut versuchen",
        ["DataGrid.ErrorLoadingData"]    = "Fehler beim Laden: {0}",
        ["DataGrid.ExpandFullscreen"]    = "Vollbild öffnen",
        ["DataGrid.ExitFullscreen"]      = "Vollbild schließen",
        ["Filter.FilterTitle"]           = "Filter: {0}",

        // ── Pagination ──────────────────────────────────────────────
        ["Pagination.Previous"]          = "Zurück",
        ["Pagination.Next"]              = "Weiter",
        ["Pagination.First"]             = "Erste",
        ["Pagination.Last"]              = "Letzte",
        ["Pagination.Page"]              = "Seite",
        ["Pagination.MorePages"]         = "Weitere Seiten",
        ["Pagination.GoToPage"]          = "Zu Seite {0}",
        ["Pagination.RowsPerPage"]       = "Zeilen pro Seite",
        ["Pagination.RangeOfTotal"]      = "{0}–{1} von {2}",

        // ── Filter operators ────────────────────────────────────────
        ["Filter.Contains"]              = "enthält",
        ["Filter.DoesNotContain"]        = "enthält nicht",
        ["Filter.Equals"]                = "gleich",
        ["Filter.NotEquals"]             = "ungleich",
        ["Filter.StartsWith"]            = "beginnt mit",
        ["Filter.EndsWith"]              = "endet mit",
        ["Filter.GreaterThan"]           = "größer als",
        ["Filter.LessThan"]              = "kleiner als",
        ["Filter.GreaterThanOrEqual"]    = "größer gleich",
        ["Filter.LessThanOrEqual"]       = "kleiner gleich",
        ["Filter.Between"]               = "zwischen",
        ["Filter.IsEmpty"]               = "ist leer",
        ["Filter.IsNotEmpty"]            = "ist nicht leer",
        ["Filter.Apply"]                 = "Anwenden",
        ["Filter.Clear"]                 = "Zurücksetzen",
        ["Filter.Value"]                 = "Wert",
        ["Filter.SelectAll"]             = "Alle",
        ["Filter.ValuePlaceholder"]      = "Wert…",
        ["Filter.ToValuePlaceholder"]    = "Bis Wert…",

        // ── ColorPicker ─────────────────────────────────────────────
        ["ColorPicker.PickColor"]        = "Farbe wählen",
        ["ColorPicker.Hue"]              = "Farbton",
        ["ColorPicker.Saturation"]       = "Sättigung",
        ["ColorPicker.Lightness"]        = "Helligkeit",
        ["ColorPicker.Value"]            = "Helligkeit",
        ["ColorPicker.Opacity"]          = "Deckkraft",
        ["ColorPicker.Hex"]              = "Hex",
        ["ColorPicker.HexValue"]         = "Hex-Wert",
        ["ColorPicker.Red"]              = "Rot",
        ["ColorPicker.Green"]            = "Grün",
        ["ColorPicker.Blue"]             = "Blau",
        ["ColorPicker.Presets"]          = "Vorlagen",

        // ── PasswordInput ───────────────────────────────────────────
        ["Password.Placeholder"]         = "Passwort eingeben",
        ["Password.Toggle"]              = "Passwort anzeigen/verbergen",
        ["Password.Weak"]                = "Schwach",
        ["Password.Fair"]                = "Ausreichend",
        ["Password.Good"]                = "Gut",
        ["Password.Strong"]              = "Stark",

        // ── FileUpload ──────────────────────────────────────────────
        ["FileUpload.DragDrop"]          = "Dateien hierher ziehen",
        ["FileUpload.Or"]                = "oder",
        ["FileUpload.Browse"]            = "Durchsuchen",
        ["FileUpload.MaxSize"]           = "Max. Größe: {0}",
        ["FileUpload.Accepted"]          = "Erlaubt: {0}",
        ["FileUpload.Remove"]            = "Entfernen",
        ["FileUpload.Uploading"]         = "Wird hochgeladen…",
        ["FileUpload.Uploaded"]          = "Hochgeladen",
        ["FileUpload.Failed"]            = "Fehlgeschlagen",
        ["FileUpload.Retry"]             = "Erneut versuchen",
        ["FileUpload.TooLarge"]          = "Datei zu groß",
        ["FileUpload.TypeNotAllowed"]    = "Dateityp nicht erlaubt",
        ["FileUpload.ChooseFile"]        = "Datei auswählen",
        ["FileUpload.ClickToUpload"]     = "Zum Hochladen klicken oder hierher ziehen",

        // ── Overlays & dialogs ──────────────────────────────────────
        ["Dialog.Close"]                 = "Schließen",
        ["Dialog.Confirm"]               = "Bestätigen",
        ["Dialog.Cancel"]                = "Abbrechen",
        ["Dialog.Ok"]                    = "OK",
        ["Dialog.Yes"]                   = "Ja",
        ["Dialog.No"]                    = "Nein",
        ["Toast.Close"]                  = "Schließen",
        ["Toast.Dismiss"]                = "Ausblenden",
        ["AlertDialog.Delete"]           = "Löschen",
        ["AlertDialog.Continue"]         = "Fortfahren",

        // ── Combobox / Command / Select ─────────────────────────────
        ["Combobox.Placeholder"]         = "Auswählen…",
        ["Combobox.SearchPlaceholder"]   = "Suchen…",
        ["Combobox.NoResults"]           = "Keine Ergebnisse",
        ["Combobox.Loading"]             = "Wird geladen…",
        ["Combobox.Clear"]               = "Zurücksetzen",
        ["Combobox.Create"]              = "\"{0}\" erstellen",
        ["Command.Placeholder"]          = "Befehl eingeben oder suchen…",
        ["Command.NoResults"]            = "Keine Ergebnisse",
        ["Select.Placeholder"]           = "Bitte auswählen",

        // ── Calendar / DatePicker ───────────────────────────────────
        ["Calendar.Today"]               = "Heute",
        ["Calendar.Clear"]               = "Zurücksetzen",
        ["Calendar.PrevMonth"]           = "Vorheriger Monat",
        ["Calendar.NextMonth"]           = "Nächster Monat",
        ["Calendar.PrevYear"]            = "Vorheriges Jahr",
        ["Calendar.NextYear"]            = "Nächstes Jahr",
        ["DatePicker.Placeholder"]       = "Datum wählen",
        ["DateRange.Placeholder"]        = "Zeitraum wählen",
        ["DateRange.From"]               = "Von",
        ["DateRange.To"]                 = "Bis",
        ["DateTimePicker.Placeholder"]   = "Datum und Uhrzeit wählen",
        ["DateTimePicker.TimeLabel"]     = "Uhrzeit",
        ["TimePicker.Placeholder"]       = "Uhrzeit wählen",

        // ── Tour ────────────────────────────────────────────────────
        ["Tour.Skip"]                    = "Überspringen",
        ["Tour.Previous"]                = "Zurück",
        ["Tour.Next"]                    = "Weiter",
        ["Tour.Finish"]                  = "Fertig",

        // ── PopConfirm ──────────────────────────────────────────────
        ["PopConfirm.Title"]             = "Sind Sie sicher?",
        ["PopConfirm.Confirm"]           = "Ja",
        ["PopConfirm.Cancel"]            = "Nein",

        // ── Misc ────────────────────────────────────────────────────
        ["Common.Search"]                = "Suchen",
        ["Common.Clear"]                 = "Zurücksetzen",
        ["Common.ClearAll"]              = "Alle zurücksetzen",
        ["Common.Close"]                 = "Schließen",
        ["Common.Loading"]               = "Wird geladen…",
        ["Common.NoResults"]             = "Keine Ergebnisse",
        ["Common.Apply"]                 = "Anwenden",
        ["Common.Reset"]                 = "Zurücksetzen",
        ["Common.Save"]                  = "Speichern",
        ["Common.Cancel"]                = "Abbrechen",
        ["Common.Copy"]                  = "Kopieren",
        ["Common.Copied"]                = "Kopiert",
        ["Common.More"]                  = "Mehr",
        ["Common.Back"]                  = "Zurück",
        ["Common.Next"]                  = "Weiter",
        ["Common.ShowMore"]              = "Mehr anzeigen",
        ["Common.ShowLess"]              = "Weniger anzeigen",

        // ── Transfer / TreeSelect / TagInput ────────────────────────
        ["Transfer.SourceHeader"]        = "Verfügbar",
        ["Transfer.TargetHeader"]        = "Ausgewählt",
        ["Transfer.MoveRight"]           = "Nach rechts verschieben",
        ["Transfer.MoveLeft"]            = "Nach links verschieben",
        ["Transfer.NoItems"]             = "Keine Einträge",
        ["TreeSelect.Placeholder"]       = "Auswählen…",
        ["TreeSelect.NoResults"]         = "Keine Ergebnisse",
        ["TagInput.Placeholder"]         = "Tag hinzufügen…",

        // ── Cascader ────────────────────────────────────────────────
        ["Cascader.Placeholder"]         = "Auswählen…",

        // ── Rating / OTP ────────────────────────────────────────────
        ["Rating.Rate"]                  = "Bewerten",
        ["Rating.RateOf"]                = "Bewertung {0} von {1}",
        ["Otp.Placeholder"]              = "Code eingeben",

        // ── Empty state defaults ────────────────────────────────────
        ["Empty.Title"]                  = "Noch nichts hier",
        ["Empty.Description"]            = "Es liegen keine Daten zum Anzeigen vor.",
    };

    internal static void ApplyDefaults(LumeoLocalizationOptions options)
    {
        options.AddMany("en", new Dictionary<string, string>(En));
        options.AddMany("de", new Dictionary<string, string>(De));
        options.AddMany("es", new Dictionary<string, string>(Es));
        options.AddMany("fr", new Dictionary<string, string>(Fr));
        options.AddMany("it", new Dictionary<string, string>(It));
        options.AddMany("pt", new Dictionary<string, string>(Pt));
        options.AddMany("nl", new Dictionary<string, string>(Nl));
        options.AddMany("pl", new Dictionary<string, string>(Pl));
        options.AddMany("ja", new Dictionary<string, string>(Ja));
        // Simplified Chinese — both the neutral "zh" and the region/script variants.
        options.AddMany("zh-Hans", new Dictionary<string, string>(ZhCn));
        options.AddMany("zh-CN", new Dictionary<string, string>(ZhCn));
        options.AddMany("zh", new Dictionary<string, string>(ZhCn));
        options.AddMany("ko", new Dictionary<string, string>(Ko));
        options.AddMany("ar", new Dictionary<string, string>(Ar));
        options.AddMany("ru", new Dictionary<string, string>(Ru));
        options.AddMany("tr", new Dictionary<string, string>(Tr));
    }
}
