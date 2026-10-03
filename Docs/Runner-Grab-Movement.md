# Runner 抓取移動首版

## 現行跳躍規則：统一腳下支撐

- 以 Runner 腳下的可碰撞表面授予跳躍；側面、頭頂及 Trigger 不授予。
- 所有未抓取表面均可落腳，包括浮木、移動物件與剛放開的木板。不記錄抓取歷史、不要求這些物件自己接觸陸地。
- 當前抓取物件若在腳下，需有外部支撐才算地面；自身與 Runner 的接觸不算。抓取側面的物件即使碰到地面也不能另行授予跳躍。
- 起跳接觸延遲不立刻恢復資格；重新落腳或受阻後站穩才恢復。使用相對支撐物的垂直速度，向上離開表面的近距離探測不算落地。
- 放開本身不直接增加跳躍，但下一個物理步若脚下有有效表面便按普通落地規則處理。因此空中放開並踩到木板可以再跳，此為使用者確認的設計。
- 移除 `carriedThisFlight` 和 `directJumpAllowance`，一般移動與抓取使用同一接地路徑。保留既有水浮力穩定修正與所有網路配置。
- 本輪 32 項相關物理測試通過，包含消耗跳躍後在上升／下降木板和實際浮木上放開並再跳、腳下抓取物件需要外部支撐、側面抓取物件不能授予跳躍、向上掠過不重置，以及既有撞頭／推物／跳高／水中穩定測試。兩個獨立 Photon Client 尚未執行。
- 本輪最新試玩版 `Builds/RunnerUnifiedJumpValidation/Egaku.exe`：Development Build 成功，0 errors、11 warnings。前述 ArchWater／PushJump／Grab 目錄為先前版本。

## 最新修復：拱形內側推物與浸水抓取

- 推物判斷接受拱形內側向下且反向的斜接觸法線，不再只接受近乎垂直側面；仍排除支撐面與沒有水平阻力的平頂接觸。
- 抓取不再將質量改成 1，保留物件原有質量／密度。Runner 的水平速度增量會同時以衝量作用於連接物件，避免恢復重量後拖動變遲鈍。
- 浸水物件保留原始重力倍率，避免把 Runner 的高重力套用至浮力運算；離水後仍共用跳躍重力。原始浮力材質、Water Prefab、場景、Ownership 與同步元件不變。
- 水測試使用 Float.prefab 的密度 20、阻尼 5；測試寬度 1／4／8 木板，以及 Runner 觸水第一步。Runner 本身碰水會重生；未驗證停用死亡後的長時間游泳，這不是現有玩法。
- 拱形雙向推物、陸地質量 20 抓取跳高及既有防連跳包含在回歸集合。兩個獨立 Photon Client 與使用者原始筆畫仍需實機驗收。
- 最新相關測試集合 26 案通過；水下全浸沒的長時間實驗仍有高速上浮，但遊戲在 Runner 觸水時即死亡放開，因此回歸測試對該狀態只驗證第一步，沒有宣稱支援長時間水下抓取。水面上抓浸水木板則驗證完整 30 個物理步。
- 最新 Development Build：`Builds/RunnerArchWaterValidation/Egaku.exe`，成功，0 errors、11 warnings；請勿混用先前 PushJump／Grab 版本。

## 後續修復：推物、撞頭恢復與一致跳高

- 未抓取時，接地 Runner 朝側面接觸的 Dynamic 木板提供質量補償水平推力。`movementTuning` 新參數：`pushAcceleration=35`、`pushMaxSpeed=6`、`pushMaxForce=1500`。最大速度限制輔助推力的目標，不截斷其他碰撞或外力造成的速度。只在本機物理權威執行；反向不拉物，靜態障礙阻擋時不追加推力，木板原始質量保留。
- `blockedJumpRecoveryTime=0.12`：起跳未能離地時，等待保護時間且實際外部支撐存在、上升停止後恢復資格。只有碰撞器而沒有 Rigidbody 的地面也有效；不因空中最高點或舊按鍵自動連跳。
- 移除抓木板額外 +15 跳速。起跳與短跳一起設定 Runner／抓取物件的垂直速度，抓取物件跟隨 Runner 有效重力，放開恢復原始重力與質量。保留 FixedJoint2D、場景碰撞及原雲跳躍加成；頭頂物件真正撞到天花板仍會限制高度。
- DrawMesh 從多邊形局部頂點計算質心，避免世界座標偏移，也不依賴 Drawer 停用模擬後不可用的物理邊界。不改 Ownership／觀察元件／RPC 合約。
- 新增低天花板重試、上下左右剛性連接跳高、短跳、質量 20 木板推動，以及模擬開／關的局部質心測試。無障礙跳高相對空手的容許差為 0.08 Unity 單位。
- 本輪完整測試曾完成 57 案，僅既有角色選擇測試失敗，依使用者指示不作阻擋；後續加入質心測試後，抓取／跳躍測試集合 18 案全部通過。兩個 Photon Client、實體輸入與完整關卡仍需實機驗收，不以隔離物理測試代替。
- 本轮新版 Windows Development Build：`Builds/RunnerPushJumpValidation/Egaku.exe`，建置成功，0 errors、11 warnings。先前 `RunnerGrabValidation` 目錄是舊版，驗收請使用新目錄。

## 設計與 Inspector

- 不增加重量懲罰；推、拉同速。
- Runner Prefab 的 `grabTuning`：最高速度 8、地面加速度 60、制動 90、轉向加速度 90、空中控制倍率 0.7、抓取／放開參數過渡 0.1 秒。
- 空手移動參數保留。空中倍率作用於加速、制動和轉向，不將空中最高速度另外乘以 0.7。
- 靜態／Kinematic 牆面阻擋時停止向障礙物施加水平目標速度，仍可反向；不自動放開、不新增彈性 Joint。
- 抓取期間保留物件原始質量，透過水平運動補償維持手感；放開不設定額外投擲速度。

## 跳躍與抓取生命週期

`GrabPhysics` 使用當前實際接觸檢查抓取物件的外部支撐，排除 Runner、自身與 Trigger，不沿過去的接觸點射線查詢。RunnerMovement 以腳下探測判斷落地，不保存抓取歷史。未抓取物件可直接落腳；當前腳下抓取物件需要外部支撐。保留 coyote time、跳躍緩衝和短跳。

所有 RPC 接收端均記錄抓取狀態與原始質量／Tag；僅 Runner 與目標的本機權威端啟用 Joint。放開先停用 Joint，再清除 connectedBody，避免空連接將 Runner 固定到世界。輸入釋放遺失或開啟設定時也會清理抓取。

## 物理權威與資產

- `TransferToRunner` 在 Runner 身分確立前暫停普通物件模擬，由合法控制者將 Ownership 交給 Runner；僅 Runner 權威端模擬。
- Battery Prefab 加入相同權威流程。
- Level 9 的 Square (12) 補上 WoodPen 與位置／旋轉同步，保留原父物件、姿態與排序。
- Level 20 的兩個木物件補上 TransferToRunner 與旋轉同步。
- 這些場景物件設為 Ownership Request；本專案 PUN 列舉為 Fixed=0、Takeover=1、Request=2。DrawnMesh 原設定保留。
- DrawMesh／DrawnMesh 的單一 PhotonTransformView 同步合約保留；不新增 Rigidbody pose writer。
- Runner／Battery 原有雙同步元件與抓取父子關係仍保留，若雙端有修正拉扯需另做觀測，不能宣稱本次已排除全部同步抖動。
- Unity 儲存場景／Prefab 帶入 SpriteRenderer、Light 等版本序列化欄位更新，與刻意修改的玩法欄位分開審查。

## 驗證紀錄

- 新增 `RunnerGrabTests` 七項隔離 PhysicsScene2D 測試通過：空中腳下物件連跳、放開再抓、有效落地重置、即時外部支撐、空手與 70% 空中控制、雙向速度與制動、卡牆反向。
- 正式 Editor 測試集合 48 項中 47 項通過；既有 `ReleaseReadinessTests.RoleSelectionHasBothButtonsAndTheNetworkDisplay` 因舊 RolesManager 欄位假設失敗，不在本次修改範圍。
- Play Mode 使用暫時 Gamepad 驗證：半推輸出約 0.43（含 deadzone），斜推輸出約 (0.71, 0.71)；暫時裝置已移除。
- Windows Development Build 成功：`Builds/RunnerGrabValidation/Egaku.exe`，0 errors、11 warnings。警告涉及 RoomListManager 的 callback／比較／序列化、過時查詢 API，以及未使用成員；尚未全部清理。Editor 已回到未修改的 AllanLauncher，停止 Play Mode，ProjectSettings／Packages 無差異。
- 實體控制器、完整遊玩及兩個獨立 Photon Client 尚需驗收；Editor 測試不代表這些流程已通過。

## 雙端驗收清單

使用同一版本的兩個獨立 Development Player，分別執行 Master=Runner 與 Master=Drawer。開啟 Runner 的 `debugGrab` 可記錄 ViewID、Actor、Owner、Controller、Master、模擬狀態、質量、位置、目標速度與阻擋狀態。

1. 空手走跑、轉向、短跳、斜坡、落地與死亡重生；鍵鼠、手柄、本地雙人分別驗證。
2. 普通木物件、Battery、玩家繪製木物件：抓取、推、拉、反向、放開、再抓；比較兩端軌跡。
3. 抓取物件置於腳下後起跳並連按跳躍，不應新增跳躍；放開後真正踩到該物件應可再跳，未踩到則不能。浮木、上升／下降物件遵守同一規則。
4. 抵牆持續輸入再反向、上下坡、電線移動、電池插拔、抓取中物件被擦除、開設定與手柄拔除。
5. 比較抓取前後質量；放開不應忽然加速或吸向固定點。DrawnMesh 完筆後應只有 Runner 模擬，Drawer 顯示一致。
6. 延遲情境、離房／重連與 Master 切換，逐 ViewID 對照雙端紀錄，確認沒有兩端同時模擬或永久停止模擬。

## 回退界線

`grabTuning.enabled=false` 僅關閉新抓取速度曲線與阻擋判斷，不回退跳躍防護、抓取狀態還原或物理權威修正。輸入、抓取生命週期、場景普通物件權威應分組審查／回退；不要整檔還原 Runner、GameplayInput 或輸入資產，因為其中包含工作開始前既有的未提交修改。未提交、推送或更改共享歷史。
