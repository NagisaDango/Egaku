using System;
using Unity.Cinemachine;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private Vector2 revivePos;
    private DrawerUICOntrol drawerUI;
    [SerializeField] private CinemachineCamera _camera;
    
    public void Init(Runner runner)
    {
        runner.GetComponent<Runner>().SetRevivePos(revivePos);
        runner.transform.position = new Vector3(revivePos.x, revivePos.y, 0);
        _camera.Follow = runner.transform;
    }

    public void Init(DrawerUICOntrol drawUI)
    {
        drawerUI = drawUI;
        TempLevelSetting();
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
        else
        {
            Debug.LogWarning("nothing set");
        }
    }

    public void EnablePenStatus(string penType)
    {
        if(drawerUI == null) 
            return;
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