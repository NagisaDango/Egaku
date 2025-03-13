using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;

public class SplinePoint : MonoBehaviour, IDragHandler, IPointerClickHandler
{
    private PlatformEditMode associateObj;

    public void OnDrag(PointerEventData eventData)
    {
        this.transform.position = (Vector2)Camera.main.ScreenToWorldPoint(eventData.position);
        this.GetComponentInParent<SpriteShapeController>().spline.SetPosition(transform.GetSiblingIndex(), transform.localPosition);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Debug.Log(transform.GetSiblingIndex());
            associateObj.DeletePoint(transform.GetSiblingIndex());
        }
    }

    public void SetAssociateObj(PlatformEditMode obj)
    {
        associateObj = obj;
    }
}
