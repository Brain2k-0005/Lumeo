namespace Lumeo.Services.Localization;

internal static partial class LumeoDefaultStrings
{
    internal static readonly IReadOnlyDictionary<string, string> Pt = new Dictionary<string, string>
    {
        // DataGrid
        ["DataGrid.NoData"] = "Sem dados",
        ["DataGrid.Loading"] = "A carregar…",
        ["DataGrid.SearchPlaceholder"] = "Pesquisar…",
        ["DataGrid.ClearSearch"] = "Limpar pesquisa",
        ["DataGrid.Columns"] = "Colunas",
        ["DataGrid.ToggleColumns"] = "Mostrar/ocultar colunas",
        ["DataGrid.ExportCsv"] = "Exportar CSV",
        ["DataGrid.ExportExcel"] = "Exportar Excel",
        ["DataGrid.ExportJson"] = "Exportar JSON",
        ["DataGrid.Export"] = "Exportar",
        ["DataGrid.Filter"] = "Filtro",
        ["DataGrid.Filters"] = "Filtros",
        ["DataGrid.ClearFilters"] = "Limpar filtros",
        ["DataGrid.ResetLayout"] = "Repor esquema",
        ["DataGrid.SaveLayout"] = "Guardar esquema",
        ["DataGrid.Layouts"] = "Esquemas",
        ["DataGrid.NewLayout"] = "Novo esquema",
        ["DataGrid.Personal"] = "Pessoal",
        ["DataGrid.Global"] = "Global",
        ["DataGrid.SystemDefault"] = "Padrão do sistema",
        ["DataGrid.LayoutName"] = "Nome do esquema",
        ["DataGrid.Save"] = "Guardar",
        ["DataGrid.Cancel"] = "Cancelar",
        ["DataGrid.Delete"] = "Eliminar",
        ["DataGrid.Rename"] = "Renomear",
        ["DataGrid.PinLeft"] = "Fixar à esquerda",
        ["DataGrid.PinRight"] = "Fixar à direita",
        ["DataGrid.Unpin"] = "Desafixar",
        ["DataGrid.PinColumn"] = "Fixar coluna",
        ["DataGrid.ResizeColumn"] = "Redimensionar coluna (use as setas, duplo clique para ajuste automático)",
        ["DataGrid.DragToReorder"] = "Arrastar para reordenar a coluna",
        ["DataGrid.FilterColumn"] = "Filtrar {0}",
        ["DataGrid.SelectRow"] = "Selecionar linha {0}",
        ["DataGrid.SelectAllRows"] = "Selecionar todas as linhas",
        ["DataGrid.ExpandRow"] = "Expandir linha {0}",
        ["DataGrid.CollapseRow"] = "Reduzir linha {0}",
        ["DataGrid.ExpandGroup"] = "Expandir grupo {0}",
        ["DataGrid.CollapseGroup"] = "Reduzir grupo {0}",
        ["DataGrid.Hide"] = "Ocultar",
        ["DataGrid.Show"] = "Mostrar",
        ["DataGrid.SortAscending"] = "Ordem crescente",
        ["DataGrid.SortDescending"] = "Ordem decrescente",
        ["DataGrid.ClearSort"] = "Limpar ordenação",
        ["DataGrid.Edit"] = "Editar",
        ["DataGrid.CommitEdit"] = "Guardar",
        ["DataGrid.CancelEdit"] = "Cancelar",
        ["DataGrid.AggregateSum"] = "Soma",
        ["DataGrid.AggregateAvg"] = "Méd.",
        ["DataGrid.AggregateCount"] = "Total",
        ["DataGrid.AggregateRow"] = "Linha de agregação",
        ["DataGrid.AggregateMin"] = "Mín",
        ["DataGrid.AggregateMax"] = "Máx",
        ["DataGrid.Items"] = "itens",
        ["DataGrid.ItemsCount"] = "{0} itens",
        ["DataGrid.ItemsCount.One"] = "{0} item",
        ["DataGrid.ItemsCount.Other"] = "{0} itens",
        ["DataGrid.CopySelected"] = "Copiar ({0})",
        ["DataGrid.ApplyLayout"] = "Aplicar esquema",
        ["DataGrid.NoSavedLayouts"] = "Sem esquemas guardados.",
        ["DataGrid.SaveCurrentLayout"] = "Guardar esquema atual…",
        ["DataGrid.Default"] = "Padrão",
        ["DataGrid.MoveUp"] = "Mover para cima",
        ["DataGrid.MoveDown"] = "Mover para baixo",
        ["DataGrid.Retry"] = "Tentar novamente",
        ["DataGrid.ErrorLoadingData"] = "Falha ao carregar: {0}",
        ["DataGrid.ExpandFullscreen"] = "Expandir para ecrã inteiro",
        ["DataGrid.ExitFullscreen"] = "Sair do ecrã inteiro",
        ["DataGrid.AddGroupLevel"] = "+ Adicionar nível de agrupamento",
        ["DataGrid.GroupPanelPlaceholder"] = "Arraste um cabeçalho de coluna agrupável para aqui ou utilize a lista pendente",
        ["DataGrid.DragToGroup"] = "Arrastar para agrupar por esta coluna",
        ["DataGrid.RemoveGrouping"] = "Remover agrupamento",
        ["DataGrid.ClearAllGrouping"] = "Limpar todo o agrupamento",
        ["DataGrid.DragToReorderRow"] = "Arrastar para reordenar a linha",
        ["DataGrid.RowReorderUnavailable"] = "A reordenação de linhas não está disponível durante o agrupamento ou a virtualização",
        ["Filter.FilterTitle"] = "Filtro: {0}",

        // Pagination
        ["Pagination.Previous"] = "Anterior",
        ["Pagination.Next"] = "Seguinte",
        ["Pagination.First"] = "Primeira",
        ["Pagination.Last"] = "Última",
        ["Pagination.Page"] = "Página",
        ["Pagination.MorePages"] = "Mais páginas",
        ["Pagination.GoToPage"] = "Ir para a página {0}",
        ["Pagination.RowsPerPage"] = "Linhas por página",
        ["Pagination.RangeOfTotal"] = "{0}–{1} de {2}",

        // Filter operators
        ["Filter.Contains"] = "contém",
        ["Filter.DoesNotContain"] = "não contém",
        ["Filter.Equals"] = "igual a",
        ["Filter.NotEquals"] = "diferente de",
        ["Filter.StartsWith"] = "começa com",
        ["Filter.EndsWith"] = "termina com",
        ["Filter.GreaterThan"] = "maior que",
        ["Filter.LessThan"] = "menor que",
        ["Filter.GreaterThanOrEqual"] = "maior ou igual",
        ["Filter.LessThanOrEqual"] = "menor ou igual",
        ["Filter.Between"] = "entre",
        ["Filter.IsEmpty"] = "está vazio",
        ["Filter.IsNotEmpty"] = "não está vazio",
        ["Filter.Apply"] = "Aplicar",
        ["Filter.Clear"] = "Limpar",
        ["Filter.Value"] = "Valor",
        ["Filter.SelectAll"] = "Todos",
        ["Filter.ValuePlaceholder"] = "Valor…",
        ["Filter.ToValuePlaceholder"] = "Até…",

        // ColorPicker
        ["ColorPicker.PickColor"] = "Escolher cor",
        ["ColorPicker.Hue"] = "Matiz",
        ["ColorPicker.Saturation"] = "Saturação",
        ["ColorPicker.Lightness"] = "Luminosidade",
        ["ColorPicker.Value"] = "Valor",
        ["ColorPicker.Opacity"] = "Opacidade",
        ["ColorPicker.Hex"] = "Hex",
        ["ColorPicker.HexValue"] = "Valor hex",
        ["ColorPicker.Red"] = "Vermelho",
        ["ColorPicker.Green"] = "Verde",
        ["ColorPicker.Blue"] = "Azul",
        ["ColorPicker.Presets"] = "Predefinições",

        // PasswordInput
        ["Password.Placeholder"] = "Introduzir palavra-passe",
        ["Password.Toggle"] = "Mostrar/ocultar palavra-passe",
        ["Password.Weak"] = "Fraca",
        ["Password.Fair"] = "Razoável",
        ["Password.Good"] = "Boa",
        ["Password.Strong"] = "Forte",

        // FileUpload
        ["FileUpload.DragDrop"] = "Arraste e largue ficheiros aqui",
        ["FileUpload.Or"] = "ou",
        ["FileUpload.Browse"] = "Procurar",
        ["FileUpload.MaxSize"] = "Tam. máx.: {0}",
        ["FileUpload.Accepted"] = "Aceites: {0}",
        ["FileUpload.Remove"] = "Remover",
        ["FileUpload.Uploading"] = "A enviar…",
        ["FileUpload.Uploaded"] = "Enviado",
        ["FileUpload.Failed"] = "Falhou",
        ["FileUpload.Retry"] = "Tentar novamente",
        ["FileUpload.TooLarge"] = "Ficheiro demasiado grande",
        ["FileUpload.TypeNotAllowed"] = "Tipo de ficheiro não permitido",
        ["FileUpload.ChooseFile"] = "Escolher ficheiro",
        ["FileUpload.ClickToUpload"] = "Clique para enviar ou arraste aqui",

        // Overlays
        ["Dialog.Close"] = "Fechar",
        ["Dialog.Confirm"] = "Confirmar",
        ["Dialog.Cancel"] = "Cancelar",
        ["Dialog.Ok"] = "OK",
        ["Dialog.Yes"] = "Sim",
        ["Dialog.No"] = "Não",
        ["Toast.Close"] = "Fechar",
        ["Toast.Dismiss"] = "Dispensar",
        ["AlertDialog.Delete"] = "Eliminar",
        ["AlertDialog.Continue"] = "Continuar",

        // Combobox / Command / Select
        ["Combobox.Placeholder"] = "Selecionar…",
        ["Combobox.SearchPlaceholder"] = "Pesquisar…",
        ["Combobox.NoResults"] = "Sem resultados",
        ["Combobox.Loading"] = "A carregar…",
        ["Combobox.Clear"] = "Limpar",
        ["Combobox.Create"] = "Criar \"{0}\"",
        ["Command.Placeholder"] = "Escreva um comando ou pesquise…",
        ["Command.NoResults"] = "Sem resultados",
        ["Select.Placeholder"] = "Selecione uma opção",

        // Calendar / DatePicker
        ["Calendar.Today"] = "Hoje",
        ["Calendar.Clear"] = "Limpar",
        ["Calendar.PrevMonth"] = "Mês anterior",
        ["Calendar.NextMonth"] = "Mês seguinte",
        ["Calendar.PrevYear"] = "Ano anterior",
        ["Calendar.NextYear"] = "Ano seguinte",
        ["DatePicker.Placeholder"] = "Escolher data",
        ["DateRange.Placeholder"] = "Escolher intervalo de datas",
        ["DateRange.From"] = "De",
        ["DateRange.To"] = "Até",
        ["DateTimePicker.Placeholder"] = "Escolher data e hora",
        ["DateTimePicker.TimeLabel"] = "Hora",
        ["TimePicker.Placeholder"] = "Escolher hora",

        // Tour
        ["Tour.Skip"] = "Saltar",
        ["Tour.Previous"] = "Anterior",
        ["Tour.Next"] = "Seguinte",
        ["Tour.Finish"] = "Concluir",

        // PopConfirm
        ["PopConfirm.Title"] = "Tem a certeza?",
        ["PopConfirm.Confirm"] = "Sim",
        ["PopConfirm.Cancel"] = "Não",

        // Misc
        ["Common.Search"] = "Pesquisar",
        ["Common.Clear"] = "Limpar",
        ["Common.ClearAll"] = "Limpar tudo",
        ["Common.Close"] = "Fechar",
        ["Common.Loading"] = "A carregar…",
        ["Common.NoResults"] = "Sem resultados",
        ["Common.Apply"] = "Aplicar",
        ["Common.Reset"] = "Repor",
        ["Common.Save"] = "Guardar",
        ["Common.Cancel"] = "Cancelar",
        ["Common.Copy"] = "Copiar",
        ["Common.Copied"] = "Copiado",
        ["Common.More"] = "Mais",
        ["Common.Back"] = "Voltar",
        ["Common.Next"] = "Seguinte",
        ["Common.ShowMore"] = "Mostrar mais",
        ["Common.ShowLess"] = "Mostrar menos",

        // Transfer / TreeSelect / TagInput
        ["Transfer.SourceHeader"] = "Disponíveis",
        ["Transfer.TargetHeader"] = "Selecionados",
        ["Transfer.MoveRight"] = "Mover para a direita",
        ["Transfer.MoveLeft"] = "Mover para a esquerda",
        ["Transfer.NoItems"] = "Sem itens",
        ["TreeSelect.Placeholder"] = "Selecionar…",
        ["TreeSelect.NoResults"] = "Sem resultados",
        ["TagInput.Placeholder"] = "Adicionar etiqueta…",

        // Cascader
        ["Cascader.Placeholder"] = "Selecionar…",

        // Rating / OTP
        ["Rating.Rate"] = "Avaliar",
        ["Rating.RateOf"] = "Avaliação {0} de {1}",
        ["Otp.Placeholder"] = "Introduzir código",

        // Empty
        ["Empty.Title"] = "Nada por aqui",
        ["Empty.Description"] = "Sem dados a apresentar.",

        // Kanban
        ["Kanban.AddCard"] = "Adicionar cartão",

        // Carousel
        ["Carousel.PreviousSlide"] = "Slide anterior",
        ["Carousel.NextSlide"] = "Próximo slide",
        ["Carousel.SlideXofY"] = "Slide {0} de {1}",

        // Stepper
        ["Stepper.Back"] = "Voltar",
        ["Stepper.Next"] = "Próximo",
        ["Stepper.Finish"] = "Concluir",
        ["Stepper.Optional"] = "Opcional",
        ["Stepper.Skip"] = "Pular",

        // Window
        ["Window.Close"] = "Fechar",
        ["Window.Minimize"] = "Minimizar",
        ["Window.Maximize"] = "Maximizar",
        ["Window.Restore"] = "Restaurar",

        // NumberInput
        ["NumberInput.Decrease"] = "Diminuir",
        ["NumberInput.Increase"] = "Aumentar",

        // DateTimePicker
        ["DateTimePicker.ClearDate"] = "Limpar data",

        // Slider
        ["Slider.End"] = "fim",

        // FileManager
        ["FileManager.EmptyTitle"] = "Esta pasta está vazia",
        ["FileManager.EmptyState"] = "Nenhum arquivo ou pasta aqui.",
        ["FileManager.MoreActions"] = "Mais ações",
        ["FileManager.MoreActionsForName"] = "Mais ações para {0}",

        // FileUpload (parameterised)
        ["FileUpload.ExceedsMaxSize"] = "O arquivo \"{0}\" excede o tamanho máximo de {1}.",

        // ConfirmButton
        ["ConfirmButton.Title"] = "Tem a certeza?",
        ["ConfirmButton.Confirm"] = "Continuar",
        ["ConfirmButton.Cancel"] = "Cancelar",

        // PickList (panels)
        ["PickList.SourceHeader"] = "Disponíveis",
        ["PickList.TargetHeader"] = "Selecionados",
        ["PickList.NoItems"] = "Sem itens",

        // FileManager (context menu / list view)
        ["FileManager.Open"] = "Abrir",
        ["FileManager.Rename"] = "Renomear",
        ["FileManager.Delete"] = "Eliminar",
        ["FileManager.Name"] = "Nome",
        ["FileManager.Size"] = "Tamanho",
        ["FileManager.Modified"] = "Modificado",
        ["FileManager.Loading"] = "A carregar…",

        // AudioPlayer
        ["AudioPlayer.Label"] = "Reprodutor de áudio",
        ["AudioPlayer.Play"] = "Reproduzir",
        ["AudioPlayer.Pause"] = "Pausar",
        ["AudioPlayer.Seek"] = "Posição de reprodução",
        ["AudioPlayer.Mute"] = "Silenciar",
        ["AudioPlayer.Unmute"] = "Ativar som",
        ["AudioPlayer.Download"] = "Transferir áudio",

        // ThemeSwitcher
        ["Theme.Color"] = "Cor",
        ["Theme.Mode"] = "Modo",
    };
}
