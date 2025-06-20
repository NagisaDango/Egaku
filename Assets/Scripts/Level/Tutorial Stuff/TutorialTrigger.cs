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
            destroyObj._OnDestroy += () => photonView.RPC("RPC_ValidateEventInvoke", RpcTarget.All, CustomTriggerType.ObserveItemDestroyed);
        
        if (PhotonNetwork.OfflineMode || GameManager.Instance.devSpawn)
        {
            if (notShowForSolo)
            {
                print("Destroying Tutorial not for solo");
                Destroy(this.gameObject);
            }
        }
        else if((designRole != RolesManager.PlayerRole.None && (RolesManager.PlayerRole)(int)PhotonNetwork.CurrentRoom.CustomProperties["Role_" + PhotonNetwork.LocalPlayer.ActorNumber] !=
                designRole) || onlyShowForSolo)
        {
            notShow = true;
            this.parent.SetActive(false);
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
        if(GameManager.Instance.devSpawn == true || PhotonNetwork.OfflineMode || designRole == RolesManager.PlayerRole.None)
        {
            InvokeEvent(triggerType);
        }
        else if (designRole != RolesManager.PlayerRole.None)
        {
            Debug.Log($"This trigger is for {designRole}, I am {(RolesManager.PlayerRole)(int)PhotonNetwork.CurrentRoom.CustomProperties["Role_" + PhotonNetwork.LocalPlayer.ActorNumber]}");
            if ((RolesManager.PlayerRole)(int)PhotonNetwork.CurrentRoom.CustomProperties["Role_" + PhotonNetwork.LocalPlayer.ActorNumber] ==
                designRole)
            {
                InvokeEvent(triggerType);
            }
        }
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
        if (triggered || notShow)
            return;
        triggered = true;
        
        photonView.RPC("RPC_ValidateEventInvoke", RpcTarget.All, CustomTriggerType.ColliderTrigger);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (((1 << collision.gameObject.layer) & layerMask) != 0 && !notShow)
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
