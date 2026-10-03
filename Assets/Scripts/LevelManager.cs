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

        LevelCatalog catalog = LevelCatalog.Load();
        if (catalog == null) { Debug.LogError("LevelCatalog is missing.", this); return; }
        foreach (LevelCatalog.Definition definition in catalog.levels)
        {
            // Level_0 remains excluded from the current selection screen.
            if (definition.id == 0) continue;
            int i = definition.id;
            GameObject go = Instantiate(levelDisplayPrefab, grid);
            go.name = "LevelDisplay_" + i;
            go.GetComponentInChildren<TMP_Text>().text = "Level " + i;
            go.transform.Find("Image").GetComponent<Image>().sprite = definition.thumbnail;
            levelDisplays.Add(go);
            
            bool unlocked = catalog.IsUnlocked(i, GameManager.Instance.levelUnlocked);
            go.GetComponent<Button>().interactable = unlocked;
            if (!unlocked)
            {
                go.transform.Find("Image").GetComponent<Image>().color = Color.grey;
            }
            //go.GetComponent<Button>().onClick.AddListener(GameManager.Instance.DevSpawnPlayers);
            print(i);

            LevelDisplay display =  go.GetComponent<LevelDisplay>();

            display.levelIndex = i;

            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (!GameManager.Instance.CanStartSelectedMode()) return;
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
