using Allan;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManageInit : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public GameObject gameManagerPrefab;
    public Button roomCreateOrJoinButton;
    public Button startGameButton;
    public Button devStartGameButton;

    public Button leaveGameButton;


    public GameObject roomSelection;
    public GameObject roleSelection;
    public GameObject levelSelection;

    public Transform gridLayout;
    public TMP_InputField nameField;


    void Awake()
    {
        GameManager go = null;
        if (!GameManager.initialized)
        {

            go = Instantiate(gameManagerPrefab, Vector3.zero, Quaternion.identity).GetComponent<GameManager>();
            Debug.Log($"{go.name} has been instantiated");
            GameManager.initialized = true;
            //go.GetComponent<PhotonView>().
        }
        else
        {
            go = GameManager.Instance;
        }

        roomCreateOrJoinButton.onClick.AddListener(() => { go.CreateJoinButton(); });
        startGameButton.onClick.AddListener(() => { go.LoadLevelSelection(); });
        devStartGameButton.onClick.AddListener(() => { go.DevSpawnPlayers(); });

        go.UpdateProperty(roomSelection, roleSelection, levelSelection, gridLayout, nameField);
        leaveGameButton.onClick.AddListener(() => { go.LeaveRoom(); });

        roomSelection.SetActive(true);
        roleSelection.SetActive(false);
        levelSelection.SetActive(false);
        //PhotonNetwork.AllocateSceneViewID(go.photonView);

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
