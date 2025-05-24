using System;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class DisplayInRoleselect : MonoBehaviourPun
{
    [SerializeField] private Image leftEye;
    [SerializeField] private Image rightEye;
    [SerializeField] private Image mouth;
    [SerializeField] private Image body;

    private void Start()
    {
        if(photonView.IsMine)
            photonView.RPC("SetUpAppearance", RpcTarget.AllBuffered
                , PhotonNetwork.LocalPlayer.CustomProperties["Eyes"]
                , PhotonNetwork.LocalPlayer.CustomProperties["Mouth"]
                , PhotonNetwork.LocalPlayer.CustomProperties["Color"]);
        //SetUpAppearance();
    }
    
    [PunRPC]
    private void SetUpAppearance(int eyeType, int mouthType, Vector3 color)
    {
        if (!leftEye.sprite || !rightEye.sprite)
        {
            print("I am " + PhotonNetwork.NickName + " setting eye type to " + eyeType);
            leftEye.sprite = Resources.Load<Sprite>("Eyes/" + eyeType);
            rightEye.sprite = Resources.Load<Sprite>("Eyes/" + eyeType);
            mouth.sprite = Resources.Load<Sprite>("Mouth/" + mouthType);
            body.color = new Color(color.x, color.y, color.z);
        }
    }
}
