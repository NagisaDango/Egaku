using Allan;
using Photon.Pun;
using UnityEngine;

public class DestinationCheck : MonoBehaviour
{
    //[SerializeField] BoxCollider2D col;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        print("Enter Final Trigger");
        if (collision.tag == "Player")
        {
            print("Enter Final");

            //gameObject.GetComponent<PhotonView>().RPC("RPC_LoadLevel", RpcTarget.AllBuffered, GameManager.Instance.currentLevel);
            EventHandler.CallReachDestinationEvent();

        }
    }

}
