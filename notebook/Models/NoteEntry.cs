using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using notebook.Services;

namespace notebook.Models
{
    /// <summary>
    /// 一則記事：屬於某一天，有標題、內容與標籤顏色。
    /// 編輯時會即時更新清單與月曆上的文字，所以實作 INotifyPropertyChanged。
    /// </summary>
    public class NoteEntry : INotifyPropertyChanged
    {
        private string _title = "";
        private string _body = "";
        private string _color = NoteColors.Default;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public DateOnly Date { get; set; }

        public string Title
        {
            get => _title;
            set { if (Set(ref _title, value ?? "")) { Changed(nameof(DisplayTitle)); Changed(nameof(IsUntitled)); } }
        }

        public string Body
        {
            get => _body;
            set { if (Set(ref _body, value ?? "")) { Changed(nameof(Preview)); Changed(nameof(DisplayTitle)); Changed(nameof(IsUntitled)); } }
        }

        /// <summary>標籤顏色的代號（見 NoteColors）；不認得的代號一律當成預設色。</summary>
        public string Color
        {
            get => _color;
            set { if (Set(ref _color, NoteColors.Normalize(value))) { Changed(nameof(ColorHex)); Changed(nameof(ColorSoftHex)); } }
        }

        /// <summary>最後修改時間（同一天的記事依建立順序排列，這個只用來顯示）。</summary>
        public DateTime Updated { get; set; } = DateTime.Now;

        // ---------- 畫面用（不存檔） ----------

        /// <summary>沒有標題時用內容的第一行；都沒有則顯示「（無標題）」。</summary>
        [JsonIgnore]
        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title)) return Title.Trim();
                var firstLine = FirstLine(Body);
                return firstLine.Length > 0 ? firstLine : Loc.T("untitled");
            }
        }

        [JsonIgnore]
        public bool IsUntitled => string.IsNullOrWhiteSpace(Title);

        /// <summary>清單上的內容預覽：去掉換行、只取前面一段。</summary>
        [JsonIgnore]
        public string Preview
        {
            get
            {
                var flat = string.Join(" ", Body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                return flat.Length <= 80 ? flat : flat[..80] + "…";
            }
        }

        [JsonIgnore]
        public string ColorHex => NoteColors.Hex(Color);

        /// <summary>半透明的標籤底色：淺色、深色主題都看得清楚。</summary>
        [JsonIgnore]
        public string ColorSoftHex => NoteColors.SoftHex(Color);

        /// <summary>標題與內容都是空白（新增後沒有輸入任何東西）。</summary>
        [JsonIgnore]
        public bool IsEmpty => string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Body);

        // 螢幕閱讀器與 UI 自動化讀到的名稱
        public override string ToString() => DisplayTitle;

        private static string FirstLine(string text) =>
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool Set(ref string field, string value, [CallerMemberName] string? name = null)
        {
            if (field == value) return false;
            field = value;
            Changed(name!);
            return true;
        }

        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>記事的標籤顏色。代號存檔，色碼只在畫面上用，之後要換色不影響舊資料。</summary>
    public static class NoteColors
    {
        public const string Default = "indigo";

        public static readonly (string Key, string Hex)[] All =
        {
            ("indigo", "#4F46E5"),
            ("sky",    "#0EA5E9"),
            ("green",  "#10B981"),
            ("amber",  "#F59E0B"),
            ("rose",   "#EF4444"),
            ("pink",   "#EC4899"),
            ("violet", "#8B5CF6"),
        };

        public static string Normalize(string? key)
        {
            var k = key?.Trim().ToLowerInvariant();
            return All.Any(c => c.Key == k) ? k! : Default;
        }

        public static string Hex(string? key) => All.First(c => c.Key == Normalize(key)).Hex;

        /// <summary>同一個顏色、約 18% 不透明度（#AARRGGBB）。</summary>
        public static string SoftHex(string? key) => "#2E" + Hex(key)[1..];
    }
}
