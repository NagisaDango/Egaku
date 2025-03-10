using UnityEngine;
using UnityEngine.Events;
using UnityEngine.U2D;

public class LevelEditor : MonoBehaviour
{
    public static LevelEditor instance;
    private Transform spawningObject;
    public GameObject test;
    SpriteShapeController controller;

    void Start()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void Update()
    {
        if (spawningObject != null)
            spawningObject.position = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
    }

    public void SpawnNewObj(string objName)
    {
        Debug.Log($"Spawning {objName}");
        spawningObject = Instantiate(test).transform;
    }
}
