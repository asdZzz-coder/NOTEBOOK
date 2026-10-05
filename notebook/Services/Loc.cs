using System.Globalization;
using System.IO;
using System.Windows;

namespace notebook.Services
{
    /// <summary>
    /// 介面語言（繁體中文 / English）。XAML 用 {DynamicResource S_key} 綁定，
    /// 程式碼用 Loc.T("key")；切換語言時會即時更新資源並通知視窗重新整理。
    /// 語言選擇存在資料資料夾的 language.txt（不含任何記事內容）。
    /// </summary>
    public static class Loc
    {
        private static readonly string SettingFile = Path.Combine(DataStore.DataDirectory, "language.txt");

        public static string Language { get; private set; } = "zh";

        public static bool IsChinese => Language == "zh";

        /// <summary>語言切換後觸發，讓視窗更新用程式碼設定的文字（標題、月份、狀態列…）。</summary>
        public static event Action? LanguageChanged;

        /// <summary>讀取使用者上次的選擇；沒有的話依系統語言決定。不依賴 WPF，可在 Application 建立前呼叫。</summary>
        public static void Load()
        {
            string? saved = null;
            try { if (File.Exists(SettingFile)) saved = File.ReadAllText(SettingFile).Trim(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            Language = saved is "zh" or "en"
                ? saved
                : CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";
        }

        /// <summary>把目前語言的字串寫進 Application.Resources（App.xaml 載入後呼叫）。</summary>
        public static void Apply()
        {
            var res = Application.Current.Resources;
            foreach (var (key, zh, en) in Table)
                res["S_" + key] = IsChinese ? zh : en;
        }

        public static void Toggle() => SetLanguage(IsChinese ? "en" : "zh");

        public static void SetLanguage(string lang)
        {
            if (lang == Language) return;
            Language = lang;
            try
            {
                Directory.CreateDirectory(DataStore.DataDirectory);
                File.WriteAllText(SettingFile, lang);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            Apply();
            LanguageChanged?.Invoke();
        }

        /// <summary>測試用：只切換語言，不寫設定檔、不更新畫面資源。</summary>
        internal static void SetLanguageForTests(string lang) => Language = lang;

        internal static bool HasKey(string key) => Lookup.ContainsKey(key);

        /// <summary>取得目前語言的文字；有參數時以 string.Format 帶入。</summary>
        public static string T(string key, params object[] args)
        {
            var text = Lookup.TryGetValue(key, out var e) ? (IsChinese ? e.zh : e.en) : key;
            return args.Length == 0 ? text : string.Format(text, args);
        }

        // ---------- 日期文字 ----------

        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
        private static readonly string[] ZhWeekdays = ["日", "一", "二", "三", "四", "五", "六"];

        /// <summary>月曆上方的星期列，從星期日開始。</summary>
        public static string[] WeekdayHeaders() => IsChinese
            ? ZhWeekdays
            : Enumerable.Range(0, 7).Select(i => English.DateTimeFormat.AbbreviatedDayNames[i]).ToArray();

        /// <summary>「2026年10月」/「October 2026」</summary>
        public static string MonthTitle(int year, int month) => IsChinese
            ? $"{year}年{month}月"
            : new DateTime(year, month, 1).ToString("MMMM yyyy", English);

        /// <summary>右側面板的大標題：「10月5日 星期日」/「Sunday, October 5」</summary>
        public static string DayTitle(DateOnly d) => IsChinese
            ? $"{d.Month}月{d.Day}日 星期{ZhWeekdays[(int)d.DayOfWeek]}"
            : d.ToString("dddd, MMMM d", English);

        /// <summary>搜尋結果上的日期：「2026/10/05（日）」/「Sun, Oct 5, 2026」</summary>
        public static string ShortDate(DateOnly d) => IsChinese
            ? $"{d:yyyy/MM/dd}（{ZhWeekdays[(int)d.DayOfWeek]}）"
            : d.ToString("ddd, MMM d, yyyy", English);

        /// <summary>和今天差幾天：「今天」「明天」「3 天後」「2 天前」…</summary>
        public static string RelativeDay(DateOnly d, DateOnly today)
        {
            int diff = d.DayNumber - today.DayNumber;
            return diff switch
            {
                0 => T("rel_today"),
                1 => T("rel_tomorrow"),
                -1 => T("rel_yesterday"),
                > 1 => T("rel_after", diff),
                _ => T("rel_before", -diff),
            };
        }

        private static readonly (string key, string zh, string en)[] Table =
        {
            // ----- 主視窗 -----
            ("app_title", "日曆記事本", "Calendar Notes"),
            ("title_installed", "日曆記事本 v{0}", "Calendar Notes v{0}"),
            ("title_dev", "日曆記事本（開發版）", "Calendar Notes (dev build)"),
            ("btn_lang", "English", "繁體中文"),
            ("btn_lang_tip", "切換成英文", "Switch to Traditional Chinese"),
            ("btn_export", "匯出 Excel", "Export Excel"),
            ("btn_import", "匯入 Excel", "Import Excel"),
            ("btn_check_update", "檢查更新", "Check for Updates"),
            ("btn_shortcut", "桌面捷徑", "Shortcut"),
            ("btn_shortcut_tip", "在桌面建立程式捷徑", "Create a desktop shortcut"),
            ("theme_system", "跟隨系統", "System"),
            ("theme_light", "淺色", "Light"),
            ("theme_dark", "深色", "Dark"),
            ("theme_tip", "主題：{0}（按一下切換）", "Theme: {0} (click to switch)"),
            ("theme_changed", "主題：{0}", "Theme: {0}"),
            ("count_text", "共 {0} 則記事", "{0} notes"),

            // ----- 月曆 -----
            ("btn_today", "今天", "Today"),
            ("prev_month_tip", "上個月", "Previous month"),
            ("next_month_tip", "下個月", "Next month"),
            ("search_placeholder", "搜尋記事…", "Search notes…"),
            ("search_none", "找不到符合的記事", "No matching notes"),
            ("search_count", "找到 {0} 則", "{0} found"),
            ("rel_today", "今天", "Today"),
            ("rel_tomorrow", "明天", "Tomorrow"),
            ("rel_yesterday", "昨天", "Yesterday"),
            ("rel_after", "{0} 天後", "In {0} days"),
            ("rel_before", "{0} 天前", "{0} days ago"),

            // ----- 右側：當天記事 -----
            ("btn_new_note", "新增記事", "New note"),
            ("btn_new_note_tip", "在這天新增一則記事（Ctrl+N）", "Add a note on this day (Ctrl+N)"),
            ("day_count", "{0} 則記事", "{0} notes"),
            ("day_empty", "這天還沒有記事\n按「新增記事」或在月曆上點兩下", "No notes on this day\nClick \"New note\" or double-click the calendar"),
            ("editor_hint", "選一則記事來編輯", "Select a note to edit"),
            ("lbl_title", "標題", "Title"),
            ("lbl_color", "顏色", "Color"),
            ("lbl_body", "內容", "Content"),
            ("title_placeholder", "輸入標題…", "Add a title…"),
            ("body_placeholder", "寫點什麼…", "Write something…"),
            ("untitled", "（無標題）", "(Untitled)"),
            ("btn_delete", "刪除", "Delete"),
            ("btn_move", "改日期", "Move"),
            ("btn_move_tip", "把這則記事移到其他日期", "Move this note to another date"),
            ("saved_at", "已自動儲存 {0:HH:mm}", "Saved automatically at {0:HH:mm}"),
            ("color_tip_indigo", "靛藍", "Indigo"),
            ("color_tip_sky", "天藍", "Sky"),
            ("color_tip_green", "綠", "Green"),
            ("color_tip_amber", "橘黃", "Amber"),
            ("color_tip_rose", "紅", "Red"),
            ("color_tip_pink", "粉紅", "Pink"),
            ("color_tip_violet", "紫", "Violet"),

            // ----- 改日期 -----
            ("move_title", "移到其他日期", "Move to Another Date"),
            ("move_prompt", "新的日期（例如 2026-10-05）", "New date (for example 2026-10-05)"),
            ("move_err", "看不懂這個日期，請用 2026-10-05 的格式。", "Could not read this date. Please use the format 2026-10-05."),
            ("moved_to", "已將「{0}」移到 {1}", "Moved \"{0}\" to {1}"),

            // ----- 刪除 -----
            ("delete_title", "刪除記事", "Delete Note"),
            ("delete_confirm", "確定刪除「{0}」？", "Delete \"{0}\"?"),
            ("deleted", "已刪除「{0}」", "Deleted \"{0}\""),

            // ----- 狀態列 / 訊息 -----
            ("dev_version", "開發版", "dev build"),
            ("version_text", "版本 {0}", "Version {0}"),
            ("btn_ok", "確定", "OK"),
            ("btn_cancel", "取消", "Cancel"),
            ("save_title", "儲存", "Save"),
            ("save_failed", "儲存失敗，記事尚未寫入硬碟：{0}", "Save failed — notes were not written to disk: {0}"),
            ("shortcut_title", "桌面捷徑", "Desktop Shortcut"),
            ("shortcut_done", "已在桌面建立捷徑", "Desktop shortcut created"),
            ("shortcut_no_source", "找不到安裝版的開始功能表捷徑，請重新執行「安裝.cmd」後再試。",
                                   "The installed app's Start menu shortcut was not found. Please run \"安裝.cmd\" again and retry."),
            ("shortcut_failed", "建立桌面捷徑失敗：{0}", "Failed to create the desktop shortcut: {0}"),
            ("already_running", "日曆記事本已經在執行中。", "Calendar Notes is already running."),

            // ----- 更新 -----
            ("update_title", "檢查更新", "Check for Updates"),
            ("update_dev", "目前是開發版（非安裝版），無法線上更新。", "This is a development build (not installed), so online updates are unavailable."),
            ("update_latest", "目前已是最新版本（{0}）。", "You are on the latest version ({0})."),
            ("update_found_title", "有新版本", "Update Available"),
            ("update_found", "發現新版本 {0}（目前 {1}）。\n\n是否現在更新？更新完成後程式會自動重新啟動。",
                             "Version {0} is available (current: {1}).\n\nUpdate now? The app will restart automatically when finished."),
            ("downloading", "下載更新中…", "Downloading update…"),
            ("downloading_pct", "下載更新中… {0}%", "Downloading update… {0}%"),
            ("update_bad_package", "更新包內容不完整，找不到安裝程式。", "The update package is incomplete: installer not found."),
            ("update_failed", "檢查更新失敗：{0}", "Update check failed: {0}"),

            // ----- Excel -----
            ("excel_filter", "Excel 檔案 (*.xlsx)|*.xlsx", "Excel files (*.xlsx)|*.xlsx"),
            ("export_filename", "日曆記事_{0}.xlsx", "CalendarNotes_{0}.xlsx"),
            ("export_title", "匯出 Excel", "Export Excel"),
            ("export_done", "匯出完成，共 {0} 則記事。", "Export complete — {0} notes."),
            ("export_failed", "匯出失敗：{0}", "Export failed: {0}"),
            ("import_title", "匯入 Excel", "Import Excel"),
            ("import_failed", "匯入失敗：{0}", "Import failed: {0}"),
            ("import_empty", "這個檔案裡沒有可匯入的記事（第一列需為標題列：日期、標題、內容、顏色）。",
                             "This file has no notes to import (the first row must be a header row: Date, Title, Content, Color)."),
            ("import_confirm", "讀到 {0} 則記事。\n\n是 = 合併到現有記事（同一天、標題與內容都相同者略過）\n否 = 清除現有記事，完全以 Excel 為準\n取消 = 不匯入",
                               "Found {0} notes.\n\nYes = merge into existing notes (identical notes on the same day are skipped)\nNo = clear existing notes and use the Excel file only\nCancel = do not import"),
            ("import_done", "匯入完成，新增 {0} 則。", "Import complete — {0} notes added."),
            ("import_skipped", "\n\n有 {0} 列的日期看不懂，已略過（請用 2026-10-05 的格式）。",
                               "\n\n{0} rows had an unreadable date and were skipped (use the format 2026-10-05)."),
            ("sheet_name", "記事", "Notes"),
            ("hdr_date", "日期", "Date"),
            ("hdr_title", "標題", "Title"),
            ("hdr_body", "內容", "Content"),
            ("hdr_color", "顏色", "Color"),
        };

        private static readonly Dictionary<string, (string zh, string en)> Lookup =
            Table.ToDictionary(t => t.key, t => (t.zh, t.en));
    }
}
