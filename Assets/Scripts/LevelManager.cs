using Allan;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NUnit.Framework;
using System.Collections.Generic;
using Photon.Pun;

public class LevelManager : MonoBehaviourPunCallbacks
{
    public Transform grid;
    public GameObject levelDisplayPrefab;
    public List<GameObject> levelDisplays;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        print(GameManager.Instance.levelCounts);

        int levelCount = GameManager.Instance.levelCounts;
        int levelUnlocked = GameManager.Instance.levelUnlocked;


        for (int i = 1; i < levelCount; i++)
        {
            GameObject go = Instantiate(levelDisplayPrefab, grid);
            go.name = "LevelDisplay_" + i;
            go.GetComponentInChildren<TMP_Text>().text = "Level " + i;
            go.transform.Find("Image").GetComponent<Image>().sprite = Resources.Load<Sprite>("LevelSS/" + i);
            levelDisplays.Add(go);
            
            if (i >= levelUnlocked)
            {
                go.transform.Find("Image").GetComponent<Image>().color = Color.grey;
            }
            //go.GetComponent<Button>().onClick.AddListener(GameManager.Instance.DevSpawnPlayers);
            print(i);

            LevelDisplay display =  go.GetComponent<LevelDisplay>();

            display.levelIndex = i;

            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom)
                    RPC_LoadLevel(display.levelIndex);
                else
                    photonView.RPC(nameof(RPC_LoadLevel), RpcTarget.AllViaServer, display.levelIndex);
            });
        }


    }


    [PunRPC]
    public void RPC_LoadLevel(int level)
    {
        AudioManager.PlayBGM(AudioManager.GAMEBGM);
        if (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient)
            GameManager.Instance.LoadLevel(level);
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
