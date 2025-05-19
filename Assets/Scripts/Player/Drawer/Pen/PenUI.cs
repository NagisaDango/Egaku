using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;

public class PenUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerMoveHandler
{
    [SerializeField] private DrawerUICOntrol drawerUI;
    [SerializeField] private Texture2D cursorTexture;
    [SerializeField] private Transform penHolder;
    [SerializeField] public Image penImage;
    [SerializeField] private string description;
    public PenType penType;
    private IPointerMoveHandler _pointerMoveHandlerImplementation;
    private PenProperty thisPenProperty;
    
    [Header("Detail page")]
    private static GameObject detailPanel;
    private static RectTransform pageTransform;
    private static TMP_Text detail_displayText;
    private float currentOffset;
    public enum PenType
    {
      Wood,
      Cloud,
      Electric,
      Steel,
      Eraser
    };

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (detail_displayText == null)
        {
            detailPanel = this.transform.parent.parent.Find("Description").gameObject;
            detail_displayText = detailPanel.transform.GetChild(0).GetComponent<TMP_Text>();
            pageTransform = detailPanel.GetComponent<RectTransform>();
        }

        switch (penType)
        {
            case PenType.Wood:
                thisPenProperty = Drawer.Instance.woodPen;
                break;
            case PenType.Cloud:
                thisPenProperty = Drawer.Instance.cloudPen;
                break;
            case PenType.Electric:
                thisPenProperty = Drawer.Instance.electricPen;
                break;
            case PenType.Steel:
                thisPenProperty = Drawer.Instance.steelPen;
                break;
            case PenType.Eraser:
                break;
            default:
                break;
        }
    }
    
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
        ShowDescription();
        DOTween.To(
            () => this.transform.localScale,
            pos => this.transform.localScale = pos,
            new Vector3(1.1f, 1.1f, 1.1f),
            0.25f
        ).SetEase(Ease.OutQuad);
    }
    
    

    public void OnPointerExit(PointerEventData eventData)
    {
        currentOffset = 0;
        NoShowDescription();
        DOTween.To(
            () => this.transform.localScale,
            pos => this.transform.localScale = pos,
            new Vector3(1.0f, 1.0f, 1.0f),
            0.25f
        ).SetEase(Ease.OutQuad);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        //a bit offset to prevent raycast block
        float offset = GetPanelOffset();
        if (currentOffset > offset + currentOffset)
        {
            currentOffset = offset + currentOffset;
        }
        detailPanel.transform.position = eventData.position + new Vector2(60, - currentOffset);
    }

    private void ShowDescription()
    {
        detailPanel.SetActive(true);
        detail_displayText.text = description;

    }
    
    private void NoShowDescription()
    {
        detailPanel.SetActive(false);
        detail_displayText.text = "";
    }

    private float GetPanelOffset()
    {
        Vector3[] corners = new Vector3[4];
        pageTransform.GetWorldCorners(corners);
        for (int i = 0; i < corners.Length; i++)
        {
            //print("Corner: " + corners[i] + " " + i);
            //print("screen bot: " + Screen.height);
        }

        if (corners[0].y < 0)
        {
            return corners[0].y;
        }
        return 0;
    }

}
