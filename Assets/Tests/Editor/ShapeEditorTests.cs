using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Egaku.Tests.Editor
{
    // Like the existing gameplay tests, use reflection to reach Assembly-CSharp
    // from the test asmdef without changing the production assembly layout.
    public sealed class ShapeEditorTests
    {
        private Type Geometry => Type.GetType("Egaku.Shapes.ShapeGeometry, Assembly-CSharp", true);
        private Type Point => Type.GetType("Egaku.Shapes.ShapePoint, Assembly-CSharp", true);
        private Type PathType => Type.GetType("Egaku.Shapes.ShapePath, Assembly-CSharp", true);
        private Type Authoring => Type.GetType("Egaku.EditorShapes.ShapeAuthoring, Assembly-CSharp-Editor", true);
        private Scene scene;
        private readonly List<Mesh> temporaryMeshes = new List<Mesh>();

        [SetUp] public void SetUp() { scene = EditorSceneManager.NewPreviewScene(); }
        [TearDown] public void TearDown()
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null && !EditorUtility.IsPersistent(f.sharedMesh)) temporaryMeshes.Add(f.sharedMesh);
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var mesh in temporaryMeshes.Distinct()) if (mesh != null) Object.DestroyImmediate(mesh);
            temporaryMeshes.Clear();
            // Only test-created objects participate in Undo; production scene is not saved.
        }
        private object Preset(string preset) => Geometry.GetMethod("Preset").Invoke(null,
            new[] { Enum.Parse(Type.GetType("Egaku.Shapes.ShapePreset, Assembly-CSharp", true), preset) });
        private IList Points(params Vector2[] positions)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Point));
            foreach (var position in positions) list.Add(Activator.CreateInstance(Point, position));
            return list;
        }
        private bool Build(object points, out Vector2[] boundary, out int[] triangles, out string error, float tolerance = 0.025f)
        {
            var args = new object[] { points, tolerance, 0.002f, 10, null, null, null };
            bool result = (bool)Geometry.GetMethod("TryBuild").Invoke(null, args);
            boundary = (Vector2[])args[4]; triangles = (int[])args[5]; error = (string)args[6]; return result;
        }
        private Vector2 Position(object point) => (Vector2)Point.GetField("position").GetValue(point);
        private void Set(object point, string field, object value) => Point.GetField(field).SetValue(point, value);
        private Component Create(string use = "Platform", string preset = "Rectangle", Vector3 position = default)
        {
            var go = (GameObject)Authoring.GetMethod("Create").Invoke(null, new object[]
            {
                Enum.Parse(Type.GetType("Egaku.Shapes.StaticShapeUse, Assembly-CSharp", true), use),
                Enum.Parse(Type.GetType("Egaku.Shapes.ShapePreset, Assembly-CSharp", true), preset), position, scene
            });
            return go.GetComponent(PathType);
        }
        private IList PathPoints(Component path) => (IList)PathType.GetField("points").GetValue(path);
        private bool Rebuild(Component path) => (bool)Authoring.GetMethod("Rebuild").Invoke(null, new object[] { path });
        private void AssertAlignment(Component path)
        {
            var collider = path.GetComponent<PolygonCollider2D>(); var mesh = path.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(collider.enabled, Is.True); Assert.That(mesh, Is.Not.Null);
            var outline = collider.GetPath(0);
            Assert.That(mesh.vertices.Select(v => (Vector2)v).ToArray(), Is.EqualTo(outline));
            float fillArea = 0;
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
                fillArea += Mathf.Abs(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).z) / 2;
            float area = 0;
            for (int i = 0; i < outline.Length; i++) area += outline[i].x * outline[(i + 1) % outline.Length].y - outline[i].y * outline[(i + 1) % outline.Length].x;
            Assert.That(fillArea, Is.EqualTo(Mathf.Abs(area) / 2).Within(0.0001f), "Fill must cover the collider area without gaps or overlapping triangles.");
        }

        [TestCase("Rectangle")][TestCase("Triangle")][TestCase("Slope")][TestCase("Circle")][TestCase("Ellipse")]
        public void PresetsHaveMatchingFillAndCollider(string preset) { AssertAlignment(Create(preset: preset)); }

        [Test] public void PlatformFillMatchesExistingSpriteTintWhenRendered()
        {
            var shape = Create(position: new Vector3(5, 0, 0));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/DrawProhibitedArea.prefab").GetComponent<SpriteRenderer>();
            var reference = new GameObject("Reference Sprite"); SceneManager.MoveGameObjectToScene(reference, scene);
            reference.transform.position = new Vector3(-5, 0, 0); reference.transform.localScale = new Vector3(4, 2, 1);
            var sprite = reference.AddComponent<SpriteRenderer>(); sprite.sprite = source.sprite;
            sprite.sharedMaterial = source.sharedMaterial;
            var area = shape.GetComponent(Type.GetType("Egaku.Shapes.StaticShapeArea, Assembly-CSharp", true));
            sprite.color = (Color)area.GetType().GetField("fillColor").GetValue(area);
            var cameraObject = new GameObject("Tint comparison camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 6;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.scene = scene; camera.enabled = false;
            var target = new RenderTexture(240, 120, 16); var pixels = new Texture2D(240, 120, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 240, 120), 0, 0); pixels.Apply();
                Color expected = pixels.GetPixel(70, 60), actual = pixels.GetPixel(170, 60);
                Assert.That(expected.r, Is.GreaterThan(0.4f), "Reference sprite must actually render.");
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f));
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f));
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f));
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [TestCase(false)][TestCase(true)]
        public void ConcaveShapeTriangulatesInBothWindings(bool reverse)
        {
            var path = Create();
            var positions = new[] { new Vector2(-2, -1), new Vector2(2, -1), new Vector2(2, 1), new Vector2(0, 0), new Vector2(-2, 1) };
            if (reverse) Array.Reverse(positions);
            PathType.GetField("points").SetValue(path, Points(positions));
            Assert.That(Rebuild(path), Is.True); AssertAlignment(path);
            var col = path.GetComponent<PolygonCollider2D>();
            Assert.That(col.OverlapPoint(new Vector2(0, 0.75f)), Is.False, "Concave notch must not be filled by a convex hull.");
            Assert.That(col.OverlapPoint(new Vector2(0, -0.5f)), Is.True);
        }

        [Test] public void MixedCurveAndStraightEdgesUseOneBoundary()
        {
            var path = Create(); var points = PathPoints(path);
            Set(points[2], "incoming", new Vector2(-1, -0.5f)); Set(points[2], "outgoing", new Vector2(-0.5f, 0.5f));
            Set(points[3], "incoming", new Vector2(0.5f, 0.5f));
            Assert.That(Rebuild(path), Is.True); AssertAlignment(path);
            Assert.That(path.GetComponent<PolygonCollider2D>().GetPath(0).Length, Is.GreaterThan(4));
        }

        [Test] public void CurveToleranceControlsErrorAndPointCount()
        {
            var points = (IList)Preset("Ellipse"); Vector2[] coarse, fine; int[] tri; string error;
            Assert.That(Build(points, out coarse, out tri, out error, 0.1f), Is.True, error);
            Assert.That(Build(points, out fine, out tri, out error, 0.005f), Is.True, error);
            Assert.That(fine.Length, Is.GreaterThan(coarse.Length)); Assert.That(fine.Length, Is.LessThan(2048));
            for (int i = 0; i < points.Count; i++)
                for (int j = 0; j <= 100; j++)
                {
                    var p = (Vector2)Geometry.GetMethod("Evaluate").Invoke(null, new object[] { points[i], points[(i + 1) % points.Count], j / 100f });
                    float closest = float.PositiveInfinity;
                    for (int k = 0; k < fine.Length; k++)
                        closest = Mathf.Min(closest, (float)Geometry.GetMethod("DistanceToSegment").Invoke(null, new object[] { p, fine[k], fine[(k + 1) % fine.Length] }));
                    Assert.That(closest, Is.LessThanOrEqualTo(0.0051f));
                }
        }

        [Test] public void CurveInsertionPreservesItsExactShape()
        {
            var points = (IList)Preset("Circle"); var a = points[0]; var b = points[1];
            var expected = Enumerable.Range(0, 101).Select(i => (Vector2)Geometry.GetMethod("Evaluate").Invoke(null, new object[] { a, b, i / 100f })).ToArray();
            Geometry.GetMethod("SplitSegment").Invoke(null, new object[] { points, 0, 0.35f });
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                Vector2 actual = (Vector2)Geometry.GetMethod("Evaluate").Invoke(null,
                    t <= 0.35f ? new object[] { points[0], points[1], t / 0.35f } : new object[] { points[1], points[2], (t - 0.35f) / 0.65f });
                Assert.That(Vector2.Distance(actual, expected[i]), Is.LessThan(0.00001f));
            }
        }

        [TestCase("self")][TestCase("duplicate")][TestCase("short")][TestCase("area")][TestCase("fold")][TestCase("nan")]
        public void InvalidShapesAreRejectedWithExplanation(string kind)
        {
            IList points;
            switch (kind)
            {
                case "self": points = Points(new Vector2(-1, -1), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1)); break;
                case "duplicate": points = Points(Vector2.zero, Vector2.right, Vector2.one, Vector2.zero); break;
                case "short": points = Points(Vector2.zero, new Vector2(0.001f, 0), Vector2.one, Vector2.up); break;
                case "area": points = Points(Vector2.zero, Vector2.right, new Vector2(2, 0)); break;
                case "fold": points = Points(Vector2.zero, new Vector2(2, 0), Vector2.right, Vector2.one, Vector2.up); break;
                default: points = Points(new Vector2(float.NaN, 0), Vector2.one, Vector2.up); break;
            }
            Vector2[] boundary; int[] tri; string error;
            Assert.That(Build(points, out boundary, out tri, out error), Is.False); Assert.That(error, Is.Not.Empty);
        }

        [Test] public void InvalidEditsDisableBothVisibleAndPhysicalOutputs()
        {
            var path = Create(); var points = PathPoints(path);
            Set(points[1], "position", Position(points[0]));
            Assert.That(Rebuild(path), Is.False);
            Assert.That(path.GetComponent<PolygonCollider2D>().enabled, Is.False);
            Assert.That(path.GetComponent<MeshRenderer>().enabled, Is.False);
            Set(points[1], "position", new Vector2(2, -1));
            Assert.That(Rebuild(path), Is.True); AssertAlignment(path);
        }

        [Test] public void SnapAndPointEditUndoRedoRestoreBothOutputs()
        {
            var path = Create();
            Vector2 snapped = (Vector2)Authoring.GetMethod("Snap").Invoke(null, new object[] { path, new Vector2(1.12f, 0.88f) });
            Assert.That(snapped, Is.EqualTo(new Vector2(1, 1)));
            var points = PathPoints(path); Vector2 initial = Position(points[0]);
            Undo.IncrementCurrentGroup();
            Authoring.GetMethod("Change").Invoke(null, new object[] { path, "Test point move", (Action)(() => Set(points[0], "position", new Vector2(-3, -1))) });
            Undo.FlushUndoRecordObjects();
            AssertAlignment(path);
            Undo.PerformUndo(); Rebuild(path);
            Assert.That(Position(PathPoints(path)[0]), Is.EqualTo(initial)); AssertAlignment(path);
            Undo.PerformRedo(); Rebuild(path);
            Assert.That(Position(PathPoints(path)[0]), Is.EqualTo(new Vector2(-3, -1))); AssertAlignment(path);
        }

        [Test] public void DuplicateGeometryEditsDoNotMutateOriginal()
        {
            var path = Create(); var initial = path.GetComponent<MeshFilter>().sharedMesh.vertices.ToArray();
            var duplicate = Object.Instantiate(path.gameObject); SceneManager.MoveGameObjectToScene(duplicate, scene);
            var copy = duplicate.GetComponent(PathType); var points = PathPoints(copy);
            Set(points[0], "position", new Vector2(-4, -1)); Assert.That(Rebuild(copy), Is.True);
            Assert.That(path.GetComponent<MeshFilter>().sharedMesh.vertices, Is.EqualTo(initial)); AssertAlignment(copy); AssertAlignment(path);
        }

        [Test] public void InspectorCommandsInsertDeleteAndAlignSmoothTangents()
        {
            var path = Create(); var editor = UnityEditor.Editor.CreateEditor(path);
            try
            {
                Assert.That(editor.GetType().FullName, Is.EqualTo("Egaku.EditorShapes.ShapePathEditor"));
                Assert.That(editor.CreateInspectorGUI(), Is.Not.Null);
                var methods = BindingFlags.Instance | BindingFlags.NonPublic;
                editor.GetType().GetMethod("Insert", methods).Invoke(editor, new object[] { 0, 0.5f });
                Assert.That(PathPoints(path).Count, Is.EqualTo(5)); AssertAlignment(path);
                editor.GetType().GetMethod("DeletePoint", methods).Invoke(editor, null);
                Assert.That(PathPoints(path).Count, Is.EqualTo(4)); AssertAlignment(path);
                var smooth = Enum.Parse(Type.GetType("Egaku.Shapes.ShapePointMode, Assembly-CSharp", true), "Smooth");
                editor.GetType().GetMethod("SetMode", methods).Invoke(editor, new[] { smooth });
                editor.GetType().GetMethod("SetTangent", methods).Invoke(editor, new object[] { true, new Vector2(0.4f, 0.2f) });
                var p = PathPoints(path)[1];
                Vector2 incoming = (Vector2)Point.GetField("incoming").GetValue(p);
                Vector2 outgoing = (Vector2)Point.GetField("outgoing").GetValue(p);
                Assert.That(Vector2.Dot(incoming.normalized, outgoing.normalized), Is.EqualTo(-1).Within(0.0001f));
                AssertAlignment(path);
            }
            finally { Object.DestroyImmediate(editor); }
        }

        [Test] public void PrefabRoundTripKeepsEditablePointsAndBakedGeometry()
        {
            // Unity Test Runner may own an untitled scene, so test Prefab persistence
            // in a preview scene. Scene save/reopen is verified in the separate Sandbox.
            string suffix = Guid.NewGuid().ToString("N");
            string prefabPath = "Assets/Tests/ShapeRoundTrip_" + suffix + ".prefab";
            GameObject loaded = null;
            try
            {
                var path = Create(preset: "Ellipse");
                var pointsBefore = JsonUtility.ToJson(path);
                var verticesBefore = path.GetComponent<MeshFilter>().sharedMesh.vertices.ToArray();
                Authoring.GetMethod("Bake").Invoke(null, new object[] { path });
                Assert.That(EditorUtility.IsPersistent(path.GetComponent<MeshFilter>().sharedMesh), Is.True);
                PrefabUtility.SaveAsPrefabAsset(path.gameObject, prefabPath);
                loaded = PrefabUtility.LoadPrefabContents(prefabPath);
                var restored = loaded.GetComponent(PathType);
                Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(pointsBefore));
                Assert.That(restored.GetComponent<MeshFilter>().sharedMesh.vertices, Is.EqualTo(verticesBefore));
                AssertAlignment(restored);
                Assert.That(Rebuild(restored), Is.True);
                Assert.That(EditorUtility.IsPersistent(restored.GetComponent<MeshFilter>().sharedMesh), Is.True,
                    "Rebuilding unchanged data must preserve the baked asset reference.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab.GetComponent(PathType), Is.Not.Null);
                Assert.That(prefab.GetComponent<MeshFilter>().sharedMesh.vertices, Is.EqualTo(verticesBefore));
                var duplicate = Object.Instantiate(restored.gameObject); SceneManager.MoveGameObjectToScene(duplicate, scene);
                var copy = duplicate.GetComponent(PathType);
                Set(PathPoints(copy)[0], "position", new Vector2(2.5f, 0)); Rebuild(copy);
                Authoring.GetMethod("Bake").Invoke(null, new object[] { copy });
                Assert.That(restored.GetComponent<MeshFilter>().sharedMesh.vertices, Is.EqualTo(verticesBefore));
                Assert.That(copy.GetComponent<MeshFilter>().sharedMesh, Is.Not.EqualTo(restored.GetComponent<MeshFilter>().sharedMesh));
            }
            finally
            {
                if (loaded != null) PrefabUtility.UnloadPrefabContents(loaded);
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        [Test] public void ProhibitedAreaQueryHitsInteriorAndBoundaryButDoesNotBlockFallingBoard()
        {
            var path = Create("DrawProhibited", "Ellipse"); var collider = path.GetComponent<PolygonCollider2D>();
            Assert.That(collider.isTrigger, Is.True); Assert.That(path.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("DrawProhibited")));
            Assert.That(collider.OverlapPoint(Vector2.zero), Is.True);
            var physics = scene.GetPhysicsScene2D();
            var filter = new ContactFilter2D { useTriggers = true }; filter.SetLayerMask(LayerMask.GetMask("DrawProhibited"));
            var hits = new RaycastHit2D[8];
            Assert.That(physics.Raycast(new Vector2(-4, 0), Vector2.right, 8, filter, hits), Is.GreaterThan(0));
            Assert.That(hits[0].point.x, Is.EqualTo(-2).Within(0.03f));
            Assert.That(physics.Raycast(Vector2.zero, Vector2.right, 0.01f, filter, hits), Is.GreaterThan(0));
            // This exercises the query contract, not DrawMesh RPC finalization or
            // actual Drawer input. Those require a connected gameplay session.
            var board = new GameObject("Falling board"); SceneManager.MoveGameObjectToScene(board, scene);
            board.layer = LayerMask.NameToLayer("Draw"); board.transform.position = new Vector3(0, 3, 0);
            board.AddComponent<BoxCollider2D>().size = new Vector2(1, 0.3f); var rb = board.AddComponent<Rigidbody2D>();
            for (int i = 0; i < 70; i++) physics.Simulate(0.02f);
            Assert.That(rb.position.y, Is.LessThan(-1.5f), "A board must fall through the Trigger volume.");
        }

        [Test] public void ExistingRunnerMotorStandsAndJumpsOnNewStaticPlatform()
        {
            var path = Create();
            var player = new GameObject("Runner physics test"); SceneManager.MoveGameObjectToScene(player, scene);
            player.layer = LayerMask.NameToLayer("Player"); player.transform.position = new Vector3(0, 4, 0);
            var collider = player.AddComponent<BoxCollider2D>(); collider.size = new Vector2(0.7f, 1);
            var body = player.AddComponent<Rigidbody2D>(); body.constraints = RigidbodyConstraints2D.FreezeRotation;
            var physics = scene.GetPhysicsScene2D();
            for (int i = 0; i < 80; i++) physics.Simulate(0.02f);
            Assert.That(body.position.y, Is.EqualTo(1.5f).Within(0.06f));
            var motorType = Type.GetType("RunnerMovement, Assembly-CSharp", true);
            var motor = Activator.CreateInstance(motorType, body, collider,
                Activator.CreateInstance(Type.GetType("RunnerMovementTuning, Assembly-CSharp", true)),
                Activator.CreateInstance(Type.GetType("RunnerGrabTuning, Assembly-CSharp", true)));
            motorType.GetMethod("FixedStep").Invoke(motor, new object[] { 0f, 2f, 10f, 22, null });
            Assert.That((bool)motorType.GetProperty("Grounded").GetValue(motor), Is.True);
            motorType.GetMethod("QueueJump").Invoke(motor, new object[] { 2.02f });
            Assert.That(motorType.GetMethod("FixedStep").Invoke(motor, new object[] { 0f, 2.02f, 10f, 22, null }), Is.EqualTo(true));
            physics.Simulate(0.02f); Assert.That(body.position.y, Is.GreaterThan(1.7f));
        }
    }
}
