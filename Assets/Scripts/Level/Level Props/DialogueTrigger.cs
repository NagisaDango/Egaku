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
    private int index = 0;
    private bool displaying = false;
    // Public UnityEvent that appears in the Inspector
    public UnityEvent onCustomEvent;

    // Method to trigger the event
    public void TriggerEvent()
    {
        if (onCustomEvent != null)
            onCustomEvent.Invoke(); // Calls all assigned functions
    }
    
    [PunRPC]
    public void ShowDialogue()
    {
        if (!displaying)
        {
            displaying = true;
            textDisplay.transform.parent.gameObject.SetActive(true);
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
        Destroy(this.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            photonView.RPC("ShowDialogue", RpcTarget.AllBuffered);
            //ShowDialogue();
        }
    }
}