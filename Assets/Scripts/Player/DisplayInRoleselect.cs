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
    private bool boundAsLocalUi;

    private void Start()
    {
        // OnlineRoleSelectionView binds a non-networked visual clone directly from
        // Photon player properties, so its legacy buffered-RPC initialization must not run.
        if (boundAsLocalUi) return;
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

    public void BindPlayer(Photon.Realtime.Player player)
    {
        if (player == null) return;
        boundAsLocalUi = true;
        Transform playerName = transform.Find("PlayerName");
        if (playerName != null && playerName.TryGetComponent(out TMP_Text nameLabel))
            nameLabel.text = player.IsInactive ? player.NickName + " (Reconnecting...)" : player.NickName;

        int eyeType = ReadInt(player, "Eyes", 0);
        int mouthType = ReadInt(player, "Mouth", 0);
        Vector3 color = player.CustomProperties.TryGetValue("Color", out object colorValue) && colorValue is Vector3 value
            ? value : Vector3.one;
        ApplyAppearance(eyeType, mouthType, color);
    }

    private static int ReadInt(Photon.Realtime.Player player, string key, int fallback)
    {
        return player.CustomProperties.TryGetValue(key, out object value) && value is int number ? number : fallback;
    }
    
    [PunRPC]
    private void SetUpAppearance(int eyeType, int mouthType, Vector3 color)
    {
        ApplyAppearance(eyeType, mouthType, color);
    }

    private void ApplyAppearance(int eyeType, int mouthType, Vector3 color)
    {
        leftEye.sprite = Resources.Load<Sprite>("Eyes/" + eyeType);
        rightEye.sprite = Resources.Load<Sprite>("Eyes/" + eyeType);
        mouth.sprite = Resources.Load<Sprite>("Mouth/" + mouthType);
        body.color = new Color(color.x, color.y, color.z);
    }
}
