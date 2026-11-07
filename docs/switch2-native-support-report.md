# Windows 原生 A2DP Sink／Switch 2 相容性問題摘要

此文件供人工檢視與回報，尚未替使用者傳送給 Microsoft、Nintendo 或硬體廠商。未附原始追蹤或裝置位址。

## 目標與限制

以 Windows 原生 AudioPlaybackConnection 與單一 USB 藍牙介面接收 Switch 2 音訊，同一介面仍供既有鍵鼠使用。不能切換 WinUSB、拔除唯一滑鼠的介面或要求先改成獨立協定堆疊。

## 測試環境

- Windows 11 Pro build 26200；完整累積更新 revision 未由最新硬體腳本收集。
- 主機板 ASUS PRIME B760M-A WIFI，Rev 1.xx。
- C&C 銷售的 Bluetooth 6.0 USB 藍牙棒，USB VID 0BDA、PID A760、USB bcdDevice 0200；零售型號／硬體版本未由圖片或賣場確認，不能將 bcdDevice 視為商品硬體版本。
- Realtek 驅動 20.11.3038.3001（2026-06-08），INF oem6.inf；目前狀態正常。
- Intel 內建介面目前代碼 31，不作為可用的第二介面，也不在此測試更動。
- Switch 2 系統 23.0.1。

## 已確認觀測

1. 控制手機可透過同一電腦與介面成功開啟 AudioPlaybackConnection 並播放聲音。
2. Switch 2 基礎連線／配對成功，Windows 可主動取得其 SDP 音訊與控制服務。
3. 原生接收來源 selector 沒列出 Switch 2，也沒有觀測到其 A2DP MEDIA 子裝置。
4. Switch 2 要求 PSM 0x0019，Windows 先回 L2CAP pending，再於約 1.5 秒回 result 0x0002（PSM not supported）。這個嘗試尚未進入 codec 協商。
5. 重新配對時，Windows 查詢全部 0x0000–0xffff 屬性；Switch 的完整回覆內含三筆音訊／控制記錄，均沒有 ServiceRecordHandle 0x0000。沒有 continuation state，也沒有截斷這段 SDP 回覆。
6. 呼叫 BluetoothSetServiceState 對遠端 110A 啟用服務曾回傳 1060，沒有得到可用音訊介面。未重複套用，也沒有重裝／強制綁定驅動。

## 不能視為已確認的因果

缺少 ServiceRecordHandle 是明確的規格／相容性線索，但尚未證明 Windows 正是因此省略音訊子裝置。

在開發機對同一類數值記錄，以 Windows 公開 BluetoothSdpEnumAttributes 測試：缺少 handle 時 parse_ok=True、4 個屬性；補入 handle 時 parse_ok=True、5 個屬性。這排除了「公開屬性解析 API 一定直接拒絕這段資料」的說法，並不等於測到 BthEnum／A2DP profile 的內部服務列舉規則。

可離線重現的比較程式：[Compare-NativeSdp.ps1](../bluetooth/research/Compare-NativeSdp.ps1)。一般使用者不需要重跑；此測試不會修復或測試實體 Switch 連線。

## 希望供應商確認的問題

- Windows BthEnum 建立 110A 音訊子裝置時，是否要求遠端 SDP 0x0000？缺少時有何受支援的診斷與相容處理？
- 這個 USB ID／驅動組合是否有已知的原生 A2DP Sink 與 Switch 2 相容性問題？是否有可核對的官方修正版與變更說明？
- Windows 對已配對但未建立 110A 音訊介面的來源，如何受支援地啟用入站 AVDTP？目前公開 API 沒有讓應用程式補寫遠端 SDP、建立缺少的音訊介面或接管 PSM 的設定。
- Nintendo 是否可確認 Switch 2 的全屬性查詢回覆缺少 ServiceRecordHandle，並說明這是否符合其 SDP 實作預期／是否有相容性修正？

## 已排除為原生修補的做法

- 假造本機 SDP：WSASetService 發布的是本機服務，不會補寫 Switch 的遠端記錄。
- 將一般藍牙配對 ID 塞入接收清單：不等於取得 AudioPlaybackConnection 要求的音訊介面。
- A2DP Windows Bridge：作者文件說明它使用 WinUSB 接管整支介面，且主要是 Windows 發送音訊到耳機；不是保留 Windows 原生藍牙接收 Switch 的現成修補。
- 任意換其他品牌驅動、刪登錄快取、建立假 PnP 子裝置：目前沒有足以驗證效果與復原性的依據。

目前沒有已驗證的原生修復版本，也沒有找到可據此推薦的 C&C 專用修正版驅動。這不代表所有原生解法都不可能。

## 來源

- [Microsoft AudioPlaybackConnection 用法](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/enable-remote-audio-playback)
- [Microsoft SDP 屬性 API](https://learn.microsoft.com/en-us/windows/win32/api/bluetoothapis/nf-bluetoothapis-bluetoothsdpenumattributes)
- [Microsoft WSASetService 的本機發布用途](https://learn.microsoft.com/en-us/windows/win32/bluetooth/bluetooth-and-wsasetservice)
- [Bluetooth SIG SDP 規格](https://www.bluetooth.com/wp-content/uploads/Files/Specification/HTML/Core-54/out/en/host/service-discovery-protocol--sdp--specification.html)
- [C&C 賣場：含多版本但無獨立驅動下載說明](https://mall.iopenmall.tw/036038/index.php?action=product_detail&prod_no=P3603807634376)
- [A2DP Windows Bridge 安裝與 WinUSB 條件](https://github.com/SeiyaFunaokaJP/A2DP-Windows-Bridge/blob/main/docs/setup.md)
