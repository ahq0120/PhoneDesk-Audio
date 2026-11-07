# Switch2 Service Probe v1.2：偵測回報與暫停發佈

2026-11-08 收到 Microsoft Defender `Trojan:Win32/Wacatac.B!ml` 偵測截圖，受影響項目指向 v1.2 ZIP 內的 `SwitchServiceProbe.exe`。使用者收到的副本經由第三方檔案傳輸；目前未核對該副本的 SHA-256，因此不能由檔名判定副本與 GitHub 成品完全相同，也不能把傳輸來源當成惡意或誤報證據。

## 暫停處置

- 從 `switch2` 分支最新版本撤下 v1.2 ZIP 與推薦下載連結；Git 歷史仍保留既有物件。
- 原始碼保留供審查，不以改名、重壓縮、重新編譯或關閉防毒作為繞過偵測的方法。
- 在釐清前不建議下載、還原或執行 v1.2，也不建議用舊版替代來避開偵測。
- 不公開使用者截圖、帳戶資訊、傳輸網址或私人診斷記錄。

## 已完成的檢查及界線

- GitHub Git blob 與本機封裝逐位元相同；ZIP 完整性檢查通過，內含 EXE 與本機編譯成品相同。
- 本機 Microsoft Defender 啟用狀態下，指定掃描 ZIP 與 EXE 均回報未發現威脅。當次安全情報版本為 `1.463.169.0`。
- 成品沒有 Authenticode 簽章；使用的 .NET Framework 編譯器簽章驗證為 Microsoft、有效。
- 這些是完整性與本機掃描結果，**不等於獨立安全認證，也不能據此證實誤報**。
- 尚未取得 Microsoft 對此樣本的人工／官方複判結果；未聲稱已提交或獲得撤銷偵測。

原始發佈樣本 SHA-256（供比對，並非安全背書）：

```text
ZIP: cac6c5d41afdba956ac6dda82a32843a307ee86ddb1c4b49ec3352c2ac629118
EXE: acd18196c366f79d3378b75049ce612625c87ed2746cb756c1c9378af99fa420
```

後續需核對實際受影響副本的雜湊，並透過 [Microsoft 樣本分析入口](https://www.microsoft.com/en-us/wdsi/filesubmission) 複判。請勿把私人記錄或其他檔案一併提交。防護處置應維持隔離，不要求使用者允許執行或建立排除。
