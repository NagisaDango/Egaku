using Allan;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NUnit.Framework;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
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


        for (int i = 0; i < levelCount; i++)
        {
            GameObject go = Instantiate(levelDisplayPrefab, grid);
            go.name = "LevelDisplay_" + i;
            go.GetComponentInChildren<TMP_Text>().text = "Level " + i;

            levelDisplays.Add(go);
            
            if (i >= levelUnlocked)
            {
                go.transform.Find("Image").GetComponent<Image>().color = Color.grey;
            }
            //go.GetComponent<Button>().onClick.AddListener(GameManager.Instance.DevSpawnPlayers);
            print(i);

            LevelDisplay display =  go.GetComponent<LevelDisplay>();

            display.levelIndex = i;

            go.GetComponent<Button>().onClick.AddListener(() => { GameManager.Instance.LoadLevel(display.levelIndex);  });

        }


    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
