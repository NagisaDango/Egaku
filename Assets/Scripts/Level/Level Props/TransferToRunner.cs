using Photon.Pun;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class TransferToRunner : MonoBehaviourPun
{
    // Scene props start under the Master, but gameplay physics belongs to the Runner.
    // Only the current legal controller transfers them; the other copy stays passive.
    private Rigidbody2D body;
    private bool originallySimulated;
    private bool wasAuthority;
    private int requestedActor;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        originallySimulated = body != null && body.simulated;
        if (body != null) body.simulated = false;
    }

    public static bool IsRunnerAuthority(PhotonView view)
    {
        Runner runner = Runner.Instance;
        return view != null && view.IsMine && runner != null && runner.photonView.IsMine &&
            (PhotonNetwork.OfflineMode || (PhotonNetwork.LocalPlayer != null &&
             runner.actorNum == PhotonNetwork.LocalPlayer.ActorNumber));
    }

    private void Update()
    {
        if (body == null) return;
        Runner runner = Runner.Instance;
        if (runner != null && runner.actorNum != 0 && photonView.IsMine &&
            photonView.OwnerActorNr != runner.actorNum && requestedActor != runner.actorNum &&
            !PhotonNetwork.OfflineMode)
        {
            requestedActor = runner.actorNum;
            photonView.TransferOwnership(runner.actorNum);
        }
        bool authority = IsRunnerAuthority(photonView);
        // Change simulation only on authority transitions. Per-frame enabling would
        // undo the Runner's deliberate suspension during electric-wire travel.
        if (authority != wasAuthority)
        {
            body.simulated = authority && originallySimulated;
            wasAuthority = authority;
        }
    }
}
