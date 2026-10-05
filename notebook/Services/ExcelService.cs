using System.IO;
using ClosedXML.Excel;
using notebook.Models;

namespace notebook.Services
{
    public static class ExcelService
    {
        // 標題列依目前介面語言輸出；匯入時會略過標題列，所以中英文的檔案互通
        private static string[] Headers => new[]
        {
            Loc.T("hdr_date"), Loc.T("hdr_title"), Loc.T("hdr_body"), Loc.T("hdr_color"),
        };

        public static void Export(IEnumerable<NoteEntry> notes, string path)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(Loc.T("sheet_name"));
            var headers = Headers;
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];
            ws.Row(1).Style.Font.Bold = true;

            int r = 2;
            foreach (var n in notes.OrderBy(n => n.Date))
            {
                // 日期寫成文字（yyyy-MM-dd），不會因為 Excel 的地區設定變成別的格式
                ws.Cell(r, 1).SetValue(n.Date.ToString("yyyy-MM-dd"));
                ws.Cell(r, 2).SetValue(Escape(n.Title));
                ws.Cell(r, 3).SetValue(Escape(n.Body));
                ws.Cell(r, 3).Style.Alignment.WrapText = true;
                ws.Cell(r, 4).SetValue(n.Color);
                r++;
            }
            ws.Columns().AdjustToContents();
            ws.Column(3).Width = Math.Min(ws.Column(3).Width, 80); // 內容很長時不要撐成超寬
            wb.SaveAs(path);
        }

        // 開頭的 ' 會被 ClosedXML 當成 Excel 的「文字前綴」而吞掉，多加一個才能原樣保留
        private static string Escape(string s) => s.StartsWith('\'') ? "'" + s : s;

        /// <summary>讀取 Excel；日期看不懂、或標題與內容都空白的列會略過，skipped 回傳略過幾列。</summary>
        public static List<NoteEntry> Import(string path, out int skipped)
        {
            skipped = 0;
            // 允許讀取「正在被 Excel 開啟」的檔案，否則會因檔案被鎖定而失敗
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.FirstOrDefault();
            if (ws == null) return new List<NoteEntry>();
            var result = new List<NoteEntry>();
            foreach (var row in ws.RowsUsed().Skip(1)) // 跳過標題列
            {
                var title = row.Cell(2).GetFormattedString().Trim();
                var body = row.Cell(3).GetFormattedString();
                if (title.Length == 0 && body.Trim().Length == 0) continue;

                var date = ReadDate(row.Cell(1));
                if (date == null) { skipped++; continue; }

                result.Add(new NoteEntry { Date = date.Value, Title = title, Body = body, Color = row.Cell(4).GetFormattedString() });
            }
            return result;
        }

        /// <summary>日期欄可能是文字（本程式匯出的），也可能是使用者在 Excel 裡打的真正日期。</summary>
        private static DateOnly? ReadDate(IXLCell cell)
        {
            if (cell.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(cell.GetDateTime());
            return CalendarService.ParseDate(cell.GetFormattedString());
        }

        /// <summary>同一天、標題與內容都相同視為重複。</summary>
        public static bool SameNote(NoteEntry a, NoteEntry b) =>
            a.Date == b.Date && a.Title.Trim() == b.Title.Trim() && a.Body.Trim() == b.Body.Trim();
    }
}
