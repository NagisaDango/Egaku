using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;

[RequireComponent(typeof(SpriteShapeController))]
public class EditableObj : MonoBehaviour, IPointerDownHandler
{
    SpriteShapeController controller;
    public GameObject pointPrefab;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (this.transform.childCount > 0) return;

        GetComponent<Collider2D>().enabled = false;
        Debug.Log("Selecting: " + this.name);
        controller = this.GetComponent<SpriteShapeController>();
        for (int i = 0; i < controller.spline.GetPointCount(); i++)
        {
            GameObject point = Instantiate(pointPrefab, Vector3.zero, Quaternion.identity, this.transform);
            point.transform.localPosition = controller.spline.GetPosition(i);
        }
    }
}
