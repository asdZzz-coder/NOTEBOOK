using notebook.Models;

namespace notebook.Services
{
    /// <summary>月曆的計算：一個月要顯示哪 42 天、每天放哪些記事，以及搜尋。不碰畫面，方便測試。</summary>
    public static class CalendarService
    {
        /// <summary>月曆固定 6 列 × 7 天，換月時版面高度不會跳動。</summary>
        public const int CellCount = 42;

        /// <summary>每格預設放得下幾行（記事標籤或「+N」各佔一行）；實際依視窗大小由畫面計算。</summary>
        public const int DefaultLines = 4;

        /// <summary>月曆第一格的日期：該月 1 號所在那一週的星期日。</summary>
        public static DateOnly FirstCell(int year, int month)
        {
            var first = new DateOnly(year, month, 1);
            return first.AddDays(-(int)first.DayOfWeek);
        }

        /// <param name="lines">每格放得下幾行。記事放得下就全放；放不下時最後一行改成「+N」。</param>
        public static List<DayCell> BuildMonth(int year, int month, IEnumerable<NoteEntry> notes, DateOnly today, DateOnly selected,
            int lines = DefaultLines)
        {
            lines = Math.Max(1, lines);
            var start = FirstCell(year, month);
            var end = start.AddDays(CellCount - 1);
            var byDay = notes.Where(n => n.Date >= start && n.Date <= end)
                             .GroupBy(n => n.Date)
                             .ToDictionary(g => g.Key, g => g.ToList());

            var cells = new List<DayCell>(CellCount);
            for (int i = 0; i < CellCount; i++)
            {
                var date = start.AddDays(i);
                var dayNotes = byDay.GetValueOrDefault(date) ?? [];
                int shown = dayNotes.Count <= lines ? dayNotes.Count : lines - 1;
                cells.Add(new DayCell(
                    date,
                    IsCurrentMonth: date.Month == month,
                    IsToday: date == today,
                    IsSelected: date == selected,
                    Previews: dayNotes.Take(shown).ToList(),
                    MoreCount: dayNotes.Count - shown));
            }
            return cells;
        }

        /// <summary>某一天的記事，依加入順序。</summary>
        public static List<NoteEntry> NotesOn(IEnumerable<NoteEntry> notes, DateOnly date) =>
            notes.Where(n => n.Date == date).ToList();

        /// <summary>標題或內容包含關鍵字（不分大小寫）的記事，新的日期在前。</summary>
        public static List<NoteEntry> Search(IEnumerable<NoteEntry> notes, string keyword)
        {
            var k = keyword.Trim();
            if (k.Length == 0) return new();
            return notes.Where(n => n.Title.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                    n.Body.Contains(k, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(n => n.Date)
                        .ToList();
        }

        /// <summary>解析使用者輸入的日期（2026-10-05、2026/10/5、20261005 都可以）；看不懂回傳 null。</summary>
        public static DateOnly? ParseDate(string? text)
        {
            var t = text?.Trim() ?? "";
            string[] formats = ["yyyy-M-d", "yyyy/M/d", "yyyy.M.d", "yyyyMMdd"];
            return DateOnly.TryParseExact(t, formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) ? d : null;
        }
    }
}
