using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;

public class SplinePoint : MonoBehaviour, IDragHandler, IPointerClickHandler
{
    private EditableObj associateObj;

    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log("Drag");
        this.transform.position = (Vector2)Camera.main.ScreenToWorldPoint(eventData.position);
        this.GetComponentInParent<SpriteShapeController>().spline.SetPosition(transform.GetSiblingIndex(), transform.localPosition);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            associateObj.DeletePoint(transform.GetSiblingIndex());
        }
    }

    public void SetAssociateObj(EditableObj obj)
    {
        associateObj = obj;
    }
}
