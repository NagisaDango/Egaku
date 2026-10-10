using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Egaku.Shapes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Egaku.EditorShapes
{
    /// <summary>Editor-only generation and persistence. Never changes unrelated scene objects.</summary>
    [InitializeOnLoad]
    public static class ShapeAuthoring
    {
        public const string MeshFolder = "Assets/Generated/ShapeMeshes";
        // OnValidate can run during import, so all Unity API work is deferred to the
        // editor update. A set collapses Inspector/Undo notifications into one rebuild.
        private static readonly HashSet<ShapePath> pending = new HashSet<ShapePath>();
        static ShapeAuthoring()
        {
            ShapePath.AuthoringChanged += Queue;
            EditorApplication.update += Flush;
            Undo.undoRedoPerformed += AfterUndo;
            EditorSceneManager.sceneSaving += BeforeSceneSave;
            // Prefab Mode has its own save event and does not always invoke the
            // regular scene-saving callback. Bake edited stage geometry there too.
            PrefabStage.prefabSaving += root =>
            {
                Flush();
                foreach (var path in root.GetComponentsInChildren<ShapePath>(true)) Bake(path);
            };
            EditorSceneManager.sceneOpened += (s, mode) => QueueMissing(s);
            EditorApplication.delayCall += () =>
            {
                foreach (var path in LoadedPaths())
                    if (path.GetComponent<MeshFilter>().sharedMesh == null) Queue(path);
            };
        }
        private static void Queue(ShapePath path) { if (path != null) lock (pending) pending.Add(path); }
        private static IEnumerable<ShapePath> LoadedPaths() => Resources.FindObjectsOfTypeAll<ShapePath>()
            .Where(p => !EditorUtility.IsPersistent(p) && p.gameObject.scene.IsValid());
        private static void QueueMissing(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var path in root.GetComponentsInChildren<ShapePath>(true))
                    if (path.GetComponent<MeshFilter>().sharedMesh == null) Queue(path);
        }
        private static void Flush()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            ShapePath[] work;
            lock (pending) { work = pending.ToArray(); pending.Clear(); }
            foreach (var path in work)
                if (path != null && !EditorUtility.IsPersistent(path)) Rebuild(path);
        }
        private static void AfterUndo()
        {
            // Only tool-owned components are regenerated; no scene-wide conversion.
            foreach (var path in LoadedPaths()) Queue(path);
            SceneView.RepaintAll();
        }

        public static GameObject Create(StaticShapeUse use, ShapePreset preset, Vector3 position, Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring shapes.");
            var go = new GameObject(use == StaticShapeUse.Platform ? "Platform Shape" : "Draw Prohibited Shape");
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create Egaku Shape");
            go.transform.position = position;
            var path = Undo.AddComponent<ShapePath>(go);
            var area = Undo.AddComponent<StaticShapeArea>(go);
            path.points = ShapeGeometry.Preset(preset);
            area.use = use;
            // Copy the established policy from an ordinary square or the existing
            // prohibited-area prefab, rather than introducing a new physics layer.
            SpriteRenderer source = null;
            if (use == StaticShapeUse.Platform)
                source = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                    .FirstOrDefault(r => r.gameObject.layer == LayerMask.NameToLayer("Platform") &&
                        r.GetComponent<BoxCollider2D>() != null && r.GetComponent<BreakablePlatform>() == null &&
                        r.GetComponent<Rigidbody2D>() == null);
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/DrawProhibitedArea.prefab");
                if (prefab != null) source = prefab.GetComponent<SpriteRenderer>();
                area.fillColor = new Color(0.8627451f, 0, 0, 0.18039216f);
            }
            if (source != null)
            {
                area.fillMaterial = source.sharedMaterial; area.fillColor = source.color;
                area.sortingLayerId = source.sortingLayerID; area.sortingOrder = source.sortingOrder;
                var sourceCollider = source.GetComponent<Collider2D>();
                if (sourceCollider != null) area.physicsMaterial = sourceCollider.sharedMaterial;
            }
            if (area.fillMaterial == null)
                area.fillMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            Rebuild(path);
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(scene);
            return go;
        }

        public static bool Rebuild(ShapePath path)
        {
            if (path == null || EditorApplication.isPlayingOrWillChangePlaymode) return false;
            lock (pending) pending.Remove(path);
            var filter = path.GetComponent<MeshFilter>();
            var renderer = path.GetComponent<MeshRenderer>();
            var collider = path.GetComponent<PolygonCollider2D>();
            var area = path.GetComponent<StaticShapeArea>();
            string error;
            Vector2[] boundary; int[] triangles;
            bool valid = ShapeGeometry.TryBuild(path.points, path.curveTolerance, path.minimumEdge,
                path.maximumSubdivisionDepth, out boundary, out triangles, out error);
            int layer = area != null ? LayerMask.NameToLayer(area.use == StaticShapeUse.Platform ? "Platform" : "DrawProhibited") : path.gameObject.layer;
            if (layer < 0) { valid = false; error = "缺少必要 Layer；工具不會修改 Project Settings。"; }
            // Static shapes must never inherit a dynamic body or a second collider.
            // Report this instead of silently removing authored gameplay components.
            if (path.GetComponent<Rigidbody2D>() != null || path.GetComponents<Collider2D>().Length != 1)
            { valid = false; error = "靜態形狀不支援 Rigidbody2D 或額外 Collider；請移除衝突元件。"; }
            if (Mathf.Abs(path.transform.lossyScale.x) < 0.0001f || Mathf.Abs(path.transform.lossyScale.y) < 0.0001f)
            { valid = false; error = "形狀縮放為零，無法形成有效世界區域。"; }
            string message = valid ? $"有效：{path.points.Count} 控制點，{boundary.Length} 輪廓點。" : error;
            Color color = area != null ? area.fillColor : Color.white;
            // SpriteRenderer converts its Inspector tint for a Linear project, but
            // Mesh vertex colors are uploaded as-is. Apply the same conversion so
            // the existing Sprites/Default material yields the established terrain tint.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.linear;
            var vertices = boundary.Select(p => new Vector3(p.x, p.y, 0)).ToArray();
            var colors = Enumerable.Repeat(color, boundary.Length).ToArray();
            // OnEnable/OnValidate also run when reopening a scene. Leave its saved
            // objects and immutable mesh references untouched when outputs already agree.
            var existing = filter.sharedMesh;
            bool policyMatches = area == null || (path.gameObject.layer == layer &&
                collider.isTrigger == (area.use == StaticShapeUse.DrawProhibited) && collider.sharedMaterial == area.physicsMaterial &&
                renderer.sharedMaterial == area.fillMaterial && renderer.sortingLayerID == area.sortingLayerId && renderer.sortingOrder == area.sortingOrder);
            if (valid && collider.enabled && renderer.enabled && policyMatches && collider.offset == Vector2.zero &&
                collider.pathCount == 1 && collider.GetPath(0).SequenceEqual(boundary) && existing != null &&
                existing.vertices.SequenceEqual(vertices) && existing.triangles.SequenceEqual(triangles) &&
                existing.colors.SequenceEqual(colors) && path.validationMessage == message) return true;
            path.validationMessage = message;
            collider.enabled = valid; renderer.enabled = valid;
            if (!valid)
            {
                // Keep editable controls and the old cache for Undo, but disable both
                // outputs so an invalid new outline never leaves invisible old physics.
                EditorUtility.SetDirty(path); EditorUtility.SetDirty(collider); EditorUtility.SetDirty(renderer);
                return false;
            }
            if (area != null)
            {
                path.gameObject.layer = layer;
                collider.isTrigger = area.use == StaticShapeUse.DrawProhibited;
                collider.sharedMaterial = area.physicsMaterial;
                renderer.sharedMaterial = area.fillMaterial;
                renderer.sortingLayerID = area.sortingLayerId;
                renderer.sortingOrder = area.sortingOrder;
            }
            collider.offset = Vector2.zero;
            collider.pathCount = 1; collider.SetPath(0, boundary);
            var mesh = new Mesh { name = "Egaku Shape Preview" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = boundary.ToArray();
            mesh.colors = colors;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var old = filter.sharedMesh; filter.sharedMesh = mesh;
            // Preview meshes are private per object. Persistent baked meshes are
            // immutable, making Unity duplication safe even across saved scenes.
            if (old != null && !EditorUtility.IsPersistent(old) &&
                !LoadedPaths().Any(p => p != path && p.GetComponent<MeshFilter>().sharedMesh == old)) Object.DestroyImmediate(old);
            EditorUtility.SetDirty(path); EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(collider); EditorUtility.SetDirty(path.gameObject);
            if (area != null) EditorUtility.SetDirty(area);
            PrefabUtility.RecordPrefabInstancePropertyModifications(path);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            PrefabUtility.RecordPrefabInstancePropertyModifications(path.gameObject);
            SceneView.RepaintAll();
            return true;
        }

        public static void Bake(ShapePath path)
        {
            bool needsRebuild;
            lock (pending) needsRebuild = pending.Contains(path);
            if (needsRebuild) Rebuild(path);
            var filter = path.GetComponent<MeshFilter>();
            var mesh = filter.sharedMesh;
            if (mesh == null || EditorUtility.IsPersistent(mesh)) return;
            EnsureFolder(MeshFolder);
            // Content-addressed immutable assets allow identical duplicates to share
            // a saved mesh without one object's edits altering another's geometry.
            var text = new StringBuilder();
            foreach (var v in mesh.vertices) text.Append(v.x.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(v.y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            foreach (int i in mesh.triangles) text.Append(i).Append(',');
            foreach (var c in mesh.colors) text.Append(c.r.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(c.g.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(c.b.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(c.a.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
            string assetPath = MeshFolder + "/" + hash + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (saved == null)
            {
                mesh.name = "Egaku Shape " + hash.Substring(0, 12);
                AssetDatabase.CreateAsset(mesh, assetPath); saved = mesh;
            }
            filter.sharedMesh = saved;
            if (mesh != saved && !LoadedPaths().Any(p => p.GetComponent<MeshFilter>().sharedMesh == mesh)) Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        }
        private static void BeforeSceneSave(Scene scene, string path)
        {
            Flush();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var shape in root.GetComponentsInChildren<ShapePath>(true)) Bake(shape);
        }
        public static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string part in path.Split('/').Skip(1))
            {
                if (!AssetDatabase.IsValidFolder(current + "/" + part)) AssetDatabase.CreateFolder(current, part);
                current += "/" + part;
            }
        }
        public static Vector2 Snap(ShapePath path, Vector2 point)
        {
            if (!path.snapToGrid || path.gridSize < 0.001f || float.IsNaN(path.gridSize) || float.IsInfinity(path.gridSize)) return point;
            return new Vector2(Mathf.Round(point.x / path.gridSize) * path.gridSize, Mathf.Round(point.y / path.gridSize) * path.gridSize);
        }
        public static void Change(ShapePath path, string label, Action edit)
        {
            Undo.RecordObject(path, label); edit();
            EditorUtility.SetDirty(path); PrefabUtility.RecordPrefabInstancePropertyModifications(path);
            Rebuild(path); EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
        }
    }
}
