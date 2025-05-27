using System;
using Unity.Cinemachine;
using UnityEngine;

public class LevelSetup : MonoBehaviour
{
    [SerializeField] private Vector2 revivePos;
    private DrawerUICOntrol drawerUI;
    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private bool WoodEnable;
    [SerializeField] private bool CloudEnable;
    [SerializeField] private bool SteelEnable;
    [SerializeField] private bool ElectricEnable;
    public void Init(Runner runner)
    {
        runner.GetComponent<Runner>().SetRevivePos(revivePos);
        runner.transform.position = new Vector3(revivePos.x, revivePos.y, 0);
    }

    public void Init(DrawerUICOntrol drawUI)
    {
        drawerUI = drawUI;
        TempLevelSetting();
    }

    public void SetUpCamera(Runner runner)
    {
        _camera.PreviousStateIsValid = false;
        _camera.Follow = runner.transform;
    }

    public Vector2 GetRevivePos()
    {
        return revivePos;
    }

    private void TempLevelSetting()
    {
        if (drawerUI != null)
        {
            if(!WoodEnable) drawerUI.TogglePenStatus(PenUI.PenType.Wood, false);
            if(!CloudEnable) drawerUI.TogglePenStatus(PenUI.PenType.Cloud, false);
            if(!ElectricEnable) drawerUI.TogglePenStatus(PenUI.PenType.Electric, false);
            if(!SteelEnable) drawerUI.TogglePenStatus(PenUI.PenType.Steel, false);
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