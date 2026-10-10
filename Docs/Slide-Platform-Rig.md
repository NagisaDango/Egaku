# Slide Platform Rig

拖入 `Assets/Prefab/Platforms/SlidePlatformRig.prefab`。展開後直接選取、拖動 **Endpoint A** 或 **Endpoint B**；可見 Track 自動連接兩點，Platform 自動沿軌道轉向並放在起始比例位置。不需要新增元件或按套用。選取根物件也可同時顯示兩端點移動把手。

Prefab 包含四個正常、可編輯子物件：Endpoint A、Endpoint B、Track、Platform。預設起始比例為 0.5；根物件 Inspector 的 Starting Position 可以改變起始位置。兩點限制的是平台外緣範圍：0 時靠 A 的外緣對齊 A，1 時靠 B 的外緣對齊 B。工具同時計入 Sprite 與 BoxCollider2D 的大小、偏移、圓角及平台縮放，將平台中心的可移動範圍縮小。尺寸變更也會自動更新。軌道和端點是可見 SpriteRenderer，沒有 Collider；只有 Platform 有實體碰撞。

編輯時軌道与平台長軸（X）沿 A 到 B 轉向。遊戲中平台不再旋轉，受外力與重力沿滑軌移動，不會自動往返。垂直或斜向滑軌會滑向較低端；需要無重力時調整 Platform 的 Gravity Scale。請讓兩點在同一 Z 平面、至少相距 0.01，且軌道不能短於平台外緣寬度；太短時會顯示錯誤、置中預覽並凍結物理，拉長後自動恢復。不要給根物件非均勻縮放，不支援遊戲中動態改動端點或移動軌道。

新版取代並刪除了上一版 TwoPointSlidePlatform、轉換選單、測試與使用文件。保留使用者原有 Limit Movement.cs 及所有既有場景修改。所有工作留在 codex/platform-shape-editor，沒有提交或推送。

## 實作與授權配置

- SlidePlatformRig.cs：保存端點／軌道／平台引用，以端點更新編輯外觀及 SliderJoint2D 範圍。遊戲中只初始化約束，不持續寫入姿態或改變 simulated。
- SlidePlatformRigEditor.cs：Scene View 端點把手及自動更新；Prefab Mode 的子物件變動由 Editor update 偵測，不依賴按鈕。僅有輸入或序列化輸出變化時更新，以免反覆將場景標記為修改。
- 新 Prefab 與 RailSquare.asset：正常 Unity 編輯資產；Full Rect Sprite 使用原有平台圖片，不修改既有圖片匯入設定。Platform 沿用 Level 10 Slide Platform 的 Draw Layer、材質、顏色、質量 20、線性阻尼 5、重力 1；Track 和端點在 Default Layer。
- 依使用者另行批准，只在新 Prefab 的 Platform 配置 Wood tag、WoodPen、TransferToRunner、PhotonView 與 PhotonTransformView。使用 Takeover、UnreliableOnChange；唯一觀察元件為 PhotonTransformView，同步世界位置與旋轉，不同步縮放。世界座標避免抓取或解除父子關係時局部座標改變。沒有修改玩家、RPC、Ownership 程式或既有關卡的 Photon 配置。

## 驗證

SlidePlatformRigTests.cs 的七個案例在隔離 Preview Scene 中直接執行，避免 Test Runner 儲存／恢復使用者未儲存的關卡。全部通過：

- 水平、垂直、斜向端點 Transform 修改會生成對應長度、角度與初始平台位置。
- 三種方向各模擬 1600 個物理步驟，施加雙向力、側向力、重力與扭矩；每一步檢查軌道偏離及端點超出小於 0.04，旋轉偏差小於 0.1 度，且能到達兩端。
- Prefab 重新載入後內部引用、四個子物件、唯一 Collider、軌道長度及平台朝向一致。
- 端點 Undo／Redo 還原與重建對應軌道長度。
- Starting Position 0／1 外緣對齊，平台縮放及非置中 Collider 後自動重算，過短軌道凍結與恢復。三方向物理測試逐步檢查可見／Collider 外緣不超出端點（物理求解容差 0.04 單位）。

實際 Prefab Mode 中也確認了普通端點 Transform 編輯後軌道／平台自動連動，並檢查 Scene View 外觀。完整 Console 檢查沒有編譯錯誤；保留既有程式警告。初次使用 sliced sprite 的匯入警告已透過新 Full Rect Sprite 解決，沒有變更原圖。

沒有 Build、Play Mode 或雙客戶端測試。Runner 抓取／乘坐、ownership 轉移、兩端碰撞時的 Photon 插值及實際双人一致性仍未驗證。Prefab 的網路配置已建立，不能把隔離物理測試視為多人遊戲通過。
