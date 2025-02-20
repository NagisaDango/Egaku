using System;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawerUICOntrol : MonoBehaviour
{
    [SerializeField] RectTransform drawerPanel;
    public bool panelStatus;
    private void Start()
    {
        OpenDrawerPanel();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left-click detection
        {
            if (!IsClickInsidePanel(drawerPanel))
            {
                CloseDrawerPanel();
            }
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleDrawerPanel();
        }
    }
    
    private bool IsClickInsidePanel(RectTransform panel)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition, null);
    }

    public void ToggleDrawerPanel()
    {
        if(panelStatus) CloseDrawerPanel();
        else OpenDrawerPanel();
    }
    
    private void OpenDrawerPanel()
    {
        DOTween.To(
            () => drawerPanel.anchoredPosition,
            pos => drawerPanel.anchoredPosition = pos,
            new Vector2(0, drawerPanel.anchoredPosition.y),
            0.5f
        ).SetEase(Ease.OutQuad);
        panelStatus = true;
    }

    private void CloseDrawerPanel()
    {
        DOTween.To(
            () => drawerPanel.anchoredPosition,
            pos => drawerPanel.anchoredPosition = pos,
            new Vector2(-500, drawerPanel.anchoredPosition.y),
            0.5f
        ).SetEase(Ease.OutQuad);
        panelStatus = false;
    }
}
