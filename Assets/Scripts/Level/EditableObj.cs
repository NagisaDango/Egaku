using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;

[RequireComponent(typeof(SpriteShapeController))]
public class EditableObj : MonoBehaviour, IPointerDownHandler
{
    private IMode currentMode;
    private PlatformEditMode editMode;
    private MoveMode moveMode;
    public GameObject pointPrefab;

    private void Start()
    {
        editMode = GetComponent<PlatformEditMode>();
        moveMode = GetComponent<MoveMode>();
    }

    private void Update()
    {
        if(currentMode != null)
            currentMode.ModeUpdate();
    }

    public void ChangeMode()
    {
        if (Input.GetKeyDown(KeyCode.Alpha3) && currentMode is not PlatformEditMode)
        {
            if(currentMode != null)
                currentMode.Dispose();
            currentMode = editMode;
            currentMode.Initialize();
            LevelEditor.instance.SetCurrentEditMode(LevelEditor.EditMode.Edit);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha1) && currentMode is not MoveMode)
        {
            if(currentMode != null)
                currentMode.Dispose();
            currentMode = moveMode;
            currentMode.Initialize();
            LevelEditor.instance.SetCurrentEditMode(LevelEditor.EditMode.Position);
        }
    }

    public void ChangeModeByArg(LevelEditor.EditMode mode)
    {
        print("ChangeModeByArg" + mode.ToString());
        if (mode == LevelEditor.EditMode.Edit && currentMode is not PlatformEditMode) 
        {
            if(currentMode != null)
                currentMode.Dispose();
            currentMode = editMode;
            currentMode.Initialize();
        }
        else if(mode == LevelEditor.EditMode.Position && currentMode is not MoveMode)
        {
            if(currentMode != null)
                currentMode.Dispose();
            currentMode = moveMode;
            currentMode.Initialize();
        }
    }

    public void BackToDefault()
    {
        print("Back to default");
        currentMode.Dispose();
        currentMode = null;
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        print("OnPointerDown");
        LevelEditor.instance.SetEditableObj(this);
    }

}
