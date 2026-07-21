namespace Lumeo.Services.Localization;

internal static partial class LumeoDefaultStrings
{
    internal static readonly IReadOnlyDictionary<string, string> It = new Dictionary<string, string>
    {
        // DataGrid
        ["DataGrid.NoData"] = "Nessun dato",
        ["DataGrid.Loading"] = "Caricamento…",
        ["DataGrid.SearchPlaceholder"] = "Cerca…",
        ["DataGrid.ClearSearch"] = "Cancella ricerca",
        ["DataGrid.Columns"] = "Colonne",
        ["DataGrid.ToggleColumns"] = "Mostra/nascondi colonne",
        ["DataGrid.ExportCsv"] = "Esporta CSV",
        ["DataGrid.ExportExcel"] = "Esporta Excel",
        ["DataGrid.ExportJson"] = "Esporta JSON",
        ["DataGrid.Export"] = "Esporta",
        ["DataGrid.Filter"] = "Filtro",
        ["DataGrid.Filters"] = "Filtri",
        ["DataGrid.ClearFilters"] = "Cancella filtri",
        ["DataGrid.ResetLayout"] = "Ripristina layout",
        ["DataGrid.SaveLayout"] = "Salva layout",
        ["DataGrid.Layouts"] = "Layout",
        ["DataGrid.NewLayout"] = "Nuovo layout",
        ["DataGrid.Personal"] = "Personale",
        ["DataGrid.Global"] = "Globale",
        ["DataGrid.SystemDefault"] = "Predefinito di sistema",
        ["DataGrid.LayoutName"] = "Nome layout",
        ["DataGrid.Save"] = "Salva",
        ["DataGrid.Cancel"] = "Annulla",
        ["DataGrid.Delete"] = "Elimina",
        ["DataGrid.Rename"] = "Rinomina",
        ["DataGrid.PinLeft"] = "Fissa a sinistra",
        ["DataGrid.PinRight"] = "Fissa a destra",
        ["DataGrid.Unpin"] = "Sblocca",
        ["DataGrid.PinColumn"] = "Fissa colonna",
        ["DataGrid.ResizeColumn"] = "Ridimensiona colonna (usa i tasti freccia, doppio clic per l'adattamento automatico)",
        ["DataGrid.DragToReorder"] = "Trascina per riordinare la colonna",
        ["DataGrid.ColumnMovedAnnouncement"] = "{0} spostato in posizione {1} di {2}",
        ["DataGrid.FilterColumn"] = "Filtra {0}",
        ["DataGrid.SelectRow"] = "Seleziona la riga {0}",
        ["DataGrid.SelectAllRows"] = "Seleziona tutte le righe",
        ["DataGrid.ExpandRow"] = "Espandi la riga {0}",
        ["DataGrid.CollapseRow"] = "Comprimi la riga {0}",
        ["DataGrid.ExpandGroup"] = "Espandi il gruppo {0}",
        ["DataGrid.CollapseGroup"] = "Comprimi il gruppo {0}",
        ["DataGrid.Hide"] = "Nascondi",
        ["DataGrid.Show"] = "Mostra",
        ["DataGrid.SortAscending"] = "Ordina crescente",
        ["DataGrid.SortDescending"] = "Ordina decrescente",
        ["DataGrid.ClearSort"] = "Rimuovi ordinamento",
        ["DataGrid.Edit"] = "Modifica",
        ["DataGrid.CommitEdit"] = "Salva",
        ["DataGrid.CancelEdit"] = "Annulla",
        ["DataGrid.AggregateSum"] = "Somma",
        ["DataGrid.AggregateAvg"] = "Media",
        ["DataGrid.AggregateCount"] = "Conteggio",
        ["DataGrid.AggregateRow"] = "Riepilogo aggregato",
        ["DataGrid.AggregateMin"] = "Min",
        ["DataGrid.AggregateMax"] = "Max",
        ["DataGrid.Items"] = "elementi",
        ["DataGrid.ItemsCount"] = "{0} elementi",
        ["DataGrid.ItemsCount.One"] = "{0} elemento",
        ["DataGrid.ItemsCount.Other"] = "{0} elementi",
        ["DataGrid.CopySelected"] = "Copia ({0})",
        ["DataGrid.ApplyLayout"] = "Applica layout",
        ["DataGrid.NoSavedLayouts"] = "Nessun layout salvato.",
        ["DataGrid.SaveCurrentLayout"] = "Salva layout corrente…",
        ["DataGrid.Default"] = "Predefinito",
        ["DataGrid.MoveUp"] = "Sposta su",
        ["DataGrid.MoveDown"] = "Sposta giù",
        ["DataGrid.Retry"] = "Riprova",
        ["DataGrid.ErrorLoadingData"] = "Errore nel caricamento: {0}",
        ["DataGrid.ExpandFullscreen"] = "Espandi a schermo intero",
        ["DataGrid.ExitFullscreen"] = "Esci da schermo intero",
        ["DataGrid.AddGroupLevel"] = "+ Aggiungi livello di raggruppamento",
        ["DataGrid.GroupPanelPlaceholder"] = "Trascina qui un'intestazione di colonna raggruppabile oppure usa il menu a discesa",
        ["DataGrid.DragToGroup"] = "Trascina per raggruppare per questa colonna",
        ["DataGrid.RemoveGrouping"] = "Rimuovi raggruppamento",
        ["DataGrid.ClearAllGrouping"] = "Cancella tutti i raggruppamenti",
        ["DataGrid.DragToReorderRow"] = "Trascina per riordinare la riga",
        ["DataGrid.RowReorderUnavailable"] = "Il riordino delle righe non è disponibile durante il raggruppamento o la virtualizzazione",
        ["Filter.FilterTitle"] = "Filtro: {0}",

        // Pagination
        ["Pagination.Previous"] = "Precedente",
        ["Pagination.Next"] = "Successiva",
        ["Pagination.First"] = "Prima",
        ["Pagination.Last"] = "Ultima",
        ["Pagination.Page"] = "Pagina",
        ["Pagination.MorePages"] = "Altre pagine",
        ["Pagination.GoToPage"] = "Vai alla pagina {0}",
        ["Pagination.RowsPerPage"] = "Righe per pagina",
        ["Pagination.RangeOfTotal"] = "{0}–{1} di {2}",

        // Filter operators
        ["Filter.Contains"] = "contiene",
        ["Filter.DoesNotContain"] = "non contiene",
        ["Filter.Equals"] = "uguale a",
        ["Filter.NotEquals"] = "diverso da",
        ["Filter.StartsWith"] = "inizia con",
        ["Filter.EndsWith"] = "finisce con",
        ["Filter.GreaterThan"] = "maggiore di",
        ["Filter.LessThan"] = "minore di",
        ["Filter.GreaterThanOrEqual"] = "maggiore o uguale",
        ["Filter.LessThanOrEqual"] = "minore o uguale",
        ["Filter.Between"] = "tra",
        ["Filter.IsEmpty"] = "è vuoto",
        ["Filter.IsNotEmpty"] = "non è vuoto",
        ["Filter.Apply"] = "Applica",
        ["Filter.Clear"] = "Cancella",
        ["Filter.Value"] = "Valore",
        ["Filter.SelectAll"] = "Tutti",
        ["Filter.ValuePlaceholder"] = "Valore…",
        ["Filter.ToValuePlaceholder"] = "A valore…",

        // ColorPicker
        ["ColorPicker.PickColor"] = "Scegli un colore",
        ["ColorPicker.Hue"] = "Tonalità",
        ["ColorPicker.Saturation"] = "Saturazione",
        ["ColorPicker.Lightness"] = "Luminosità",
        ["ColorPicker.Value"] = "Valore",
        ["ColorPicker.Opacity"] = "Opacità",
        ["ColorPicker.Hex"] = "Hex",
        ["ColorPicker.HexValue"] = "Valore hex",
        ["ColorPicker.Red"] = "Rosso",
        ["ColorPicker.Green"] = "Verde",
        ["ColorPicker.Blue"] = "Blu",
        ["ColorPicker.Presets"] = "Preimpostati",

        // PasswordInput
        ["Password.Placeholder"] = "Inserisci password",
        ["Password.Toggle"] = "Mostra/nascondi password",
        ["Password.Weak"] = "Debole",
        ["Password.Fair"] = "Discreta",
        ["Password.Good"] = "Buona",
        ["Password.Strong"] = "Forte",

        // FileUpload
        ["FileUpload.DragDrop"] = "Trascina e rilascia i file qui",
        ["FileUpload.Or"] = "oppure",
        ["FileUpload.Browse"] = "Sfoglia",
        ["FileUpload.MaxSize"] = "Dim. max: {0}",
        ["FileUpload.Accepted"] = "Accettati: {0}",
        ["FileUpload.Remove"] = "Rimuovi",
        ["FileUpload.Uploading"] = "Caricamento…",
        ["FileUpload.Uploaded"] = "Caricato",
        ["FileUpload.Failed"] = "Non riuscito",
        ["FileUpload.Retry"] = "Riprova",
        ["FileUpload.TooLarge"] = "File troppo grande",
        ["FileUpload.TypeNotAllowed"] = "Tipo di file non consentito",
        ["FileUpload.ChooseFile"] = "Scegli file",
        ["FileUpload.ClickToUpload"] = "Clicca per caricare o trascina qui",

        // Overlays
        ["Dialog.Close"] = "Chiudi",
        ["Dialog.Confirm"] = "Conferma",
        ["Dialog.Cancel"] = "Annulla",
        ["Dialog.Ok"] = "OK",
        ["Dialog.Yes"] = "Sì",
        ["Dialog.No"] = "No",
        ["Toast.Close"] = "Chiudi",
        ["Toast.Dismiss"] = "Ignora",
        ["AlertDialog.Delete"] = "Elimina",
        ["AlertDialog.Continue"] = "Continua",

        // Combobox / Command / Select
        ["Combobox.Placeholder"] = "Seleziona…",
        ["Combobox.SearchPlaceholder"] = "Cerca…",
        ["Combobox.NoResults"] = "Nessun risultato",
        ["Combobox.Loading"] = "Caricamento…",
        ["Combobox.Clear"] = "Cancella",
        ["Combobox.Create"] = "Crea \"{0}\"",
        ["Command.Placeholder"] = "Digita un comando o cerca…",
        ["Command.NoResults"] = "Nessun risultato",
        ["Select.Placeholder"] = "Seleziona un'opzione",

        // Calendar / DatePicker
        ["Calendar.Today"] = "Oggi",
        ["Calendar.Clear"] = "Cancella",
        ["Calendar.PrevMonth"] = "Mese precedente",
        ["Calendar.NextMonth"] = "Mese successivo",
        ["Calendar.PrevYear"] = "Anno precedente",
        ["Calendar.NextYear"] = "Anno successivo",
        ["DatePicker.Placeholder"] = "Scegli una data",
        ["DateRange.Placeholder"] = "Scegli un intervallo",
        ["DateRange.From"] = "Dal",
        ["DateRange.To"] = "Al",
        ["DateTimePicker.Placeholder"] = "Scegli data e ora",
        ["DateTimePicker.TimeLabel"] = "Ora",
        ["TimePicker.Placeholder"] = "Scegli un'ora",

        // Tour
        ["Tour.Skip"] = "Salta",
        ["Tour.Previous"] = "Precedente",
        ["Tour.Next"] = "Successivo",
        ["Tour.Finish"] = "Fine",

        // PopConfirm
        ["PopConfirm.Title"] = "Sei sicuro?",
        ["PopConfirm.Confirm"] = "Sì",
        ["PopConfirm.Cancel"] = "No",

        // Misc
        ["Common.Search"] = "Cerca",
        ["Common.Clear"] = "Cancella",
        ["Common.ClearAll"] = "Cancella tutto",
        ["Common.Close"] = "Chiudi",
        ["Common.Loading"] = "Caricamento…",
        ["Common.NoResults"] = "Nessun risultato",
        ["Common.Apply"] = "Applica",
        ["Common.Reset"] = "Reimposta",
        ["Common.Save"] = "Salva",
        ["Common.Cancel"] = "Annulla",
        ["Common.Copy"] = "Copia",
        ["Common.Copied"] = "Copiato",
        ["Common.More"] = "Altro",
        ["Common.Back"] = "Indietro",
        ["Common.Next"] = "Avanti",
        ["Common.ShowMore"] = "Mostra altro",
        ["Common.ShowLess"] = "Mostra meno",

        // Transfer / TreeSelect / TagInput
        ["Transfer.SourceHeader"] = "Disponibili",
        ["Transfer.TargetHeader"] = "Selezionati",
        ["Transfer.MoveRight"] = "Sposta a destra",
        ["Transfer.MoveLeft"] = "Sposta a sinistra",
        ["Transfer.NoItems"] = "Nessun elemento",
        ["TreeSelect.Placeholder"] = "Seleziona…",
        ["TreeSelect.NoResults"] = "Nessun risultato",
        ["TagInput.Placeholder"] = "Aggiungi tag…",

        // Cascader
        ["Cascader.Placeholder"] = "Seleziona…",

        // Rating / OTP
        ["Rating.Rate"] = "Valuta",
        ["Rating.RateOf"] = "Valutazione {0} di {1}",
        ["Otp.Placeholder"] = "Inserisci codice",

        // Empty
        ["Empty.Title"] = "Niente qui per ora",
        ["Empty.Description"] = "Nessun dato da mostrare.",

        // Kanban
        ["Kanban.AddCard"] = "Aggiungi scheda",

        // Carousel
        ["Carousel.PreviousSlide"] = "Diapositiva precedente",
        ["Carousel.NextSlide"] = "Diapositiva successiva",
        ["Carousel.SlideXofY"] = "Diapositiva {0} di {1}",

        // Stepper
        ["Stepper.Back"] = "Indietro",
        ["Stepper.Next"] = "Avanti",
        ["Stepper.Finish"] = "Fine",
        ["Stepper.Optional"] = "Facoltativo",
        ["Stepper.Skip"] = "Salta",

        // Window
        ["Window.Close"] = "Chiudi",
        ["Window.Minimize"] = "Riduci a icona",
        ["Window.Maximize"] = "Ingrandisci",
        ["Window.Restore"] = "Ripristina",

        // NumberInput
        ["NumberInput.Decrease"] = "Diminuisci",
        ["NumberInput.Increase"] = "Aumenta",

        // DateTimePicker
        ["DateTimePicker.ClearDate"] = "Cancella data",

        // Slider
        ["Slider.End"] = "fine",

        // FileManager
        ["FileManager.EmptyTitle"] = "Questa cartella è vuota",
        ["FileManager.EmptyState"] = "Nessun file o cartella qui.",
        ["FileManager.MoreActions"] = "Altre azioni",
        ["FileManager.MoreActionsForName"] = "Altre azioni per {0}",

        // FileUpload (parameterised)
        ["FileUpload.ExceedsMaxSize"] = "Il file «{0}» supera la dimensione massima di {1}.",

        // ConfirmButton
        ["ConfirmButton.Title"] = "Sei sicuro?",
        ["ConfirmButton.Confirm"] = "Continua",
        ["ConfirmButton.Cancel"] = "Annulla",

        // PickList (panels)
        ["PickList.SourceHeader"] = "Disponibili",
        ["PickList.TargetHeader"] = "Selezionati",
        ["PickList.NoItems"] = "Nessun elemento",

        // FileManager (context menu / list view)
        ["FileManager.Open"] = "Apri",
        ["FileManager.Rename"] = "Rinomina",
        ["FileManager.Delete"] = "Elimina",
        ["FileManager.Name"] = "Nome",
        ["FileManager.Size"] = "Dimensione",
        ["FileManager.Modified"] = "Modificato",
        ["FileManager.Loading"] = "Caricamento…",

        // AudioPlayer
        ["AudioPlayer.Label"] = "Lettore audio",
        ["AudioPlayer.Play"] = "Riproduci",
        ["AudioPlayer.Pause"] = "Pausa",
        ["AudioPlayer.Seek"] = "Posizione di riproduzione",
        ["AudioPlayer.Mute"] = "Disattiva audio",
        ["AudioPlayer.Unmute"] = "Riattiva audio",
        ["AudioPlayer.Download"] = "Scarica audio",

        // ThemeSwitcher
        ["Theme.Color"] = "Colore",
        ["Theme.Mode"] = "Modalità",

        // Gantt (Codex round 4, P2 #6 — locale completeness: previously only en/de had any Gantt.* keys)
        ["Gantt.Day"] = "Giorno",
        ["Gantt.Week"] = "Settimana",
        ["Gantt.Month"] = "Mese",
        ["Gantt.Year"] = "Anno",
        ["Gantt.Today"] = "Oggi",
        ["Gantt.PreviousPeriod"] = "Periodo precedente",
        ["Gantt.NextPeriod"] = "Periodo successivo",
        ["Gantt.ExpandRow"] = "Espandi {0}",
        ["Gantt.CollapseRow"] = "Comprimi {0}",
        ["Gantt.NoTasksToDisplay"] = "Nessuna attività da visualizzare",
        ["Gantt.TaskAriaLabel"] = "{0}, dal {1} al {2}",
    };
}
