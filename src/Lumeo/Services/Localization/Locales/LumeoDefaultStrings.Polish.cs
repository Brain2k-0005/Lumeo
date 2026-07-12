namespace Lumeo.Services.Localization;

internal static partial class LumeoDefaultStrings
{
    internal static readonly IReadOnlyDictionary<string, string> Pl = new Dictionary<string, string>
    {
        // DataGrid
        ["DataGrid.NoData"] = "Brak danych",
        ["DataGrid.Loading"] = "Ładowanie…",
        ["DataGrid.SearchPlaceholder"] = "Szukaj…",
        ["DataGrid.ClearSearch"] = "Wyczyść wyszukiwanie",
        ["DataGrid.Columns"] = "Kolumny",
        ["DataGrid.ToggleColumns"] = "Pokaż/ukryj kolumny",
        ["DataGrid.ExportCsv"] = "Eksportuj CSV",
        ["DataGrid.ExportExcel"] = "Eksportuj Excel",
        ["DataGrid.ExportJson"] = "Eksportuj JSON",
        ["DataGrid.Export"] = "Eksportuj",
        ["DataGrid.Filter"] = "Filtr",
        ["DataGrid.Filters"] = "Filtry",
        ["DataGrid.ClearFilters"] = "Wyczyść filtry",
        ["DataGrid.ResetLayout"] = "Resetuj układ",
        ["DataGrid.SaveLayout"] = "Zapisz układ",
        ["DataGrid.Layouts"] = "Układy",
        ["DataGrid.NewLayout"] = "Nowy układ",
        ["DataGrid.Personal"] = "Osobisty",
        ["DataGrid.Global"] = "Globalny",
        ["DataGrid.SystemDefault"] = "Domyślny systemu",
        ["DataGrid.LayoutName"] = "Nazwa układu",
        ["DataGrid.Save"] = "Zapisz",
        ["DataGrid.Cancel"] = "Anuluj",
        ["DataGrid.Delete"] = "Usuń",
        ["DataGrid.Rename"] = "Zmień nazwę",
        ["DataGrid.PinLeft"] = "Przypnij do lewej",
        ["DataGrid.PinRight"] = "Przypnij do prawej",
        ["DataGrid.Unpin"] = "Odepnij",
        ["DataGrid.PinColumn"] = "Przypnij kolumnę",
        ["DataGrid.ResizeColumn"] = "Zmień szerokość kolumny (użyj klawiszy strzałek, kliknij dwukrotnie, aby dopasować automatycznie)",
        ["DataGrid.DragToReorder"] = "Przeciągnij, aby zmienić kolejność kolumny",
        ["DataGrid.FilterColumn"] = "Filtruj {0}",
        ["DataGrid.SelectRow"] = "Zaznacz wiersz {0}",
        ["DataGrid.SelectAllRows"] = "Zaznacz wszystkie wiersze",
        ["DataGrid.ExpandRow"] = "Rozwiń wiersz {0}",
        ["DataGrid.CollapseRow"] = "Zwiń wiersz {0}",
        ["DataGrid.ExpandGroup"] = "Rozwiń grupę {0}",
        ["DataGrid.CollapseGroup"] = "Zwiń grupę {0}",
        ["DataGrid.Hide"] = "Ukryj",
        ["DataGrid.Show"] = "Pokaż",
        ["DataGrid.SortAscending"] = "Sortuj rosnąco",
        ["DataGrid.SortDescending"] = "Sortuj malejąco",
        ["DataGrid.ClearSort"] = "Usuń sortowanie",
        ["DataGrid.Edit"] = "Edytuj",
        ["DataGrid.CommitEdit"] = "Zapisz",
        ["DataGrid.CancelEdit"] = "Anuluj",
        ["DataGrid.AggregateSum"] = "Suma",
        ["DataGrid.AggregateAvg"] = "Śr.",
        ["DataGrid.AggregateCount"] = "Licznik",
        ["DataGrid.AggregateRow"] = "Wiersz agregacji",
        ["DataGrid.AggregateMin"] = "Min",
        ["DataGrid.AggregateMax"] = "Maks",
        ["DataGrid.Items"] = "elementów",
        ["DataGrid.ItemsCount"] = "{0} elementów",
        // Polish plural forms: 1 → "element"; 2-4 (excl. 12-14) → "elementy"; rest → "elementów".
        ["DataGrid.ItemsCount.One"] = "{0} element",
        ["DataGrid.ItemsCount.Few"] = "{0} elementy",
        ["DataGrid.ItemsCount.Many"] = "{0} elementów",
        ["DataGrid.ItemsCount.Other"] = "{0} elementów",
        ["DataGrid.CopySelected"] = "Kopiuj ({0})",
        ["DataGrid.ApplyLayout"] = "Zastosuj układ",
        ["DataGrid.NoSavedLayouts"] = "Brak zapisanych układów.",
        ["DataGrid.SaveCurrentLayout"] = "Zapisz bieżący układ…",
        ["DataGrid.Default"] = "Domyślny",
        ["DataGrid.MoveUp"] = "Przenieś w górę",
        ["DataGrid.MoveDown"] = "Przenieś w dół",
        ["DataGrid.Retry"] = "Spróbuj ponownie",
        ["DataGrid.ErrorLoadingData"] = "Błąd ładowania: {0}",
        ["DataGrid.ExpandFullscreen"] = "Przejdź na pełny ekran",
        ["DataGrid.ExitFullscreen"] = "Zamknij pełny ekran",
        ["DataGrid.AddGroupLevel"] = "+ Dodaj poziom grupowania",
        ["DataGrid.GroupPanelPlaceholder"] = "Przeciągnij tutaj nagłówek kolumny umożliwiającej grupowanie lub użyj listy rozwijanej",
        ["DataGrid.DragToGroup"] = "Przeciągnij, aby grupować według tej kolumny",
        ["DataGrid.RemoveGrouping"] = "Usuń grupowanie",
        ["DataGrid.ClearAllGrouping"] = "Wyczyść całe grupowanie",
        ["DataGrid.DragToReorderRow"] = "Przeciągnij, aby zmienić kolejność wiersza",
        ["DataGrid.RowReorderUnavailable"] = "Zmiana kolejności wierszy jest niedostępna podczas grupowania lub wirtualizacji",
        ["Filter.FilterTitle"] = "Filtr: {0}",

        // Pagination
        ["Pagination.Previous"] = "Poprzednia",
        ["Pagination.Next"] = "Następna",
        ["Pagination.First"] = "Pierwsza",
        ["Pagination.Last"] = "Ostatnia",
        ["Pagination.Page"] = "Strona",
        ["Pagination.MorePages"] = "Więcej stron",
        ["Pagination.GoToPage"] = "Przejdź do strony {0}",
        ["Pagination.RowsPerPage"] = "Wierszy na stronie",
        ["Pagination.RangeOfTotal"] = "{0}–{1} z {2}",

        // Filter operators
        ["Filter.Contains"] = "zawiera",
        ["Filter.DoesNotContain"] = "nie zawiera",
        ["Filter.Equals"] = "równe",
        ["Filter.NotEquals"] = "różne",
        ["Filter.StartsWith"] = "zaczyna się od",
        ["Filter.EndsWith"] = "kończy się na",
        ["Filter.GreaterThan"] = "większe niż",
        ["Filter.LessThan"] = "mniejsze niż",
        ["Filter.GreaterThanOrEqual"] = "większe lub równe",
        ["Filter.LessThanOrEqual"] = "mniejsze lub równe",
        ["Filter.Between"] = "między",
        ["Filter.IsEmpty"] = "jest puste",
        ["Filter.IsNotEmpty"] = "nie jest puste",
        ["Filter.Apply"] = "Zastosuj",
        ["Filter.Clear"] = "Wyczyść",
        ["Filter.Value"] = "Wartość",
        ["Filter.SelectAll"] = "Wszystkie",
        ["Filter.ValuePlaceholder"] = "Wartość…",
        ["Filter.ToValuePlaceholder"] = "Do…",

        // ColorPicker
        ["ColorPicker.PickColor"] = "Wybierz kolor",
        ["ColorPicker.Hue"] = "Barwa",
        ["ColorPicker.Saturation"] = "Nasycenie",
        ["ColorPicker.Lightness"] = "Jasność",
        ["ColorPicker.Value"] = "Wartość",
        ["ColorPicker.Opacity"] = "Krycie",
        ["ColorPicker.Hex"] = "Hex",
        ["ColorPicker.HexValue"] = "Wartość hex",
        ["ColorPicker.Red"] = "Czerwony",
        ["ColorPicker.Green"] = "Zielony",
        ["ColorPicker.Blue"] = "Niebieski",
        ["ColorPicker.Presets"] = "Szablony",

        // PasswordInput
        ["Password.Placeholder"] = "Wprowadź hasło",
        ["Password.Toggle"] = "Pokaż/ukryj hasło",
        ["Password.Weak"] = "Słabe",
        ["Password.Fair"] = "Średnie",
        ["Password.Good"] = "Dobre",
        ["Password.Strong"] = "Silne",

        // FileUpload
        ["FileUpload.DragDrop"] = "Przeciągnij i upuść pliki tutaj",
        ["FileUpload.Or"] = "lub",
        ["FileUpload.Browse"] = "Przeglądaj",
        ["FileUpload.MaxSize"] = "Maks. rozmiar: {0}",
        ["FileUpload.Accepted"] = "Dozwolone: {0}",
        ["FileUpload.Remove"] = "Usuń",
        ["FileUpload.Uploading"] = "Przesyłanie…",
        ["FileUpload.Uploaded"] = "Przesłano",
        ["FileUpload.Failed"] = "Niepowodzenie",
        ["FileUpload.Retry"] = "Spróbuj ponownie",
        ["FileUpload.TooLarge"] = "Plik zbyt duży",
        ["FileUpload.TypeNotAllowed"] = "Niedozwolony typ pliku",
        ["FileUpload.ChooseFile"] = "Wybierz plik",
        ["FileUpload.ClickToUpload"] = "Kliknij, aby przesłać lub przeciągnij tutaj",

        // Overlays
        ["Dialog.Close"] = "Zamknij",
        ["Dialog.Confirm"] = "Potwierdź",
        ["Dialog.Cancel"] = "Anuluj",
        ["Dialog.Ok"] = "OK",
        ["Dialog.Yes"] = "Tak",
        ["Dialog.No"] = "Nie",
        ["Toast.Close"] = "Zamknij",
        ["Toast.Dismiss"] = "Odrzuć",
        ["AlertDialog.Delete"] = "Usuń",
        ["AlertDialog.Continue"] = "Kontynuuj",

        // Combobox / Command / Select
        ["Combobox.Placeholder"] = "Wybierz…",
        ["Combobox.SearchPlaceholder"] = "Szukaj…",
        ["Combobox.NoResults"] = "Brak wyników",
        ["Combobox.Loading"] = "Ładowanie…",
        ["Combobox.Clear"] = "Wyczyść",
        ["Combobox.Create"] = "Utwórz \"{0}\"",
        ["Command.Placeholder"] = "Wpisz polecenie lub wyszukaj…",
        ["Command.NoResults"] = "Brak wyników",
        ["Select.Placeholder"] = "Wybierz opcję",

        // Calendar / DatePicker
        ["Calendar.Today"] = "Dzisiaj",
        ["Calendar.Clear"] = "Wyczyść",
        ["Calendar.PrevMonth"] = "Poprzedni miesiąc",
        ["Calendar.NextMonth"] = "Następny miesiąc",
        ["Calendar.PrevYear"] = "Poprzedni rok",
        ["Calendar.NextYear"] = "Następny rok",
        ["DatePicker.Placeholder"] = "Wybierz datę",
        ["DateRange.Placeholder"] = "Wybierz zakres dat",
        ["DateRange.From"] = "Od",
        ["DateRange.To"] = "Do",
        ["DateTimePicker.Placeholder"] = "Wybierz datę i godzinę",
        ["DateTimePicker.TimeLabel"] = "Godzina",
        ["TimePicker.Placeholder"] = "Wybierz godzinę",

        // Tour
        ["Tour.Skip"] = "Pomiń",
        ["Tour.Previous"] = "Wstecz",
        ["Tour.Next"] = "Dalej",
        ["Tour.Finish"] = "Zakończ",

        // PopConfirm
        ["PopConfirm.Title"] = "Czy na pewno?",
        ["PopConfirm.Confirm"] = "Tak",
        ["PopConfirm.Cancel"] = "Nie",

        // Misc
        ["Common.Search"] = "Szukaj",
        ["Common.Clear"] = "Wyczyść",
        ["Common.ClearAll"] = "Wyczyść wszystko",
        ["Common.Close"] = "Zamknij",
        ["Common.Loading"] = "Ładowanie…",
        ["Common.NoResults"] = "Brak wyników",
        ["Common.Apply"] = "Zastosuj",
        ["Common.Reset"] = "Resetuj",
        ["Common.Save"] = "Zapisz",
        ["Common.Cancel"] = "Anuluj",
        ["Common.Copy"] = "Kopiuj",
        ["Common.Copied"] = "Skopiowano",
        ["Common.More"] = "Więcej",
        ["Common.Back"] = "Wstecz",
        ["Common.Next"] = "Dalej",
        ["Common.ShowMore"] = "Pokaż więcej",
        ["Common.ShowLess"] = "Pokaż mniej",

        // Transfer / TreeSelect / TagInput
        ["Transfer.SourceHeader"] = "Dostępne",
        ["Transfer.TargetHeader"] = "Wybrane",
        ["Transfer.MoveRight"] = "Przenieś w prawo",
        ["Transfer.MoveLeft"] = "Przenieś w lewo",
        ["Transfer.NoItems"] = "Brak elementów",
        ["TreeSelect.Placeholder"] = "Wybierz…",
        ["TreeSelect.NoResults"] = "Brak wyników",
        ["TagInput.Placeholder"] = "Dodaj tag…",

        // Cascader
        ["Cascader.Placeholder"] = "Wybierz…",

        // Rating / OTP
        ["Rating.Rate"] = "Oceń",
        ["Rating.RateOf"] = "Ocena {0} z {1}",
        ["Otp.Placeholder"] = "Wpisz kod",

        // Empty
        ["Empty.Title"] = "Jeszcze nic tu nie ma",
        ["Empty.Description"] = "Brak danych do wyświetlenia.",

        // Kanban
        ["Kanban.AddCard"] = "Dodaj kartę",

        // Carousel
        ["Carousel.PreviousSlide"] = "Poprzedni slajd",
        ["Carousel.NextSlide"] = "Następny slajd",
        ["Carousel.SlideXofY"] = "Slajd {0} z {1}",

        // Stepper
        ["Stepper.Back"] = "Wstecz",
        ["Stepper.Next"] = "Dalej",
        ["Stepper.Finish"] = "Zakończ",
        ["Stepper.Optional"] = "Opcjonalne",
        ["Stepper.Skip"] = "Pomiń",

        // Window
        ["Window.Close"] = "Zamknij",
        ["Window.Minimize"] = "Minimalizuj",
        ["Window.Maximize"] = "Maksymalizuj",
        ["Window.Restore"] = "Przywróć",

        // NumberInput
        ["NumberInput.Decrease"] = "Zmniejsz",
        ["NumberInput.Increase"] = "Zwiększ",

        // DateTimePicker
        ["DateTimePicker.ClearDate"] = "Wyczyść datę",

        // Slider
        ["Slider.End"] = "koniec",

        // FileManager
        ["FileManager.EmptyTitle"] = "Ten folder jest pusty",
        ["FileManager.EmptyState"] = "Brak plików i folderów.",
        ["FileManager.MoreActions"] = "Więcej akcji",
        ["FileManager.MoreActionsForName"] = "Więcej akcji dla {0}",

        // FileUpload (parameterised)
        ["FileUpload.ExceedsMaxSize"] = "Plik „{0}” przekracza maksymalny rozmiar {1}.",

        // ConfirmButton
        ["ConfirmButton.Title"] = "Czy na pewno?",
        ["ConfirmButton.Confirm"] = "Kontynuuj",
        ["ConfirmButton.Cancel"] = "Anuluj",

        // PickList (panels)
        ["PickList.SourceHeader"] = "Dostępne",
        ["PickList.TargetHeader"] = "Wybrane",
        ["PickList.NoItems"] = "Brak elementów",

        // FileManager (context menu / list view)
        ["FileManager.Open"] = "Otwórz",
        ["FileManager.Rename"] = "Zmień nazwę",
        ["FileManager.Delete"] = "Usuń",
        ["FileManager.Name"] = "Nazwa",
        ["FileManager.Size"] = "Rozmiar",
        ["FileManager.Modified"] = "Zmodyfikowano",
        ["FileManager.Loading"] = "Ładowanie…",

        // AudioPlayer
        ["AudioPlayer.Label"] = "Odtwarzacz audio",
        ["AudioPlayer.Play"] = "Odtwórz",
        ["AudioPlayer.Pause"] = "Wstrzymaj",
        ["AudioPlayer.Seek"] = "Pozycja odtwarzania",
        ["AudioPlayer.Mute"] = "Wycisz",
        ["AudioPlayer.Unmute"] = "Wyłącz wyciszenie",
        ["AudioPlayer.Download"] = "Pobierz audio",

        // ThemeSwitcher
        ["Theme.Color"] = "Kolor",
        ["Theme.Mode"] = "Tryb",
    };
}
