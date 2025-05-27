using System;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;

public class DisplayInRoleselect : MonoBehaviourPunCallbacks
{
    [SerializeField] private Image leftEye;
    [SerializeField] private Image rightEye;
    [SerializeField] private Image mouth;
    [SerializeField] private Image body;

    public bool isMine = true;
    public bool isFinal;

    private void Start()
    {
        if (isFinal)
        {
            TMP_Text name = transform.Find("PlayerName").GetComponent<TMP_Text>();
            if (isMine)
            {
                SetUpAppearance((int)PhotonNetwork.LocalPlayer.CustomProperties["Eyes"]
                    , (int)PhotonNetwork.LocalPlayer.CustomProperties["Mouth"]
                    , (Vector3)PhotonNetwork.LocalPlayer.CustomProperties["Color"]);
                name.text = PhotonNetwork.LocalPlayer.NickName;

            }
            else
            {
                SetUpAppearance((int)PhotonNetwork.PlayerListOthers[0].CustomProperties["Eyes"]
                    , (int)PhotonNetwork.PlayerListOthers[0].CustomProperties["Mouth"]
                    , (Vector3)PhotonNetwork.PlayerListOthers[0].CustomProperties["Color"]);
                name.text = PhotonNetwork.PlayerListOthers[0].NickName;

            }
        }
        else
        {
            if (photonView.IsMine)
                photonView.RPC("SetUpAppearance", RpcTarget.AllBuffered
                    , PhotonNetwork.LocalPlayer.CustomProperties["Eyes"]
                    , PhotonNetwork.LocalPlayer.CustomProperties["Mouth"]
                    , PhotonNetwork.LocalPlayer.CustomProperties["Color"]);
        }



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
