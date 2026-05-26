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
        ["DataGrid.NoData"] = "No data available",
        ["DataGrid.Loading"] = "Loading…",
        ["DataGrid.SearchPlaceholder"] = "Search…",
        ["DataGrid.ClearSearch"] = "Clear search",
        ["DataGrid.Columns"] = "Columns",
        ["DataGrid.ToggleColumns"] = "Toggle columns",
        ["DataGrid.ExportCsv"] = "Export CSV",
        ["DataGrid.ExportExcel"] = "Export Excel",
        ["DataGrid.ExportJson"] = "Export JSON",
        ["DataGrid.Export"] = "Export",
        ["DataGrid.Filter"] = "Filter",
        ["DataGrid.Filters"] = "Filters",
        ["DataGrid.ClearFilters"] = "Clear filters",
        ["DataGrid.ResetLayout"] = "Reset layout",
        ["DataGrid.SaveLayout"] = "Save layout",
        ["DataGrid.Layouts"] = "Layouts",
        ["DataGrid.NewLayout"] = "New layout",
        ["DataGrid.Personal"] = "Personal",
        ["DataGrid.Global"] = "Global",
        ["DataGrid.SystemDefault"] = "System default",
        ["DataGrid.LayoutName"] = "Layout name",
        ["DataGrid.Save"] = "Save",
        ["DataGrid.Cancel"] = "Cancel",
        ["DataGrid.Delete"] = "Delete",
        ["DataGrid.Rename"] = "Rename",
        ["DataGrid.PinLeft"] = "Pin to left",
        ["DataGrid.PinRight"] = "Pin to right",
        ["DataGrid.Unpin"] = "Unpin",
        ["DataGrid.PinColumn"] = "Pin column",
        ["DataGrid.Hide"] = "Hide",
        ["DataGrid.Show"] = "Show",
        ["DataGrid.SortAscending"] = "Sort ascending",
        ["DataGrid.SortDescending"] = "Sort descending",
        ["DataGrid.ClearSort"] = "Clear sort",
        ["DataGrid.Edit"] = "Edit",
        ["DataGrid.CommitEdit"] = "Save",
        ["DataGrid.CancelEdit"] = "Cancel",
        ["DataGrid.AggregateSum"] = "Sum",
        ["DataGrid.AggregateAvg"] = "Avg",
        ["DataGrid.AggregateCount"] = "Count",
        ["DataGrid.AggregateRow"] = "Aggregate summary",
        ["DataGrid.AggregateMin"] = "Min",
        ["DataGrid.AggregateMax"] = "Max",
        ["DataGrid.Items"] = "items",
        ["DataGrid.ItemsCount"] = "{0} items",
        ["DataGrid.ItemsCount.One"] = "{0} item",
        ["DataGrid.ItemsCount.Other"] = "{0} items",
        ["DataGrid.CopySelected"] = "Copy ({0})",
        ["DataGrid.ApplyLayout"] = "Apply layout",
        ["DataGrid.NoSavedLayouts"] = "No saved layouts yet.",
        ["DataGrid.SaveCurrentLayout"] = "Save current layout…",
        ["DataGrid.Default"] = "Default",
        ["DataGrid.MoveUp"] = "Move up",
        ["DataGrid.MoveDown"] = "Move down",
        ["DataGrid.Retry"] = "Retry",
        ["DataGrid.ErrorLoadingData"] = "Failed to load data: {0}",
        ["DataGrid.ExpandFullscreen"] = "Expand to fullscreen",
        ["DataGrid.ExitFullscreen"] = "Exit fullscreen",
        ["DataGrid.AddGroupLevel"] = "+ Add group level",
        ["DataGrid.GroupPanelPlaceholder"] = "Drag a Groupable column header here, or use the dropdown",
        ["DataGrid.DragToGroup"] = "Drag to group by this column",
        ["DataGrid.RemoveGrouping"] = "Remove grouping",
        ["DataGrid.ClearAllGrouping"] = "Clear all grouping",
        ["Filter.FilterTitle"] = "Filter: {0}",

        // ── Pagination ──────────────────────────────────────────────
        ["Pagination.Previous"] = "Previous",
        ["Pagination.Next"] = "Next",
        ["Pagination.First"] = "First",
        ["Pagination.Last"] = "Last",
        ["Pagination.Page"] = "Page",
        ["Pagination.MorePages"] = "More pages",
        ["Pagination.GoToPage"] = "Go to page {0}",
        ["Pagination.RowsPerPage"] = "Rows per page",
        ["Pagination.RangeOfTotal"] = "{0}–{1} of {2}",

        // ── Filter operators ────────────────────────────────────────
        ["Filter.Contains"] = "contains",
        ["Filter.DoesNotContain"] = "does not contain",
        ["Filter.Equals"] = "equals",
        ["Filter.NotEquals"] = "not equals",
        ["Filter.StartsWith"] = "starts with",
        ["Filter.EndsWith"] = "ends with",
        ["Filter.GreaterThan"] = "greater than",
        ["Filter.LessThan"] = "less than",
        ["Filter.GreaterThanOrEqual"] = "greater or equal",
        ["Filter.LessThanOrEqual"] = "less or equal",
        ["Filter.Between"] = "between",
        ["Filter.IsEmpty"] = "is empty",
        ["Filter.IsNotEmpty"] = "is not empty",
        ["Filter.Apply"] = "Apply",
        ["Filter.Clear"] = "Clear",
        ["Filter.Value"] = "Value",
        ["Filter.SelectAll"] = "All",
        ["Filter.ValuePlaceholder"] = "Value…",
        ["Filter.ToValuePlaceholder"] = "To value…",

        // ── ColorPicker ─────────────────────────────────────────────
        ["ColorPicker.PickColor"] = "Pick a color",
        ["ColorPicker.Hue"] = "Hue",
        ["ColorPicker.Saturation"] = "Saturation",
        ["ColorPicker.Lightness"] = "Lightness",
        ["ColorPicker.Value"] = "Value",
        ["ColorPicker.Opacity"] = "Opacity",
        ["ColorPicker.Hex"] = "Hex",
        ["ColorPicker.HexValue"] = "Hex value",
        ["ColorPicker.Red"] = "Red",
        ["ColorPicker.Green"] = "Green",
        ["ColorPicker.Blue"] = "Blue",
        ["ColorPicker.Presets"] = "Presets",

        // ── PasswordInput ───────────────────────────────────────────
        ["Password.Placeholder"] = "Enter password",
        ["Password.Toggle"] = "Toggle password visibility",
        ["Password.Weak"] = "Weak",
        ["Password.Fair"] = "Fair",
        ["Password.Good"] = "Good",
        ["Password.Strong"] = "Strong",

        // ── FileUpload ──────────────────────────────────────────────
        ["FileUpload.DragDrop"] = "Drag & drop files here",
        ["FileUpload.Or"] = "or",
        ["FileUpload.Browse"] = "Browse",
        ["FileUpload.MaxSize"] = "Max size: {0}",
        ["FileUpload.Accepted"] = "Accepted: {0}",
        ["FileUpload.Remove"] = "Remove",
        ["FileUpload.Uploading"] = "Uploading…",
        ["FileUpload.Uploaded"] = "Uploaded",
        ["FileUpload.Failed"] = "Failed",
        ["FileUpload.Retry"] = "Retry",
        ["FileUpload.TooLarge"] = "File too large",
        ["FileUpload.TypeNotAllowed"] = "File type not allowed",
        ["FileUpload.ChooseFile"] = "Choose file",
        ["FileUpload.ClickToUpload"] = "Click to upload or drag and drop",

        // ── Overlays & dialogs ──────────────────────────────────────
        ["Dialog.Close"] = "Close",
        ["Dialog.Confirm"] = "Confirm",
        ["Dialog.Cancel"] = "Cancel",
        ["Dialog.Ok"] = "OK",
        ["Dialog.Yes"] = "Yes",
        ["Dialog.No"] = "No",
        ["Toast.Close"] = "Close",
        ["Toast.Dismiss"] = "Dismiss",
        ["AlertDialog.Delete"] = "Delete",
        ["AlertDialog.Continue"] = "Continue",

        // ── Combobox / Command / Select ─────────────────────────────
        ["Combobox.Placeholder"] = "Select…",
        ["Combobox.SearchPlaceholder"] = "Search…",
        ["Combobox.NoResults"] = "No results found",
        ["Combobox.Loading"] = "Loading…",
        ["Combobox.Clear"] = "Clear",
        ["Combobox.Create"] = "Create \"{0}\"",
        ["Command.Placeholder"] = "Type a command or search…",
        ["Command.NoResults"] = "No results found",
        ["Select.Placeholder"] = "Select an option",
        ["Select.ClearSelection"] = "Clear selection",

        // ── Calendar / DatePicker ───────────────────────────────────
        ["Calendar.Today"] = "Today",
        ["Calendar.Clear"] = "Clear",
        ["Calendar.PrevMonth"] = "Previous month",
        ["Calendar.NextMonth"] = "Next month",
        ["Calendar.PrevYear"] = "Previous year",
        ["Calendar.NextYear"] = "Next year",
        ["DatePicker.Placeholder"] = "Pick a date",
        ["DateRange.Placeholder"] = "Pick a date range",
        ["DateRange.From"] = "From",
        ["DateRange.To"] = "To",
        ["DateTimePicker.Placeholder"] = "Select date and time",
        ["DateTimePicker.TimeLabel"] = "Time",
        ["DateTimePicker.OpenCalendar"] = "Open calendar",
        ["DatePicker.OpenCalendar"] = "Open calendar",
        ["DatePicker.InvalidDate"] = "Invalid date",
        ["TimePicker.Placeholder"] = "Select time",

        // ── Tour ────────────────────────────────────────────────────
        ["Tour.Skip"] = "Skip",
        ["Tour.Previous"] = "Previous",
        ["Tour.Next"] = "Next",
        ["Tour.Finish"] = "Finish",

        // ── PopConfirm ──────────────────────────────────────────────
        ["PopConfirm.Title"] = "Are you sure?",
        ["PopConfirm.Confirm"] = "Yes",
        ["PopConfirm.Cancel"] = "No",

        // ── Misc ────────────────────────────────────────────────────
        ["Common.Search"] = "Search",
        ["Common.Clear"] = "Clear",
        ["Common.ClearAll"] = "Clear all",
        ["Common.Close"] = "Close",
        ["Common.Loading"] = "Loading…",
        ["Common.NoResults"] = "No results",
        ["Common.Apply"] = "Apply",
        ["Common.Reset"] = "Reset",
        ["Common.Save"] = "Save",
        ["Common.Cancel"] = "Cancel",
        ["Common.Copy"] = "Copy",
        ["Common.Copied"] = "Copied",
        ["Common.More"] = "More",
        ["Common.Back"] = "Back",
        ["Common.Next"] = "Next",
        ["Common.ShowMore"] = "Show more",
        ["Common.ShowLess"] = "Show less",
        ["Common.MoreOptions"] = "More options",
        ["Common.DragHandle"] = "Drag handle",
        ["Common.Actions"] = "Actions",
        ["Common.BackToTop"] = "Back to top",

        // ── Transfer / TreeSelect / TagInput ────────────────────────
        ["Transfer.SourceHeader"] = "Available",
        ["Transfer.TargetHeader"] = "Selected",
        ["Transfer.MoveRight"] = "Move right",
        ["Transfer.MoveLeft"] = "Move left",
        ["Transfer.NoItems"] = "No items",
        ["TreeSelect.Placeholder"] = "Select…",
        ["TreeSelect.NoResults"] = "No results",
        ["TagInput.Placeholder"] = "Add tag…",

        // ── Cascader ────────────────────────────────────────────────
        ["Cascader.Placeholder"] = "Select…",
        ["Cascader.ClearSelection"] = "Clear selection",

        // ── Rating / OTP ────────────────────────────────────────────
        ["Rating.Rate"] = "Rate",
        ["Rating.RateOf"] = "Rate {0} out of {1}",
        ["Otp.Placeholder"] = "Enter code",

        // ── Stepper ─────────────────────────────────────────────────
        ["Stepper.Back"] = "Back",
        ["Stepper.Next"] = "Next",
        ["Stepper.Finish"] = "Finish",
        ["Stepper.Optional"] = "Optional",
        ["Stepper.Skip"] = "Skip",

        // ── Window ──────────────────────────────────────────────────
        ["Window.Close"] = "Close",
        ["Window.Minimize"] = "Minimize",
        ["Window.Maximize"] = "Maximize",
        ["Window.Restore"] = "Restore",

        // ── Empty state defaults ────────────────────────────────────
        ["Empty.Title"] = "Nothing here yet",
        ["Empty.Description"] = "There's no data to display.",

        // ── Kanban ──────────────────────────────────────────────────────
        ["Kanban.AddCard"] = "Add card",

        // ── Carousel ────────────────────────────────────────────────────
        ["Carousel.PreviousSlide"] = "Previous slide",
        ["Carousel.NextSlide"] = "Next slide",
        ["Carousel.SlideXofY"] = "Slide {0} of {1}",

        // ── NumberInput ─────────────────────────────────────────────
        ["NumberInput.Decrease"] = "Decrease",
        ["NumberInput.Increase"] = "Increase",

        // ── DateTimePicker ──────────────────────────────────────────
        ["DateTimePicker.ClearDate"] = "Clear date",

        // ── Slider ──────────────────────────────────────────────────
        ["Slider.End"] = "end",

        // ── FileManager ─────────────────────────────────────────────
        ["FileManager.EmptyTitle"] = "This folder is empty",
        ["FileManager.EmptyState"] = "No files or folders here.",
        ["FileManager.MoreActions"] = "More actions",
        ["FileManager.MoreActionsForName"] = "More actions for {0}",
        ["FileManager.NavigateUp"] = "Navigate up",
        ["FileManager.UpOneLevel"] = "Up one level",
        ["FileManager.Path"] = "File manager path",
        ["FileManager.Root"] = "Root",
        ["FileManager.NewFolder"] = "New folder",
        ["FileManager.DeleteSelected"] = "Delete selected",
        ["FileManager.ListView"] = "List view",
        ["FileManager.GridView"] = "Grid view",
        ["FileManager.SwitchListView"] = "Switch to list view",
        ["FileManager.SwitchGridView"] = "Switch to grid view",
        ["FileManager.FolderTree"] = "Folder tree",
        ["FileManager.FileActions"] = "File actions",

        // ── Chart ───────────────────────────────────────────────────
        ["Chart.Loading"] = "Loading…",

        // ── BottomNav ───────────────────────────────────────────────
        ["BottomNav.Label"] = "Bottom navigation",
        ["BottomNav.PrimaryAction"] = "Primary action",

        // ── Badge ───────────────────────────────────────────────────
        ["Badge.Remove"] = "Remove",

        // ── Dock ────────────────────────────────────────────────────
        ["Dock.ApplicationDock"] = "Application Dock",

        // ── QueryBuilder ────────────────────────────────────────────
        ["QueryBuilder.QueryGroup"] = "Query group",
        ["QueryBuilder.NestedQueryGroup"] = "Nested query group",
        ["QueryBuilder.Combinator"] = "Combinator",
        ["QueryBuilder.AddRule"] = "Add rule",
        ["QueryBuilder.AddGroup"] = "Add group",
        ["QueryBuilder.RemoveGroup"] = "Remove group",
        ["QueryBuilder.RemoveRule"] = "Remove rule",
        ["QueryBuilder.Field"] = "Field",
        ["QueryBuilder.Operator"] = "Operator",

        // ── PickList ────────────────────────────────────────────────
        ["PickList.MoveAll"] = "Move all",
        ["PickList.MoveSelected"] = "Move selected",
        ["PickList.MoveBackSelected"] = "Move back selected",
        ["PickList.MoveBackAll"] = "Move back all",

        // ── Navigation ──────────────────────────────────────────────
        ["Navigation.Toggle"] = "Toggle navigation",

        // ── Sidebar ─────────────────────────────────────────────────
        ["Sidebar.Toggle"] = "Toggle sidebar",

        // ── Editor ──────────────────────────────────────────────────
        ["Editor.Suggestions"] = "Suggestions",
        ["Editor.Paragraph"] = "Paragraph",
        ["Editor.Heading1"] = "Heading 1",
        ["Editor.Heading2"] = "Heading 2",
        ["Editor.Heading3"] = "Heading 3",
        ["Editor.Bold"] = "Bold",
        ["Editor.Italic"] = "Italic",
        ["Editor.Underline"] = "Underline",
        ["Editor.Strikethrough"] = "Strikethrough",
        ["Editor.InlineCode"] = "Inline code",
        ["Editor.BulletList"] = "Bullet list",
        ["Editor.NumberedList"] = "Numbered list",
        ["Editor.TaskList"] = "Task list",
        ["Editor.Quote"] = "Quote",
        ["Editor.CodeBlock"] = "Code block",
        ["Editor.HorizontalRule"] = "Horizontal rule",
        ["Editor.Link"] = "Link",
        ["Editor.Unlink"] = "Unlink",
        ["Editor.InsertTable"] = "Insert table",
        ["Editor.InsertImage"] = "Insert image",
        ["Editor.ImportDocx"] = "Import .docx",
        ["Editor.AiActions"] = "AI actions",
        ["Editor.Undo"] = "Undo",
        ["Editor.Redo"] = "Redo",
        ["Editor.ClearFormatting"] = "Clear formatting",
        ["Editor.FormatSelection"] = "Format selection",
        ["Editor.NoResults"] = "No results",
        ["Editor.InsertLinkTitle"] = "Insert link",
        ["Editor.InsertLinkDescription"] = "Paste or type a URL to link the selected text to.",
        ["Editor.Cancel"] = "Cancel",
        ["Editor.Apply"] = "Apply",
        ["Editor.AiImproveWriting"] = "Improve writing",
        ["Editor.AiMakeShorter"] = "Make shorter",
        ["Editor.AiMakeLonger"] = "Make longer",
        ["Editor.AiTranslate"] = "Translate",
        ["Editor.AiSummarize"] = "Summarize",
        ["Editor.AiFixGrammar"] = "Fix grammar",
        ["Editor.SlashHeading1"] = "Heading 1",
        ["Editor.SlashHeading1Sub"] = "Large section heading",
        ["Editor.SlashHeading2"] = "Heading 2",
        ["Editor.SlashHeading2Sub"] = "Medium section heading",
        ["Editor.SlashHeading3"] = "Heading 3",
        ["Editor.SlashHeading3Sub"] = "Small section heading",
        ["Editor.SlashBulletList"] = "Bullet list",
        ["Editor.SlashBulletListSub"] = "Simple bulleted list",
        ["Editor.SlashNumberedList"] = "Numbered list",
        ["Editor.SlashNumberedListSub"] = "Ordered list",
        ["Editor.SlashTaskList"] = "Task list",
        ["Editor.SlashTaskListSub"] = "Check items off as you complete them",
        ["Editor.SlashQuote"] = "Quote",
        ["Editor.SlashQuoteSub"] = "Block quotation",
        ["Editor.SlashCodeBlock"] = "Code block",
        ["Editor.SlashCodeBlockSub"] = "Syntax-highlighted code",
        ["Editor.SlashTable"] = "Table",
        ["Editor.SlashTableSub"] = "Insert a 3×3 table",
        ["Editor.SlashImage"] = "Image",
        ["Editor.SlashImageSub"] = "Upload from device",
        ["Editor.SlashDivider"] = "Divider",
        ["Editor.SlashDividerSub"] = "Horizontal line",
        ["Editor.ToolbarAriaLabel"] = "Text formatting",

        // ── FileUpload (parameterised) ──────────────────────────────
        ["FileUpload.ExceedsMaxSize"] = "File \"{0}\" exceeds the maximum size of {1}.",

        // ── Scheduler ───────────────────────────────────────────────
        ["Scheduler.Previous"] = "Previous",
        ["Scheduler.Next"] = "Next",
        ["Scheduler.ResourceLegend"] = "Resource legend",
        ["Scheduler.Day"] = "Day",
        ["Scheduler.Week"] = "Week",
        ["Scheduler.Month"] = "Month",
        ["Scheduler.List"] = "List",
        ["Scheduler.Today"] = "Today",
        ["Scheduler.WeekOf"] = "Week of",

        // ── Gantt ───────────────────────────────────────────────────
        ["Gantt.Day"] = "Day",
        ["Gantt.Week"] = "Week",
        ["Gantt.Month"] = "Month",
        ["Gantt.Year"] = "Year",

        // ── Theme ───────────────────────────────────────────────────
        ["Theme.Light"] = "Light",
        ["Theme.Dark"] = "Dark",
        ["Theme.System"] = "System",
        ["Theme.Toggle"] = "Toggle theme",

        // ── Tabs ────────────────────────────────────────────────────
        ["Tabs.CloseTab"] = "Close tab",

        // ── Chip ────────────────────────────────────────────────────
        ["Chip.Remove"] = "Remove",

        // ── Alert ───────────────────────────────────────────────────
        ["Alert.Dismiss"] = "Dismiss",

        // ── ImageCompare ────────────────────────────────────────────
        ["ImageCompare.Slider"] = "Image compare slider",

        // ── PromptInput ─────────────────────────────────────────────
        ["PromptInput.Send"] = "Send",

        // ── Editor (RichTextEditor) ─────────────────────────────────
        ["Editor.StartWriting"] = "Start writing…",

        // ── PivotGrid ───────────────────────────────────────────────
        ["PivotGrid.GrandTotal"] = "Grand Total",
    };

    internal static readonly IReadOnlyDictionary<string, string> De = new Dictionary<string, string>
    {
        // ── DataGrid ────────────────────────────────────────────────
        ["DataGrid.NoData"] = "Keine Daten vorhanden",
        ["DataGrid.Loading"] = "Wird geladen…",
        ["DataGrid.SearchPlaceholder"] = "Suchen…",
        ["DataGrid.ClearSearch"] = "Suche löschen",
        ["DataGrid.Columns"] = "Spalten",
        ["DataGrid.ToggleColumns"] = "Spalten ein-/ausblenden",
        ["DataGrid.ExportCsv"] = "CSV exportieren",
        ["DataGrid.ExportExcel"] = "Excel exportieren",
        ["DataGrid.ExportJson"] = "JSON exportieren",
        ["DataGrid.Export"] = "Exportieren",
        ["DataGrid.Filter"] = "Filter",
        ["DataGrid.Filters"] = "Filter",
        ["DataGrid.ClearFilters"] = "Filter zurücksetzen",
        ["DataGrid.ResetLayout"] = "Layout zurücksetzen",
        ["DataGrid.SaveLayout"] = "Layout speichern",
        ["DataGrid.Layouts"] = "Layouts",
        ["DataGrid.NewLayout"] = "Neues Layout",
        ["DataGrid.Personal"] = "Persönlich",
        ["DataGrid.Global"] = "Global",
        ["DataGrid.SystemDefault"] = "Systemstandard",
        ["DataGrid.LayoutName"] = "Layoutname",
        ["DataGrid.Save"] = "Speichern",
        ["DataGrid.Cancel"] = "Abbrechen",
        ["DataGrid.Delete"] = "Löschen",
        ["DataGrid.Rename"] = "Umbenennen",
        ["DataGrid.PinLeft"] = "Links anheften",
        ["DataGrid.PinRight"] = "Rechts anheften",
        ["DataGrid.Unpin"] = "Lösen",
        ["DataGrid.PinColumn"] = "Spalte anheften",
        ["DataGrid.Hide"] = "Ausblenden",
        ["DataGrid.Show"] = "Einblenden",
        ["DataGrid.SortAscending"] = "Aufsteigend sortieren",
        ["DataGrid.SortDescending"] = "Absteigend sortieren",
        ["DataGrid.ClearSort"] = "Sortierung entfernen",
        ["DataGrid.Edit"] = "Bearbeiten",
        ["DataGrid.CommitEdit"] = "Speichern",
        ["DataGrid.CancelEdit"] = "Abbrechen",
        ["DataGrid.AggregateSum"] = "Summe",
        ["DataGrid.AggregateAvg"] = "Ø",
        ["DataGrid.AggregateRow"] = "Aggregatzeile",
        ["DataGrid.AggregateCount"] = "Anzahl",
        ["DataGrid.AggregateMin"] = "Min",
        ["DataGrid.AggregateMax"] = "Max",
        ["DataGrid.Items"] = "Einträge",
        ["DataGrid.ItemsCount"] = "{0} Einträge",
        ["DataGrid.ItemsCount.One"] = "{0} Eintrag",
        ["DataGrid.ItemsCount.Other"] = "{0} Einträge",
        ["DataGrid.CopySelected"] = "Kopieren ({0})",
        ["DataGrid.ApplyLayout"] = "Layout anwenden",
        ["DataGrid.NoSavedLayouts"] = "Noch keine gespeicherten Layouts.",
        ["DataGrid.SaveCurrentLayout"] = "Aktuelles Layout speichern…",
        ["DataGrid.Default"] = "Standard",
        ["DataGrid.MoveUp"] = "Nach oben",
        ["DataGrid.MoveDown"] = "Nach unten",
        ["DataGrid.Retry"] = "Erneut versuchen",
        ["DataGrid.ErrorLoadingData"] = "Fehler beim Laden: {0}",
        ["DataGrid.ExpandFullscreen"] = "Vollbild öffnen",
        ["DataGrid.AddGroupLevel"] = "+ Gruppe hinzufügen",
        ["DataGrid.GroupPanelPlaceholder"] = "Spaltenkopf hierher ziehen oder Dropdown verwenden",
        ["DataGrid.DragToGroup"] = "Zum Gruppieren nach dieser Spalte ziehen",
        ["DataGrid.ExitFullscreen"] = "Vollbild schließen",
        ["DataGrid.RemoveGrouping"] = "Gruppierung entfernen",
        ["DataGrid.ClearAllGrouping"] = "Alle Gruppierungen entfernen",
        ["Filter.FilterTitle"] = "Filter: {0}",

        // ── Pagination ──────────────────────────────────────────────
        ["Pagination.Previous"] = "Zurück",
        ["Pagination.Next"] = "Weiter",
        ["Pagination.First"] = "Erste",
        ["Pagination.Last"] = "Letzte",
        ["Pagination.Page"] = "Seite",
        ["Pagination.MorePages"] = "Weitere Seiten",
        ["Pagination.GoToPage"] = "Zu Seite {0}",
        ["Pagination.RowsPerPage"] = "Zeilen pro Seite",
        ["Pagination.RangeOfTotal"] = "{0}–{1} von {2}",

        // ── Filter operators ────────────────────────────────────────
        ["Filter.Contains"] = "enthält",
        ["Filter.DoesNotContain"] = "enthält nicht",
        ["Filter.Equals"] = "gleich",
        ["Filter.NotEquals"] = "ungleich",
        ["Filter.StartsWith"] = "beginnt mit",
        ["Filter.EndsWith"] = "endet mit",
        ["Filter.GreaterThan"] = "größer als",
        ["Filter.LessThan"] = "kleiner als",
        ["Filter.GreaterThanOrEqual"] = "größer gleich",
        ["Filter.LessThanOrEqual"] = "kleiner gleich",
        ["Filter.Between"] = "zwischen",
        ["Filter.IsEmpty"] = "ist leer",
        ["Filter.IsNotEmpty"] = "ist nicht leer",
        ["Filter.Apply"] = "Anwenden",
        ["Filter.Clear"] = "Zurücksetzen",
        ["Filter.Value"] = "Wert",
        ["Filter.SelectAll"] = "Alle",
        ["Filter.ValuePlaceholder"] = "Wert…",
        ["Filter.ToValuePlaceholder"] = "Bis Wert…",

        // ── ColorPicker ─────────────────────────────────────────────
        ["ColorPicker.PickColor"] = "Farbe wählen",
        ["ColorPicker.Hue"] = "Farbton",
        ["ColorPicker.Saturation"] = "Sättigung",
        ["ColorPicker.Lightness"] = "Helligkeit",
        ["ColorPicker.Value"] = "Helligkeit",
        ["ColorPicker.Opacity"] = "Deckkraft",
        ["ColorPicker.Hex"] = "Hex",
        ["ColorPicker.HexValue"] = "Hex-Wert",
        ["ColorPicker.Red"] = "Rot",
        ["ColorPicker.Green"] = "Grün",
        ["ColorPicker.Blue"] = "Blau",
        ["ColorPicker.Presets"] = "Vorlagen",

        // ── PasswordInput ───────────────────────────────────────────
        ["Password.Placeholder"] = "Passwort eingeben",
        ["Password.Toggle"] = "Passwort anzeigen/verbergen",
        ["Password.Weak"] = "Schwach",
        ["Password.Fair"] = "Ausreichend",
        ["Password.Good"] = "Gut",
        ["Password.Strong"] = "Stark",

        // ── FileUpload ──────────────────────────────────────────────
        ["FileUpload.DragDrop"] = "Dateien hierher ziehen",
        ["FileUpload.Or"] = "oder",
        ["FileUpload.Browse"] = "Durchsuchen",
        ["FileUpload.MaxSize"] = "Max. Größe: {0}",
        ["FileUpload.Accepted"] = "Erlaubt: {0}",
        ["FileUpload.Remove"] = "Entfernen",
        ["FileUpload.Uploading"] = "Wird hochgeladen…",
        ["FileUpload.Uploaded"] = "Hochgeladen",
        ["FileUpload.Failed"] = "Fehlgeschlagen",
        ["FileUpload.Retry"] = "Erneut versuchen",
        ["FileUpload.TooLarge"] = "Datei zu groß",
        ["FileUpload.TypeNotAllowed"] = "Dateityp nicht erlaubt",
        ["FileUpload.ChooseFile"] = "Datei auswählen",
        ["FileUpload.ClickToUpload"] = "Zum Hochladen klicken oder hierher ziehen",

        // ── Overlays & dialogs ──────────────────────────────────────
        ["Dialog.Close"] = "Schließen",
        ["Dialog.Confirm"] = "Bestätigen",
        ["Dialog.Cancel"] = "Abbrechen",
        ["Dialog.Ok"] = "OK",
        ["Dialog.Yes"] = "Ja",
        ["Dialog.No"] = "Nein",
        ["Toast.Close"] = "Schließen",
        ["Toast.Dismiss"] = "Ausblenden",
        ["AlertDialog.Delete"] = "Löschen",
        ["AlertDialog.Continue"] = "Fortfahren",

        // ── Combobox / Command / Select ─────────────────────────────
        ["Combobox.Placeholder"] = "Auswählen…",
        ["Combobox.SearchPlaceholder"] = "Suchen…",
        ["Combobox.NoResults"] = "Keine Ergebnisse",
        ["Combobox.Loading"] = "Wird geladen…",
        ["Combobox.Clear"] = "Zurücksetzen",
        ["Combobox.Create"] = "\"{0}\" erstellen",
        ["Command.Placeholder"] = "Befehl eingeben oder suchen…",
        ["Command.NoResults"] = "Keine Ergebnisse",
        ["Select.Placeholder"] = "Bitte auswählen",
        ["Select.ClearSelection"] = "Auswahl leeren",

        // ── Calendar / DatePicker ───────────────────────────────────
        ["Calendar.Today"] = "Heute",
        ["Calendar.Clear"] = "Zurücksetzen",
        ["Calendar.PrevMonth"] = "Vorheriger Monat",
        ["Calendar.NextMonth"] = "Nächster Monat",
        ["Calendar.PrevYear"] = "Vorheriges Jahr",
        ["Calendar.NextYear"] = "Nächstes Jahr",
        ["DatePicker.Placeholder"] = "Datum wählen",
        ["DateRange.Placeholder"] = "Zeitraum wählen",
        ["DateRange.From"] = "Von",
        ["DateRange.To"] = "Bis",
        ["DateTimePicker.Placeholder"] = "Datum und Uhrzeit wählen",
        ["DateTimePicker.TimeLabel"] = "Uhrzeit",
        ["DateTimePicker.OpenCalendar"] = "Kalender öffnen",
        ["DatePicker.OpenCalendar"] = "Kalender öffnen",
        ["DatePicker.InvalidDate"] = "Ungültiges Datum",
        ["TimePicker.Placeholder"] = "Uhrzeit wählen",

        // ── Tour ────────────────────────────────────────────────────
        ["Tour.Skip"] = "Überspringen",
        ["Tour.Previous"] = "Zurück",
        ["Tour.Next"] = "Weiter",
        ["Tour.Finish"] = "Fertig",

        // ── PopConfirm ──────────────────────────────────────────────
        ["PopConfirm.Title"] = "Sind Sie sicher?",
        ["PopConfirm.Confirm"] = "Ja",
        ["PopConfirm.Cancel"] = "Nein",

        // ── Misc ────────────────────────────────────────────────────
        ["Common.Search"] = "Suchen",
        ["Common.Clear"] = "Zurücksetzen",
        ["Common.ClearAll"] = "Alle zurücksetzen",
        ["Common.Close"] = "Schließen",
        ["Common.Loading"] = "Wird geladen…",
        ["Common.NoResults"] = "Keine Ergebnisse",
        ["Common.Apply"] = "Anwenden",
        ["Common.Reset"] = "Zurücksetzen",
        ["Common.Save"] = "Speichern",
        ["Common.Cancel"] = "Abbrechen",
        ["Common.Copy"] = "Kopieren",
        ["Common.Copied"] = "Kopiert",
        ["Common.More"] = "Mehr",
        ["Common.Back"] = "Zurück",
        ["Common.Next"] = "Weiter",
        ["Common.ShowMore"] = "Mehr anzeigen",
        ["Common.ShowLess"] = "Weniger anzeigen",
        ["Common.MoreOptions"] = "Weitere Optionen",
        ["Common.DragHandle"] = "Ziehgriff",
        ["Common.Actions"] = "Aktionen",
        ["Common.BackToTop"] = "Nach oben",

        // ── Transfer / TreeSelect / TagInput ────────────────────────
        ["Transfer.SourceHeader"] = "Verfügbar",
        ["Transfer.TargetHeader"] = "Ausgewählt",
        ["Transfer.MoveRight"] = "Nach rechts verschieben",
        ["Transfer.MoveLeft"] = "Nach links verschieben",
        ["Transfer.NoItems"] = "Keine Einträge",
        ["TreeSelect.Placeholder"] = "Auswählen…",
        ["TreeSelect.NoResults"] = "Keine Ergebnisse",
        ["TagInput.Placeholder"] = "Tag hinzufügen…",

        // ── Cascader ────────────────────────────────────────────────
        ["Cascader.Placeholder"] = "Auswählen…",
        ["Cascader.ClearSelection"] = "Auswahl löschen",

        // ── Rating / OTP ────────────────────────────────────────────
        ["Rating.Rate"] = "Bewerten",
        ["Rating.RateOf"] = "Bewertung {0} von {1}",
        ["Otp.Placeholder"] = "Code eingeben",

        // ── Empty state defaults ────────────────────────────────────
        ["Empty.Title"] = "Noch nichts hier",
        ["Empty.Description"] = "Es liegen keine Daten zum Anzeigen vor.",

        // ── Kanban ──────────────────────────────────────────────────────
        ["Kanban.AddCard"] = "Karte hinzufügen",

        // ── Carousel ────────────────────────────────────────────────────
        ["Carousel.PreviousSlide"] = "Vorherige Folie",
        ["Carousel.NextSlide"] = "Nächste Folie",
        ["Carousel.SlideXofY"] = "Folie {0} von {1}",

        // ── Stepper ─────────────────────────────────────────────────
        ["Stepper.Back"] = "Zurück",
        ["Stepper.Next"] = "Weiter",
        ["Stepper.Finish"] = "Fertigstellen",
        ["Stepper.Optional"] = "Optional",
        ["Stepper.Skip"] = "Überspringen",

        // ── Window ──────────────────────────────────────────────────
        ["Window.Close"] = "Schließen",
        ["Window.Minimize"] = "Minimieren",
        ["Window.Maximize"] = "Maximieren",
        ["Window.Restore"] = "Wiederherstellen",

        // ── NumberInput ─────────────────────────────────────────────
        ["NumberInput.Decrease"] = "Verringern",
        ["NumberInput.Increase"] = "Erhöhen",

        // ── DateTimePicker ──────────────────────────────────────────
        ["DateTimePicker.ClearDate"] = "Datum löschen",

        // ── Slider ──────────────────────────────────────────────────
        ["Slider.End"] = "Ende",

        // ── FileManager ─────────────────────────────────────────────
        ["FileManager.EmptyTitle"] = "Dieser Ordner ist leer",
        ["FileManager.EmptyState"] = "Keine Dateien oder Ordner vorhanden.",
        ["FileManager.MoreActions"] = "Weitere Aktionen",
        ["FileManager.MoreActionsForName"] = "Weitere Aktionen für {0}",
        ["FileManager.NavigateUp"] = "Nach oben navigieren",
        ["FileManager.UpOneLevel"] = "Eine Ebene nach oben",
        ["FileManager.Path"] = "Dateimanager-Pfad",
        ["FileManager.Root"] = "Stamm",
        ["FileManager.NewFolder"] = "Neuer Ordner",
        ["FileManager.DeleteSelected"] = "Auswahl löschen",
        ["FileManager.ListView"] = "Listenansicht",
        ["FileManager.GridView"] = "Rasteransicht",
        ["FileManager.SwitchListView"] = "Zur Listenansicht wechseln",
        ["FileManager.SwitchGridView"] = "Zur Rasteransicht wechseln",
        ["FileManager.FolderTree"] = "Ordnerbaum",
        ["FileManager.FileActions"] = "Dateiaktionen",

        // ── Chart ───────────────────────────────────────────────────
        ["Chart.Loading"] = "Wird geladen…",

        // ── BottomNav ───────────────────────────────────────────────
        ["BottomNav.Label"] = "Untere Navigation",
        ["BottomNav.PrimaryAction"] = "Primäre Aktion",

        // ── Badge ───────────────────────────────────────────────────
        ["Badge.Remove"] = "Entfernen",

        // ── Dock ────────────────────────────────────────────────────
        ["Dock.ApplicationDock"] = "Anwendungs-Dock",

        // ── QueryBuilder ────────────────────────────────────────────
        ["QueryBuilder.QueryGroup"] = "Abfragegruppe",
        ["QueryBuilder.NestedQueryGroup"] = "Verschachtelte Abfragegruppe",
        ["QueryBuilder.Combinator"] = "Verknüpfung",
        ["QueryBuilder.AddRule"] = "Regel hinzufügen",
        ["QueryBuilder.AddGroup"] = "Gruppe hinzufügen",
        ["QueryBuilder.RemoveGroup"] = "Gruppe entfernen",
        ["QueryBuilder.RemoveRule"] = "Regel entfernen",
        ["QueryBuilder.Field"] = "Feld",
        ["QueryBuilder.Operator"] = "Operator",

        // ── PickList ────────────────────────────────────────────────
        ["PickList.MoveAll"] = "Alle verschieben",
        ["PickList.MoveSelected"] = "Auswahl verschieben",
        ["PickList.MoveBackSelected"] = "Auswahl zurück verschieben",
        ["PickList.MoveBackAll"] = "Alle zurück verschieben",

        // ── Navigation ──────────────────────────────────────────────
        ["Navigation.Toggle"] = "Navigation ein-/ausblenden",

        // ── Sidebar ─────────────────────────────────────────────────
        ["Sidebar.Toggle"] = "Seitenleiste ein-/ausblenden",

        // ── Editor ──────────────────────────────────────────────────
        ["Editor.Suggestions"] = "Vorschläge",
        ["Editor.Paragraph"] = "Absatz",
        ["Editor.Heading1"] = "Überschrift 1",
        ["Editor.Heading2"] = "Überschrift 2",
        ["Editor.Heading3"] = "Überschrift 3",
        ["Editor.Bold"] = "Fett",
        ["Editor.Italic"] = "Kursiv",
        ["Editor.Underline"] = "Unterstrichen",
        ["Editor.Strikethrough"] = "Durchgestrichen",
        ["Editor.InlineCode"] = "Inline-Code",
        ["Editor.BulletList"] = "Aufzählung",
        ["Editor.NumberedList"] = "Nummerierte Liste",
        ["Editor.TaskList"] = "Aufgabenliste",
        ["Editor.Quote"] = "Zitat",
        ["Editor.CodeBlock"] = "Codeblock",
        ["Editor.HorizontalRule"] = "Horizontale Linie",
        ["Editor.Link"] = "Link",
        ["Editor.Unlink"] = "Link entfernen",
        ["Editor.InsertTable"] = "Tabelle einfügen",
        ["Editor.InsertImage"] = "Bild einfügen",
        ["Editor.ImportDocx"] = ".docx importieren",
        ["Editor.AiActions"] = "KI-Aktionen",
        ["Editor.Undo"] = "Rückgängig",
        ["Editor.Redo"] = "Wiederherstellen",
        ["Editor.ClearFormatting"] = "Formatierung entfernen",
        ["Editor.FormatSelection"] = "Auswahl formatieren",
        ["Editor.NoResults"] = "Keine Ergebnisse",
        ["Editor.InsertLinkTitle"] = "Link einfügen",
        ["Editor.InsertLinkDescription"] = "URL einfügen oder eingeben, um den ausgewählten Text zu verlinken.",
        ["Editor.Cancel"] = "Abbrechen",
        ["Editor.Apply"] = "Anwenden",
        ["Editor.AiImproveWriting"] = "Schreibstil verbessern",
        ["Editor.AiMakeShorter"] = "Kürzen",
        ["Editor.AiMakeLonger"] = "Verlängern",
        ["Editor.AiTranslate"] = "Übersetzen",
        ["Editor.AiSummarize"] = "Zusammenfassen",
        ["Editor.AiFixGrammar"] = "Grammatik korrigieren",
        ["Editor.SlashHeading1"] = "Überschrift 1",
        ["Editor.SlashHeading1Sub"] = "Große Abschnittsüberschrift",
        ["Editor.SlashHeading2"] = "Überschrift 2",
        ["Editor.SlashHeading2Sub"] = "Mittlere Abschnittsüberschrift",
        ["Editor.SlashHeading3"] = "Überschrift 3",
        ["Editor.SlashHeading3Sub"] = "Kleine Abschnittsüberschrift",
        ["Editor.SlashBulletList"] = "Aufzählung",
        ["Editor.SlashBulletListSub"] = "Einfache Liste mit Punkten",
        ["Editor.SlashNumberedList"] = "Nummerierte Liste",
        ["Editor.SlashNumberedListSub"] = "Geordnete Liste",
        ["Editor.SlashTaskList"] = "Aufgabenliste",
        ["Editor.SlashTaskListSub"] = "Aufgaben beim Erledigen abhaken",
        ["Editor.SlashQuote"] = "Zitat",
        ["Editor.SlashQuoteSub"] = "Blockzitat",
        ["Editor.SlashCodeBlock"] = "Codeblock",
        ["Editor.SlashCodeBlockSub"] = "Code mit Syntaxhervorhebung",
        ["Editor.SlashTable"] = "Tabelle",
        ["Editor.SlashTableSub"] = "Eine 3×3-Tabelle einfügen",
        ["Editor.SlashImage"] = "Bild",
        ["Editor.SlashImageSub"] = "Vom Gerät hochladen",
        ["Editor.SlashDivider"] = "Trennlinie",
        ["Editor.SlashDividerSub"] = "Horizontale Linie",
        ["Editor.ToolbarAriaLabel"] = "Textformatierung",

        // ── FileUpload (parameterised) ──────────────────────────────
        ["FileUpload.ExceedsMaxSize"] = "Datei „{0}“ überschreitet die maximale Größe von {1}.",

        // ── Scheduler ───────────────────────────────────────────────
        ["Scheduler.Previous"] = "Zurück",
        ["Scheduler.Next"] = "Weiter",
        ["Scheduler.ResourceLegend"] = "Ressourcen-Legende",
        ["Scheduler.Day"] = "Tag",
        ["Scheduler.Week"] = "Woche",
        ["Scheduler.Month"] = "Monat",
        ["Scheduler.List"] = "Liste",
        ["Scheduler.Today"] = "Heute",
        ["Scheduler.WeekOf"] = "Woche vom",

        // ── Gantt ───────────────────────────────────────────────────
        ["Gantt.Day"] = "Tag",
        ["Gantt.Week"] = "Woche",
        ["Gantt.Month"] = "Monat",
        ["Gantt.Year"] = "Jahr",

        // ── Theme ───────────────────────────────────────────────────
        ["Theme.Light"] = "Hell",
        ["Theme.Dark"] = "Dunkel",
        ["Theme.System"] = "System",
        ["Theme.Toggle"] = "Theme umschalten",

        // ── Tabs ────────────────────────────────────────────────────
        ["Tabs.CloseTab"] = "Tab schließen",

        // ── Chip ────────────────────────────────────────────────────
        ["Chip.Remove"] = "Entfernen",

        // ── Alert ───────────────────────────────────────────────────
        ["Alert.Dismiss"] = "Ausblenden",

        // ── ImageCompare ────────────────────────────────────────────
        ["ImageCompare.Slider"] = "Bildvergleichs-Schieberegler",

        // ── PromptInput ─────────────────────────────────────────────
        ["PromptInput.Send"] = "Senden",

        // ── Editor (RichTextEditor) ─────────────────────────────────
        ["Editor.StartWriting"] = "Schreiben beginnen…",

        // ── PivotGrid ───────────────────────────────────────────────
        ["PivotGrid.GrandTotal"] = "Gesamtsumme",
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
