using System.Windows;
using notebook.Services;

namespace notebook
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Loc.Load();          // 讀取使用者選的介面語言
            ThemeService.Load(); // 讀取使用者選的主題（跟隨系統 / 淺色 / 深色）

            // 只允許開一個視窗，避免兩個視窗互相覆蓋對方存的資料
            using var mutex = new Mutex(true, SingleInstanceName(), out bool isFirst);
            if (!isFirst)
            {
                MessageBox.Show(Loc.T("already_running"), Loc.T("app_title"));
                return;
            }

            if (args.Contains(InstallService.UninstallArg, StringComparer.OrdinalIgnoreCase))
            {
                Uninstall();
                return;
            }

            // 安裝版：補開始功能表捷徑、更新「應用程式」清單；剛安裝完再建立桌面捷徑
            InstallService.OnStartup(args, new UpdateService().Version);

            var app = new App();
            app.InitializeComponent();
            Loc.Apply(); // 必須在 App.xaml 載入之後，字串資源才不會被覆蓋
            ThemeService.Apply();
            ThemeService.WatchSystemTheme();
            app.Run();
        }

        /// <summary>
        /// 同一個資料資料夾只能開一個視窗。用 CALENDARNOTES_DATA_DIR 指定測試資料夾時另外算一個，
        /// 這樣使用者開著正式版時，自動測試仍能用測試資料啟動，兩邊也不會寫到同一份資料。
        /// </summary>
        private static string SingleInstanceName()
        {
            const string name = @"Local\CalendarNotes.SingleInstance";
            var custom = Environment.GetEnvironmentVariable("CALENDARNOTES_DATA_DIR");
            if (string.IsNullOrEmpty(custom)) return name;
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(custom).ToUpperInvariant())))[..16];
            return name + "." + hash;
        }

        /// <summary>從「設定 → 應用程式」按解除安裝時執行。</summary>
        private static void Uninstall()
        {
            var title = Loc.T("uninstall_title");
            if (!InstallService.IsInstalled)
            {
                MessageBox.Show(Loc.T("uninstall_not_installed"), title);
                return;
            }

            var ok = MessageBox.Show(Loc.T("uninstall_confirm"), title,
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (ok != MessageBoxResult.Yes) return;

            try
            {
                InstallService.Uninstall();
                MessageBox.Show(Loc.T("uninstall_done", DataStore.DataDirectory), title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("uninstall_failed", ex.Message), title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
