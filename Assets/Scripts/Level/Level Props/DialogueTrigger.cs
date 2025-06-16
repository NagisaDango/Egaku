using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class DialogueTrigger : MonoBehaviourPun
{
    [SerializeField] private TMP_Text textDisplay;
    [SerializeField] private List<string> sentences;
    [SerializeField] private GameObject iconImage;
    public static LevelTip tipsGO;

    private int index = 0;
    private bool displaying = false;
    // Public UnityEvent that appears in the Inspector
    public UnityEvent onCustomEvent;

    public void RemoveIconImage()
    {
        if (iconImage != null)
        {
            photonView.RPC("RPC_RemoveIconImage", RpcTarget.All);
        }
    }
    
    [PunRPC]
    private void RPC_RemoveIconImage()
    {
        AudioManager.PlayOne(AudioManager.COLLECTSFX);
        iconImage.SetActive(false);
    }

    public static void InitBinding(LevelTip tip)
    {
        tipsGO = tip;
    }

    // Method to trigger the event
    public void TriggerEvent()
    {
        if (onCustomEvent != null)
            onCustomEvent.Invoke(); // Calls all assigned functions
    }
    
    [PunRPC]
    public void RPC_ShowDialogue()
    {
        if (!displaying)
        {
            displaying = true;
            textDisplay.transform.parent.gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(DisplaySentence());
        }
    }

    private IEnumerator DisplaySentence()
    {
        while (index < sentences.Count)
        {
            textDisplay.text = sentences[index];

            yield return new WaitForSeconds(2f);

            index++;
        }

        FinishDialogue();
    }

    private void FinishDialogue()
    {
        TriggerEvent();
        textDisplay.text = "";
        textDisplay.transform.parent.gameObject.SetActive(false);
        displaying = false;
        if(photonView.IsMine)
            PhotonNetwork.Destroy(this.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            string[] sentenceArray = sentences.ToArray();
            print(sentenceArray.GetType() + " typeeeeee");
            TriggerEvent();
            tipsGO.photonView.RPC("RPC_QueueDialogue", RpcTarget.AllBuffered, sentenceArray);
            Destroy(this.gameObject);
            //photonView.RPC("RPC_ShowDialogue", RpcTarget.All);
            //ShowDialogue();
        }
    }
}