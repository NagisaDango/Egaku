using UnityEngine;

public class WoodPen : MonoBehaviour, HoldableObject
{
    private Collider2D col;
    private Rigidbody2D rb;
    public Runner holder;

    private void Awake()
    {
        // Drawn wood receives this component at stroke completion; cache immediately
        // so a grab arriving that same frame does not race Start.
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    public void Reset()
    {
        // Unity also calls Reset when this component is authored in the Editor.
        // That must never detach or otherwise modify an authored scene object.
        if (!Application.isPlaying || rb == null) return;
        if (holder != null) holder.HoldingObjLost();
        holder = null;
        gameObject.tag = "Wood";
        // Runner restores the exact pre-grab mass, including authored scene wood.
        transform.SetParent(null, true);
        ToggleRbSimulated();
    }

    private void OnDestroy()
    {
        if (holder != null) holder.HoldingObjLost();
    }

    public bool ValidateHold()
    {
        // Only a live external contact provides support. The old saved-point raycast
        // could hit this same board underfoot and allow a mid-air jump.
        return holder != null && GrabPhysics.HasExternalSupport(rb, holder.GetComponent<Rigidbody2D>());
    }

    public void ToggleCollider(bool status)
    {
        if (col != null) col.enabled = status;
    }

    public void ToggleRbSimulated(bool status)
    {
        if (rb != null)
            rb.simulated = status && TransferToRunner.IsRunnerAuthority(GetComponent<Photon.Pun.PhotonView>());
    }

    public void ToggleRbSimulated()
    {
        // Scene props can start with transfer pending. Finished drawn wood and scene
        // wood both resume only on the Runner, including after electric-wire travel.
        ToggleRbSimulated(true);
    }
}
