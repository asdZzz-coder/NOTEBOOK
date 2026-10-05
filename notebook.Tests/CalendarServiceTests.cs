using notebook.Models;
using notebook.Services;

namespace notebook.Tests
{
    /// <summary>月曆計算（每月 42 格、每格的記事預覽）、搜尋與日期解析的自動測試。</summary>
    public class CalendarServiceTests
    {
        private static NoteEntry Note(string date, string title = "x", string body = "") =>
            new() { Date = DateOnly.Parse(date), Title = title, Body = body };

        // ---------- 月曆格子 ----------

        [Theory]
        [InlineData(2026, 10, "2026-09-27")] // 10/1 是星期四 → 往前推到星期日
        [InlineData(2026, 2, "2026-02-01")]  // 2/1 剛好是星期日
        [InlineData(2026, 3, "2026-03-01")]
        [InlineData(2027, 1, "2026-12-27")]  // 跨年
        public void FirstCell_IsSundayOnOrBeforeTheFirst(int year, int month, string expected)
        {
            var first = CalendarService.FirstCell(year, month);
            Assert.Equal(DateOnly.Parse(expected), first);
            Assert.Equal(DayOfWeek.Sunday, first.DayOfWeek);
        }

        [Fact]
        public void BuildMonth_Always42ConsecutiveDays()
        {
            for (int m = 1; m <= 12; m++)
            {
                var cells = CalendarService.BuildMonth(2026, m, [], new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1));
                Assert.Equal(42, cells.Count);
                for (int i = 1; i < cells.Count; i++)
                    Assert.Equal(cells[i - 1].Date.AddDays(1), cells[i].Date);
                // 整個月都在格子裡
                Assert.Equal(DateTime.DaysInMonth(2026, m), cells.Count(c => c.IsCurrentMonth));
                Assert.Contains(cells, c => c.Date == new DateOnly(2026, m, DateTime.DaysInMonth(2026, m)));
            }
        }

        [Fact]
        public void BuildMonth_MarksTodayAndSelected()
        {
            var today = new DateOnly(2026, 10, 5);
            var selected = new DateOnly(2026, 10, 20);

            var cells = CalendarService.BuildMonth(2026, 10, [], today, selected);

            Assert.Equal(today, Assert.Single(cells, c => c.IsToday).Date);
            Assert.Equal(selected, Assert.Single(cells, c => c.IsSelected).Date);
        }

        [Fact]
        public void BuildMonth_ShowsAtMostThreePreviewsAndCountsTheRest()
        {
            var notes = Enumerable.Range(1, 5).Select(i => Note("2026-10-05", "n" + i)).ToList();
            notes.Add(Note("2026-10-06", "other"));
            notes.Add(Note("2026-12-25", "outside")); // 不在格子範圍內

            var cells = CalendarService.BuildMonth(2026, 10, notes, default, default);

            var oct5 = cells.Single(c => c.Date == new DateOnly(2026, 10, 5));
            Assert.Equal(["n1", "n2", "n3"], oct5.Previews.Select(n => n.Title));
            Assert.Equal(2, oct5.MoreCount);
            Assert.True(oct5.HasMore);
            Assert.Equal("+2", oct5.MoreText);

            var oct6 = cells.Single(c => c.Date == new DateOnly(2026, 10, 6));
            Assert.Single(oct6.Previews);
            Assert.False(oct6.HasMore);

            Assert.DoesNotContain(cells.SelectMany(c => c.Previews), n => n.Title == "outside");
        }

        [Theory]
        [InlineData(4, 4, 4, 0)] // 剛好放得下：全放，不需要「+N」
        [InlineData(5, 4, 3, 2)] // 放不下：最後一行改成「+2」
        [InlineData(3, 1, 0, 3)] // 只剩一行：只顯示「+3」
        [InlineData(1, 1, 1, 0)]
        [InlineData(2, 0, 0, 2)] // 高度不夠時至少保留一行
        public void BuildMonth_FitsPreviewsIntoAvailableLines(int count, int lines, int shown, int more)
        {
            var notes = Enumerable.Range(1, count).Select(i => Note("2026-10-05", "n" + i)).ToList();

            var cell = CalendarService.BuildMonth(2026, 10, notes, default, default, lines)
                .Single(c => c.Date == new DateOnly(2026, 10, 5));

            Assert.Equal(shown, cell.Previews.Count);
            Assert.Equal(more, cell.MoreCount);
        }

        [Fact]
        public void BuildMonth_IncludesNotesOnLeadingAndTrailingDays()
        {
            var cells = CalendarService.BuildMonth(2026, 10, [Note("2026-09-28", "sep"), Note("2026-11-07", "nov")], default, default);

            Assert.Contains(cells, c => !c.IsCurrentMonth && c.Previews.Any(n => n.Title == "sep"));
            Assert.Contains(cells, c => !c.IsCurrentMonth && c.Previews.Any(n => n.Title == "nov"));
        }

        [Fact]
        public void NotesOn_KeepsInsertionOrder()
        {
            var notes = new[] { Note("2026-10-05", "b"), Note("2026-10-04", "x"), Note("2026-10-05", "a") };
            Assert.Equal(["b", "a"], CalendarService.NotesOn(notes, new DateOnly(2026, 10, 5)).Select(n => n.Title));
        }

        // ---------- 搜尋 ----------

        [Fact]
        public void Search_MatchesTitleOrBodyIgnoringCase_NewestFirst()
        {
            var notes = new[]
            {
                Note("2026-01-01", "Shopping", "milk"),
                Note("2026-03-01", "會議", "跟 SHOP 討論"),
                Note("2026-02-01", "Gym", "leg day"),
            };

            var result = CalendarService.Search(notes, "  shop ");

            Assert.Equal(["會議", "Shopping"], result.Select(n => n.Title));
        }

        [Fact]
        public void Search_EmptyKeyword_ReturnsNothing() =>
            Assert.Empty(CalendarService.Search([Note("2026-01-01", "a")], "   "));

        // ---------- 日期解析（改日期、匯入 Excel） ----------

        [Theory]
        [InlineData("2026-10-05", 2026, 10, 5)]
        [InlineData("2026-1-5", 2026, 1, 5)]
        [InlineData("2026/10/5", 2026, 10, 5)]
        [InlineData(" 2026.10.05 ", 2026, 10, 5)]
        [InlineData("20261005", 2026, 10, 5)]
        public void ParseDate_AcceptsCommonFormats(string text, int y, int m, int d) =>
            Assert.Equal(new DateOnly(y, m, d), CalendarService.ParseDate(text));

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("明天")]
        [InlineData("2026-02-30")]
        [InlineData("10/05/2026")]
        public void ParseDate_RejectsInvalid(string? text) => Assert.Null(CalendarService.ParseDate(text));
    }
}
