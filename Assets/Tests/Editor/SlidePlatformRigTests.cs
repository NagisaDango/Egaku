using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SlidePlatformRigTests
{
    public const string PrefabPath = "Assets/Prefab/Platforms/SlidePlatformRig.prefab";

    [TestCase(6f, 0f)]
    [TestCase(0f, 6f)]
    [TestCase(5f, 4f)]
    public void EndpointTransformEditsAutomaticallyRebuildAndConstrainPhysics(float x, float y)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(asset, Is.Not.Null);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            root.transform.position = new Vector3(8, 7, 0);
            var a = root.transform.Find("Endpoint A");
            var b = root.transform.Find("Endpoint B");
            var track = root.transform.Find("Track");
            var platform = root.transform.Find("Platform").GetComponent<Rigidbody2D>();
            var type = Type.GetType("SlidePlatformRig, Assembly-CSharp", true);
            var rig = root.GetComponent(type);
            a.localPosition = new Vector3(-x * .5f, -y * .5f, 0);
            b.localPosition = -a.localPosition;
            // Exercise the automatic edit-mode callback, without an Apply button.
            type.GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(rig, null);
            float angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(track.eulerAngles.z, angle)), Is.LessThan(.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(platform.rotation, angle)), Is.LessThan(.01f));
            Assert.That(track.GetComponent<SpriteRenderer>().size.x, Is.EqualTo(new Vector2(x, y).magnitude).Within(.001f));
            Assert.That(Vector2.Distance(platform.position, root.transform.position), Is.LessThan(.001f));
            Assert.That(track.GetComponent<Collider2D>(), Is.Null);
            Assert.That(a.GetComponent<Collider2D>(), Is.Null);
            Assert.That(b.GetComponent<Collider2D>(), Is.Null);
            var dir = new Vector2(x, y).normalized;
            var normal = new Vector2(-dir.y, dir.x);
            float halfLength = platform.GetComponent<SliderJoint2D>().limits.max;
            var physics = scene.GetPhysicsScene2D();
            // Force both ways with a perpendicular load and torque; mass and
            // damping match Level 10, so allow enough time to reach each end.
            for (int sign = -1; sign <= 1; sign += 2)
            {
                for (int i = 0; i < 800; i++)
                {
                    platform.AddForce(dir * sign * 1800 + normal * 600);
                    platform.AddTorque(200);
                    physics.Simulate(.02f);
                    var displacement = platform.position - (Vector2)root.transform.position;
                    Assert.That(Mathf.Abs(Vector2.Dot(displacement, normal)), Is.LessThan(.04f));
                    Assert.That(Mathf.Abs(Vector2.Dot(displacement, dir)), Is.LessThan(halfLength + .04f));
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(platform.rotation, angle)), Is.LessThan(.1f));
                    AssertEdgesInside(platform, a.position, b.position);
                }
                Assert.That(Vector2.Dot(platform.position - (Vector2)root.transform.position, dir) * sign,
                    Is.GreaterThan(halfLength - .04f));
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void AssertEdgesInside(Rigidbody2D body, Vector3 a, Vector3 b)
    {
        var sprite = body.GetComponent<SpriteRenderer>();
        var box = body.GetComponent<BoxCollider2D>();
        var bounds = sprite.localBounds;
        bounds.Encapsulate((Vector3)box.offset - (Vector3)box.size * .5f - new Vector3(box.edgeRadius, box.edgeRadius));
        bounds.Encapsulate((Vector3)box.offset + (Vector3)box.size * .5f + new Vector3(box.edgeRadius, box.edgeRadius));
        var dir = (b - a).normalized;
        float length = Vector3.Distance(a, b);
        foreach (float x in new[] { bounds.min.x, bounds.max.x })
            foreach (float y in new[] { bounds.min.y, bounds.max.y })
            {
                float distance = Vector3.Dot(body.transform.TransformPoint(new Vector3(x, y)) - a, dir);
                Assert.That(distance, Is.InRange(-.04f, length + .04f), "Visible/collider outer edge must stay within endpoints.");
            }
    }

    [TestCase(0f)]
    [TestCase(1f)]
    public void StartingRatioAlignsOuterEdgesAndResizingUpdatesLimits(float start)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            var a = root.transform.Find("Endpoint A"); var b = root.transform.Find("Endpoint B");
            a.localPosition = new Vector3(-5, 0, 0); b.localPosition = new Vector3(5, 0, 0);
            var body = root.transform.Find("Platform").GetComponent<Rigidbody2D>();
            var type = Type.GetType("SlidePlatformRig, Assembly-CSharp", true); var rig = root.GetComponent(type);
            type.GetField("startingPosition").SetValue(rig, start);
            var update = type.GetMethod("UpdateAuthoringIfNeeded"); update.Invoke(rig, null);
            var sprite = body.GetComponent<SpriteRenderer>();
            float edge = start == 0 ? sprite.localBounds.min.x : sprite.localBounds.max.x;
            Assert.That(body.transform.TransformPoint(new Vector3(edge, 0)).x, Is.EqualTo(start == 0 ? -5 : 5).Within(.001f));
            // Change only platform dimensions/offset, keeping endpoints and ratio.
            body.transform.localScale = new Vector3(1.5f, 1, 1);
            body.GetComponent<BoxCollider2D>().offset = new Vector2(.75f, 0);
            update.Invoke(rig, null);
            AssertEdgesInside(body, a.position, b.position);
            float low = sprite.localBounds.min.x * 1.5f;
            float high = (body.GetComponent<BoxCollider2D>().offset.x + body.GetComponent<BoxCollider2D>().size.x * .5f) * 1.5f;
            Assert.That(body.position.x + (start == 0 ? low : high), Is.EqualTo(start == 0 ? -5 : 5).Within(.001f));
            b.localPosition = a.localPosition + Vector3.right;
            update.Invoke(rig, null);
            Assert.That((bool)type.GetProperty("IsValid").GetValue(rig), Is.False);
            Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeAll));
            b.localPosition = new Vector3(5, 0, 0); update.Invoke(rig, null);
            Assert.That((bool)type.GetProperty("IsValid").GetValue(rig), Is.True);
            Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test] public void PrefabHasPersistentInternalReferences()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var type = Type.GetType("SlidePlatformRig, Assembly-CSharp", true);
            var rig = root.GetComponent(type);
            Assert.That(root.transform.childCount, Is.EqualTo(4));
            foreach (string name in new[] { "endpointA", "endpointB", "track", "platform" })
                Assert.That(((Component)type.GetField(name).GetValue(rig)).transform.IsChildOf(root.transform), Is.True);
            Assert.That(root.GetComponentsInChildren<Collider2D>().Length, Is.EqualTo(1));
            var a = root.transform.Find("Endpoint A").position;
            var b = root.transform.Find("Endpoint B").position;
            var delta = b - a;
            var track = root.transform.Find("Track");
            var platform = root.transform.Find("Platform");
            Assert.That(track.GetComponent<SpriteRenderer>().size.x, Is.EqualTo(delta.magnitude).Within(.001f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(track.eulerAngles.z, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg)), Is.LessThan(.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(platform.eulerAngles.z, track.eulerAngles.z)), Is.LessThan(.01f));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test] public void EndpointUndoRedoRestoresDerivedTrack()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            var endpoint = root.transform.Find("Endpoint B");
            var track = root.transform.Find("Track");
            var type = Type.GetType("SlidePlatformRig, Assembly-CSharp", true);
            var rig = root.GetComponent(type);
            var update = type.GetMethod("UpdateAuthoringIfNeeded");
            update.Invoke(rig, null);
            var before = endpoint.localPosition;
            float lengthBefore = track.GetComponent<SpriteRenderer>().size.x;
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(endpoint, "Test endpoint move");
            endpoint.localPosition += new Vector3(1, 2, 0);
            Undo.FlushUndoRecordObjects();
            update.Invoke(rig, null);
            float lengthAfter = track.GetComponent<SpriteRenderer>().size.x;
            Assert.That(lengthAfter, Is.Not.EqualTo(lengthBefore));
            Undo.PerformUndo(); update.Invoke(rig, null);
            Assert.That(endpoint.localPosition, Is.EqualTo(before));
            Assert.That(track.GetComponent<SpriteRenderer>().size.x, Is.EqualTo(lengthBefore).Within(.001f));
            Undo.PerformRedo(); update.Invoke(rig, null);
            Assert.That(track.GetComponent<SpriteRenderer>().size.x, Is.EqualTo(lengthAfter).Within(.001f));
            Undo.ClearUndo(endpoint);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
