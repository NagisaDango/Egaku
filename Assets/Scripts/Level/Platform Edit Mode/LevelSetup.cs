using System;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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
    // A recovery scene reload can fire a pickup trigger before the Drawer UI's Start method.
    // Keep those local unlocks until Init binds the new UI instead of losing or buffering them.
    private readonly HashSet<string> pendingPenUnlocks = new HashSet<string>();
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

        if (pendingPenUnlocks.Count > 0)
        {
            string[] queuedUnlocks = new string[pendingPenUnlocks.Count];
            pendingPenUnlocks.CopyTo(queuedUnlocks);
            pendingPenUnlocks.Clear();
            foreach (string penType in queuedUnlocks)
                ApplyPenUnlock(penType);
        }
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
        string csvPath = "Assets/Resources/LevelSetup.csv";

        string[] lines = File.ReadAllLines(csvPath);

        Dictionary<int, int[]> inkDict = new Dictionary<int, int[]>();
        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = lines[i].Split(',');
            Debug.Log("LevelSetup " + string.Join(" | ", values));
            int[] inks = new int[4];
            inks[0] = int.Parse(values[1]);
            inks[1] = int.Parse(values[2]);
            inks[2] = int.Parse(values[3]);
            inks[3] = int.Parse(values[4]);
            inkDict.Add(i-1, inks);

        }

        int level = int.Parse( SceneManager.GetActiveScene().name.Split("_")[1]);

        Drawer.Instance.woodPen.maxStrokes = inkDict[level][0];
        Drawer.Instance.cloudPen.maxStrokes = inkDict[level][1];
        Drawer.Instance.steelPen.maxStrokes = inkDict[level][2];
        Drawer.Instance.electricPen.maxStrokes = inkDict[level][3];


        if (drawerUI != null)
        {
            if (!WoodEnable)
            { drawerUI.TogglePenStatus(PenUI.PenType.Wood, false); Drawer.penStatus[0] = false; }
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
        {
            ApplyPenUnlock(penType);
            return;
        }

        if (IsLocalDrawer())
        {
            pendingPenUnlocks.Add(penType);
            return;
        }

        // Pickups are detected by the Runner's local physics. Forward once to the Drawer;
        // a full recovery reload reconstructs the level, so this transient command is not buffered.
        if (PhotonNetwork.InRoom)
            photonView.RPC(nameof(RPC_EnablePen), RpcTarget.Others, penType);
    }

    [PunRPC]
    private void RPC_EnablePen(string penType)
    {
        if (drawerUI == null)
        {
            pendingPenUnlocks.Add(penType);
            return;
        }

        ApplyPenUnlock(penType);
    }

    private void ApplyPenUnlock(string penType)
    {
        Color color = Color.white;
        PenProperty.PenType type = PenProperty.PenType.Wood;
        switch (penType)
        {
            case "Wood":
                drawerUI.TogglePenStatus(PenUI.PenType.Wood, true);
                Drawer.penStatus[0] = true;
                Drawer.OnPenSelect.Invoke(PenUI.PenType.Wood);
                color = Drawer.Instance.FindPenProperty(PenUI.PenType.Wood).material.color;
                type = PenProperty.PenType.Wood;    
                break;
            case "Cloud":
                drawerUI.TogglePenStatus(PenUI.PenType.Cloud, true);
                Drawer.OnPenSelect.Invoke(PenUI.PenType.Cloud);
                Drawer.penStatus[1] = true;
                color = Drawer.Instance.FindPenProperty(PenUI.PenType.Cloud).material.color;
                type= PenProperty.PenType.Cloud;
                break;
            case "Steel":
                drawerUI.TogglePenStatus(PenUI.PenType.Steel, true);
                Drawer.OnPenSelect.Invoke(PenUI.PenType.Steel);
                Drawer.penStatus[2] = true;
                color = Drawer.Instance.FindPenProperty(PenUI.PenType.Steel).material.color;
                type = PenProperty.PenType.Steel;
                break;
            case "Electric":
                drawerUI.TogglePenStatus(PenUI.PenType.Electric, true);
                Drawer.OnPenSelect.Invoke(PenUI.PenType.Electric);
                Drawer.penStatus[3] = true;
                color = Drawer.Instance.FindPenProperty(PenUI.PenType.Electric).material.color;
                type = PenProperty.PenType.Electric;
                break;
            default:
                Debug.LogWarning(penType + "Unknown type");
                break;
        }

        Drawer.Instance.ChangeSliderColor(color.r, color.g, color.b, (int)type);
        Drawer.Instance.UpdateSlider(1f);

    }

    private static bool IsLocalDrawer()
    {
        if (PhotonNetwork.OfflineMode || (Allan.GameManager.Instance != null && Allan.GameManager.Instance.devSpawn))
            return true;

        return PhotonNetwork.LocalPlayer != null &&
               PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Role", out object roleValue) &&
               (RolesManager.PlayerRole)(int)roleValue == RolesManager.PlayerRole.Drawer;
    }
}
