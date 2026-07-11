namespace Lumeo.Services.Localization;

internal static partial class LumeoDefaultStrings
{
    internal static readonly IReadOnlyDictionary<string, string> Ar = new Dictionary<string, string>
    {
        // DataGrid
        ["DataGrid.NoData"] = "لا توجد بيانات",
        ["DataGrid.Loading"] = "جارٍ التحميل…",
        ["DataGrid.SearchPlaceholder"] = "بحث…",
        ["DataGrid.ClearSearch"] = "مسح البحث",
        ["DataGrid.Columns"] = "الأعمدة",
        ["DataGrid.ToggleColumns"] = "إظهار/إخفاء الأعمدة",
        ["DataGrid.ExportCsv"] = "تصدير CSV",
        ["DataGrid.ExportExcel"] = "تصدير Excel",
        ["DataGrid.ExportJson"] = "تصدير JSON",
        ["DataGrid.Export"] = "تصدير",
        ["DataGrid.Filter"] = "تصفية",
        ["DataGrid.Filters"] = "عوامل التصفية",
        ["DataGrid.ClearFilters"] = "مسح عوامل التصفية",
        ["DataGrid.ResetLayout"] = "إعادة تعيين التخطيط",
        ["DataGrid.SaveLayout"] = "حفظ التخطيط",
        ["DataGrid.Layouts"] = "التخطيطات",
        ["DataGrid.NewLayout"] = "تخطيط جديد",
        ["DataGrid.Personal"] = "شخصي",
        ["DataGrid.Global"] = "عام",
        ["DataGrid.SystemDefault"] = "افتراضي النظام",
        ["DataGrid.LayoutName"] = "اسم التخطيط",
        ["DataGrid.Save"] = "حفظ",
        ["DataGrid.Cancel"] = "إلغاء",
        ["DataGrid.Delete"] = "حذف",
        ["DataGrid.Rename"] = "إعادة تسمية",
        ["DataGrid.PinLeft"] = "تثبيت إلى اليسار",
        ["DataGrid.PinRight"] = "تثبيت إلى اليمين",
        ["DataGrid.Unpin"] = "إلغاء التثبيت",
        ["DataGrid.ResizeColumn"] = "تغيير حجم العمود (استخدم مفاتيح الأسهم، انقر نقرًا مزدوجًا للملاءمة التلقائية)",
        ["DataGrid.DragToReorder"] = "اسحب لإعادة ترتيب العمود",
        ["DataGrid.Hide"] = "إخفاء",
        ["DataGrid.Show"] = "إظهار",
        ["DataGrid.SortAscending"] = "ترتيب تصاعدي",
        ["DataGrid.SortDescending"] = "ترتيب تنازلي",
        ["DataGrid.ClearSort"] = "مسح الترتيب",
        ["DataGrid.Edit"] = "تعديل",
        ["DataGrid.CommitEdit"] = "حفظ",
        ["DataGrid.CancelEdit"] = "إلغاء",
        ["DataGrid.AggregateSum"] = "المجموع",
        ["DataGrid.AggregateAvg"] = "المتوسط",
        ["DataGrid.AggregateCount"] = "العدد",
        ["DataGrid.AggregateMin"] = "الأدنى",
        ["DataGrid.AggregateMax"] = "الأعلى",
        ["DataGrid.Items"] = "عنصر",
        ["DataGrid.ItemsCount"] = "{0} عنصر",
        // Arabic plural forms (CLDR collapsed): one (1) → "عنصر"; few (2 or 3-10) → "عنصرين"/"عناصر";
        // many (11-99) → "عنصرًا"; rest/zero collapse to many. PluralKey() in LumeoLocalizer
        // collapses CLDR zero/two into many/few so consumers only fill these four sub-keys.
        ["DataGrid.ItemsCount.One"] = "عنصر واحد",
        ["DataGrid.ItemsCount.Few"] = "{0} عناصر",
        ["DataGrid.ItemsCount.Many"] = "{0} عنصرًا",
        ["DataGrid.ItemsCount.Other"] = "{0} عنصر",
        ["DataGrid.CopySelected"] = "نسخ ({0})",
        ["DataGrid.ApplyLayout"] = "تطبيق التخطيط",
        ["DataGrid.NoSavedLayouts"] = "لا توجد تخطيطات محفوظة بعد.",
        ["DataGrid.SaveCurrentLayout"] = "حفظ التخطيط الحالي…",
        ["DataGrid.Default"] = "افتراضي",
        ["DataGrid.MoveUp"] = "تحريك لأعلى",
        ["DataGrid.MoveDown"] = "تحريك لأسفل",
        ["DataGrid.Retry"] = "إعادة المحاولة",
        ["DataGrid.ErrorLoadingData"] = "فشل التحميل: {0}",
        ["DataGrid.DragToReorderRow"] = "اسحب لإعادة ترتيب الصف",
        ["DataGrid.RowReorderUnavailable"] = "إعادة ترتيب الصفوف غير متاحة أثناء التجميع أو الافتراضية",
        ["Filter.FilterTitle"] = "تصفية: {0}",

        // Pagination
        ["Pagination.Previous"] = "السابق",
        ["Pagination.Next"] = "التالي",
        ["Pagination.First"] = "الأولى",
        ["Pagination.Last"] = "الأخيرة",
        ["Pagination.Page"] = "صفحة",
        ["Pagination.MorePages"] = "المزيد من الصفحات",
        ["Pagination.GoToPage"] = "الانتقال إلى الصفحة {0}",
        ["Pagination.RowsPerPage"] = "الصفوف في الصفحة",
        ["Pagination.RangeOfTotal"] = "{0}–{1} من {2}",

        // Filter operators
        ["Filter.Contains"] = "يحتوي على",
        ["Filter.DoesNotContain"] = "لا يحتوي على",
        ["Filter.Equals"] = "يساوي",
        ["Filter.NotEquals"] = "لا يساوي",
        ["Filter.StartsWith"] = "يبدأ بـ",
        ["Filter.EndsWith"] = "ينتهي بـ",
        ["Filter.GreaterThan"] = "أكبر من",
        ["Filter.LessThan"] = "أصغر من",
        ["Filter.GreaterThanOrEqual"] = "أكبر من أو يساوي",
        ["Filter.LessThanOrEqual"] = "أصغر من أو يساوي",
        ["Filter.Between"] = "بين",
        ["Filter.IsEmpty"] = "فارغ",
        ["Filter.IsNotEmpty"] = "غير فارغ",
        ["Filter.Apply"] = "تطبيق",
        ["Filter.Clear"] = "مسح",
        ["Filter.Value"] = "القيمة",
        ["Filter.SelectAll"] = "الكل",
        ["Filter.ValuePlaceholder"] = "القيمة…",
        ["Filter.ToValuePlaceholder"] = "إلى القيمة…",

        // ColorPicker
        ["ColorPicker.PickColor"] = "اختر لونًا",
        ["ColorPicker.Hue"] = "درجة اللون",
        ["ColorPicker.Saturation"] = "التشبع",
        ["ColorPicker.Lightness"] = "الإضاءة",
        ["ColorPicker.Value"] = "القيمة",
        ["ColorPicker.Opacity"] = "الشفافية",
        ["ColorPicker.Hex"] = "Hex",
        ["ColorPicker.HexValue"] = "قيمة Hex",
        ["ColorPicker.Red"] = "أحمر",
        ["ColorPicker.Green"] = "أخضر",
        ["ColorPicker.Blue"] = "أزرق",
        ["ColorPicker.Presets"] = "الإعدادات المسبقة",

        // PasswordInput
        ["Password.Placeholder"] = "أدخل كلمة المرور",
        ["Password.Toggle"] = "إظهار/إخفاء كلمة المرور",
        ["Password.Weak"] = "ضعيفة",
        ["Password.Fair"] = "مقبولة",
        ["Password.Good"] = "جيدة",
        ["Password.Strong"] = "قوية",

        // FileUpload
        ["FileUpload.DragDrop"] = "اسحب الملفات وأفلتها هنا",
        ["FileUpload.Or"] = "أو",
        ["FileUpload.Browse"] = "تصفح",
        ["FileUpload.MaxSize"] = "الحد الأقصى: {0}",
        ["FileUpload.Accepted"] = "المقبولة: {0}",
        ["FileUpload.Remove"] = "إزالة",
        ["FileUpload.Uploading"] = "جارٍ الرفع…",
        ["FileUpload.Uploaded"] = "تم الرفع",
        ["FileUpload.Failed"] = "فشل",
        ["FileUpload.Retry"] = "إعادة المحاولة",
        ["FileUpload.TooLarge"] = "الملف كبير جدًا",
        ["FileUpload.TypeNotAllowed"] = "نوع الملف غير مسموح",
        ["FileUpload.ChooseFile"] = "اختر ملفًا",
        ["FileUpload.ClickToUpload"] = "انقر للرفع أو اسحب وأفلت",

        // Overlays
        ["Dialog.Close"] = "إغلاق",
        ["Dialog.Confirm"] = "تأكيد",
        ["Dialog.Cancel"] = "إلغاء",
        ["Dialog.Ok"] = "موافق",
        ["Dialog.Yes"] = "نعم",
        ["Dialog.No"] = "لا",
        ["Toast.Close"] = "إغلاق",
        ["Toast.Dismiss"] = "تجاهل",
        ["AlertDialog.Delete"] = "حذف",
        ["AlertDialog.Continue"] = "متابعة",

        // Combobox / Command / Select
        ["Combobox.Placeholder"] = "اختر…",
        ["Combobox.SearchPlaceholder"] = "بحث…",
        ["Combobox.NoResults"] = "لا توجد نتائج",
        ["Combobox.Loading"] = "جارٍ التحميل…",
        ["Combobox.Clear"] = "مسح",
        ["Combobox.Create"] = "إنشاء \"{0}\"",
        ["Command.Placeholder"] = "اكتب أمرًا أو ابحث…",
        ["Command.NoResults"] = "لا توجد نتائج",
        ["Select.Placeholder"] = "اختر خيارًا",

        // Calendar / DatePicker
        ["Calendar.Today"] = "اليوم",
        ["Calendar.Clear"] = "مسح",
        ["Calendar.PrevMonth"] = "الشهر السابق",
        ["Calendar.NextMonth"] = "الشهر التالي",
        ["Calendar.PrevYear"] = "السنة السابقة",
        ["Calendar.NextYear"] = "السنة التالية",
        ["DatePicker.Placeholder"] = "اختر تاريخًا",
        ["DateRange.Placeholder"] = "اختر نطاقًا زمنيًا",
        ["DateRange.From"] = "من",
        ["DateRange.To"] = "إلى",
        ["DateTimePicker.Placeholder"] = "اختر التاريخ والوقت",
        ["DateTimePicker.TimeLabel"] = "الوقت",
        ["TimePicker.Placeholder"] = "اختر الوقت",

        // Tour
        ["Tour.Skip"] = "تخطي",
        ["Tour.Previous"] = "السابق",
        ["Tour.Next"] = "التالي",
        ["Tour.Finish"] = "إنهاء",

        // PopConfirm
        ["PopConfirm.Title"] = "هل أنت متأكد؟",
        ["PopConfirm.Confirm"] = "نعم",
        ["PopConfirm.Cancel"] = "لا",

        // Misc
        ["Common.Search"] = "بحث",
        ["Common.Clear"] = "مسح",
        ["Common.ClearAll"] = "مسح الكل",
        ["Common.Close"] = "إغلاق",
        ["Common.Loading"] = "جارٍ التحميل…",
        ["Common.NoResults"] = "لا توجد نتائج",
        ["Common.Apply"] = "تطبيق",
        ["Common.Reset"] = "إعادة تعيين",
        ["Common.Save"] = "حفظ",
        ["Common.Cancel"] = "إلغاء",
        ["Common.Copy"] = "نسخ",
        ["Common.Copied"] = "تم النسخ",
        ["Common.More"] = "المزيد",
        ["Common.Back"] = "رجوع",
        ["Common.Next"] = "التالي",
        ["Common.ShowMore"] = "عرض المزيد",
        ["Common.ShowLess"] = "عرض أقل",

        // Transfer / TreeSelect / TagInput
        ["Transfer.SourceHeader"] = "المتاحة",
        ["Transfer.TargetHeader"] = "المحددة",
        ["Transfer.MoveRight"] = "نقل إلى اليمين",
        ["Transfer.MoveLeft"] = "نقل إلى اليسار",
        ["Transfer.NoItems"] = "لا توجد عناصر",
        ["TreeSelect.Placeholder"] = "اختر…",
        ["TreeSelect.NoResults"] = "لا توجد نتائج",
        ["TagInput.Placeholder"] = "أضف وسمًا…",

        // Cascader
        ["Cascader.Placeholder"] = "اختر…",

        // Rating / OTP
        ["Rating.Rate"] = "تقييم",
        ["Rating.RateOf"] = "تقييم {0} من {1}",
        ["Otp.Placeholder"] = "أدخل الرمز",

        // Empty
        ["Empty.Title"] = "لا شيء هنا بعد",
        ["Empty.Description"] = "لا توجد بيانات للعرض.",

        // Kanban
        ["Kanban.AddCard"] = "إضافة بطاقة",

        // Carousel
        ["Carousel.PreviousSlide"] = "الشريحة السابقة",
        ["Carousel.NextSlide"] = "الشريحة التالية",
        ["Carousel.SlideXofY"] = "الشريحة {0} من {1}",

        // Chart
        ["Chart.Loading"] = "جارٍ التحميل…",

        // Scheduler
        ["Scheduler.Today"] = "اليوم",
        ["Scheduler.Month"] = "شهر",
        ["Scheduler.Week"] = "أسبوع",
        ["Scheduler.Day"] = "يوم",
        ["Scheduler.List"] = "قائمة",
        ["Scheduler.WeekOf"] = "أسبوع {0}",

        // Gantt
        ["Gantt.Day"] = "يوم",
        ["Gantt.Week"] = "أسبوع",
        ["Gantt.Month"] = "شهر",
        ["Gantt.Year"] = "سنة",

        // Editor
        ["Editor.Paragraph"] = "فقرة",
        ["Editor.Heading1"] = "عنوان 1",
        ["Editor.Heading2"] = "عنوان 2",
        ["Editor.Heading3"] = "عنوان 3",
        ["Editor.Bold"] = "غامق",
        ["Editor.Italic"] = "مائل",
        ["Editor.Underline"] = "تسطير",
        ["Editor.Strikethrough"] = "يتوسطه خط",
        ["Editor.InlineCode"] = "كود مضمّن",
        ["Editor.BulletList"] = "قائمة نقطية",
        ["Editor.NumberedList"] = "قائمة مرقمة",
        ["Editor.TaskList"] = "قائمة المهام",
        ["Editor.Quote"] = "اقتباس",
        ["Editor.CodeBlock"] = "كتلة الكود",
        ["Editor.HorizontalRule"] = "خط أفقي",
        ["Editor.Link"] = "رابط",
        ["Editor.Unlink"] = "إزالة الرابط",
        ["Editor.InsertTable"] = "إدراج جدول",
        ["Editor.InsertImage"] = "إدراج صورة",
        ["Editor.ImportDocx"] = "استيراد .docx",
        ["Editor.AiActions"] = "إجراءات الذكاء الاصطناعي",
        ["Editor.Undo"] = "تراجع",
        ["Editor.Redo"] = "إعادة",
        ["Editor.ClearFormatting"] = "مسح التنسيق",
        ["Editor.FormatSelection"] = "تنسيق التحديد",
        ["Editor.Suggestions"] = "اقتراحات",
        ["Editor.NoResults"] = "لا توجد نتائج",
        ["Editor.InsertLinkTitle"] = "إدراج رابط",
        ["Editor.InsertLinkDescription"] = "الصق أو اكتب عنوان URL لربط النص المحدد.",
        ["Editor.Cancel"] = "إلغاء",
        ["Editor.Apply"] = "تطبيق",
        ["Editor.AiImproveWriting"] = "تحسين الكتابة",
        ["Editor.AiMakeShorter"] = "تقصير",
        ["Editor.AiMakeLonger"] = "تمديد",
        ["Editor.AiTranslate"] = "ترجمة",
        ["Editor.AiSummarize"] = "تلخيص",
        ["Editor.AiFixGrammar"] = "تصحيح القواعد",
        ["Editor.SlashHeading1"] = "عنوان 1",
        ["Editor.SlashHeading1Sub"] = "عنوان قسم كبير",
        ["Editor.SlashHeading2"] = "عنوان 2",
        ["Editor.SlashHeading2Sub"] = "عنوان قسم متوسط",
        ["Editor.SlashHeading3"] = "عنوان 3",
        ["Editor.SlashHeading3Sub"] = "عنوان قسم صغير",
        ["Editor.SlashBulletList"] = "قائمة نقطية",
        ["Editor.SlashBulletListSub"] = "قائمة بسيطة",
        ["Editor.SlashNumberedList"] = "قائمة مرقمة",
        ["Editor.SlashNumberedListSub"] = "قائمة مرتبة",
        ["Editor.SlashTaskList"] = "قائمة المهام",
        ["Editor.SlashTaskListSub"] = "ضع علامة على العناصر أثناء التقدم",
        ["Editor.SlashQuote"] = "اقتباس",
        ["Editor.SlashQuoteSub"] = "اقتباس مكتلة",
        ["Editor.SlashCodeBlock"] = "كتلة الكود",
        ["Editor.SlashCodeBlockSub"] = "كود مع تمييز بناء الجملة",
        ["Editor.SlashTable"] = "جدول",
        ["Editor.SlashTableSub"] = "إدراج جدول 3×3",
        ["Editor.SlashImage"] = "صورة",
        ["Editor.SlashImageSub"] = "تحميل من الجهاز",
        ["Editor.SlashDivider"] = "فاصل",
        ["Editor.SlashDividerSub"] = "خط أفقي",
        ["Editor.ToolbarAriaLabel"] = "تنسيق النص",

        // Stepper
        ["Stepper.Back"] = "السابق",
        ["Stepper.Next"] = "التالي",
        ["Stepper.Finish"] = "إنهاء",
        ["Stepper.Optional"] = "اختياري",
        ["Stepper.Skip"] = "تخطٍ",

        // Window
        ["Window.Close"] = "إغلاق",
        ["Window.Minimize"] = "تصغير",
        ["Window.Maximize"] = "تكبير",
        ["Window.Restore"] = "استعادة",

        // NumberInput
        ["NumberInput.Decrease"] = "إنقاص",
        ["NumberInput.Increase"] = "زيادة",

        // DateTimePicker
        ["DateTimePicker.ClearDate"] = "مسح التاريخ",

        // Slider
        ["Slider.End"] = "النهاية",

        // FileManager
        ["FileManager.EmptyTitle"] = "هذا المجلد فارغ",
        ["FileManager.EmptyState"] = "لا توجد ملفات أو مجلدات هنا.",
        ["FileManager.MoreActions"] = "إجراءات أخرى",
        ["FileManager.MoreActionsForName"] = "إجراءات أخرى لـ {0}",

        // FileUpload (parameterised)
        ["FileUpload.ExceedsMaxSize"] = "الملف «{0}» يتجاوز الحجم الأقصى {1}.",

        // ConfirmButton
        ["ConfirmButton.Title"] = "هل أنت متأكد؟",
        ["ConfirmButton.Confirm"] = "متابعة",
        ["ConfirmButton.Cancel"] = "إلغاء",

        // PickList (panels)
        ["PickList.SourceHeader"] = "المتاحة",
        ["PickList.TargetHeader"] = "المحددة",
        ["PickList.NoItems"] = "لا توجد عناصر",

        // FileManager (context menu / list view)
        ["FileManager.Open"] = "فتح",
        ["FileManager.Rename"] = "إعادة تسمية",
        ["FileManager.Delete"] = "حذف",
        ["FileManager.Name"] = "الاسم",
        ["FileManager.Size"] = "الحجم",
        ["FileManager.Modified"] = "آخر تعديل",
        ["FileManager.Loading"] = "جارٍ التحميل…",

        // AudioPlayer
        ["AudioPlayer.Label"] = "مشغل الصوت",
        ["AudioPlayer.Play"] = "تشغيل",
        ["AudioPlayer.Pause"] = "إيقاف مؤقت",
        ["AudioPlayer.Seek"] = "موضع التشغيل",
        ["AudioPlayer.Mute"] = "كتم الصوت",
        ["AudioPlayer.Unmute"] = "إلغاء كتم الصوت",
        ["AudioPlayer.Download"] = "تنزيل الصوت",

        // ThemeSwitcher
        ["Theme.Color"] = "اللون",
        ["Theme.Mode"] = "الوضع",
    };
}
