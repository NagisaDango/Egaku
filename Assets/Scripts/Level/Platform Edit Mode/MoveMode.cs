using UnityEngine;
using UnityEngine.EventSystems;

public class MoveMode : MonoBehaviour, IMode, IDragHandler
{
    private bool active;
    public void Initialize()
    {
        active = true;
    }

    public void ModeUpdate()
    {
    }

    public void Dispose()
    {
        active = false;
    }
    
    
    public void OnDrag(PointerEventData eventData)
    {
        if (active)
        {
            this.transform.position += (Vector3)eventData.delta * 0.01f;
        }
    }
}
