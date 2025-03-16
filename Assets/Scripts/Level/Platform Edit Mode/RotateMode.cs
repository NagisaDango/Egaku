using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;

public class RotateMode : MonoBehaviour, IMode, IDragHandler, IBeginDragHandler
{
    private SpriteShapeRenderer spriteRenderer;
    private bool active = false;
    private float initialAngle;
    private Vector3 initialMousePosition;
    private Vector3 lastMousePosition;

    public void OnBeginDrag(PointerEventData eventData)
    {
        Vector2 worldMousePos = GetWorldMousePosition(eventData);
        initialMousePosition = worldMousePos;
        initialAngle = transform.eulerAngles.z;
    }
    public void OnDrawGizmosSelected()
    {
        var r = GetComponent<Renderer>();
        if (r == null)
            return;
        var bounds = r.bounds;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(bounds.center, bounds.extents * 2);
    }
    
    private Vector2 GetWorldMousePosition(PointerEventData eventData)
    {
        Vector2 screenPosition = eventData.position;
        //screenPosition.z = Mathf.Abs(Camera.main.transform.position.z - transform.position.z);
        return Camera.main.ScreenToWorldPoint(screenPosition);
    }

    private void RotateObject(Vector3 mousePosition)
    {
        var r = GetComponent<Renderer>();
        if (r == null) return;

        Vector3 center = r.bounds.center;
        Vector3 mouseDelta = mousePosition - lastMousePosition;

        float rotationSpeed = 5f;
        transform.RotateAround(center, Vector3.forward, mouseDelta.magnitude * rotationSpeed);
    }

    public void Initialize()
    {
        if(spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteShapeRenderer>();
        print(spriteRenderer.bounds.center);
        active = true;
    }

    public void ModeUpdate()
    {
        //throw new System.NotImplementedException();
    }

    public void Dispose()
    {
        active = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (active)
        {
            Vector3 worldMousePos = GetWorldMousePosition(eventData);
            RotateObject(worldMousePos);
            lastMousePosition = worldMousePos;
        }
    }
}