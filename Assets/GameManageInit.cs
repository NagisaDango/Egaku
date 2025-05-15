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
    public Button startGaemButton;
    public Button leaveGameButton;


    public GameObject roomSelection;
    public GameObject roleSelection;
    public Transform gridLayout;
    public TMP_InputField nameField;


    void Start()
    {
        GameManager go = null;
        if (!GameManager.initialized)
        {

            Debug.Log(gameManagerPrefab.name);

            GameObject g = Instantiate(gameManagerPrefab, Vector3.zero, Quaternion.identity).gameObject;
            go = g.GetComponent<GameManager>();
            Debug.Log($"{go.name} has been instantiated");
            GameManager.initialized = true;
            //go.GetComponent<PhotonView>().
        }
        else
        {
            go = GameManager.Instance;
        }

        roomCreateOrJoinButton.onClick.AddListener(() => { go.CreateJoinButton(); });
        startGaemButton.onClick.AddListener(() => { go.DevSpawnPlayers(); });
        go.UpdateProperty(roomSelection, roleSelection, gridLayout, nameField);
        leaveGameButton.onClick.AddListener(() => { go.LeaveRoom(); });

        roomSelection.SetActive(true);
        roleSelection.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
