using System;
using Unity.Cinemachine;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private Vector2 revivePos;
    private DrawerUICOntrol drawerUI;
    [SerializeField] private CinemachineCamera _camera;
    
    private void Start()
    {
        GameObject runner = GameObject.Find("Runner");
        runner.GetComponent<Runner>().SetRevivePos(revivePos);
        runner.transform.position = new Vector3(revivePos.x, revivePos.y, 0);
        drawerUI = GameObject.Find("DrawerUICanvas(Clone)/DrawerUIPanel").GetComponent<DrawerUICOntrol>();
        TempLevelSetting();
        _camera.Follow = runner.transform;
    }
    
    private void TempLevelSetting()
    {
        if (drawerUI != null)
        {
            drawerUI.TogglePenStatus(PenUI.PenType.Wood, false);
            drawerUI.TogglePenStatus(PenUI.PenType.Cloud, false);
            drawerUI.TogglePenStatus(PenUI.PenType.Electric, false);
            drawerUI.TogglePenStatus(PenUI.PenType.Steel, false);
        }
    }

    public void EnablePenStatus(string penType)
    {
        switch (penType)
        {
            case "Wood":
                drawerUI.TogglePenStatus(PenUI.PenType.Wood, true);
                break;
            case "Cloud":
                drawerUI.TogglePenStatus(PenUI.PenType.Cloud, true);
                break;
            case "Electric":
                drawerUI.TogglePenStatus(PenUI.PenType.Electric, true);
                break;
            case "Steel":
                drawerUI.TogglePenStatus(PenUI.PenType.Steel, true);
                break;
            default:
                Debug.LogWarning(penType + "Unknown type");
                break;
        }
    }
}