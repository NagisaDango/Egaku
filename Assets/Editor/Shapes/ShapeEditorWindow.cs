using Egaku.Shapes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Egaku.EditorShapes
{
    public sealed class ShapeEditorWindow : EditorWindow
    {
        [SerializeField] private StaticShapeUse use;
        [SerializeField] private ShapePreset preset;
        private bool placing;
        [MenuItem("Egaku/Shapes/Shape Editor")]
        public static void Open() => GetWindow<ShapeEditorWindow>("Egaku Shapes");
        private void OnEnable() { SceneView.duringSceneGui += PlacementGUI; }
        private void OnDisable() { SceneView.duringSceneGui -= PlacementGUI; }
        public void CreateGUI()
        {
            rootVisualElement.Add(new HelpBox("僅建立靜態平台／禁畫區。既有關卡物件不會自動轉換。選取新物件後，在 Inspector 與 Scene View 編輯形狀。", HelpBoxMessageType.Info));
            var useField = new EnumField("用途", use);
            useField.RegisterValueChangedCallback(e => use = (StaticShapeUse)e.newValue); rootVisualElement.Add(useField);
            var presetField = new EnumField("形狀預設", preset);
            presetField.RegisterValueChangedCallback(e => preset = (ShapePreset)e.newValue); rootVisualElement.Add(presetField);
            rootVisualElement.Add(new Button(() => { placing = true; SceneView.RepaintAll(); }) { text = "在 Scene View 點選建立（Esc 取消）" });
            rootVisualElement.Add(new Button(() =>
            {
                var view = SceneView.lastActiveSceneView;
                var position = view != null ? view.pivot : Vector3.zero; position.z = 0;
                ShapeAuthoring.Create(use, preset, position, SceneManager.GetActiveScene());
            }) { text = "在 Scene View 中央建立" });
            rootVisualElement.Add(new Button(() =>
            {
                var shape = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<ShapePath>() : null;
                if (shape == null) { ShowNotification(new GUIContent("請先選取 ShapePath 物件。")); return; }
                if (!ShapeAuthoring.Rebuild(shape)) { ShowNotification(new GUIContent(shape.validationMessage)); return; }
                ShapeAuthoring.Bake(shape);
                string path = EditorUtility.SaveFilePanelInProject("Save Shape Prefab", shape.name, "prefab", "選擇新 Prefab 的位置");
                if (!string.IsNullOrEmpty(path))
                {
                    // Existing prefab replacement is deliberately refused; Save As
                    // produces a reusable asset without overwriting another author's work.
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                    { ShowNotification(new GUIContent("該 Prefab 已存在；請使用新名稱。")); return; }
                    PrefabUtility.SaveAsPrefabAsset(shape.gameObject, path);
                }
            }) { text = "將選取形狀另存為新 Prefab" });
            rootVisualElement.Add(new HelpBox("Ctrl+D 複製；Ctrl+Z／Ctrl+Y 復原／重做；Ctrl+S 正常儲存場景。Mesh 會在 Editor 儲存時烘焙成資產。綠線＝實際 Collider；藍線＝控制曲線；紅線＝無效形狀。", HelpBoxMessageType.Info));
        }
        private void PlacementGUI(SceneView view)
        {
            if (!placing || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape) { placing = false; evt.Use(); return; }
            if (evt.alt) return;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 p = ray.GetPoint(distance);
            p.x = Mathf.Round(p.x * 4) / 4; p.y = Mathf.Round(p.y * 4) / 4; p.z = 0;
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(p, Vector3.forward, HandleUtility.GetHandleSize(p) * 0.15f);
            Handles.Label(p, "點選建立 " + use + " / " + preset);
            if (evt.type == EventType.MouseDown && evt.button == 0)
            { ShapeAuthoring.Create(use, preset, p, SceneManager.GetActiveScene()); placing = false; evt.Use(); }
            view.Repaint();
        }
    }
}
