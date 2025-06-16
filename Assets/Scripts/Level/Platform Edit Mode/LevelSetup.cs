using System;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

public class LevelSetup : MonoBehaviourPun
{
    [SerializeField] private Vector2 revivePos;
    private DrawerUICOntrol drawerUI;
    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private bool WoodEnable;
    [SerializeField] private bool CloudEnable;
    [SerializeField] private bool SteelEnable;
    [SerializeField] private bool ElectricEnable;
    [SerializeField] private PenUI.PenType initPenType;
    public void Init(Runner runner)
    {
        runner.GetComponent<Runner>().SetRevivePos(revivePos);
        runner.transform.position = new Vector3(revivePos.x, revivePos.y, 0);
    }

    public void Init(DrawerUICOntrol drawUI)
    {
        Drawer.OnPenSelect.Invoke(initPenType);
        drawerUI = drawUI;
        TempLevelSetting();
    }

    public void SetUpCamera(Runner runner)
    {
        //Debug.LogError("ENTER SET UP CAMERA" );
        _camera.PreviousStateIsValid = false;
        _camera.Follow = runner.transform;
        //Debug.LogError(_camera.Follow.gameObject.name);
    }

    public Vector2 GetRevivePos()
    {
        return revivePos;
    }

    private void TempLevelSetting()
    {
        if (drawerUI != null)
        {
            if (!WoodEnable) 
            {drawerUI.TogglePenStatus(PenUI.PenType.Wood, false); Drawer.penStatus[0] = false;}
            else Drawer.penStatus[0] = true;
            if(!CloudEnable) 
            {drawerUI.TogglePenStatus(PenUI.PenType.Cloud, false); Drawer.penStatus[1] = false;}
            else Drawer.penStatus[1] = true;
            if(!SteelEnable) 
            {drawerUI.TogglePenStatus(PenUI.PenType.Steel, false); Drawer.penStatus[2] = false;}
            else Drawer.penStatus[2] = true;
            if(!ElectricEnable) 
            {drawerUI.TogglePenStatus(PenUI.PenType.Electric, false); Drawer.penStatus[3] = false;}
            else Drawer.penStatus[3] = true;
        }
        else
        {
            Debug.LogWarning("nothing set");
        }
    }

    public void EnablePenStatus(string penType)
    {
        if (drawerUI != null)
            RPC_EnablePen(penType);
        else
            photonView.RPC("RPC_EnablePen", RpcTarget.OthersBuffered, penType);
    }

    [PunRPC]
    private void RPC_EnablePen(string penType)
    {
        switch (penType)
        {
            case "Wood":
                drawerUI.TogglePenStatus(PenUI.PenType.Wood, true);
                Drawer.penStatus[0] = true;
                Drawer.OnPenSelect.Invoke(PenUI.PenType.Wood);
                break;
            case "Cloud":
                drawerUI.TogglePenStatus(PenUI.PenType.Cloud, true);
                Drawer.OnPenSelect.Invoke(PenUI.PenType.Cloud);
                Drawer.penStatus[1] = true;
                break;
            case "Steel":
                drawerUI.TogglePenStatus(PenUI.PenType.Steel, true);
                Drawer.OnPenSelect.Invoke(PenUI.PenType.Steel);
                Drawer.penStatus[2] = true;
                break;
            case "Electric":
                drawerUI.TogglePenStatus(PenUI.PenType.Electric, true);
                Drawer.OnPenSelect.Invoke(PenUI.PenType.Electric);
                Drawer.penStatus[3] = true;
                break;
            default:
                Debug.LogWarning(penType + "Unknown type");
                break;
        }
    }
}