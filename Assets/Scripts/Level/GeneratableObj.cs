using UnityEngine;
using UnityEngine.EventSystems;

public class GeneratableObj : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        LevelEditor.instance.SendMessage("SpawnNewObj", this.name);
    }
}
