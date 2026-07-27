namespace Lumeo.Services.Localization;

internal static partial class LumeoDefaultStrings
{
    internal static readonly IReadOnlyDictionary<string, string> Es = new Dictionary<string, string>
    {
        // DataGrid
        ["DataGrid.NoData"] = "Sin datos",
        ["DataGrid.Loading"] = "Cargando…",
        ["DataGrid.SearchPlaceholder"] = "Buscar…",
        ["DataGrid.ClearSearch"] = "Borrar búsqueda",
        ["DataGrid.Columns"] = "Columnas",
        ["DataGrid.ToggleColumns"] = "Mostrar/ocultar columnas",
        ["DataGrid.ExportCsv"] = "Exportar CSV",
        ["DataGrid.ExportExcel"] = "Exportar Excel",
        ["DataGrid.ExportJson"] = "Exportar JSON",
        ["DataGrid.Export"] = "Exportar",
        ["DataGrid.Filter"] = "Filtro",
        ["DataGrid.Filters"] = "Filtros",
        ["DataGrid.ClearFilters"] = "Limpiar filtros",
        ["DataGrid.ResetLayout"] = "Restablecer diseño",
        ["DataGrid.SaveLayout"] = "Guardar diseño",
        ["DataGrid.Layouts"] = "Diseños",
        ["DataGrid.NewLayout"] = "Nuevo diseño",
        ["DataGrid.Personal"] = "Personal",
        ["DataGrid.Global"] = "Global",
        ["DataGrid.SystemDefault"] = "Predeterminado del sistema",
        ["DataGrid.LayoutName"] = "Nombre del diseño",
        ["DataGrid.Save"] = "Guardar",
        ["DataGrid.Cancel"] = "Cancelar",
        ["DataGrid.Delete"] = "Eliminar",
        ["DataGrid.Rename"] = "Renombrar",
        ["DataGrid.PinLeft"] = "Fijar a la izquierda",
        ["DataGrid.PinRight"] = "Fijar a la derecha",
        ["DataGrid.Unpin"] = "Desanclar",
        ["DataGrid.PinColumn"] = "Fijar columna",
        ["DataGrid.ResizeColumn"] = "Cambiar tamaño de columna (use las flechas, doble clic para ajuste automático)",
        ["DataGrid.DragToReorder"] = "Arrastrar para reordenar la columna",
        ["DataGrid.ColumnMovedAnnouncement"] = "{0} movido a la posición {1} de {2}",
        ["DataGrid.FilterColumn"] = "Filtrar {0}",
        ["DataGrid.SelectRow"] = "Seleccionar fila {0}",
        ["DataGrid.SelectAllRows"] = "Seleccionar todas las filas",
        ["DataGrid.ExpandRow"] = "Expandir fila {0}",
        ["DataGrid.CollapseRow"] = "Contraer fila {0}",
        ["DataGrid.ExpandGroup"] = "Expandir grupo {0}",
        ["DataGrid.CollapseGroup"] = "Contraer grupo {0}",
        ["DataGrid.Hide"] = "Ocultar",
        ["DataGrid.Show"] = "Mostrar",
        ["DataGrid.SortAscending"] = "Orden ascendente",
        ["DataGrid.SortDescending"] = "Orden descendente",
        ["DataGrid.ClearSort"] = "Quitar orden",
        ["DataGrid.Edit"] = "Editar",
        ["DataGrid.CommitEdit"] = "Guardar",
        ["DataGrid.CancelEdit"] = "Cancelar",
        ["DataGrid.AggregateSum"] = "Suma",
        ["DataGrid.AggregateAvg"] = "Prom.",
        ["DataGrid.AggregateCount"] = "Total",
        ["DataGrid.AggregateRow"] = "Fila de agregación",
        ["DataGrid.AggregateMin"] = "Mín",
        ["DataGrid.AggregateMax"] = "Máx",
        ["DataGrid.Items"] = "elementos",
        ["DataGrid.ItemsCount"] = "{0} elementos",
        ["DataGrid.ItemsCount.One"] = "{0} elemento",
        ["DataGrid.ItemsCount.Other"] = "{0} elementos",
        ["DataGrid.CopySelected"] = "Copiar ({0})",
        ["DataGrid.ApplyLayout"] = "Aplicar diseño",
        ["DataGrid.NoSavedLayouts"] = "Aún no hay diseños guardados.",
        ["DataGrid.SaveCurrentLayout"] = "Guardar diseño actual…",
        ["DataGrid.Default"] = "Predeterminado",
        ["DataGrid.MoveUp"] = "Subir",
        ["DataGrid.MoveDown"] = "Bajar",
        ["DataGrid.Retry"] = "Reintentar",
        ["DataGrid.ErrorLoadingData"] = "Error al cargar los datos: {0}",
        ["DataGrid.ExpandFullscreen"] = "Expandir a pantalla completa",
        ["DataGrid.ExitFullscreen"] = "Salir de pantalla completa",
        ["DataGrid.AddGroupLevel"] = "+ Añadir nivel de agrupación",
        ["DataGrid.GroupPanelPlaceholder"] = "Arrastre aquí un encabezado de columna agrupable o use la lista desplegable",
        ["DataGrid.DragToGroup"] = "Arrastrar para agrupar por esta columna",
        ["DataGrid.RemoveGrouping"] = "Quitar agrupación",
        ["DataGrid.ClearAllGrouping"] = "Borrar toda la agrupación",
        ["DataGrid.DragToReorderRow"] = "Arrastrar para reordenar la fila",
        ["DataGrid.RowReorderUnavailable"] = "El reordenamiento de filas no está disponible durante la agrupación o la virtualización",
        ["Filter.FilterTitle"] = "Filtro: {0}",

        // Pagination
        ["Pagination.Previous"] = "Anterior",
        ["Pagination.Next"] = "Siguiente",
        ["Pagination.First"] = "Primera",
        ["Pagination.Last"] = "Última",
        ["Pagination.Page"] = "Página",
        ["Pagination.MorePages"] = "Más páginas",
        ["Pagination.GoToPage"] = "Ir a la página {0}",
        ["Pagination.RowsPerPage"] = "Filas por página",
        ["Pagination.RangeOfTotal"] = "{0}–{1} de {2}",

        // Filter operators
        ["Filter.Contains"] = "contiene",
        ["Filter.DoesNotContain"] = "no contiene",
        ["Filter.Equals"] = "igual a",
        ["Filter.NotEquals"] = "distinto de",
        ["Filter.StartsWith"] = "empieza con",
        ["Filter.EndsWith"] = "termina con",
        ["Filter.GreaterThan"] = "mayor que",
        ["Filter.LessThan"] = "menor que",
        ["Filter.GreaterThanOrEqual"] = "mayor o igual",
        ["Filter.LessThanOrEqual"] = "menor o igual",
        ["Filter.Between"] = "entre",
        ["Filter.IsEmpty"] = "está vacío",
        ["Filter.IsNotEmpty"] = "no está vacío",
        ["Filter.Apply"] = "Aplicar",
        ["Filter.Clear"] = "Limpiar",
        ["Filter.Value"] = "Valor",
        ["Filter.SelectAll"] = "Todos",
        ["Filter.ValuePlaceholder"] = "Valor…",
        ["Filter.ToValuePlaceholder"] = "Hasta…",

        // ColorPicker
        ["ColorPicker.PickColor"] = "Elegir un color",
        ["ColorPicker.Hue"] = "Tono",
        ["ColorPicker.Saturation"] = "Saturación",
        ["ColorPicker.Lightness"] = "Luminosidad",
        ["ColorPicker.Value"] = "Valor",
        ["ColorPicker.Opacity"] = "Opacidad",
        ["ColorPicker.Hex"] = "Hex",
        ["ColorPicker.HexValue"] = "Valor hex",
        ["ColorPicker.Red"] = "Rojo",
        ["ColorPicker.Green"] = "Verde",
        ["ColorPicker.Blue"] = "Azul",
        ["ColorPicker.Presets"] = "Preajustes",

        // PasswordInput
        ["Password.Placeholder"] = "Introducir contraseña",
        ["Password.Toggle"] = "Mostrar/ocultar contraseña",
        ["Password.Weak"] = "Débil",
        ["Password.Fair"] = "Aceptable",
        ["Password.Good"] = "Buena",
        ["Password.Strong"] = "Fuerte",

        // FileUpload
        ["FileUpload.DragDrop"] = "Arrastra y suelta archivos aquí",
        ["FileUpload.Or"] = "o",
        ["FileUpload.Browse"] = "Examinar",
        ["FileUpload.MaxSize"] = "Tamaño máx.: {0}",
        ["FileUpload.Accepted"] = "Aceptados: {0}",
        ["FileUpload.Remove"] = "Quitar",
        ["FileUpload.Uploading"] = "Subiendo…",
        ["FileUpload.Uploaded"] = "Subido",
        ["FileUpload.Failed"] = "Error",
        ["FileUpload.Retry"] = "Reintentar",
        ["FileUpload.TooLarge"] = "Archivo demasiado grande",
        ["FileUpload.TypeNotAllowed"] = "Tipo de archivo no permitido",
        ["FileUpload.ChooseFile"] = "Elegir archivo",
        ["FileUpload.ClickToUpload"] = "Haz clic para subir o arrastra aquí",

        // Overlays
        ["Dialog.Close"] = "Cerrar",
        ["Dialog.Confirm"] = "Confirmar",
        ["Dialog.Cancel"] = "Cancelar",
        ["Dialog.Ok"] = "Aceptar",
        ["Dialog.Yes"] = "Sí",
        ["Dialog.No"] = "No",
        ["Toast.Close"] = "Cerrar",
        ["Toast.Dismiss"] = "Descartar",
        ["AlertDialog.Delete"] = "Eliminar",
        ["AlertDialog.Continue"] = "Continuar",

        // Combobox / Command / Select
        ["Combobox.Placeholder"] = "Seleccionar…",
        ["Combobox.SearchPlaceholder"] = "Buscar…",
        ["Combobox.NoResults"] = "Sin resultados",
        ["Combobox.Loading"] = "Cargando…",
        ["Combobox.Clear"] = "Limpiar",
        ["Combobox.Create"] = "Crear \"{0}\"",
        ["Command.Placeholder"] = "Escribe un comando o busca…",
        ["Command.NoResults"] = "Sin resultados",
        ["Select.Placeholder"] = "Selecciona una opción",

        // Calendar / DatePicker
        ["Calendar.Today"] = "Hoy",
        ["Calendar.Clear"] = "Limpiar",
        ["Calendar.PrevMonth"] = "Mes anterior",
        ["Calendar.NextMonth"] = "Mes siguiente",
        ["Calendar.PrevYear"] = "Año anterior",
        ["Calendar.NextYear"] = "Año siguiente",
        ["DatePicker.Placeholder"] = "Elegir fecha",
        ["DateRange.Placeholder"] = "Elegir rango de fechas",
        ["DateRange.From"] = "Desde",
        ["DateRange.To"] = "Hasta",
        ["DateTimePicker.Placeholder"] = "Elegir fecha y hora",
        ["DateTimePicker.TimeLabel"] = "Hora",
        ["TimePicker.Placeholder"] = "Elegir hora",

        // Tour
        ["Tour.Skip"] = "Omitir",
        ["Tour.Previous"] = "Anterior",
        ["Tour.Next"] = "Siguiente",
        ["Tour.Finish"] = "Finalizar",

        // PopConfirm
        ["PopConfirm.Title"] = "¿Estás seguro?",
        ["PopConfirm.Confirm"] = "Sí",
        ["PopConfirm.Cancel"] = "No",

        // Misc
        ["Common.Search"] = "Buscar",
        ["Common.Clear"] = "Limpiar",
        ["Common.ClearAll"] = "Limpiar todo",
        ["Common.Close"] = "Cerrar",
        ["Common.Loading"] = "Cargando…",
        ["Common.NoResults"] = "Sin resultados",
        ["Common.Apply"] = "Aplicar",
        ["Common.Reset"] = "Restablecer",
        ["Common.Save"] = "Guardar",
        ["Common.Cancel"] = "Cancelar",
        ["Common.Copy"] = "Copiar",
        ["Common.Copied"] = "Copiado",
        ["Common.More"] = "Más",
        ["Common.Back"] = "Atrás",
        ["Common.Next"] = "Siguiente",
        ["Common.ShowMore"] = "Mostrar más",
        ["Common.ShowLess"] = "Mostrar menos",

        // Transfer / TreeSelect / TagInput
        ["Transfer.SourceHeader"] = "Disponibles",
        ["Transfer.TargetHeader"] = "Seleccionados",
        ["Transfer.MoveRight"] = "Mover a la derecha",
        ["Transfer.MoveLeft"] = "Mover a la izquierda",
        ["Transfer.NoItems"] = "Sin elementos",
        ["TreeSelect.Placeholder"] = "Seleccionar…",
        ["TreeSelect.NoResults"] = "Sin resultados",
        ["TagInput.Placeholder"] = "Añadir etiqueta…",

        // Cascader
        ["Cascader.Placeholder"] = "Seleccionar…",

        // Rating / OTP
        ["Rating.Rate"] = "Valorar",
        ["Rating.RateOf"] = "Valoración {0} de {1}",
        ["Otp.Placeholder"] = "Introducir código",

        // Empty
        ["Empty.Title"] = "Nada por aquí",
        ["Empty.Description"] = "No hay datos para mostrar.",

        // Kanban
        ["Kanban.AddCard"] = "Añadir tarjeta",

        // Carousel
        ["Carousel.PreviousSlide"] = "Diapositiva anterior",
        ["Carousel.NextSlide"] = "Siguiente diapositiva",
        ["Carousel.SlideXofY"] = "Diapositiva {0} de {1}",

        // Stepper
        ["Stepper.Back"] = "Atrás",
        ["Stepper.Next"] = "Siguiente",
        ["Stepper.Finish"] = "Finalizar",
        ["Stepper.Optional"] = "Opcional",
        ["Stepper.Skip"] = "Omitir",

        // Window
        ["Window.Close"] = "Cerrar",
        ["Window.Minimize"] = "Minimizar",
        ["Window.Maximize"] = "Maximizar",
        ["Window.Restore"] = "Restaurar",

        // NumberInput
        ["NumberInput.Decrease"] = "Disminuir",
        ["NumberInput.Increase"] = "Aumentar",

        // DateTimePicker
        ["DateTimePicker.ClearDate"] = "Borrar fecha",

        // Slider
        ["Slider.End"] = "final",

        // FileManager
        ["FileManager.EmptyTitle"] = "Esta carpeta está vacía",
        ["FileManager.EmptyState"] = "No hay archivos ni carpetas aquí.",
        ["FileManager.MoreActions"] = "Más acciones",
        ["FileManager.MoreActionsForName"] = "Más acciones para {0}",

        // FileUpload (parameterised)
        ["FileUpload.ExceedsMaxSize"] = "El archivo «{0}» supera el tamaño máximo de {1}.",

        // ConfirmButton
        ["ConfirmButton.Title"] = "¿Estás seguro?",
        ["ConfirmButton.Confirm"] = "Continuar",
        ["ConfirmButton.Cancel"] = "Cancelar",

        // PickList (panels)
        ["PickList.SourceHeader"] = "Disponibles",
        ["PickList.TargetHeader"] = "Seleccionados",
        ["PickList.NoItems"] = "Sin elementos",

        // FileManager (context menu / list view)
        ["FileManager.Open"] = "Abrir",
        ["FileManager.Rename"] = "Renombrar",
        ["FileManager.Delete"] = "Eliminar",
        ["FileManager.Name"] = "Nombre",
        ["FileManager.Size"] = "Tamaño",
        ["FileManager.Modified"] = "Modificado",
        ["FileManager.Loading"] = "Cargando…",

        // AudioPlayer
        ["AudioPlayer.Label"] = "Reproductor de audio",
        ["AudioPlayer.Play"] = "Reproducir",
        ["AudioPlayer.Pause"] = "Pausar",
        ["AudioPlayer.Seek"] = "Posición de reproducción",
        ["AudioPlayer.Mute"] = "Silenciar",
        ["AudioPlayer.Unmute"] = "Activar sonido",
        ["AudioPlayer.Download"] = "Descargar audio",

        // ThemeSwitcher
        ["Theme.Color"] = "Color",
        ["Theme.Mode"] = "Modo",

        // Gantt (Codex round 4, P2 #6 — locale completeness: previously only en/de had any Gantt.* keys)
        ["Gantt.Day"] = "Día",
        ["Gantt.Week"] = "Semana",
        ["Gantt.Month"] = "Mes",
        ["Gantt.Year"] = "Año",
        ["Gantt.Today"] = "Hoy",
        ["Gantt.PreviousPeriod"] = "Período anterior",
        ["Gantt.NextPeriod"] = "Período siguiente",
        ["Gantt.ExpandRow"] = "Expandir {0}",
        ["Gantt.CollapseRow"] = "Contraer {0}",
        ["Gantt.NoTasksToDisplay"] = "No hay tareas que mostrar",
        ["Gantt.TaskAriaLabel"] = "{0}, del {1} al {2}",
        ["Gantt.PercentComplete"] = "{0}% completado",
    };
}
