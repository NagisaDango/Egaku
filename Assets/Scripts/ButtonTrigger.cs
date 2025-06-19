using System;
using System.Collections.Generic;
using DG.Tweening;
using Photon.Pun;
using UnityEngine;

public class ButtonTrigger : MonoBehaviourPun
{
    private bool pressed = false;
    public List<ButtonTrigger> otherRequire;
    [SerializeField] public GameObject controlling;
    public LayerMask layerMask;
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        //TODO: not working for multiple layer now
        if((layerMask == (layerMask | (1 << collision.gameObject.layer)))) //|| (collision.gameObject.CompareTag("Player") && playerCanPressed))
        {
            photonView.RPC("RPC_VisualChange", RpcTarget.AllBuffered);
        }
    }

    [PunRPC]
    private void RPC_VisualChange()
    {            
        SpriteRenderer sr = this.GetComponent<SpriteRenderer>();
        DOTween.To(
            () => sr.color,
            color => sr.color = color,
            Color.forestGreen,
            0.2f
        ).SetEase(Ease.OutQuad);
        pressed = true;
        if (otherRequire.Count > 0)
        {
            foreach (ButtonTrigger bt in otherRequire)
            {
                if (bt.pressed == false)
                    return;
            }
        }
        controlling.GetComponent<IControlObj>().Activate();
    }
}
