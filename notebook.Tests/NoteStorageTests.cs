using System.IO;
using notebook.Models;
using notebook.Services;

namespace notebook.Tests
{
    /// <summary>
    /// 記事存檔（notes.json）、Excel 匯出匯入與記事顯示文字的自動測試。全部在暫存資料夾裡進行。
    /// </summary>
    public sealed class NoteStorageTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "CalendarNotes-Tests-" + Guid.NewGuid().ToString("N"));

        private string NotesFile => Path.Combine(_root, "notes.json");

        public NoteStorageTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static List<NoteEntry> Sample() =>
        [
            new() { Date = new DateOnly(2026, 10, 5), Title = "看牙醫", Body = "下午 3 點\n記得帶健保卡", Color = "rose" },
            new() { Date = new DateOnly(2026, 10, 5), Title = "", Body = "'開頭是單引號", Color = "sky" },
            new() { Date = new DateOnly(2027, 1, 1), Title = "New year", Body = "", Color = "indigo" },
        ];

        private static void AssertSameNotes(IReadOnlyList<NoteEntry> expected, IReadOnlyList<NoteEntry> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].Date, actual[i].Date);
                Assert.Equal(expected[i].Title, actual[i].Title);
                Assert.Equal(expected[i].Body.ReplaceLineEndings("\n"), actual[i].Body.ReplaceLineEndings("\n"));
                Assert.Equal(expected[i].Color, actual[i].Color);
            }
        }

        // ---------- notes.json ----------

        [Fact]
        public void DataStore_RoundTripsNotes()
        {
            var notes = Sample();

            DataStore.Save(NotesFile, notes);
            var loaded = DataStore.Load(NotesFile);

            AssertSameNotes(notes, loaded);
            Assert.Equal(notes.Select(n => n.Id), loaded.Select(n => n.Id));
        }

        [Fact]
        public void DataStore_WritesReadableChineseAndPlainDates()
        {
            DataStore.Save(NotesFile, Sample());

            var json = File.ReadAllText(NotesFile);
            Assert.Contains("看牙醫", json);
            Assert.Contains("\"2026-10-05\"", json);
            Assert.False(File.Exists(NotesFile + ".tmp"));
        }

        [Fact]
        public void DataStore_MissingFile_ReturnsEmpty() => Assert.Empty(DataStore.Load(NotesFile));

        [Fact]
        public void DataStore_CorruptFile_IsBackedUpAndStartsEmpty()
        {
            File.WriteAllText(NotesFile, "{ 這不是 JSON");

            var loaded = DataStore.Load(NotesFile);

            Assert.Empty(loaded);
            Assert.False(File.Exists(NotesFile));
            var backup = Assert.Single(Directory.GetFiles(_root, "notes.json.corrupt-*"));
            Assert.Equal("{ 這不是 JSON", File.ReadAllText(backup)); // 原本的內容沒有丟掉
        }

        [Fact]
        public void DataStore_UnknownColor_FallsBackToDefault()
        {
            File.WriteAllText(NotesFile, """[{"Id":"a","Date":"2026-10-05","Title":"t","Body":"","Color":"purple-ish"}]""");

            Assert.Equal(NoteColors.Default, Assert.Single(DataStore.Load(NotesFile)).Color);
        }

        // ---------- Excel ----------

        [Fact]
        public void Excel_RoundTripsNotesSortedByDate()
        {
            var notes = Sample();
            notes.Reverse(); // 匯出時會依日期排序
            var path = Path.Combine(_root, "export.xlsx");

            ExcelService.Export(notes, path);
            var imported = ExcelService.Import(path, out int skipped);

            Assert.Equal(0, skipped);
            AssertSameNotes(notes.OrderBy(n => n.Date).ToList(), imported);
        }

        [Fact]
        public void Excel_Import_AcceptsRealDateCellsAndSkipsBadRows()
        {
            var path = Path.Combine(_root, "manual.xlsx");
            using (var wb = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Sheet1");
                ws.Cell(1, 1).Value = "日期"; ws.Cell(1, 2).Value = "標題"; ws.Cell(1, 3).Value = "內容";
                ws.Cell(2, 1).Value = new DateTime(2026, 10, 5); ws.Cell(2, 2).Value = "Excel 日期";
                ws.Cell(3, 1).Value = "2026/10/6"; ws.Cell(3, 2).Value = "文字日期";
                ws.Cell(4, 1).Value = "下週";      ws.Cell(4, 2).Value = "看不懂的日期";
                ws.Cell(5, 1).Value = "2026-10-07"; // 標題、內容都空白 → 忽略，不算略過
                wb.SaveAs(path);
            }

            var imported = ExcelService.Import(path, out int skipped);

            Assert.Equal(["Excel 日期", "文字日期"], imported.Select(n => n.Title));
            Assert.Equal([new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6)], imported.Select(n => n.Date));
            Assert.All(imported, n => Assert.Equal(NoteColors.Default, n.Color)); // 沒有顏色欄
            Assert.Equal(1, skipped);
        }

        [Fact]
        public void SameNote_ComparesDateTitleAndBody()
        {
            var a = new NoteEntry { Date = new DateOnly(2026, 10, 5), Title = "t ", Body = "b", Color = "rose" };
            Assert.True(ExcelService.SameNote(a, new NoteEntry { Date = a.Date, Title = "t", Body = "b\n" }));
            Assert.False(ExcelService.SameNote(a, new NoteEntry { Date = a.Date.AddDays(1), Title = "t", Body = "b" }));
            Assert.False(ExcelService.SameNote(a, new NoteEntry { Date = a.Date, Title = "t", Body = "c" }));
        }

        // ---------- 記事的顯示文字 ----------

        [Fact]
        public void DisplayTitle_FallsBackToFirstLineOfBody()
        {
            Assert.Equal("標題", new NoteEntry { Title = "  標題 ", Body = "內容" }.DisplayTitle);
            Assert.Equal("第一行", new NoteEntry { Body = "\n  第一行  \n第二行" }.DisplayTitle);
            Assert.Equal(Loc.T("untitled"), new NoteEntry().DisplayTitle);
        }

        [Fact]
        public void Preview_FlattensLinesAndTruncates()
        {
            Assert.Equal("a b c", new NoteEntry { Body = "a\r\n\r\nb\n c " }.Preview);
            var longBody = new string('字', 100);
            Assert.Equal(new string('字', 80) + "…", new NoteEntry { Body = longBody }.Preview);
        }

        [Fact]
        public void IsEmpty_OnlyWhenTitleAndBodyAreBlank()
        {
            Assert.True(new NoteEntry { Title = " ", Body = "\n" }.IsEmpty);
            Assert.False(new NoteEntry { Body = "x" }.IsEmpty);
        }

        [Fact]
        public void EditingNote_RaisesChangesForDisplayedText()
        {
            var note = new NoteEntry();
            var changed = new List<string?>();
            note.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            note.Title = "a";
            note.Body = "b";
            note.Color = "green";

            Assert.Contains(nameof(NoteEntry.DisplayTitle), changed);
            Assert.Contains(nameof(NoteEntry.Preview), changed);
            Assert.Contains(nameof(NoteEntry.ColorSoftHex), changed);
        }

        [Fact]
        public void NoteColors_SoftHexIsTranslucentVersionOfSameColor()
        {
            foreach (var (key, hex) in NoteColors.All)
            {
                var soft = NoteColors.SoftHex(key);
                Assert.Equal(9, soft.Length);
                Assert.EndsWith(hex[1..], soft);
            }
        }
    }
}
