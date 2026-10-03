using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Draws a local-only gamepad cursor and forwards RT clicks to the existing
/// uGUI pointer handlers. GameplayInput consumes the same cursor coordinates.
/// </summary>
[DefaultExecutionOrder(-300)]
public sealed class GamepadDrawerPointer : MonoBehaviour
{
    private const float CursorDisplaySize = 36f;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private Drawer drawer;
    private PointerEventData pointer;
    private EventSystem pointerEventSystem;
    private GameObject hovered;
    // A UI press owns RT until release; its trigger must never also create a stroke.
    private GameObject pressed;
    private bool capturesDraw;

    public static bool CapturesDraw { get; private set; }

    private void Awake() => drawer = GetComponent<Drawer>();

    private void Update()
    {
        EventSystem events = EventSystem.current;
        Gamepad pad = GameplayInput.PadFor(false);
        if (EgakuSettingsMenu.IsOpen || !GameplayInput.DrawerUsesGamepad || events == null || pad == null)
        {
            ReleasePointer();
            return;
        }

        if (pointer == null || pointerEventSystem != events)
        {
            pointerEventSystem = events;
            pointer = new PointerEventData(events) { pointerId = -101, button = PointerEventData.InputButton.Left };
        }

        Vector2 position = GameplayInput.PointerScreenPosition(false);
        pointer.delta = position - pointer.position;
        pointer.position = position;
        hits.Clear();
        events.RaycastAll(pointer, hits);
        pointer.pointerCurrentRaycast = hits.Count > 0 ? hits[0] : default;
        GameObject hit = hits.Count > 0 ? hits[0].gameObject : null;
        GameObject handler = hit != null ? ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit) : null;
        if (handler == null && hit != null)
            handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);

        GameObject hoverHandler = hit != null ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hit) : null;
        if (hovered != hoverHandler)
        {
            if (hovered != null) ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerExitHandler);
            hovered = hoverHandler;
            if (hovered != null) ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerEnterHandler);
        }
        if (hovered != null && pointer.delta != Vector2.zero)
            ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerMoveHandler);

        if (GameplayInput.PointerClickPressed && handler != null)
        {
            pressed = handler;
            capturesDraw = true;
            CapturesDraw = true;
            pointer.pointerPress = pressed;
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerDownHandler);
        }
        if (GameplayInput.PointerClickReleased)
        {
            if (pressed != null)
            {
                ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
                if (pressed == handler)
                    ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
            }
            pressed = null;
            // Keep this frame captured so releasing a UI click cannot end a stroke.
            CapturesDraw = capturesDraw;
            capturesDraw = false;
        }
        else
        {
            CapturesDraw = capturesDraw;
        }
    }

    private void OnGUI()
    {
        if (EgakuSettingsMenu.IsOpen || !GameplayInput.DrawerUsesGamepad || GameplayInput.PadFor(false) == null) return;
        Vector2 position = GameplayInput.PointerScreenPosition(false);
        float y = Screen.height - position.y;
        float displaySize = CursorDisplaySize * EgakuSettings.DrawerBrushCursorScale;
        Texture2D texture = drawer != null ? drawer.ActiveCursorTexture : null;
        Color previousColor = GUI.color;
        if (texture != null)
        {
            // Cursor imports are much larger than a screen cursor. Preserve their
            // aspect ratio and the mouse cursor's bottom-left drawing hotspot.
            float scale = displaySize / Mathf.Max(texture.width, texture.height);
            float width = texture.width * scale;
            float height = texture.height * scale;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(position.x, y - height, width, height), texture, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
            return;
        }

        // Keep the pointer visible before a pen is selected or if its icon is missing.
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(position.x - displaySize * 0.25f, y - displaySize * 0.25f,
            displaySize * 0.5f, displaySize * 0.5f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(position.x - displaySize / 6f, y - displaySize / 6f,
            displaySize / 3f, displaySize / 3f), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void ReleasePointer()
    {
        // A disconnected pad or scene unload must clear a pressed Button visually.
        if (pressed != null && pointer != null)
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
        if (hovered != null && pointer != null)
            ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerExitHandler);
        hovered = null;
        pressed = null;
        capturesDraw = false;
        CapturesDraw = false;
    }

    private void OnDisable() => ReleasePointer();
}
