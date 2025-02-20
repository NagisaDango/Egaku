using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawerUICOntrol : MonoBehaviour
{
    [SerializeField] RectTransform drawerPanel;

    private void Start()
    {
        OpenDrawerPanel();
    }

    private void OpenDrawerPanel()
    {
        DOTween.To(
            () => drawerPanel.anchoredPosition,
            pos => drawerPanel.anchoredPosition = pos,
            new Vector2(0, drawerPanel.anchoredPosition.y),
            0.5f
        ).SetEase(Ease.OutQuad);
    }

    private void CloseDrawerPanel()
    {
        DOTween.To(
            () => drawerPanel.anchoredPosition,
            pos => drawerPanel.anchoredPosition = pos,
            new Vector2(-500, drawerPanel.anchoredPosition.y),
            0.5f
        ).SetEase(Ease.OutQuad);
    }
}
