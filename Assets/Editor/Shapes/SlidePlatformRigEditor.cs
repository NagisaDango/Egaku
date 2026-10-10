using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class SlidePlatformRigAuthoring
{
    static SlidePlatformRigAuthoring() { EditorApplication.update += Update; }
    private static double nextUpdate;
    private static void Update()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextUpdate) return;
        nextUpdate = EditorApplication.timeSinceStartup + 0.05;
        // Child Transform moves do not reliably schedule ExecuteAlways callbacks
        // in Prefab Mode. Poll only loaded scene rigs; unchanged inputs do nothing.
        foreach (var rig in Resources.FindObjectsOfTypeAll<SlidePlatformRig>())
            if (rig != null && !EditorUtility.IsPersistent(rig) && rig.gameObject.scene.IsValid() && rig.gameObject.scene.isLoaded)
                rig.UpdateAuthoringIfNeeded();
    }
}

[CustomEditor(typeof(SlidePlatformRig))]
public sealed class SlidePlatformRigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (target == null) return;
        DrawDefaultInspector();
        var rig = (SlidePlatformRig)target;
        EditorGUILayout.HelpBox("拖動 Endpoint A / Endpoint B，軌道長度與平台朝向會自動更新。平台受力滑動，沒有自動往返。", MessageType.Info);
        if (!rig.IsValid) EditorGUILayout.HelpBox("兩端點須在同一 Z 平面，間距至少 0.01，且軌道長度不能短於平台外緣寬度。無法容納時會停止滑動。", MessageType.Error);
    }

    private void OnSceneGUI()
    {
        if (target == null || Application.isPlaying) return;
        var rig = (SlidePlatformRig)target;
        if (rig.endpointA == null || rig.endpointB == null) return;
        Handles.color = Color.cyan;
        Handles.Label(rig.endpointA.position, "A"); Handles.Label(rig.endpointB.position, "B");
        EditorGUI.BeginChangeCheck();
        var a = Handles.PositionHandle(rig.endpointA.position, Quaternion.identity);
        var b = Handles.PositionHandle(rig.endpointB.position, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObjects(new Object[] { rig.endpointA, rig.endpointB }, "Move slide endpoints");
            // Keep the complete rig in its existing 2D plane.
            a.z = rig.transform.position.z; b.z = a.z;
            rig.endpointA.position = a; rig.endpointB.position = b;
            rig.RefreshAuthoring();
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.endpointA);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.endpointB);
        }
    }
}
