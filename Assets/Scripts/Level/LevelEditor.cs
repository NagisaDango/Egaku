using UnityEngine;
using UnityEngine.Events;
using UnityEngine.U2D;
using UnityEngine.EventSystems;

public class LevelEditor : MonoBehaviour
{
    public static LevelEditor instance;
    private Transform spawningObject;
    public GameObject test;
    SpriteShapeController controller;
    private EditableObj editingObj;
    private EditMode currentEditMode;
    public enum EditMode
    {
        Position,
        Rotation,
        Edit
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void Update()
    {
        if (spawningObject != null)
            spawningObject.position = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (Input.GetMouseButtonDown(0)) // Left mouse click
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Collider2D hit = Physics2D.OverlapPoint(mousePos);
            if (hit == null)
            {
                Debug.Log("Clicked on empty space!");
                Deselect();
            }
        }

        if (editingObj)
        {
            editingObj.ChangeMode();
            ActionOnObject();
        }
    }

    private void ActionOnObject()
    {
        switch (currentEditMode)
        {
            case EditMode.Edit:
                //editingObj.EditModeAction();
                break;
            case EditMode.Position:
                break;
            case EditMode.Rotation:
                break;
            default:
                break;
        }
    }

    private void Deselect()
    {
        if(editingObj != null)
            editingObj.BackToDefault();
    }
    
    public void SpawnNewObj(string objName)
    {
        Debug.Log($"Spawning {objName}");
        spawningObject = Instantiate(test).transform;
    }

    public void SetCurrentEditMode(EditMode mode)
    {
        currentEditMode = mode;
    }
    
    public void SetEditableObj(EditableObj editableObj)
    {
        if(editingObj != null && editingObj != editableObj)
            editingObj.BackToDefault();
        editingObj = editableObj;
        editableObj.ChangeModeByArg(currentEditMode);
    }
}
