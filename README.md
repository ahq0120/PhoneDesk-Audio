# PhoneDesk Audio

**Switch 2 連線事件收集：** [下載腳本 v1.0](https://github.com/ahq0120/PhoneDesk-Audio/raw/refs/heads/switch2/downloads/Switch2-Connection-Logs-v1.0.zip)，解壓後雙擊 `Run-Connection.cmd`，依提示重現連線失敗。收集裝置前後狀態、PnP／藍牙事件及 SetupAPI 尾端；[使用說明與隱私範圍](bluetooth/connection/README.txt)。

**安全性通知（2026-11-08）：Switch2 Service Probe v1.2 暫停下載與執行。** 收到 Microsoft Defender `Trojan:Win32/Wacatac.B!ml` 偵測回報，受影響項目為封裝內的 `SwitchServiceProbe.exe`。尚未證實誤報；不要停用防毒、加入排除或還原執行。該版 ZIP 已自分支最新版本撤下，原始碼保留供檢查。[檢查狀態](docs/switch2-v12-security-review.md)。

**藍牙／Windows 音訊診斷工具：** [下載完整檢查腳本](https://github.com/ahq0120/PhoneDesk-Audio/raw/refs/heads/switch2/downloads/PhoneDesk-Audio-Diagnostics-v1.0.zip)，解壓後雙擊 `Run-Diagnostics.cmd`。[檢查範圍與使用方式](bluetooth/diagnostics/README.txt)。不修改設定、不錄音、不上傳；完成後自行分享產生的報告 ZIP。

**Switch 2 藍牙接收工具下載：** [PhoneDesk Bluetooth v0.4 診斷包](https://github.com/ahq0120/PhoneDesk-Audio/raw/refs/heads/switch2/downloads/PhoneDesk-Bluetooth-v0.4.zip)。完整解壓縮後執行 `PhoneDeskBluetooth.exe`；[使用說明](bluetooth/README.txt)。Switch 2 實際配對與收音仍待目標電腦驗證。


> **switch2 分支**：新增獨立的 [PhoneDesk Bluetooth 接收工具](bluetooth/README.txt)，原始碼在 `bluetooth/`。執行 `bluetooth/Build.ps1` 建置；可在程式內開啟每天覆蓋的 `logs/current.log`。Switch 2 實際藍牙配對與聲音仍需在目標電腦驗證。本分支保留原有 iPhone AirPlay 工具。

在 Windows 耳機同時聽 iPhone 與電腦的聲音。建議透過螢幕鏡像的音訊路徑連線，電腦平常不顯示手機畫面，需要時才開啟獨立投影視窗。

## 建議使用方式

1. 手機與電腦連到相同區域網路，開啟 `PhoneDesk.exe`。
2. 手機控制中心 → **螢幕鏡像輸出**（兩個重疊矩形）→ **PhoneDesk Audio**。
3. 保持「動畫／影片同步」未勾選。聲音輸出到 Windows 預設耳機／喇叭，與電腦聲音混合。
4. 要看畫面時按「開啟手機畫面」，在 iMirror 按 Connect，再選 PhoneDesk Screen。
5. 關閉投影後，從手機「螢幕鏡像輸出」重新選 PhoneDesk Audio。

接收器使用 `-vs 0`：不顯示畫面，但手機仍處於鏡像狀態，接收器要求約 1fps，仍會傳送畫面資料；實際頻率由手機決定。

也支援從播放控制的 AirPlay 音訊清單連線。這是另一條音訊路徑；本次妮姬實測中，一般操作較適合關閉同步，動畫則需要勾選「動畫／影片同步」。勾選可在同一次連線生效，但操作音效可能延後。

## 功能

- 原生 Windows Forms，無須 Electron、Node.js、ESP32、手機 USB 線或自製手機 App。
- WASAPI 共享輸出，使用 Windows 預設音訊裝置。
- 畫面預設不顯示，獨立 iMirror 投影視窗需要時再開。
- LAN 介面選擇避開常見 VPN／虛擬介面，接收器驗證指定 IP 確實屬於本機。
- 有上限的音訊佇列、實際清空緩衝的 FLUSH、同連線手動同步切換。
- 不含鍵鼠、手機控制或文字輸入。

「接收器已啟動」只表示程序存在，不代表已收到手機音訊。切換路徑需要在手機重新選取。電話、鬧鐘、受保護內容及部分 App 不一定使用相同路由。

## 建置

Windows x64、PowerShell 7、Git、.NET Framework 4.x 編譯器。Native helper 使用固定版本的 MSYS2 UCRT64 套件；首次下載及編譯需要時間與磁碟空間。

```powershell
pwsh -File .\scripts\Build.ps1
```

來源 commit 固定於 `native/upstream.json`；`native/phonedesk-stable.patch` 包含正式 native 修正，`native/msys-build-lock.json` 記錄編譯依賴。成品位於 `dist/PhoneDesk/`。保留 exe、resources、授權與配套原始碼。

建議短的 ASCII 工作路徑及 ASCII `TEMP`／`TMP`，避免 MinGW 路徑限制。已備妥相同 UCRT64 SDK 時可指定：

```powershell
pwsh -File .\scripts\Build.ps1 -SdkRoot C:\toolchains\ucrt64
.\scripts\Test.ps1
# 啟動接收器與選用投影，避免與使用中的其他接收器衝突
.\scripts\Test.ps1 -Integration
```

## 驗證範圍與限制

2026-11-07 的單一 iPhone／Windows／妮姬實測：鏡像路徑確認為 AAC-ELD，使用者回報動畫同步、返回操作後沒有明顯變慢；後續約 30 秒持續播放無可聞斷音。先前曾短暫斷音，尚不能宣稱中斷已完全解決，也不是跨裝置或長時間保證。

ALAC 路徑改變延遲回報型別及數值，未改善該動畫同步；audioMode 在該次場景切換也未提供可用訊號。詳見[研究摘要](docs/audio-routing-research.md)。協定時間差不是實測端到端延遲。

`research/` 保留獨立診斷介面及額外 native patch，**預設建置不啟用**。不包含本機日誌、裝置識別、錄音、手機畫面、憑證或私人設定。

來源與授權見 [THIRD-PARTY.md](THIRD-PARTY.md)。目前未另行指定 PhoneDesk 自有原始碼授權；公開可見不等同授權任意重用。
