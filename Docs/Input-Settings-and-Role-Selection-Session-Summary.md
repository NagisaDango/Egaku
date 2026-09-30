# Egaku 輸入裝置、設定介面與角色選擇改動摘要

更新日期：2026-09-29  
工作分支：`codex/controller-and-settings`

## 文件範圍

本文件記錄本次工作中與下列功能直接相關的改動：

- 共用設定介面與設定保存。
- 本地雙人裝置分配、角色選擇及輸入路由。
- 鍵鼠與多支手柄的辨識、切換及斷線處理。
- 線上 Photon 房間中的角色選擇、準備狀態及關卡選擇銜接。
- 舊 RoleSelect 流程的保留與逐步停用。

視覺素材、場景排版與 Prefab 外觀曾由使用者持續調整。本文件著重目前程式責任與維護注意事項，不把所有 Unity YAML 差異視為程式邏輯改動。

## 整體摘要

### 設定介面

- 設定介面改為由 `Assets/Resources/UI/EgakuSettingsMenu.prefab` 保存完整階層與外觀。
- Runtime 只負責載入 Prefab、綁定數值、輸入及頁面行為，不動態建立主要視覺階層。
- 已提供 Display、Audio、Controls、Keys 四頁。
- 已提供解析度、螢幕模式、Master／BGM／SFX 音量、手柄游標速度、Drawer 畫筆游標大小及 Runner 位置游標大小。
- Keys 頁目前只顯示 Runner／Drawer 的完整鍵位，不支援自訂重綁。
- 所有頁面修改由 `Apply Settings` 一次保存；取消時回復開啟介面前的設定快照。
- Display 修改有暫時預覽與逾時回復，避免保存無法使用的顯示模式。
- 鍵盤以 Q／E、手柄以 LB／RB 切換頁面，切頁後會選取該頁第一個可操作項目。
- Dropdown 與 Slider 採用進入編輯、確認、取消的操作階段，並處理同一個 Submit 同時進入及離開編輯的問題。
- Dropdown 展開後會維持選中項目可見，完成選擇後回到頁面導覽。
- 設定介面使用自己的 `EventSystem` 與 `InputSystemUIInputModule`。開啟者的裝置取得介面控制權，場景原本的 EventSystem 會暫停，關閉後再恢復。

### 本地雙人裝置與角色選擇

- Runner 與 Drawer 各自擁有獨立 `InputUser`、Input Action Asset 副本及實體裝置配對。
- 不再以 `Gamepad.current` 或單一 `keyboardRunnerInLocal` 推斷目前角色的實體控制器。
- 支援鍵鼠＋手柄及手柄＋手柄。
- Player 1 預設為鍵盤／滑鼠；Player 2 預設為第一支實際偵測到的 Gamepad，沒有 Gamepad 時為 None。
- 裝置 Dropdown 只列出目前實際存在的鍵鼠組及 Gamepad，不產生 Controller 2 到 Controller 7 等虛擬選項。
- Input System 裝置新增或移除時會自動更新 Dropdown。
- 兩張玩家卡可用各自裝置的左右輸入在 Runner、中立及 Drawer 欄位之間移動。
- 玩家位於 Runner 或 Drawer 欄位時，可按 Enter／Space 或手柄 Confirm 進入準備狀態。
- 兩位玩家選擇不同角色且都準備後，才進入關卡選擇。
- 選擇對方正在使用的裝置時採交換流程，避免兩個角色同時取得同一支實體裝置。
- 只保存偏好的裝置類型組合，不保存跨啟動可能改變的 Gamepad ID。每次啟動仍重新辨識實體裝置。
- Gamepad 斷線時，若鍵鼠未被另一角色占用，該角色會回退到鍵鼠；否則角色變為未分配並進入恢復流程。

### 線上角色選擇

- 建立／加入 Photon 房間後，改用與本地選擇相近的三欄式流程。
- 中間玩家卡由 `PlayerDisplay` 視覺建立，顯示 Photon 玩家名稱及角色外觀。
- 每位 Client 只控制自己的卡片；左右輸入在 Runner、中立及 Drawer 間移動。
- Enter／Space 或手柄 A 可切換準備與取消準備。
- Runner 與 Drawer 都有唯一玩家且雙方準備後，由 Master Client 將房間階段推進到關卡選擇。
- 卡片移動使用平滑插值。Photon 房間屬性更新時不再先把既有卡片瞬移到目標位置。
- Runner／Drawer 欄位的 Image 與 Ready Text 會依準備狀態顯示。
- 角色占用、準備者及目前選擇階段保存於 Photon Room Custom Properties，角色裝置資訊不會寫入 Photon。
- 角色占用使用 Compare-And-Swap 條件更新，降低兩位玩家同時認領同一角色的競爭問題。
- 目前線上角色選擇正式操作範圍為鍵盤與手柄。滑鼠選擇暫不使用；Column Button 啟用後仍有狀態問題，後續若恢復滑鼠操作需要獨立處理。

## 主要新增腳本

### `Assets/Scripts/EgakuSettings.cs`

- 集中保存遊戲設定的 Runtime 狀態。
- 使用 `PlayerPrefs` 保存音量、顯示模式、指標速度、兩種 Drawer 端游標大小、線上裝置模式及本地裝置配置偏好。
- 將預覽修改與正式保存分開，使設定介面可以取消整批修改。
- 啟動時驗證先前保存的解析度仍存在，再套用顯示設定。

### `Assets/Scripts/EgakuSettingsMenu.cs`

- 控制設定 Prefab 的開啟、關閉、頁面、導覽與編輯狀態。
- 根據開啟介面的實體裝置限制 UI 輸入。
- 在本地雙人關卡中辨識開啟者所屬角色，只顯示該角色適用的角色設定。
- 控制 Dropdown、Slider、Apply、Cancel、顯示模式安全預覽及裝置恢復／交換流程。
- 處理 Tab 選中顏色、頁面第一個選項、自動捲動 Dropdown 及 Submit 防重複觸發。

### `Assets/Scripts/Player/InputDeviceRouter.cs`

- 本機 Runner／Drawer 裝置分配的主要權威來源。
- 每個角色維護獨立 `InputUser`、動作副本、裝置種類及實體 Gamepad。
- 提供裝置認領、查詢擁有者、交換、清除、斷線與重新配對通知。
- 確保本地裝置分配不進入 Photon 房間資料。

### `Assets/Scripts/Player/GameplayInput.cs`

- 提供 Runner、Drawer 及 UI 共用的輸入讀取入口。
- 本地模式從 `InputDeviceRouter` 所屬角色的 Action Asset 讀取。
- 線上模式支援 Auto、Keyboard/Mouse、Gamepad Preferred。
- 管理手柄模擬游標位置，使繪圖、鏡頭及可見游標使用一致座標。
- `OnlineUiGamepad` 讓線上角色選擇沿用遊戲中的裝置選擇政策。

### `Assets/Scripts/Player/LocalDeviceClaimView.cs`

- 控制本地雙人開局前的 Player 1／Player 2 裝置與角色認領流程。
- 管理三欄卡片移動、Ready 狀態、裝置 Dropdown 及實體裝置熱插拔。
- 只有兩個玩家取得不同角色、有效裝置且都準備完成時才繼續。

### `Assets/Scripts/Player/OnlineRoleSelectionView.cs`

- 控制線上 Photon 角色選擇 Prefab。
- 從房間玩家資料建立本機 UI 卡片，顯示名稱與外觀。
- 處理角色移動、角色認領與釋放、準備切換、Ready 顯示及關卡選擇階段切換。
- 既有卡片保留更新前的位置，再平滑移動到 Photon 狀態對應的欄位。
- 新建立的卡片直接放到目前房間狀態的位置，避免從無意義的世界座標飛入。

### 其他輸入與游標腳本

- `Assets/Scripts/Player/Drawer/DrawerCameraController.cs`：提供 Drawer 手柄鏡頭控制。
- `Assets/Scripts/Player/Drawer/GamepadDrawerPointer.cs`：管理 Drawer 手柄指標與 UI／繪圖游標行為。
- `Assets/Scripts/Player/RunnerMouseLocalSize.cs`：套用 Drawer 端所見 Runner 位置游標大小。

## 主要修改腳本

### `Assets/Scripts/Allan Network/GameManager.cs`

- 整合本地裝置選擇及線上角色選擇 Prefab。
- 場景已存在 Selection View 時優先沿用，避免重複 Instantiate。
- 線上角色選擇與關卡選擇依 Photon 房間階段同步顯示。
- 本地模式開始前檢查兩個角色都有有效裝置。
- 保留舊 RoleSelect 程式碼，但將已不再使用的按鈕欄位、查找、Listener 與舊 `RolesManager` UI 停用區塊註釋掉。
- 目前仍被新流程使用的房間、角色頁、關卡頁、生成玩家及 Photon 回復流程不應一併移除。

### `Assets/Scripts/Allan Network/PhotonSessionPolicy.cs`

- 增加 Runner／Drawer 擁有者、準備者及角色選擇階段的 Room Property Key。
- 提供角色到房間屬性 Key 的一致映射。
- 房間初始化及玩家離開時可清理準備與階段狀態。

### `Assets/Scripts/Player/DisplayInRoleselect.cs`

- 新增從指定 Photon `Player` 綁定名稱、眼睛、嘴巴及身體顏色的流程。
- 線上角色選擇建立的是本機 UI Clone，因此會跳過舊有 buffered RPC 初始化，避免產生第二個網路物件。

### `Assets/Scripts/Allan Network/RolesManager.cs`

- 舊按鈕式 RoleSelect 的 Button 欄位與 UI 更新路徑目前保留為註釋。
- 類別內仍有舊角色占用與 Photon callback 程式；在所有場景與 Prefab 引用確認完成前不要直接刪除檔案或改 GUID。

### Runner、Drawer 與繪圖相關腳本

- `Assets/Scripts/Player/Runner.cs`、`RunnerMovement.cs`：改從角色所屬輸入讀取移動、跳躍、抓取及 Runner 游標。
- `Assets/Scripts/Player/Drawer/Drawer.cs`、`DrawerUICOntrol.cs`：改從 Drawer 所屬輸入讀取繪圖、擦除、筆刷及 UI 操作。
- `Assets/Scripts/DrawMesh.cs`：設定開啟或裝置切換時可安全結束正在繪製的線，避免持續按住狀態跨越切換。
- `Assets/Scripts/Audio/AudioManager.cs`：接收 Master、BGM、SFX 設定並即時套用。

## 主要 Prefab 與資產

### `Assets/Resources/UI/EgakuSettingsMenu.prefab`

- 保存設定介面的完整可編輯外觀、頁面階層、EventSystem、Input System UI Module 及固定按鈕事件。
- 長期外觀調整、Tab 動畫與版面修改應優先在此 Prefab 完成。

### `Assets/Resources/UI/LocalDeviceClaimView.prefab`

- 保存本地 Player 1／Player 2 卡片、三個角色欄位、裝置 Dropdown 與 Ready 視覺。
- 程式負責狀態與行為，Prefab 負責外觀與固定元件引用。

### `Assets/Resources/UI/OnlineSelection.prefab`

- 由使用者建立的線上角色選擇 Prefab。
- 保存 Runner、中立、Drawer 欄位、Ready 視覺、離房按鈕及 `OnlineRoleSelectionView` 引用。
- 玩家卡由程式使用 `Assets/Resources/PlayerDisplay.prefab` 建立，但角色選擇主介面本身不由程式動態組裝。

### `Assets/Inputs/Egaku.inputactions`

- 擴充 Runner／Drawer 的鍵鼠與 Gamepad 動作，包括移動、跳躍、抓取、繪圖、筆刷、鏡頭、指標與 UI 導覽。
- Binding Group 必須維持 `Keyboard&Mouse` 與 `Gamepad` 的一致名稱，`InputDeviceRouter` 依這些 Group 套用角色遮罩。

## 維護注意事項

- Runner、Drawer 的角色唯一性由 Photon 房間狀態決定；輸入裝置分配只存在本機，兩者不可混為同一權威來源。
- 不要重新以 `Gamepad.current` 推斷本地角色。兩支 Gamepad 同時存在時，必須從 `InputDeviceRouter.GamepadFor(role)` 或角色的 Action Asset 取得輸入。
- 不要保存 Gamepad device ID。裝置 ID 可能在重新啟動或重新插入後改變。
- 裝置交換必須先解除兩邊配對，再原子地重新分配，避免一幀內兩個角色同時讀取同一裝置。
- 切換或失去裝置時要清除持續按住狀態；Drawer 必須先結束正在畫的線。
- 設定介面和角色選擇等需要長期調整外觀的 UI 應維持 Prefab authoring。新增視覺階層前應修改 Prefab，不要在 Runtime 動態建構。
- `EgakuSettingsMenu` 會暫停場景 EventSystem。新增其他常駐 UI 時，要確認設定關閉後 EventSystem、選中物件與 Time Scale 都能正確恢復。
- Ready 欄位 Image 目前以 CanvasRenderer Alpha 顯示或隱藏，Image Component 本身仍存在。若未來恢復滑鼠操作，需要重新設計 Column Button 的啟用狀態、射線命中及確認／取消流程。
- `RoleSelection.unity` 在本次工作期間有大量使用者整理過的序列化變更。提交前應在 Unity Inspector 逐一確認場景引用，不要以文字工具重建或格式化整份 YAML。
- `Assets/Scenes/Legacy/RoleSelection 1.unity` 是保留的舊場景副本，不應取代目前 Build Settings 中的正式 `RoleSelection.unity`。
- 不要刪除 `.meta`、更換 Prefab GUID、PhotonView 或既有 RPC 名稱。

## 目前驗證狀態

- Unity 腳本編譯已達零 Error。
- 設定介面的鍵盤／手柄頁面切換、選項導覽、Dropdown、Slider、Apply 與 Cancel 曾在 Editor Play Mode 驗證。
- 本地裝置 Dropdown 已確認只顯示實際裝置，並會在插入／移除時刷新。
- 線上角色選擇已完成單一 Editor Client 的 Runtime 結構、Prefab 引用及 Console smoke test。
- 線上角色卡平滑移動、Ready 圖片及確認鍵取消準備已完成程式修正。
- 尚未在本次最後狀態下完成兩個獨立 Photon Client 的全流程驗證。
- 尚未完整覆蓋兩支實體 Gamepad、Gamepad 斷線／重接及所有裝置交換排列。

## ToDo

### 高優先級

- 使用兩支實體 Gamepad 驗證本地 Runner／Drawer 同時操作、兩邊角色交換、各自開啟設定及斷線恢復。
- 驗證鍵鼠＋Gamepad 的兩種角色排列，包含 Player 1／Player 2 Dropdown 手動交換。
- 使用兩個 Development Build 驗證線上建立／加入房間、左右選角、準備／取消準備、雙方進入關卡選擇及回到角色選擇。
- 在線上測試中分別覆蓋 Master 為 Runner、Master 為 Drawer、玩家離房、重連及 Master Client 切換。
- 在提交前檢查 `RoleSelection.unity`、三個 UI Prefab、`PlayerDisplay.prefab` 及所有新增序列化引用。

### 中優先級

- 實作 `Restore current page to default`；目前按鈕刻意設為不可操作。
- 完成按鍵自訂重綁。目前 Keys 頁只顯示固定配置。
- 補上設定頁切換動畫，例如翻頁動畫；行為狀態應繼續留在 `EgakuSettingsMenu`，動畫與 Animator 參數放在 Prefab。
- 視需要恢復線上角色選擇的滑鼠操作。需同時處理 Column Button 啟用、卡片遮擋、一次點擊只改變一次狀態，以及再次點擊取消準備。
- 補充更明確的裝置斷線與交換提示文字。

### 後續清理

- 所有正式場景確認不再依賴舊 RoleSelect 後，再規劃移除 `RolesManager` 舊 UI 路徑；移除前須檢查 PhotonView、Prefab 與 Scene serialized references。
- 將本次輸入／設定功能與其他不相關的視覺或場景改動分開審查及提交。
- 完成 Development Build 與兩 Client 驗證後，將實際結果補入本文件或 `Docs/Release/TwoClientValidation.md`。
