using Photon.Pun;
using UnityEngine;

public class TransferToRunner : MonoBehaviourPun
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        photonView.OwnershipTransfer = OwnershipOption.Takeover;
        photonView.TransferOwnership(Runner.Instance.actorNum);
    }
}
