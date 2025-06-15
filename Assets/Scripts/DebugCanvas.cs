using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Allan;

public class DebugCanvas : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created


    public static DebugCanvas Instance;

    public UnityEngine.UI.Toggle isConnected;
    public UnityEngine.UI.Toggle inLobbyToggle;
    public UnityEngine.UI.Toggle inRoomToggle;
    public TMP_Text levelIndexText;



    private void Awake()
    {
        if (Instance != null && Instance != this)
        {

            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        isConnected.isOn = PhotonNetwork.IsConnected;
        inLobbyToggle.isOn = PhotonNetwork.InLobby;
        inRoomToggle.isOn = PhotonNetwork.InRoom;
        if (PhotonNetwork.InRoom)
        {
            Text t = inRoomToggle.GetComponentInChildren<Text>();
            string t0 = "NA";
            string t1 = "NA";

            if (PhotonNetwork.CurrentRoom.Players!= null)
            {
                t0 = PhotonNetwork.CurrentRoom.Players.ContainsKey(0) ? PhotonNetwork.CurrentRoom.Players[0].NickName : "none";
                t1 = PhotonNetwork.CurrentRoom.Players.ContainsKey(1) ? PhotonNetwork.CurrentRoom.Players[1].NickName : "none";

            }

            t.text = "In Room: " + PhotonNetwork.CurrentRoom.Name + " , " + t0  + " , " + t1;
        }

        if (GameManager.Instance)
        {
            levelIndexText.text = "current level: " + GameManager.Instance.currentLevel.ToString();
        }


    }

    public void SkipLevel()
    {
        EventHandler.CallReachDestinationEvent();
    }
}
