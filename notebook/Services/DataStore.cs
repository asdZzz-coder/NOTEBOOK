using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using notebook.Models;

namespace notebook.Services
{
    /// <summary>
    /// 記事存成 %AppData%\CalendarNotes\notes.json（一般 UTF-8 JSON，可以直接備份或用記事本打開）。
    /// 換電腦時請用「匯出 Excel → 匯入」搬資料，或直接複製這個檔案。
    /// </summary>
    public static class DataStore
    {
        // 環境變數 CALENDARNOTES_DATA_DIR 可指定其他資料夾（測試用）；平常不設定，存在 %AppData%\CalendarNotes
        public static readonly string DataDirectory =
            Environment.GetEnvironmentVariable("CALENDARNOTES_DATA_DIR")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CalendarNotes");

        public static string FilePath => Path.Combine(DataDirectory, FileName);

        internal const string FileName = "notes.json";

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            // 中文直接寫成中文，不要變成 \uXXXX，打開檔案才看得懂
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static List<NoteEntry> Load() => Load(FilePath);

        public static void Save(IEnumerable<NoteEntry> notes) => Save(FilePath, notes);

        internal static List<NoteEntry> Load(string path)
        {
            if (!File.Exists(path)) return new();
            try
            {
                return JsonSerializer.Deserialize<List<NoteEntry>>(File.ReadAllText(path, Encoding.UTF8), Options) ?? new();
            }
            catch (JsonException)
            {
                // 檔案壞掉（例如手動改錯），備份後以空資料開始，原本的內容還留在備份檔裡
                File.Move(path, path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
                return new();
            }
        }

        /// <summary>先寫到 .tmp 再取代，寫到一半當機也不會弄壞原本的檔案。</summary>
        internal static void Save(string path, IEnumerable<NoteEntry> notes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(notes, Options), new UTF8Encoding(false));
            File.Move(tmp, path, overwrite: true);
        }
    }
}
