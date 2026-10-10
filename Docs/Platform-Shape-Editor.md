# Egaku Unity Editor 形狀工具

本輪只新增静態平台與禁畫區的場景編輯工具。從本機 `codex/egaku-development`（`6eb804a`，與已知 `origin/codex/egaku-development` 相同）建立 `codex/platform-shape-editor`；開始時工作目錄乾淨。Unity instance 是 `GDIM161_Phantom@9cd4b152973640f5`、Unity `6000.5.1f1`、專案 `E:/Unity Projects/Egaku/GDIM161_Phantom`；原作用中場景是未修改的 `Level_10`。

## 調查與選擇

- GDD 要求靜態平台阻擋玩家與筆畫，禁畫區禁止繪製但不阻擋已生成物體。
- 目前 `Level_10/Platforms` 下的 Square 使用 `Platform` Layer、非 Trigger BoxCollider2D、沒有 Rigidbody2D、預設 null PhysicsMaterial2D、Sprites/Default、棕色 RGBA 約 `(0.933, 0.714, 0.490, 1)`、sorting order 0。工具建立時優先從目前場景普通靜態 Square 複製外觀及材質設定；空場景使用相同預設。
- `PlatformOutlineCameraEffect` 收集 Platform Layer 的一般 Renderer，依 `PlatformOutlineGroup` 或預設 ground 分組。因此新 MeshRenderer 可參與既有平台描邊，不需要改描邊程式。描邊效果由既有 Bootstrap 安裝在 `Level_*` 主攝影機；普通 Scene View／獨立 Sandbox 不會自動顯示這個遊戲攝影機效果。
- 已處理 Linear 色彩空間下 Mesh 頂點色與 SpriteRenderer tint 的轉換差異；直接渲染比較測試確認新平台與既有 Sprite 使用相同填色時，RGB 在 0.01 內一致。
- `DrawProhibitedArea.prefab` 使用 `DrawProhibited` Layer、紅色半透明 SpriteRenderer、Trigger BoxCollider2D。工具沿用該 Prefab 的顏色、材質、排序與 PhysicsMaterial2D 政策，但形狀改為 PolygonCollider2D。
- 現有 `DrawMesh._DrawPathValidate` 查詢 DrawProhibited；非電線另查 Platform 與 Draw。Project Settings 的 queriesHitTriggers 與 queriesStartInColliders 均已啟用。本輪沒有修改這些設定。
- 專案有 SpriteShape（旧 TestSpriteShape／LevelEditor）。SpriteShape 可提供成熟曲線與素材邊框，但其 rendering/collider detail、profile 與自動 collider 工作流程需要另外約束才能保證同一輪廓。這次選擇自訂貝茲細分、凹多邊形三角化與 PolygonCollider2D；兩個輸出直接使用同一組取樣頂點，並沿用目前純色平台與攝影機描邊。
- 舊 `LevelEditor` 使用 runtime Input、單例、Play Mode 操作；`LevelSaver` 依 Holder/SpriteShape、JSON 與 runtime 重建整關。沒有擴充或替換它們。

## 使用

1. 停止 Play Mode，開啟 **Egaku > Shapes > Shape Editor**。
2. 選 Platform 或 DrawProhibited，以及 Rectangle、Triangle、Slope、Circle、Ellipse。
3. 按「在 Scene View 點選建立」，在 XY 平面點選位置，Esc 取消；也可在 Scene View 中央建立。初始放置吸附 0.25 單位。
4. 選物件的 ShapePath，點選控制點後拖曳黃色把手。其他點是青色；切線把手為橘色／紫色。Inspector 可以輸入位置、切線及模式。
5. Ctrl＋左鍵在邊附近插點，或用 Inspector 插入下一條邊的中點。曲線以 De Casteljau 精確分割，不會吸附插入點而破壞原曲線。Delete 刪除選取點，至少保留三點。停用「Scene View 控制點」後可正常操作 Transform／刪除整個物件。
6. Corner 的兩個切線可以獨立調整；零切線代表直線。Smooth 切線保持反向共線、可不同長度。「尖角＋清除兩側切線」建立完全直線的角。由矩形開始插點、移點即可製作自訂封閉／凹多邊形。
7. 網格採物件本地 XY 座標，gridSize 預設 0.25，可停用。移動控制點和切線端點會吸附；任意 Transform 的移動／旋轉仍使用 Unity 自己的工具設定。
8. Ctrl+Z／Ctrl+Y 使用 Unity Undo／Redo；Ctrl+D 使用 Unity 複製。點操作不改動其他形狀。
9. Ctrl+S 正常保存場景；也可由工具將選取形狀另存為新的 Prefab。工具拒絕覆寫已存在 Prefab。Prefab Mode 保存也會烘焙 Mesh。
10. **Egaku > Shapes > Open Shape Sandbox (Additive)** 打開範例。若尚未存在，建立新 Sandbox，保留其他開啟場景並只保存新場景；不變更 Build scene list。已有 Sandbox 不會被重製或覆寫。

隨附 `Assets/Prefab/Shapes/PlatformShape.prefab` 與 `DrawProhibitedShape.prefab`，根物件位置為零，方便直接拖進場景。Sandbox 包含兩種用途各五個預設、凹形與混合曲線，共 14 個形狀。Additive 開啟會與原場景並列；驗證期間 Scene View 已使用 Isolation View 顯示 Sandbox，按右下方 Exit 可退出隔離。沒有保存原關卡。

綠線表示實際 Collider 取樣輪廓；藍線表示控制曲線。無效時以紅線顯示控制曲線，保留控制資料、停用 MeshRenderer 與 Collider，Inspector 說明原因。檢查涵蓋自交／相觸、重複控制點、過短取樣邊、相鄰折返、無效數值、零縮放及退化面積。形狀物件不得加 Rigidbody2D 或額外 Collider；工具會提示而不自行刪除衝突元件。

## 資料與保存

- `ShapePath`：封閉控制點、進出切線、尖角／平滑模式、曲線容差與吸附設定。座標均在本地 XY。
- `ShapeGeometry`：用途無關的純幾何演算法。曲線容差預設 0.025 本地單位，最短邊 0.002，最大細分深度 10（可調 4–12），輪廓最多 2048 點。容差越小通常取樣越多；超過上限或生成過短邊會提示，不會默默生成粗糙替代輪廓。物件放大會同比放大世界空間誤差，應按尺寸調小容差。
- `StaticShapeArea`：用途、填色、共用 Material、PhysicsMaterial2D 與排序，獨立於控制點。平台無 Rigidbody2D、非 Trigger；禁畫區為 Trigger。
- `ShapeAuthoring`：僅 Editor 生成 Mesh／Collider，排程 Inspector 更新、Undo 再生成，以及保存前烘焙。
- `Assets/Generated/ShapeMeshes` 保存以內容雜湊命名的不可變 Mesh。相同幾何與顏色共用資產；編輯複本生成不同資產，不能修改原件。保留這些 `.asset` 與 `.meta`，它們是場景／Prefab 的依賴。歷史形狀可能留下未使用的 Mesh；首版不自動刪除，以免破壞其他場景引用。
- Player 直接使用已保存的普通 MeshFilter、MeshRenderer、PolygonCollider2D；沒有 Play Mode 才生成的視覺階層、JSON 整關載入或新網路物件。

## 禁畫區的既有落筆限制

唯讀追蹤發現：Drawer 的 DrawPressed 分支先 `PhotonNetwork.Instantiate` 再初始化零面積筆畫；CanUsePointer 只委派攝影機可操作區域。禁畫區檢查位於首次移動的 DrawMesh 路徑驗證。因此「區域內完全不能開始落筆／建立筆畫」目前沒有初始輸入 gate。

已提出在 Drawer 建立筆畫前增加 DrawProhibited 的 OverlapPoint 檢查；使用者選擇本輪保留玩家程式，因此沒有實作。新禁畫區保留現有 Trigger／Layer 合約，不會用實體障礙繞過這個限制。非電線跨界的既有路徑驗證會 RequestFinishDraw／ForceFinishDraw；電線分支只拒絕線段，沒有同樣的 finish 呼叫。這些既有 gameplay 差異均未修改。

## 驗證紀錄

初次啟動測試因腳本尚未匯入，找到零測試，沒有計為通過。完整重新匯入遇到 MCP 無回應；使用者 reload 後恢復。所有新增型別已在 Editor 載入，已檢查完整 Console，沒有編譯錯誤；既有 RoomListManager、RunnerMouse、未使用成員與第三方 Demo 警告保留。

第一輪 `ShapeEditorTests`：21／21 通過，包含五種預設、凹多邊形正反順序、Mesh 面積／Collider 頂點一致、混合曲線、細分容差、保形插點、六類無效資料、無效輸出停用、吸附、控制點 Undo／Redo、複製隔離。隔離 PhysicsScene2D 亦驗證既有 RunnerMovement 核心在新平台站立／跳躍，以及禁畫區內部起始查詢、邊界射線與 Dynamic 方塊自然落穿。

最終 `ShapeEditorTests`：**24／24 通過**（job `6573fc8ca0bc4c1783287e1063c2d495`）。新增覆蓋實際自訂 Inspector 的插點／刪點／Smooth 切線指令、Prefab 保存／LoadPrefabContents 往返、烘焙 Mesh 複製隔離，以及 Mesh 與 SpriteRenderer 的直接渲染填色比較。

場景往返另行在正常 Editor 執行：Sandbox 的 14 個物件保存、關閉、Additive 重新開啟；比較完整 ShapePath JSON、Collider 點序列與 Mesh 資產路徑，全部一致。重新重建後場景保持非 Dirty，所有形狀有效且 Mesh 皆為持久資產；原 Level_10 保持非 Dirty。色彩修正後重跑相同往返檢查仍通過。

已視覺檢查矩形、三角、斜坡、圓／橢圓、凹形與混合曲線，並確認 Scene View 自訂控制點與 Collider 預覽回呼顯示。MCP 合成的 Scene View 滑鼠事件沒有被視窗接受，因此**没有宣稱實體滑鼠選點／拖曳／Ctrl 點插入的端到端操作通過**；Inspector 指令與核心 Undo／Redo 有自動驗證。這部分仍需人工驗收。

上述隔離測試亦不代表完整 Runner Prefab 遊玩、DrawMesh 木板權威、Drawer 輸入、RPC 停筆或兩個 Photon 客戶端完整流程通過。沒有啟動 Play Mode 或建立 Player Build，沒有宣稱 gameplay/network release gate 通過。跨界只驗證相同 Layer／Trigger 的幾何查詢；實際停筆事件尚未實機驗證。平台攝影機描邊已確認 Renderer 收集相容，未做完整 Level 攝影機效果的遊玩回歸。

最終完整 Console 沒有 Error。除既有編譯警告外，測試工具曾報 MCP WebSocket 重新載入警告、測試烘焙資產未清理提示，以及唯讀依賴掃描觸發舊 Photon Demo LightingData 的版本警告；沒有重存 vendor 資產。只在本次新增的 ShapeMeshes 目錄中、確認沒有任何場景／Prefab／其他資產引用後，清除本轮產生的 16 個舊色彩／測試快取；保留 Sandbox／Prefab 所需的 14 個 Mesh。產品工具本身沒有自動刪除歷史 Mesh 的功能。

Git 最終除本輪新增檔案外，`Level_10.unity` 顯示被觸碰；Unity 最後一次 Test Runner 的場景保存／恢復流程改了換行格式。`git diff` 與 numstat 沒有內容差異，正規化後 `git hash-object` 與 HEAD blob 均為 `d470e353ce17525c0f081ed9cd23f1a154c9e4df`。保留此自動變動，未自行還原；原場景的序列化內容與 Editor Dirty 狀態均未改變。沒有修改既有 Prefab、Packages、Project Settings、Photon、玩家或 LevelEditor／LevelSaver。變更尚未 commit，沒有 push、merge、rebase 或建立 PR。

## 修改檔案與後續擴充

新增 Runtime/data：`Assets/Scripts/Level/Shapes/{ShapePath,ShapeGeometry,StaticShapeArea}.cs`。

新增 Editor：`Assets/Editor/Shapes/{ShapeAuthoring,ShapePathEditor,ShapeEditorWindow,ShapeSandbox}.cs`。

新增測試：`Assets/Tests/Editor/ShapeEditorTests.cs`。新增範例場景：`Assets/Scenes/ShapeTools/ShapeSandbox.unity`。新增 Prefab：`Assets/Prefab/Shapes/PlatformShape.prefab`、`DrawProhibitedShape.prefab`。新增 14 個 `Assets/Generated/ShapeMeshes/*.asset` 與 Unity 產生的 `.meta`；這些序列化／烘焙資料與有意新增的程式分開審查。本文件為 `Docs/Platform-Shape-Editor.md`。

後續靜態用途可新增用途元件／政策或擴充 StaticShapeArea，不需重寫 ShapePath、曲線工具、取樣或三角化。洞、布林運算、局部切割、水域、Death Zone、動態道具、機關連線、關卡管理與遊戲內編輯器均不在本輪。玩家初始落筆 gate 與電線的跨界 finish 語義需獨立 gameplay 授權及雙端驗證後再處理。
