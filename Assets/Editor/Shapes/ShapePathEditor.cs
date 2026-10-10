using System;
using Egaku.Shapes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Egaku.EditorShapes
{
    [CustomEditor(typeof(ShapePath))]
    public sealed class ShapePathEditor : UnityEditor.Editor
    {
        private int selected;
        private bool editControls = true;
        private HelpBox status;
        private VisualElement pointFields;
        private ShapePath Path => (ShapePath)target;
        private void OnEnable() { Undo.undoRedoPerformed += UndoChanged; }
        private void OnDisable() { Undo.undoRedoPerformed -= UndoChanged; }
        private void UndoChanged() { UpdatePointFields(); Repaint(); }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var edit = new Toggle("Scene View 控制點") { value = editControls };
            edit.RegisterValueChangedCallback(e => { editControls = e.newValue; SceneView.RepaintAll(); }); root.Add(edit);
            foreach (string name in new[] { "curveTolerance", "minimumEdge", "maximumSubdivisionDepth", "snapToGrid", "gridSize" })
                root.Add(new PropertyField(serializedObject.FindProperty(name)));
            root.Add(new HelpBox("點選控制點，再拖曳黃色把手。Ctrl + 左鍵插入曲線上的點；Delete 刪點（至少保留三點）。尖角可保留獨立切線；平滑點把手保持反向共線。網格採本地 XY。", HelpBoxMessageType.Info));
            var index = new IntegerField("選取控制點（從 1 開始）") { value = selected + 1 };
            index.RegisterValueChangedCallback(e => { selected = Mathf.Clamp(e.newValue - 1, 0, Path.points.Count - 1); UpdatePointFields(); SceneView.RepaintAll(); }); root.Add(index);
            pointFields = new VisualElement(); root.Add(pointFields); UpdatePointFields();
            root.Add(new Button(() => Insert(selected, 0.5f)) { text = "在下一條邊插入中點（保留曲線）" });
            root.Add(new Button(DeletePoint) { text = "刪除選取點" });
            root.Add(new Button(() => ShapeAuthoring.Change(Path, "Straight Shape Corner", () =>
            { var p = Path.points[selected]; p.mode = ShapePointMode.Corner; p.incoming = p.outgoing = Vector2.zero; })) { text = "尖角＋清除兩側切線" });
            root.Add(new Button(() => ShapeAuthoring.Rebuild(Path)) { text = "重建／驗證" });
            status = new HelpBox(Path.validationMessage ?? "", HelpBoxMessageType.Info); root.Add(status);
            root.schedule.Execute(() =>
            {
                if (target == null) return;
                status.text = Path.validationMessage;
                status.messageType = Path.GetComponent<PolygonCollider2D>().enabled ? HelpBoxMessageType.Info : HelpBoxMessageType.Error;
            }).Every(200);
            return root;
        }
        private void UpdatePointFields()
        {
            if (pointFields == null || Path.points.Count == 0) return;
            selected = Mathf.Clamp(selected, 0, Path.points.Count - 1); pointFields.Clear();
            var p = Path.points[selected];
            var position = new Vector2Field("位置") { value = p.position };
            position.RegisterValueChangedCallback(e => ShapeAuthoring.Change(Path, "Move Shape Point", () => p.position = ShapeAuthoring.Snap(Path, e.newValue))); pointFields.Add(position);
            var mode = new EnumField("接點模式", p.mode);
            mode.RegisterValueChangedCallback(e => SetMode((ShapePointMode)e.newValue)); pointFields.Add(mode);
            var incoming = new Vector2Field("進入切線偏移") { value = p.incoming };
            incoming.RegisterValueChangedCallback(e => SetTangent(false, e.newValue)); pointFields.Add(incoming);
            var outgoing = new Vector2Field("離開切線偏移") { value = p.outgoing };
            outgoing.RegisterValueChangedCallback(e => SetTangent(true, e.newValue)); pointFields.Add(outgoing);
        }
        private void SetMode(ShapePointMode mode)
        {
            ShapeAuthoring.Change(Path, "Change Shape Point Mode", () =>
            {
                var p = Path.points[selected]; p.mode = mode;
                if (mode == ShapePointMode.Smooth)
                {
                    // Smoothing an empty corner seeds a usable tangent along its
                    // neighbors. Nonzero handles retain independent lengths.
                    Vector2 direction = (Path.points[(selected + 1) % Path.points.Count].position - Path.points[(selected + Path.points.Count - 1) % Path.points.Count].position).normalized;
                    if (p.outgoing != Vector2.zero) direction = p.outgoing.normalized;
                    else if (p.incoming != Vector2.zero) direction = -p.incoming.normalized;
                    p.outgoing = direction * (p.outgoing == Vector2.zero ? 0.5f : p.outgoing.magnitude);
                    p.incoming = -direction * (p.incoming == Vector2.zero ? 0.5f : p.incoming.magnitude);
                }
            }); UpdatePointFields();
        }
        private void SetTangent(bool outgoing, Vector2 value)
        {
            ShapeAuthoring.Change(Path, "Move Shape Tangent", () =>
            {
                var p = Path.points[selected];
                if (outgoing) p.outgoing = value; else p.incoming = value;
                if (p.mode == ShapePointMode.Smooth && value != Vector2.zero)
                {
                    if (outgoing) p.incoming = -value.normalized * p.incoming.magnitude;
                    else p.outgoing = -value.normalized * p.outgoing.magnitude;
                }
            });
        }
        private void Insert(int segment, float t)
        {
            if (Path.points.Count < 2) return;
            ShapeAuthoring.Change(Path, "Insert Shape Point", () => ShapeGeometry.SplitSegment(Path.points, segment, t));
            selected = segment + 1; UpdatePointFields();
        }
        private void DeletePoint()
        {
            if (Path.points.Count <= 3) return;
            ShapeAuthoring.Change(Path, "Delete Shape Point", () => Path.points.RemoveAt(selected));
            selected = Mathf.Clamp(selected, 0, Path.points.Count - 1); UpdatePointFields();
        }
        private void OnSceneGUI()
        {
            if (!editControls || EditorApplication.isPlayingOrWillChangePlaymode || Path.points.Count < 3) return;
            var tr = Path.transform;
            var evt = Event.current;
            // Drawing the saved collider distinguishes the actual output from red
            // editable controls when validation has disabled an invalid outline.
            var collider = Path.GetComponent<PolygonCollider2D>();
            if (collider.enabled && collider.pathCount > 0)
            {
                var points = collider.GetPath(0); var line = new Vector3[points.Length + 1];
                for (int i = 0; i <= points.Length; i++) line[i] = tr.TransformPoint(points[i % points.Length]);
                Handles.color = Color.green; Handles.DrawAAPolyLine(3, line);
            }
            for (int i = 0; i < Path.points.Count; i++)
            {
                var a = Path.points[i]; var b = Path.points[(i + 1) % Path.points.Count];
                Handles.color = collider.enabled ? new Color(0.2f, 0.7f, 1f) : Color.red;
                var curve = new Vector3[33];
                for (int j = 0; j <= 32; j++) curve[j] = tr.TransformPoint(ShapeGeometry.Evaluate(a, b, j / 32f));
                Handles.DrawAAPolyLine(2, curve);
                Vector3 world = tr.TransformPoint(a.position); float size = HandleUtility.GetHandleSize(world) * 0.055f;
                Handles.color = i == selected ? Color.yellow : Color.cyan;
                if (i != selected && Handles.Button(world, tr.rotation, size, size * 1.4f, Handles.DotHandleCap))
                { selected = i; UpdatePointFields(); Repaint(); }
            }
            selected = Mathf.Clamp(selected, 0, Path.points.Count - 1);
            var point = Path.points[selected];
            Vector3 current = tr.TransformPoint(point.position);
            Handles.color = Color.yellow;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(current, HandleUtility.GetHandleSize(current) * 0.07f, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                ShapeAuthoring.Change(Path, "Move Shape Point", () => point.position = ShapeAuthoring.Snap(Path, tr.InverseTransformPoint(moved)));
                UpdatePointFields();
            }
            TangentHandle(false); TangentHandle(true);
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Delete)
            { DeletePoint(); evt.Use(); }
            if (evt.control && !evt.alt)
            {
                int segment; float t; Vector3 closest;
                FindInsertion(evt.mousePosition, out segment, out t, out closest);
                if (Vector2.Distance(HandleUtility.WorldToGUIPoint(closest), evt.mousePosition) < 18f)
                {
                    Handles.color = Color.magenta;
                    Handles.DotHandleCap(0, closest, Quaternion.identity, HandleUtility.GetHandleSize(closest) * 0.06f, EventType.Repaint);
                    if (evt.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                    if (evt.type == EventType.MouseDown && evt.button == 0)
                    { Insert(segment, t); evt.Use(); }
                }
            }
            Handles.Label(tr.TransformPoint(point.position) + Vector3.up * HandleUtility.GetHandleSize(current) * 0.15f, $"點 {selected + 1} ({point.mode})");
        }
        private void TangentHandle(bool outgoing)
        {
            var p = Path.points[selected]; var tr = Path.transform;
            Vector2 offset = outgoing ? p.outgoing : p.incoming;
            // Zero handles are still accessible via numeric fields or Smooth mode;
            // drawing both at the point would intercept vertex dragging.
            if (offset == Vector2.zero) return;
            Vector3 start = tr.TransformPoint(p.position), end = tr.TransformPoint(p.position + offset);
            Handles.color = outgoing ? Color.magenta : new Color(1, 0.6f, 0);
            Handles.DrawLine(start, end);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(end, HandleUtility.GetHandleSize(end) * 0.045f, Vector3.zero, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                SetTangent(outgoing, ShapeAuthoring.Snap(Path, tr.InverseTransformPoint(moved)) - p.position);
                UpdatePointFields();
            }
        }
        private void FindInsertion(Vector2 mouse, out int segment, out float t, out Vector3 closest)
        {
            segment = 0; t = 0.5f; closest = Path.transform.TransformPoint(Path.points[0].position); float best = float.PositiveInfinity;
            // Search displayed curve segments in screen space, then refine locally.
            // The inserted point is on the exact Bezier and is never grid-snapped.
            for (int i = 0; i < Path.points.Count; i++)
                for (int j = 1; j < 64; j++)
                {
                    float u = j / 64f;
                    Vector3 world = Path.transform.TransformPoint(ShapeGeometry.Evaluate(Path.points[i], Path.points[(i + 1) % Path.points.Count], u));
                    float distance = (HandleUtility.WorldToGUIPoint(world) - mouse).sqrMagnitude;
                    if (distance < best) { best = distance; segment = i; t = u; closest = world; }
                }
            float low = Mathf.Max(0.001f, t - 1f / 64), high = Mathf.Min(0.999f, t + 1f / 64);
            for (int k = 0; k < 12; k++)
            {
                float left = Mathf.Lerp(low, high, 1f / 3), right = Mathf.Lerp(low, high, 2f / 3);
                var a = Path.points[segment]; var b = Path.points[(segment + 1) % Path.points.Count];
                float dl = (HandleUtility.WorldToGUIPoint(Path.transform.TransformPoint(ShapeGeometry.Evaluate(a, b, left))) - mouse).sqrMagnitude;
                float dr = (HandleUtility.WorldToGUIPoint(Path.transform.TransformPoint(ShapeGeometry.Evaluate(a, b, right))) - mouse).sqrMagnitude;
                if (dl < dr) high = right; else low = left;
            }
            t = (low + high) / 2;
            closest = Path.transform.TransformPoint(ShapeGeometry.Evaluate(Path.points[segment], Path.points[(segment + 1) % Path.points.Count], t));
        }
    }

    [CustomEditor(typeof(StaticShapeArea))]
    public sealed class StaticShapeAreaEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("用途設定與形狀資料分離。禁畫區使用 Trigger，不阻擋 Runner／木板。外觀預設沿用現有平台或禁畫區。", HelpBoxMessageType.Info));
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }
    }
}
