using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;

public class RotateMode : MonoBehaviour, IMode, IDragHandler, IBeginDragHandler
{
    private SpriteShapeRenderer spriteRenderer;
    private bool active = false;
    private float initialAngle;
    private Vector3 initialMousePosition;

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
        Vector3 objectPosition = transform.position;
        Vector2 initialDirection = initialMousePosition - objectPosition;
        Vector2 currentDirection = mousePosition - objectPosition;

        float angleDifference = Vector2.SignedAngle(initialDirection, currentDirection);
        transform.rotation = Quaternion.Euler(0, 0, initialAngle + angleDifference);
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
        }
    }
}