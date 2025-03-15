using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.UI;

public class PenUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [SerializeField] private DrawerUICOntrol drawerUI;
    [SerializeField] private Texture2D cursorTexture;
    [SerializeField] private Transform penHolder;
    [SerializeField] public Image penImage;
    public PenType penType;
    public enum PenType
    {
      Wood,
      Cloud,
      Electric,
      Steel,
      Eraser
    };

    
    public void OnPointerDown(PointerEventData eventData)
    {
        //if panel is open then do this
        if(drawerUI.panelStatus)
        {
            drawerUI.ToggleDrawerPanel();
            Drawer.OnPenSelect.Invoke(penType);
            Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        DOTween.To(
            () => this.transform.localScale,
            pos => this.transform.localScale = pos,
            new Vector3(1.1f, 1.1f, 1.1f),
            0.25f
        ).SetEase(Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        DOTween.To(
            () => this.transform.localScale,
            pos => this.transform.localScale = pos,
            new Vector3(1.0f, 1.0f, 1.0f),
            0.25f
        ).SetEase(Ease.OutQuad);
    }
}
