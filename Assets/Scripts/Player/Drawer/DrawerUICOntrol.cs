using System;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DrawerUICOntrol : MonoBehaviour
{
    [SerializeField] RectTransform drawerPanel;
    Dictionary<PenUI.PenType, PenUI> penUI = new Dictionary<PenUI.PenType, PenUI>();
    [SerializeField] private Transform penHolder;
    public bool panelStatus;

    [SerializeField] private Button unLockBtn;


    private void Awake()
    {
        //OpenDrawerPanel();
        InitDictionary();
        CloseDrawerPanel();
    }

    private void InitDictionary()
    {
        penUI[PenUI.PenType.Wood] = penHolder.Find("Wood").GetComponent<PenUI>();
        penUI[PenUI.PenType.Cloud] = penHolder.Find("Cloud").GetComponent<PenUI>();
        penUI[PenUI.PenType.Electric] = penHolder.Find("Electric").GetComponent<PenUI>();
        penUI[PenUI.PenType.Steel] = penHolder.Find("Steel").GetComponent<PenUI>();
    }
    
    public void TogglePenStatus(PenUI.PenType penType, bool status)
    {
        if (status == true)
        {
            penUI[penType].penImage.enabled = true;
        }
        else
        {
            penUI[penType].penImage.enabled = false;
        }
    }

    public void UnlockAllPen()
    {
        TogglePenStatus(PenUI.PenType.Wood, true);
        TogglePenStatus(PenUI.PenType.Cloud, true);
        TogglePenStatus(PenUI.PenType.Steel, true);
        TogglePenStatus(PenUI.PenType.Electric, true);

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
