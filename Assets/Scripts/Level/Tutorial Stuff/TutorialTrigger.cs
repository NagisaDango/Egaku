using System;
using Allan;
using UnityEngine;
using DG.Tweening;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.Events;

public class TutorialTrigger : MonoBehaviourPun
{
    [SerializeField] private GameObject parent;
    [SerializeField] private TMP_Text tutorialText;
    private bool triggered = false;
    [SerializeField] private TutorialTrigger closeTrigger;
    [SerializeField] private DestroyObserve destroyObj;
    public UnityEvent onTrigger;
    public UnityEvent onObserveObjDestroyed;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private bool leaveDestroy;
    [SerializeField] private RolesManager.PlayerRole designRole;
    [SerializeField] private bool notShowForSolo = false;
    [SerializeField] private bool onlyShowForSolo = false;
    [SerializeField] private LoopType loopType;
    [SerializeField] private Ease easeType;
    private bool notShow;
    
    
    [Header("Animation")]
    [SerializeField] private GameObject controllingAnim;
    [SerializeField] private Transform startPos;
    [SerializeField] private Transform endPos;
    [SerializeField] private float animDuration;


    private void Start()
    {
        
        if(controllingAnim)
            controllingAnim.transform.localPosition = startPos.localPosition;
        if(closeTrigger)
            closeTrigger.onTrigger.AddListener(Close);
        if(destroyObj)
        {
            // DestroyAll and scene reload can destroy this trigger before the observed object.
            // Capture the view now and guard it instead of accessing this component's photonView later.
            PhotonView triggerView = photonView;
            destroyObj._OnDestroy += () =>
            {
                if (triggerView != null && PhotonNetwork.InRoom)
                    triggerView.RPC("RPC_ValidateEventInvoke", RpcTarget.All, CustomTriggerType.ObserveItemDestroyed);
            };
        }
        
        if (PhotonNetwork.OfflineMode || GameManager.Instance.devSpawn)
        {
            if (notShowForSolo)
            {
                print("Destroying Tutorial not for solo");
                notShow = true;
                Destroy(this.gameObject);
            }
        }
        else if ((designRole != RolesManager.PlayerRole.None &&
                  TryGetLocalRole(out RolesManager.PlayerRole localRole) && localRole != designRole) || onlyShowForSolo)
        {
            notShow = true;
            if (parent != null)
                parent.SetActive(false);
        }
    }

    public void MoveGO()
    {
        if(controllingAnim == null)
            return;
        
        DOTween.To(
            () => controllingAnim.transform.localPosition,
            pos => controllingAnim.transform.localPosition = pos,
            endPos.localPosition,
            animDuration
        ).SetEase(easeType)
        .SetLoops(-1, loopType);
    }
    
    [PunRPC]
    private void RPC_ValidateEventInvoke(CustomTriggerType triggerType)
    {
        if (notShow)
            return;
        
        if(GameManager.Instance.devSpawn == true || PhotonNetwork.OfflineMode || designRole == RolesManager.PlayerRole.None)
        {
            InvokeEvent(triggerType);
        }
        else if (designRole != RolesManager.PlayerRole.None && TryGetLocalRole(out RolesManager.PlayerRole localRole))
        {
            if (localRole == designRole)
            {
                InvokeEvent(triggerType);
            }
        }
    }

    /// <summary>Room role properties can be absent during scene handoff; player properties are the fallback.</summary>
    private static bool TryGetLocalRole(out RolesManager.PlayerRole role)
    {
        role = RolesManager.PlayerRole.None;
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return false;

        object roleValue = null;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Role", out roleValue) &&
            roleValue is int playerRole && Enum.IsDefined(typeof(RolesManager.PlayerRole), playerRole))
        {
            role = (RolesManager.PlayerRole)playerRole;
            if (role != RolesManager.PlayerRole.None)
                return true;
        }

        string roomRoleKey = "Role_" + PhotonNetwork.LocalPlayer.ActorNumber;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(roomRoleKey, out roleValue) &&
            roleValue is int roomRole && Enum.IsDefined(typeof(RolesManager.PlayerRole), roomRole))
        {
            role = (RolesManager.PlayerRole)roomRole;
            return role != RolesManager.PlayerRole.None;
        }

        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PhotonSessionPolicy.DrawerOwnerKey, out roleValue) &&
            roleValue is int drawerOwner && drawerOwner == actorNumber)
        {
            role = RolesManager.PlayerRole.Drawer;
            return true;
        }
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PhotonSessionPolicy.RunnerOwnerKey, out roleValue) &&
            roleValue is int runnerOwner && runnerOwner == actorNumber)
        {
            role = RolesManager.PlayerRole.Runner;
            return true;
        }

        return false;
    }

    private void InvokeEvent(CustomTriggerType triggerType)
    {            
        switch (triggerType)
        {
            case CustomTriggerType.ColliderTrigger:
                onTrigger?.Invoke();
                if (parent != null)
                    parent.SetActive(true);
                break;
            case CustomTriggerType.ObserveItemDestroyed:
                onObserveObjDestroyed?.Invoke();
                break;
        }
    }
    
    private enum CustomTriggerType
    {
        ColliderTrigger,
        ObserveItemDestroyed
    }

    private void TriggerEvent()
    {
        if (triggered)
            return;
        triggered = true;
        
        photonView.RPC("RPC_ValidateEventInvoke", RpcTarget.All, CustomTriggerType.ColliderTrigger);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (((1 << collision.gameObject.layer) & layerMask) != 0)
        {
            Debug.Log(this.name+ " even6t triggered");
            TriggerEvent();
        }
    }

    /*
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (leaveDestroy && ((1 << collision.gameObject.layer) & layerMask) != 0 && triggered)
        {
            Debug.Log(collision.gameObject.transform.position);
            Debug.Log("Destroying this dialogue trigger: " + this.gameObject.name + "name " + collision.gameObject.name);
            if (PhotonNetwork.IsMasterClient || photonView.IsMine)
            {
                PhotonNetwork.Destroy(this.gameObject);
            }
            else
            {
                photonView.RPC("RPC_Destroy", RpcTarget.MasterClient, photonView.ViewID);
            }
        }
    }
    */

    private void Close()
    {
        this.gameObject.SetActive(false);
    }
    
    public void SetText(string text)
    {
        tutorialText.text = text;
    }

    public void DestroyObj(GameObject obj)
    {
        if (PhotonNetwork.IsMasterClient || obj.GetPhotonView().IsMine)
        {
            PhotonNetwork.Destroy(obj);
        }
        else
        {
            photonView.RPC("RPC_Destroy", RpcTarget.MasterClient, obj.GetPhotonView().ViewID);
        }
    }

    [PunRPC]
    private void RPC_Destroy(int objID)
    {
        PhotonNetwork.Destroy(PhotonView.Find(objID));
    }

    public void EnableObj(GameObject obj)
    {
        obj.SetActive(true);
    }
}
