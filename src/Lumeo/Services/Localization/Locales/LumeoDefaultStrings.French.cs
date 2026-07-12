namespace Lumeo.Services.Localization;

internal static partial class LumeoDefaultStrings
{
    internal static readonly IReadOnlyDictionary<string, string> Fr = new Dictionary<string, string>
    {
        // DataGrid
        ["DataGrid.NoData"] = "Aucune donnée",
        ["DataGrid.Loading"] = "Chargement…",
        ["DataGrid.SearchPlaceholder"] = "Rechercher…",
        ["DataGrid.ClearSearch"] = "Effacer la recherche",
        ["DataGrid.Columns"] = "Colonnes",
        ["DataGrid.ToggleColumns"] = "Afficher/masquer les colonnes",
        ["DataGrid.ExportCsv"] = "Exporter CSV",
        ["DataGrid.ExportExcel"] = "Exporter Excel",
        ["DataGrid.ExportJson"] = "Exporter JSON",
        ["DataGrid.Export"] = "Exporter",
        ["DataGrid.Filter"] = "Filtre",
        ["DataGrid.Filters"] = "Filtres",
        ["DataGrid.ClearFilters"] = "Effacer les filtres",
        ["DataGrid.ResetLayout"] = "Réinitialiser la mise en page",
        ["DataGrid.SaveLayout"] = "Enregistrer la mise en page",
        ["DataGrid.Layouts"] = "Mises en page",
        ["DataGrid.NewLayout"] = "Nouvelle mise en page",
        ["DataGrid.Personal"] = "Personnel",
        ["DataGrid.Global"] = "Global",
        ["DataGrid.SystemDefault"] = "Par défaut",
        ["DataGrid.LayoutName"] = "Nom de la mise en page",
        ["DataGrid.Save"] = "Enregistrer",
        ["DataGrid.Cancel"] = "Annuler",
        ["DataGrid.Delete"] = "Supprimer",
        ["DataGrid.Rename"] = "Renommer",
        ["DataGrid.PinLeft"] = "Épingler à gauche",
        ["DataGrid.PinRight"] = "Épingler à droite",
        ["DataGrid.Unpin"] = "Désépingler",
        ["DataGrid.PinColumn"] = "Épingler la colonne",
        ["DataGrid.ResizeColumn"] = "Redimensionner la colonne (flèches du clavier, double-clic pour ajuster automatiquement)",
        ["DataGrid.DragToReorder"] = "Glisser pour réorganiser la colonne",
        ["DataGrid.FilterColumn"] = "Filtrer {0}",
        ["DataGrid.SelectRow"] = "Sélectionner la ligne {0}",
        ["DataGrid.SelectAllRows"] = "Sélectionner toutes les lignes",
        ["DataGrid.ExpandRow"] = "Développer la ligne {0}",
        ["DataGrid.CollapseRow"] = "Réduire la ligne {0}",
        ["DataGrid.ExpandGroup"] = "Développer le groupe {0}",
        ["DataGrid.CollapseGroup"] = "Réduire le groupe {0}",
        ["DataGrid.Hide"] = "Masquer",
        ["DataGrid.Show"] = "Afficher",
        ["DataGrid.SortAscending"] = "Tri croissant",
        ["DataGrid.SortDescending"] = "Tri décroissant",
        ["DataGrid.ClearSort"] = "Effacer le tri",
        ["DataGrid.Edit"] = "Modifier",
        ["DataGrid.CommitEdit"] = "Enregistrer",
        ["DataGrid.CancelEdit"] = "Annuler",
        ["DataGrid.AggregateSum"] = "Somme",
        ["DataGrid.AggregateAvg"] = "Moy.",
        ["DataGrid.AggregateCount"] = "Nb",
        ["DataGrid.AggregateRow"] = "Ligne d'agrégation",
        ["DataGrid.AggregateMin"] = "Min",
        ["DataGrid.AggregateMax"] = "Max",
        ["DataGrid.Items"] = "éléments",
        ["DataGrid.ItemsCount"] = "{0} éléments",
        ["DataGrid.ItemsCount.One"] = "{0} élément",
        ["DataGrid.ItemsCount.Other"] = "{0} éléments",
        ["DataGrid.CopySelected"] = "Copier ({0})",
        ["DataGrid.ApplyLayout"] = "Appliquer la mise en page",
        ["DataGrid.NoSavedLayouts"] = "Aucune mise en page enregistrée.",
        ["DataGrid.SaveCurrentLayout"] = "Enregistrer la mise en page actuelle…",
        ["DataGrid.Default"] = "Par défaut",
        ["DataGrid.MoveUp"] = "Monter",
        ["DataGrid.MoveDown"] = "Descendre",
        ["DataGrid.Retry"] = "Réessayer",
        ["DataGrid.ErrorLoadingData"] = "Échec du chargement : {0}",
        ["DataGrid.ExpandFullscreen"] = "Passer en plein écran",
        ["DataGrid.ExitFullscreen"] = "Quitter le plein écran",
        ["DataGrid.AddGroupLevel"] = "+ Ajouter un niveau de regroupement",
        ["DataGrid.GroupPanelPlaceholder"] = "Glissez un en-tête de colonne groupable ici, ou utilisez la liste déroulante",
        ["DataGrid.DragToGroup"] = "Glisser pour regrouper par cette colonne",
        ["DataGrid.RemoveGrouping"] = "Supprimer le regroupement",
        ["DataGrid.ClearAllGrouping"] = "Effacer tous les regroupements",
        ["DataGrid.DragToReorderRow"] = "Glisser pour réorganiser la ligne",
        ["DataGrid.RowReorderUnavailable"] = "La réorganisation des lignes n'est pas disponible en cas de regroupement ou de virtualisation",
        ["Filter.FilterTitle"] = "Filtre : {0}",

        // Pagination
        ["Pagination.Previous"] = "Précédent",
        ["Pagination.Next"] = "Suivant",
        ["Pagination.First"] = "Première",
        ["Pagination.Last"] = "Dernière",
        ["Pagination.Page"] = "Page",
        ["Pagination.MorePages"] = "Plus de pages",
        ["Pagination.GoToPage"] = "Aller à la page {0}",
        ["Pagination.RowsPerPage"] = "Lignes par page",
        ["Pagination.RangeOfTotal"] = "{0}–{1} sur {2}",

        // Filter operators
        ["Filter.Contains"] = "contient",
        ["Filter.DoesNotContain"] = "ne contient pas",
        ["Filter.Equals"] = "égal à",
        ["Filter.NotEquals"] = "différent de",
        ["Filter.StartsWith"] = "commence par",
        ["Filter.EndsWith"] = "se termine par",
        ["Filter.GreaterThan"] = "supérieur à",
        ["Filter.LessThan"] = "inférieur à",
        ["Filter.GreaterThanOrEqual"] = "supérieur ou égal",
        ["Filter.LessThanOrEqual"] = "inférieur ou égal",
        ["Filter.Between"] = "entre",
        ["Filter.IsEmpty"] = "est vide",
        ["Filter.IsNotEmpty"] = "n'est pas vide",
        ["Filter.Apply"] = "Appliquer",
        ["Filter.Clear"] = "Effacer",
        ["Filter.Value"] = "Valeur",
        ["Filter.SelectAll"] = "Tous",
        ["Filter.ValuePlaceholder"] = "Valeur…",
        ["Filter.ToValuePlaceholder"] = "Jusqu'à…",

        // ColorPicker
        ["ColorPicker.PickColor"] = "Choisir une couleur",
        ["ColorPicker.Hue"] = "Teinte",
        ["ColorPicker.Saturation"] = "Saturation",
        ["ColorPicker.Lightness"] = "Luminosité",
        ["ColorPicker.Value"] = "Valeur",
        ["ColorPicker.Opacity"] = "Opacité",
        ["ColorPicker.Hex"] = "Hex",
        ["ColorPicker.HexValue"] = "Valeur hex",
        ["ColorPicker.Red"] = "Rouge",
        ["ColorPicker.Green"] = "Vert",
        ["ColorPicker.Blue"] = "Bleu",
        ["ColorPicker.Presets"] = "Préréglages",

        // PasswordInput
        ["Password.Placeholder"] = "Saisir le mot de passe",
        ["Password.Toggle"] = "Afficher/masquer le mot de passe",
        ["Password.Weak"] = "Faible",
        ["Password.Fair"] = "Moyen",
        ["Password.Good"] = "Bon",
        ["Password.Strong"] = "Fort",

        // FileUpload
        ["FileUpload.DragDrop"] = "Glissez-déposez les fichiers ici",
        ["FileUpload.Or"] = "ou",
        ["FileUpload.Browse"] = "Parcourir",
        ["FileUpload.MaxSize"] = "Taille max. : {0}",
        ["FileUpload.Accepted"] = "Acceptés : {0}",
        ["FileUpload.Remove"] = "Supprimer",
        ["FileUpload.Uploading"] = "Envoi…",
        ["FileUpload.Uploaded"] = "Envoyé",
        ["FileUpload.Failed"] = "Échec",
        ["FileUpload.Retry"] = "Réessayer",
        ["FileUpload.TooLarge"] = "Fichier trop volumineux",
        ["FileUpload.TypeNotAllowed"] = "Type de fichier non autorisé",
        ["FileUpload.ChooseFile"] = "Choisir un fichier",
        ["FileUpload.ClickToUpload"] = "Cliquez pour envoyer ou glissez-déposez",

        // Overlays
        ["Dialog.Close"] = "Fermer",
        ["Dialog.Confirm"] = "Confirmer",
        ["Dialog.Cancel"] = "Annuler",
        ["Dialog.Ok"] = "OK",
        ["Dialog.Yes"] = "Oui",
        ["Dialog.No"] = "Non",
        ["Toast.Close"] = "Fermer",
        ["Toast.Dismiss"] = "Ignorer",
        ["AlertDialog.Delete"] = "Supprimer",
        ["AlertDialog.Continue"] = "Continuer",

        // Combobox / Command / Select
        ["Combobox.Placeholder"] = "Sélectionner…",
        ["Combobox.SearchPlaceholder"] = "Rechercher…",
        ["Combobox.NoResults"] = "Aucun résultat",
        ["Combobox.Loading"] = "Chargement…",
        ["Combobox.Clear"] = "Effacer",
        ["Combobox.Create"] = "Créer \"{0}\"",
        ["Command.Placeholder"] = "Saisir une commande ou rechercher…",
        ["Command.NoResults"] = "Aucun résultat",
        ["Select.Placeholder"] = "Sélectionner une option",

        // Calendar / DatePicker
        ["Calendar.Today"] = "Aujourd'hui",
        ["Calendar.Clear"] = "Effacer",
        ["Calendar.PrevMonth"] = "Mois précédent",
        ["Calendar.NextMonth"] = "Mois suivant",
        ["Calendar.PrevYear"] = "Année précédente",
        ["Calendar.NextYear"] = "Année suivante",
        ["DatePicker.Placeholder"] = "Choisir une date",
        ["DateRange.Placeholder"] = "Choisir une période",
        ["DateRange.From"] = "Du",
        ["DateRange.To"] = "Au",
        ["DateTimePicker.Placeholder"] = "Choisir date et heure",
        ["DateTimePicker.TimeLabel"] = "Heure",
        ["TimePicker.Placeholder"] = "Choisir l'heure",

        // Tour
        ["Tour.Skip"] = "Passer",
        ["Tour.Previous"] = "Précédent",
        ["Tour.Next"] = "Suivant",
        ["Tour.Finish"] = "Terminer",

        // PopConfirm
        ["PopConfirm.Title"] = "Êtes-vous sûr ?",
        ["PopConfirm.Confirm"] = "Oui",
        ["PopConfirm.Cancel"] = "Non",

        // Misc
        ["Common.Search"] = "Rechercher",
        ["Common.Clear"] = "Effacer",
        ["Common.ClearAll"] = "Tout effacer",
        ["Common.Close"] = "Fermer",
        ["Common.Loading"] = "Chargement…",
        ["Common.NoResults"] = "Aucun résultat",
        ["Common.Apply"] = "Appliquer",
        ["Common.Reset"] = "Réinitialiser",
        ["Common.Save"] = "Enregistrer",
        ["Common.Cancel"] = "Annuler",
        ["Common.Copy"] = "Copier",
        ["Common.Copied"] = "Copié",
        ["Common.More"] = "Plus",
        ["Common.Back"] = "Retour",
        ["Common.Next"] = "Suivant",
        ["Common.ShowMore"] = "Afficher plus",
        ["Common.ShowLess"] = "Afficher moins",

        // Transfer / TreeSelect / TagInput
        ["Transfer.SourceHeader"] = "Disponibles",
        ["Transfer.TargetHeader"] = "Sélectionnés",
        ["Transfer.MoveRight"] = "Déplacer à droite",
        ["Transfer.MoveLeft"] = "Déplacer à gauche",
        ["Transfer.NoItems"] = "Aucun élément",
        ["TreeSelect.Placeholder"] = "Sélectionner…",
        ["TreeSelect.NoResults"] = "Aucun résultat",
        ["TagInput.Placeholder"] = "Ajouter une étiquette…",

        // Cascader
        ["Cascader.Placeholder"] = "Sélectionner…",

        // Rating / OTP
        ["Rating.Rate"] = "Évaluer",
        ["Rating.RateOf"] = "Note {0} sur {1}",
        ["Otp.Placeholder"] = "Saisir le code",

        // Empty
        ["Empty.Title"] = "Rien pour l'instant",
        ["Empty.Description"] = "Aucune donnée à afficher.",

        // Kanban
        ["Kanban.AddCard"] = "Ajouter une carte",

        // Carousel
        ["Carousel.PreviousSlide"] = "Diapositive précédente",
        ["Carousel.NextSlide"] = "Diapositive suivante",
        ["Carousel.SlideXofY"] = "Diapositive {0} sur {1}",

        // Stepper
        ["Stepper.Back"] = "Retour",
        ["Stepper.Next"] = "Suivant",
        ["Stepper.Finish"] = "Terminer",
        ["Stepper.Optional"] = "Facultatif",
        ["Stepper.Skip"] = "Passer",

        // Window
        ["Window.Close"] = "Fermer",
        ["Window.Minimize"] = "Réduire",
        ["Window.Maximize"] = "Agrandir",
        ["Window.Restore"] = "Restaurer",

        // NumberInput
        ["NumberInput.Decrease"] = "Diminuer",
        ["NumberInput.Increase"] = "Augmenter",

        // DateTimePicker
        ["DateTimePicker.ClearDate"] = "Effacer la date",

        // Slider
        ["Slider.End"] = "fin",

        // FileManager
        ["FileManager.EmptyTitle"] = "Ce dossier est vide",
        ["FileManager.EmptyState"] = "Aucun fichier ou dossier ici.",
        ["FileManager.MoreActions"] = "Plus d'actions",
        ["FileManager.MoreActionsForName"] = "Plus d'actions pour {0}",

        // FileUpload (parameterised)
        ["FileUpload.ExceedsMaxSize"] = "Le fichier « {0} » dépasse la taille maximale de {1}.",

        // ConfirmButton
        ["ConfirmButton.Title"] = "Êtes-vous sûr ?",
        ["ConfirmButton.Confirm"] = "Continuer",
        ["ConfirmButton.Cancel"] = "Annuler",

        // PickList (panels)
        ["PickList.SourceHeader"] = "Disponibles",
        ["PickList.TargetHeader"] = "Sélectionnés",
        ["PickList.NoItems"] = "Aucun élément",

        // FileManager (context menu / list view)
        ["FileManager.Open"] = "Ouvrir",
        ["FileManager.Rename"] = "Renommer",
        ["FileManager.Delete"] = "Supprimer",
        ["FileManager.Name"] = "Nom",
        ["FileManager.Size"] = "Taille",
        ["FileManager.Modified"] = "Modifié",
        ["FileManager.Loading"] = "Chargement…",

        // AudioPlayer
        ["AudioPlayer.Label"] = "Lecteur audio",
        ["AudioPlayer.Play"] = "Lecture",
        ["AudioPlayer.Pause"] = "Pause",
        ["AudioPlayer.Seek"] = "Position de lecture",
        ["AudioPlayer.Mute"] = "Couper le son",
        ["AudioPlayer.Unmute"] = "Rétablir le son",
        ["AudioPlayer.Download"] = "Télécharger l'audio",

        // ThemeSwitcher
        ["Theme.Color"] = "Couleur",
        ["Theme.Mode"] = "Mode",
    };
}
