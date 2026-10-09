using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// Runner reports contact; Master accepts once, and the legal Photon controller removes the pickup.
[RequireComponent(typeof(BoxCollider2D), typeof(PhotonView))]
public class InkSupply : MonoBehaviourPun
{
    [SerializeField] private List<AddingInk> penInk = new List<AddingInk>();
    private bool requestPending;
    private bool awardCommitted;
    private bool consumed;

    private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

    // Retry an initial overlap until Drawer.Start has applied budgets. Once requested,
    // repeated contacts and multiple Runner colliders must not send per-frame RPCs.
    private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

    private void TryCollect(Collider2D other)
    {
        if (consumed || requestPending || !HasUsableInk() ||
            Allan.GameManager.InteractionsPausedForRecovery) return;
        Runner runner = other.GetComponentInParent<Runner>();
        if (runner == null || !runner.SceneReady || !runner.photonView.IsMine ||
            runner.gameObject.scene != gameObject.scene) return;
        if (!PhotonNetwork.InRoom)
        {
            ApplyInk();
            Destroy(gameObject);
            return;
        }
        if (photonView.ViewID == 0)
        {
            // Scene pickups require the same serialized View ID on both clients.
            Debug.LogError("InkSupply needs a configured PhotonView scene View ID.", this);
            requestPending = true;
            return;
        }
        requestPending = true;
        photonView.RPC(nameof(RPC_RequestCollect), RpcTarget.MasterClient, runner.photonView.ViewID);
    }

    [PunRPC]
    private void RPC_RequestCollect(int runnerViewId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || awardCommitted || consumed) return;
        PhotonView runnerView = PhotonView.Find(runnerViewId);
        Runner runner = runnerView != null ? runnerView.GetComponent<Runner>() : null;
        // Runner owns contact detection regardless of which role is Master.
        if (runner == null || info.Sender == null || runnerView.Owner != info.Sender ||
            runner.gameObject.scene != gameObject.scene || !HasUsableInk()) return;
        awardCommitted = true;
        photonView.RPC(nameof(RPC_ApplyCollect), RpcTarget.AllViaServer);
    }

    [PunRPC]
    private void RPC_ApplyCollect(PhotonMessageInfo info)
    {
        if (consumed || info.Sender == null || info.Sender != PhotonNetwork.MasterClient) return;
        ApplyInk();
        // Award first on each copy. The detecting Runner cannot destroy a Master-owned scene view.
        if (photonView.IsMine) PhotonNetwork.Destroy(gameObject);
    }

    private bool HasUsableInk()
    {
        if (Drawer.Instance == null || !Drawer.Instance.SceneReady ||
            Drawer.Instance.gameObject.scene != gameObject.scene || penInk == null) return false;
        LevelCatalog catalog = LevelCatalog.Load();
        LevelCatalog.Definition level = catalog != null ? catalog.FindScene(gameObject.scene.name) : null;
        foreach (AddingInk add in penInk)
        {
            if (add.amount <= 0 || add.type == PenProperty.PenType.Eraser) continue;
            // Only the owning Drawer initializes scene budgets in Start. The Runner's
            // remote Drawer copy can still have prefab defaults, so use catalog data
            // for shared eligibility instead of letting the Master role change the result.
            if (level == null)
            {
                if (Drawer.Instance.CanAddInkToPen(add.type, add.amount)) return true;
                continue;
            }
            int budget = add.type == PenProperty.PenType.Wood ? level.wood :
                add.type == PenProperty.PenType.Cloud ? level.cloud :
                add.type == PenProperty.PenType.Steel ? level.steel :
                add.type == PenProperty.PenType.Electric ? level.electric : -1;
            if (budget >= 0) return true;
        }
        return false;
    }

    private void ApplyInk()
    {
        if (consumed) return;
        consumed = true;
        if (Drawer.Instance != null && penInk != null)
            foreach (AddingInk add in penInk) Drawer.Instance.AddInkToPen(add.type, add.amount);
        foreach (Collider2D trigger in GetComponentsInChildren<Collider2D>()) trigger.enabled = false;
    }

    [Serializable]
    private struct AddingInk
    {
        public PenProperty.PenType type;
        [Min(1)] public int amount;
    }
}
