using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using notebook.Models;
using notebook.Services;

namespace notebook
{
    public partial class MainWindow : Window
    {
        private List<NoteEntry> _notes;
        private readonly ObservableCollection<NoteEntry> _dayNotes = new();
        private readonly UpdateService _updater = new();

        // 月曆目前顯示的月份，以及右側面板選的那天
        private int _year, _month;
        private DateOnly _selectedDate;

        /// <summary>編輯區正在編輯的記事（沒選時為 null）。</summary>
        private NoteEntry? _current;

        /// <summary>把記事填進編輯區時會觸發 TextChanged，這段期間不要當成使用者修改。</summary>
        private bool _loadingForm;

        /// <summary>打字時延遲一下再存檔，不會每按一個鍵就寫一次硬碟。</summary>
        private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };

        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

        public MainWindow()
        {
            InitializeComponent();
            // 小螢幕（例如筆電）上不要讓視窗超出可用範圍
            Height = Math.Min(Height, SystemParameters.WorkArea.Height - 20);
            Width = Math.Min(Width, SystemParameters.WorkArea.Width - 20);

            _notes = DataStore.Load();
            DayNotesList.ItemsSource = _dayNotes;
            _saveTimer.Tick += (_, _) => SaveNow();

            _selectedDate = Today;
            (_year, _month) = (_selectedDate.Year, _selectedDate.Month);
            RefreshWeekdays();
            RefreshCalendar();
            RefreshDay(select: CalendarService.NotesOn(_notes, _selectedDate).FirstOrDefault());
            UpdateStatus();

            Loc.LanguageChanged += OnLanguageChanged;
            ThemeService.ThemeChanged += OnThemeChanged; // 切換主題（或系統深淺色改變）時更新標題列與按鈕
            UpdateThemeButton();
        }

        private void OnLanguageChanged()
        {
            RefreshWeekdays();
            RefreshCalendar();
            RefreshDayHeader();
            RefreshColorPicker();
            RefreshSearch();
            RefreshSavedText();
            UpdateStatus();
            UpdateThemeButton();
        }

        private void Lang_Click(object sender, RoutedEventArgs e) => Loc.Toggle();

        // ---------- 主題：跟隨系統 / 淺色 / 深色 ----------

        private void Theme_Click(object sender, RoutedEventArgs e)
        {
            ThemeService.Cycle();
            StatusText.Text = Loc.T("theme_changed", ThemeName(ThemeService.Mode));
        }

        private static string ThemeName(AppTheme mode) => Loc.T(mode switch
        {
            AppTheme.Light => "theme_light",
            AppTheme.Dark => "theme_dark",
            _ => "theme_system",
        });

        private void OnThemeChanged()
        {
            UpdateThemeButton();
            ApplyTitleBar();
        }

        private void UpdateThemeButton()
        {
            ThemeIcon.Text = ThemeService.Mode switch
            {
                AppTheme.Light => "", // 太陽
                AppTheme.Dark => "",  // 月亮
                _ => "",              // 電腦（跟隨系統）
            };
            ThemeButton.ToolTip = Loc.T("theme_tip", ThemeName(ThemeService.Mode));
        }

        // ---------- Windows 11：標題列底色與視窗背景同色，看起來是一整片 ----------

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyTitleBar();
        }

        private void ApplyTitleBar() => WindowTheme.ApplyTitleBar(this);

        // ---------- 啟動 / 關閉 ----------

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CleanupService.RunInBackground(); // 清掉更新後遺留的舊檔
            DesktopShortcutService.TidyUp(_updater.IsInstalled);     // 更新後：重複捷徑只留最新的、工作列釘選改指向新版
            DesktopShortcutService.EnsureOnce(_updater.IsInstalled); // 安裝版第一次開啟時補上桌面捷徑

            await CheckForUpdateAsync(manual: false);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_saveTimer.IsEnabled) SaveNow(); // 還沒存的修改在關閉前寫進去
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
            {
                NewNote();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && SearchBox.Text.Length > 0)
            {
                SearchBox.Clear();
                e.Handled = true;
            }
        }

        // ---------- 更新與捷徑 ----------

        private void Shortcut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DesktopShortcutService.Create(_updater.IsInstalled) == ShortcutResult.SourceNotFound)
                {
                    MessageBox.Show(Loc.T("shortcut_no_source"), Loc.T("shortcut_title"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                StatusText.Text = Loc.T("shortcut_done");
                MessageBox.Show(Loc.T("shortcut_done"), Loc.T("shortcut_title"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or COMException)
            {
                MessageBox.Show(Loc.T("shortcut_failed", ex.Message), Loc.T("shortcut_title"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdateAsync(manual: true);
        }

        private async Task CheckForUpdateAsync(bool manual)
        {
            if (!_updater.IsInstalled)
            {
                if (manual)
                    MessageBox.Show(Loc.T("update_dev"), Loc.T("update_title"));
                return;
            }

            try
            {
                var info = await _updater.CheckAsync();
                if (info == null)
                {
                    if (manual) MessageBox.Show(Loc.T("update_latest", _updater.CurrentVersion), Loc.T("update_title"));
                    return;
                }

                var answer = MessageBox.Show(
                    Loc.T("update_found", info.Version, _updater.CurrentVersion),
                    Loc.T("update_found_title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;

                if (_saveTimer.IsEnabled) SaveNow(); // 更新會關閉程式，先把還沒存的修改寫進去
                StatusText.Text = Loc.T("downloading");
                await _updater.DownloadAndLaunchAsync(info, p => Dispatcher.Invoke(() => StatusText.Text = Loc.T("downloading_pct", p)));
                // 安裝程式已啟動，結束本程式讓它能覆蓋檔案；安裝完成後會自動重新開啟
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                UpdateStatus();
                // 啟動時的自動檢查失敗（例如沒網路）不打擾使用者
                if (manual) MessageBox.Show(Loc.T("update_failed", ex.Message), Loc.T("update_title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ---------- 月曆 ----------

        private void RefreshWeekdays() =>
            WeekdayHeader.ItemsSource = Loc.WeekdayHeaders().Select((t, i) => new WeekdayLabel(t, i is 0 or 6)).ToList();

        private void RefreshCalendar()
        {
            MonthTitle.Text = Loc.MonthTitle(_year, _month);
            MonthGrid.ItemsSource = CalendarService.BuildMonth(_year, _month, _notes, Today, _selectedDate, _cellLines);
        }

        /// <summary>每格放得下幾行記事標籤，依視窗大小變動。</summary>
        private int _cellLines = CalendarService.DefaultLines;

        // 格子高度扣掉外距、框線、內距與日期數字約 42px，每行標籤約 18px
        private void MonthGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            int lines = Math.Max(1, (int)((e.NewSize.Height / 6 - 42) / 18));
            if (lines == _cellLines) return;
            _cellLines = lines;
            RefreshCalendar();
        }

        private void ShowMonth(int year, int month)
        {
            (_year, _month) = (year, month);
            RefreshCalendar();
        }

        private void ShiftMonth(int delta)
        {
            var d = new DateOnly(_year, _month, 1).AddMonths(delta);
            ShowMonth(d.Year, d.Month);
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e) => ShiftMonth(-1);

        private void NextMonth_Click(object sender, RoutedEventArgs e) => ShiftMonth(1);

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            SelectDate(Today);
        }

        private void MonthGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ShiftMonth(e.Delta > 0 ? -1 : 1);
            e.Handled = true;
        }

        private void Day_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: DayCell cell }) SelectDate(cell.Date);
        }

        // 在格子上點兩下：直接在那天新增記事
        private void Day_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: DayCell cell }) return;
            SelectDate(cell.Date);
            NewNote();
            e.Handled = true;
        }

        /// <summary>選某一天：右側換成那天的記事；那天不在目前顯示的月份時，月曆也跟著跳過去。</summary>
        private void SelectDate(DateOnly date, NoteEntry? note = null)
        {
            if (_current != null && _current != note) DiscardIfEmpty(_current);
            _selectedDate = date;
            (_year, _month) = (date.Year, date.Month);
            RefreshCalendar();
            RefreshDay(select: note ?? CalendarService.NotesOn(_notes, date).FirstOrDefault());
        }

        // ---------- 右側：當天記事 ----------

        private void RefreshDay(NoteEntry? select)
        {
            _dayNotes.Clear();
            foreach (var n in CalendarService.NotesOn(_notes, _selectedDate)) _dayNotes.Add(n);
            RefreshDayHeader();
            DayNotesList.SelectedItem = select != null && _dayNotes.Contains(select) ? select : null;
            if (DayNotesList.SelectedItem == null) LoadForm(null);
        }

        private void RefreshDayHeader()
        {
            DayTitleText.Text = Loc.DayTitle(_selectedDate);
            var sub = $"{_selectedDate.Year} · {Loc.RelativeDay(_selectedDate, Today)}";
            if (_dayNotes.Count > 0) sub += " · " + Loc.T("day_count", _dayNotes.Count);
            DaySubText.Text = sub;
        }

        private void DayNotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var next = DayNotesList.SelectedItem as NoteEntry;
            if (next == _current) return;
            var previous = _current;
            LoadForm(next);
            // 新增後什麼都沒寫就換到別則：等選取處理完再把空白的那則拿掉
            if (previous != null && previous.IsEmpty)
                Dispatcher.BeginInvoke(() => { if (previous != _current) DiscardIfEmpty(previous); });
        }

        /// <summary>把記事填進編輯區（null = 清空並顯示提示）。</summary>
        private void LoadForm(NoteEntry? note)
        {
            _loadingForm = true;
            _current = note;
            TitleBox.Text = note?.Title ?? "";
            BodyBox.Text = note?.Body ?? "";
            _loadingForm = false;

            var visible = note != null ? Visibility.Visible : Visibility.Collapsed;
            EditorPanel.Visibility = visible;
            EditorActions.Visibility = visible;
            EditorHint.Visibility = note != null ? Visibility.Collapsed : Visibility.Visible;
            RefreshColorPicker();
            RefreshSavedText();
        }

        private void RefreshColorPicker()
        {
            var selected = _current?.Color;
            ColorPicker.ItemsSource = NoteColors.All
                .Select(c => new ColorOption(c.Key, c.Hex, Loc.T("color_tip_" + c.Key), c.Key == selected))
                .ToList();
        }

        private void RefreshSavedText() =>
            SavedText.Text = _current == null ? "" : Loc.T("saved_at", _current.Updated);

        /// <summary>新增後什麼都沒寫的記事直接拿掉，不留一堆「（無標題）」。</summary>
        private void DiscardIfEmpty(NoteEntry note)
        {
            if (!note.IsEmpty || !_notes.Remove(note)) return;
            _dayNotes.Remove(note);
            if (_current == note) LoadForm(null);
            RefreshCalendar();
            RefreshDayHeader();
            UpdateStatus();
        }

        // ---------- 新增 / 編輯 / 改日期 / 刪除 ----------

        private void NewNote_Click(object sender, RoutedEventArgs e) => NewNote();

        private void NewNote()
        {
            // 正在編輯的這則還是空白的，就直接用它，不要再多一則空白的
            if (_current is { IsEmpty: true } && _current.Date == _selectedDate)
            {
                TitleBox.Focus();
                return;
            }
            var note = new NoteEntry { Date = _selectedDate, Updated = DateTime.Now };
            _notes.Add(note);
            _dayNotes.Add(note);
            DayNotesList.SelectedItem = note;
            DayNotesList.ScrollIntoView(note);
            RefreshCalendar();
            RefreshDayHeader();
            TitleBox.Focus();
        }

        private void TitleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loadingForm || _current == null) return;
            _current.Title = TitleBox.Text;
            ScheduleSave();
        }

        // 標題打完按 Enter 直接跳到內容
        private void TitleBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            BodyBox.Focus();
            BodyBox.CaretIndex = BodyBox.Text.Length;
            e.Handled = true;
        }

        private void BodyBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loadingForm || _current == null) return;
            _current.Body = BodyBox.Text;
            ScheduleSave();
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || sender is not FrameworkElement { DataContext: ColorOption option }) return;
            _current.Color = option.Key;
            RefreshColorPicker();
            ScheduleSave();
        }

        private void ScheduleSave()
        {
            _current!.Updated = DateTime.Now;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        /// <summary>寫入硬碟。空白的記事（新增後沒寫任何東西）不存。</summary>
        private void SaveNow()
        {
            _saveTimer.Stop();
            try
            {
                DataStore.Save(_notes.Where(n => !n.IsEmpty));
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("save_failed", ex.Message), Loc.T("save_title"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            RefreshSavedText();
            UpdateStatus();
        }

        private void Move_Click(object sender, RoutedEventArgs e)
        {
            if (_current is not { } note) return;
            var input = InputDialog.Ask(this, Loc.T("move_title"), Loc.T("move_prompt"), note.Date.ToString("yyyy-MM-dd"),
                text => CalendarService.ParseDate(text) == null ? Loc.T("move_err") : null);
            if (input == null) return;
            var date = CalendarService.ParseDate(input)!.Value;
            if (date == note.Date) return;

            note.Date = date;
            // 移到那天的最後面
            _notes.Remove(note);
            _notes.Add(note);
            note.Updated = DateTime.Now;
            SaveNow();
            SelectDate(date, note);
            StatusText.Text = Loc.T("moved_to", note.DisplayTitle, Loc.ShortDate(date));
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_current is not { } note) return;
            if (!note.IsEmpty)
            {
                var ok = MessageBox.Show(Loc.T("delete_confirm", note.DisplayTitle), Loc.T("delete_title"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (ok != MessageBoxResult.Yes) return;
            }

            int index = _dayNotes.IndexOf(note);
            _notes.Remove(note);
            _dayNotes.Remove(note);
            LoadForm(null);
            SaveNow();
            RefreshCalendar();
            RefreshSearch();
            // 選同一位置的下一則（刪的是最後一則就選上一則）
            if (_dayNotes.Count > 0) DayNotesList.SelectedItem = _dayNotes[Math.Min(index, _dayNotes.Count - 1)];
            RefreshDayHeader();
            StatusText.Text = Loc.T("deleted", note.DisplayTitle);
        }

        // ---------- 搜尋 ----------

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshSearch();

        private void RefreshSearch()
        {
            var keyword = SearchBox.Text.Trim();
            if (keyword.Length == 0)
            {
                SearchPanel.Visibility = Visibility.Collapsed;
                SearchList.ItemsSource = null;
                return;
            }
            var results = CalendarService.Search(_notes, keyword);
            SearchList.ItemsSource = results.Select(n => new SearchResult(n, Loc.ShortDate(n.Date))).ToList();
            SearchCountText.Text = Loc.T("search_count", results.Count);
            SearchPanel.Visibility = Visibility.Visible;
        }

        // 點搜尋結果：右側跳到那則記事；搜尋結果保留，方便繼續點其他筆
        private void SearchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchList.SelectedItem is SearchResult r) SelectDate(r.Note.Date, r.Note);
        }

        // ---------- 狀態列 ----------

        private void UpdateStatus()
        {
            // 視窗標題列顯示版本：安裝版為「日曆記事本 v1.0.0」，直接從 Visual Studio 執行則標示開發版
            Title = _updater.IsInstalled ? Loc.T("title_installed", _updater.CurrentVersion) : Loc.T("title_dev");
            CountText.Text = Loc.T("count_text", _notes.Count(n => !n.IsEmpty));
            StatusText.Text = Loc.T("version_text", _updater.CurrentVersion);
        }

        // ---------- Excel 匯出 / 匯入 ----------

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = Loc.T("excel_filter"),
                FileName = Loc.T("export_filename", DateTime.Now.ToString("yyyyMMdd")),
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var notes = _notes.Where(n => !n.IsEmpty).ToList();
                ExcelService.Export(notes, dlg.FileName);
                MessageBox.Show(Loc.T("export_done", notes.Count), Loc.T("export_title"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("export_failed", ex.Message), Loc.T("export_title"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = Loc.T("excel_filter") };
            if (dlg.ShowDialog() != true) return;

            List<NoteEntry> imported;
            int skipped;
            try
            {
                imported = ExcelService.Import(dlg.FileName, out skipped);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("import_failed", ex.Message), Loc.T("import_title"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (imported.Count == 0)
            {
                MessageBox.Show(Loc.T("import_empty") + (skipped > 0 ? Loc.T("import_skipped", skipped) : ""), Loc.T("import_title"));
                return;
            }

            var mode = MessageBox.Show(
                Loc.T("import_confirm", imported.Count),
                Loc.T("import_title"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (mode == MessageBoxResult.Cancel) return;

            if (mode == MessageBoxResult.No)
                _notes.Clear();
            else
                _notes.RemoveAll(n => n.IsEmpty);

            int added = 0;
            foreach (var item in imported)
            {
                if (_notes.Any(x => ExcelService.SameNote(x, item))) continue;
                _notes.Add(item);
                added++;
            }

            LoadForm(null);
            SaveNow();
            RefreshCalendar();
            RefreshDay(select: CalendarService.NotesOn(_notes, _selectedDate).FirstOrDefault());
            RefreshSearch();
            MessageBox.Show(Loc.T("import_done", added) + (skipped > 0 ? Loc.T("import_skipped", skipped) : ""), Loc.T("import_title"));
        }
    }
}
