using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.U2D;

public class LevelSaver : MonoBehaviour
{
    public Transform levelParent;
    //Change it to loading them from prefabs folder, prefabs array, by using name if getting too hard to manage?
    public GameObject platformPrefab;

    public void SaveLevel(string fileName)
    {
        LevelData levelData = new LevelData();
        levelData.platforms = new List<PlatformData>();

        foreach(Transform holder in levelParent)
        {
            if (holder.gameObject.tag == "Holder")
            {
                foreach (Transform platform in holder)
                {
                    Debug.Log($"Saved: {platform.name}");
                    PlatformData platformData = new PlatformData
                    {
                        name = platform.name,
                        position = platform.position,
                        rotation = platform.rotation,
                        splinePoints = new List<Vector3>()
                    };
                    Spline spline = platform.gameObject.GetComponent<SpriteShapeController>().spline;
                    for (int i = 0; i < spline.GetPointCount(); i++)
                    {
                        Debug.Log(spline);
                        platformData.splinePoints.Add(spline.GetPosition(i));
                    }

                    levelData.platforms.Add(platformData);
                }
            }
            else
            {
                Debug.LogWarning($"{holder.name} not a holder tag, skipping");
            }
        }

        string json = JsonUtility.ToJson(levelData);
        string path = Path.Combine(Application.persistentDataPath, fileName);
        Debug.Log($"Data saved at {path}");
        File.WriteAllText(path, json);
    }

    public void LoadLevel(string fileName)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"File path not exist {path}");
            return;
        }
        ClearLevel();
        string json = File.ReadAllText(path);
        LevelData levelData = JsonUtility.FromJson<LevelData>(json);

        Transform platformParent  = new GameObject().transform;
        platformParent.gameObject.tag = "Holder";
        platformParent.SetParent(levelParent);
        platformParent.name = "Platform Holder";
        foreach (var platform in levelData.platforms)
        {
            Debug.Log($"{platform.name}");
            Transform instantiatedObj = Instantiate(platformPrefab, platform.position, platform.rotation, platformParent).transform;
            instantiatedObj.name = platform.name;
            instantiatedObj.position = platform.position;
            instantiatedObj.rotation = platform.rotation;
            Spline spline = instantiatedObj.GetComponent<SpriteShapeController>().spline;

            for(int i = 0; i < platform.splinePoints.Count; i++)
            {
                spline.InsertPointAt(i, platform.splinePoints[i]);
                spline.SetTangentMode(i, ShapeTangentMode.Continuous);
            }
        }
    }

    public void ClearLevel()
    {
        foreach(Transform child in levelParent)
        {
            Destroy(child.gameObject);
        }
    }
}

[Serializable]
public class LevelData
{
    public List<PlatformData> platforms;
}

[Serializable]
public class PlatformData
{
    public string name;
    public Vector3 position;
    public Quaternion rotation;
    public List<Vector3> splinePoints;
    //public Vector3 scale;
}