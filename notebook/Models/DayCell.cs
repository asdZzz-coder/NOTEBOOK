namespace notebook.Models
{
    /// <summary>月曆上的一格（一天）。每次換月、換選取或記事變動時整批重建，所以不需要通知變更。</summary>
    public record DayCell(
        DateOnly Date,
        bool IsCurrentMonth,
        bool IsToday,
        bool IsSelected,
        IReadOnlyList<NoteEntry> Previews,
        int MoreCount)
    {
        public int Day => Date.Day;

        public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        public bool HasMore => MoreCount > 0;

        public string MoreText => "+" + MoreCount;

        // 螢幕閱讀器讀到的名稱
        public override string ToString() => Date.ToString("yyyy-MM-dd");
    }

    /// <summary>月曆上方星期列的一格。</summary>
    public record WeekdayLabel(string Text, bool IsWeekend);

    /// <summary>標籤顏色選擇器的一個選項。</summary>
    public record ColorOption(string Key, string Hex, string Name, bool IsSelected);

    /// <summary>搜尋結果的一列：記事本身 + 依目前語言格式化的日期。</summary>
    public record SearchResult(NoteEntry Note, string DateText);
}
