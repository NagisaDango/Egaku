using System;
using Photon.Pun;
using UnityEngine;

public class BreakablePlatform : MonoBehaviourPun
{
    [SerializeField] private float breakThreshhold = 40f;

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Draw") && other.rigidbody.mass >= breakThreshhold)
        {
            PhotonNetwork.Destroy(this.gameObject);
        }
    }
}
