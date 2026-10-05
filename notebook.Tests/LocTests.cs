using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using notebook.Services;

// 這裡的測試會暫時切換介面語言（全域狀態），其他測試也會讀介面文字，所以不要平行執行
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace notebook.Tests
{
    /// <summary>介面文字：日期格式與 XAML 用到的字串是否都有中英文。</summary>
    public class LocTests
    {
        private static string SourceDir([CallerFilePath] string here = "") =>
            Path.Combine(Path.GetDirectoryName(here)!, "..", "notebook");

        [Fact]
        public void DateTexts_InBothLanguages()
        {
            var d = new DateOnly(2026, 10, 5); // 星期一
            var original = Loc.Language;
            try
            {
                Loc.SetLanguageForTests("zh");
                Assert.Equal("2026年10月", Loc.MonthTitle(2026, 10));
                Assert.Equal("10月5日 星期一", Loc.DayTitle(d));
                Assert.Equal("2026/10/05（一）", Loc.ShortDate(d));
                Assert.Equal(["日", "一", "二", "三", "四", "五", "六"], Loc.WeekdayHeaders());

                Loc.SetLanguageForTests("en");
                Assert.Equal("October 2026", Loc.MonthTitle(2026, 10));
                Assert.Equal("Monday, October 5", Loc.DayTitle(d));
                Assert.Equal("Mon, Oct 5, 2026", Loc.ShortDate(d));
                Assert.Equal("Sun", Loc.WeekdayHeaders()[0]);
            }
            finally { Loc.SetLanguageForTests(original); }
        }

        [Theory]
        [InlineData(0, "今天")]
        [InlineData(1, "明天")]
        [InlineData(-1, "昨天")]
        [InlineData(5, "5 天後")]
        [InlineData(-12, "12 天前")]
        public void RelativeDay_Chinese(int offset, string expected)
        {
            var original = Loc.Language;
            try
            {
                Loc.SetLanguageForTests("zh");
                var today = new DateOnly(2026, 10, 5);
                Assert.Equal(expected, Loc.RelativeDay(today.AddDays(offset), today));
            }
            finally { Loc.SetLanguageForTests(original); }
        }

        [Theory]
        [InlineData("MainWindow.xaml")]
        [InlineData("InputDialog.xaml")]
        public void Xaml_StringResourcesAllExist(string file)
        {
            var xaml = File.ReadAllText(Path.Combine(SourceDir(), file));
            foreach (Match m in Regex.Matches(xaml, @"DynamicResource S_(\w+)\}"))
                Assert.True(Loc.HasKey(m.Groups[1].Value), $"{file} 用到 S_{m.Groups[1].Value}，但 Loc 沒有這個字串");
        }

        [Fact]
        public void Code_StringKeysAllExist()
        {
            // 程式碼裡 Loc.T("key") 用到的 key 都要有翻譯（"color_tip_" 是組出來的，另外檢查）
            foreach (var file in Directory.GetFiles(SourceDir(), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
                if (Path.GetFileName(file) == "Loc.cs") continue; // 字串表本身（註解裡有 Loc.T("key") 的範例）
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"Loc\.T\(""(\w+)""[,)]"))
                    Assert.True(Loc.HasKey(m.Groups[1].Value), $"{Path.GetFileName(file)} 用到 \"{m.Groups[1].Value}\"，但 Loc 沒有這個字串");
            }
            foreach (var (key, _) in notebook.Models.NoteColors.All)
                Assert.True(Loc.HasKey("color_tip_" + key), $"顏色 {key} 沒有名稱");
        }
    }
}
