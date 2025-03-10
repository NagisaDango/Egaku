using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;

public class SplinePoint : MonoBehaviour, IDragHandler
{
    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log("Drag");
        this.transform.position = (Vector2)Camera.main.ScreenToWorldPoint(eventData.position);
        this.GetComponentInParent<SpriteShapeController>().spline.SetPosition(transform.GetSiblingIndex(), transform.localPosition);
    }
}
