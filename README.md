# NOTEBOOK — 日曆記事本

用月曆來記事的 Windows 桌面程式。點一天就能寫那天的記事，打字會自動儲存。

## 功能

- **月曆**：一次看一整個月，每天顯示記事標籤（放不下時顯示「+N」）。用滾輪或箭頭換月，「今天」按鈕可以跳回今天。
- **記事**：每天可以有多則記事，各有標題、內容和標籤顏色。打字會自動儲存，不需要按儲存鍵。
- **改日期**：把記事移到其他天。
- **搜尋**：用標題或內容找記事，點結果就會跳到那一天。
- **匯出 / 匯入 Excel**：備份或搬到另一台電腦時使用。
- **介面**：淺色、深色、跟隨系統三種主題，可以切換繁體中文和英文。
- **線上更新**：程式開啟時會檢查 GitHub Releases，有新版本會詢問是否更新。

快捷鍵：`Ctrl+N` 新增記事、`Ctrl+F` 搜尋、`Esc` 清除搜尋、在月曆上點兩下直接在那天新增記事。

## 安裝

1. 到 [Releases](https://github.com/asdZzz-coder/NOTEBOOK/releases) 下載最新的 `CalendarNotes-Setup.zip`。
2. 解壓縮整個 zip。
3. 執行 `安裝.cmd`。

安裝完成後，程式會自動打開，開始功能表和桌面都會有「日曆記事本」的捷徑。之後有新版本時，打開程式就會詢問是否更新。

- 程式裝在 `%LOCALAPPDATA%\Programs\CalendarNotes`，不需要系統管理員權限，也不用另外安裝 .NET。
- 解除安裝：Windows「設定 → 應用程式」找「日曆記事本」。記事會保留。
- 不用 ClickOnce：ClickOnce 會附一個每個程式各自產生、沒有簽章的 `Launcher.exe`，開著 Windows「智慧型應用程式控制」的電腦會擋下它，程式就打不開（1.0.0 就是這樣）。

記事存在 `%AppData%\CalendarNotes\notes.json`，是一般的 JSON 檔，可以直接備份。

## 開發

需要 .NET 10 SDK。

```bash
dotnet build notebook.slnx
dotnet test notebook.Tests/notebook.Tests.csproj
dotnet run --project notebook
```

測試時可以設定環境變數 `CALENDARNOTES_DATA_DIR`，改用其他資料夾存放資料，避免動到正式資料。

## 發佈新版本

推送 `v*` 標籤後，GitHub Actions 會依序執行：

1. 跑自動測試。
2. 用 `dotnet publish` 打包（自帶 .NET 執行環境）。
3. 把「安裝.cmd + app 資料夾」壓成 `CalendarNotes-Setup.zip`，上傳到 GitHub Releases。

```bash
git tag v1.0.2
git push origin v1.0.2
```
