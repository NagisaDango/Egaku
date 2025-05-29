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

            //go.GetComponent<Button>().onClick.AddListener(() => { GameManager.Instance.LoadLevel(display.levelIndex);  });
            //go.GetComponent<Button>().onClick.AddListener(() => { GameManager.Instance.photonView.RPC("RPC_LoadLevel", RpcTarget.AllBuffered, display.levelIndex); });
            go.GetComponent<Button>().onClick.AddListener(() => { photonView.RPC("RPC_LoadLevel", RpcTarget.AllBuffered, display.levelIndex); });
            go.GetComponent<Button>().onClick.AddListener(() => { photonView.RPC("RPC_PlayGameBGM", RpcTarget.AllBuffered); });
        }


    }


    [PunRPC]
    public void RPC_LoadLevel(int level)
    {
        GameManager.Instance.LoadLevel(level);
    }

    [PunRPC]
    public void RPC_PlayGameBGM()
    {
        AudioManager.PlayBGM(AudioManager.GAMEBGM);
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
