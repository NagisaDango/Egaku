using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using DG.Tweening;

public class LevelTip : MonoBehaviourPun
{
    [SerializeField] private TMP_Text textDisplay;
    [SerializeField] private GameObject panel;
    private Queue<string> dialogueQueue = new Queue<string>();
    public bool courutineRunning { get; set; }
    //[SerializeField] private GameObject iconImage;

    private void Start()
    {
        DialogueTrigger.InitBinding(this);
    }
    
    [PunRPC]
    public void RPC_QueueDialogue(string[] dialogue)
    {
        foreach (string s in dialogue)
        {
            if(s == "")
                continue;
            dialogueQueue.Enqueue(s);
        }

        if (!courutineRunning)
        {
            DOTween.To(
                () => this.transform.localPosition,
                pos => this.transform.localPosition = pos,
                new Vector3(0, 0),
                0.5f
            ).SetEase(Ease.OutQuad);
            StartCoroutine(DisplaySentence());
        }
    }
    
    private IEnumerator DisplaySentence()
    {
        panel.SetActive(true);
        courutineRunning = true;
        while (dialogueQueue.Count > 0)
        {
            textDisplay.text = dialogueQueue.Dequeue();
            textDisplay.ForceMeshUpdate();
            print(textDisplay.textInfo.characterCount + " Text count");
            //TODO: Not working here, keep use the time from last time
            yield return new WaitForSeconds(textDisplay.textInfo.characterCount * 0.05f + 0.6f);
        }
        courutineRunning = false;
        FinishSentence();
    }
    
    private void FinishSentence()
    {
        DOTween.To(
                () => this.transform.localPosition,
                pos => this.transform.localPosition = pos,
                new Vector3(0, 500),
                0.5f
            ).SetEase(Ease.OutQuad)
            .OnComplete(() => {
                    panel.SetActive(false);
            });
        //textDisplay.text = "";
    }
}
