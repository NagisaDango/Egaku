using System;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Net;
using static UnityEngine.Rendering.DebugUI;
using Allan;

public class Drawer : MonoBehaviourPun
{
    public static Drawer Instance;
    // Remote copies need no UI; the local copy acknowledges only after UI/camera setup succeeds.
    public bool SceneReady { get; private set; }
    public DrawMesh drawMeshPrefab;
    public DrawMesh drawMeshSpriteShapePrefab;
    private DrawMesh currentDrawer;

    [SerializeField] private bool eraserMode;
    [SerializeField] private bool interactable;
    [SerializeField] private GameObject drawerPanelPrefab;
    [Header("Cursor")]
    [SerializeField] private Texture2D woodCursorTexture;
    [SerializeField] private Texture2D cloudCursorTexture;
    [SerializeField] private Texture2D electricCursorTexture;
    [SerializeField] private Texture2D steelCursorTexture;
    [SerializeField] private Texture2D eraserCursorTexture;
    // The local gamepad pointer reads the same selected icon as the mouse cursor;
    // cursor appearance is presentation only and does not enter Photon state.
    public Texture2D ActiveCursorTexture { get; private set; }
    // Tracks the physical cursor mode so hot swapping restores the same pen icon.
    private bool cursorUsedGamepad;
    [Header("Camera")]
    [SerializeField, Min(0f)] private float cameraPanSpeed = 15f;
    // Only the owning Drawer creates this local controller; it never becomes Photon state.
    private DrawerCameraController cameraController;
    
    public static Action<PenUI.PenType> OnPenSelect;
    private PenUI.PenType currentPenType;
    private int currentPenIndex;
    public float drawSize;
    public int drawStrokeLimit = 300;
    private int drawStrokeTotal;
    [SerializeField] private Slider inkSlider;
    public PenProperty.PenType sliderPenType;
    public float time = 0.2f;

    // The Master Client serializes erase results so a drag crossing the same object
    // cannot refund ink or destroy the network object more than once.
    private readonly HashSet<int> processedEraseViewIds = new HashSet<int>();
    private readonly HashSet<string> processedSceneErasePaths = new HashSet<string>();

    [Header("Pen")] 
    [SerializeField] public PenProperty woodPen;
    [SerializeField] public PenProperty cloudPen;
    [SerializeField] public PenProperty steelPen;
    [SerializeField] public PenProperty electricPen;
    public List<PenProperty> penProperties;
    public static bool[] penStatus = new bool[4];
    public static bool multipleEraseMode;
    private void Awake()
    {
        // The Drawer can survive Photon scene refreshes, so reset scene-target erase guards whenever Unity loads a level.
        SceneManager.sceneLoaded += ResetEraseDedupeForLoadedScene;

        //DontDestroyOnLoad(this.gameObject);
        Instance = this;
        inkSlider = GameObject.Find("GameCanvas/Panel/Slider").GetComponent<Slider>();
        currentPenType = PenUI.PenType.None;
        penProperties =
            new List<PenProperty>
            {
                woodPen, cloudPen, steelPen, electricPen
            };

        time = 0.2f;

        drawStrokeTotal = drawStrokeLimit;
    }
    
    
    private void Start()
    {
        //if (Instance == null)
        //{
        //    Instance = this;
        //}
        //else
        //{
        //    Debug.LogWarning("Drawer is already active and set, destroying this drawer.");
        //    Destroy(this.gameObject);
        //}
        if (photonView.IsMine)
        {
            EgakuSettings.Changed += RefreshCursorForSettings;
            print("This is the draweer spawning UI");
            OnPenSelect += SetPenProperties;
            GameObject UI = Instantiate(drawerPanelPrefab).transform.GetChild(0).gameObject;
            LevelSetup levelSetup = LevelSetup.FindInScene(gameObject.scene);
            DrawerUICOntrol drawerUI = UI.GetComponent<DrawerUICOntrol>();
            levelSetup.Init(drawerUI);

            // The level setup may intentionally leave the initial tool as None. Pick an
            // unlocked tool before reading its material so a scene reload cannot abort Start.
            if (currentPenType == PenUI.PenType.None)
            {
                for (int i = 0; i < penProperties.Count; i++)
                {
                    if (penStatus[i] && penProperties[i] != null)
                    {
                        SetPenProperties(penProperties[i].penType);
                        break;
                    }
                }
            }

            PenProperty initialPen = FindPenProperty(currentPenType);
            if (initialPen != null && initialPen.material != null)
            {
                Color color = initialPen.material.color;
                ChangeSliderColor(color.r, color.g, color.b, (int)initialPen.penType);
            }

            cameraController = gameObject.AddComponent<DrawerCameraController>();
            // Keep the local pointer ready even if the online Drawer starts with
            // mouse input and plugs in a gamepad later. It draws only in gamepad mode.
            gameObject.AddComponent<GamepadDrawerPointer>();
            if (!levelSetup.TryRegisterDrawerCamera(cameraController, cameraPanSpeed))
            {
                // A level without its authored Confiner remains playable with the original follow view.
                Debug.LogWarning("Drawer free camera is unavailable because this level has no camera boundary.");
                Destroy(cameraController);
                cameraController = null;
            }
        }
        SceneReady = true;
    }


    public PenProperty FindPenProperty(PenUI.PenType currentPenType)
    {
        switch (currentPenType)
        {
            case PenUI.PenType.Wood:
                return woodPen;
            case PenUI.PenType.Cloud:
                return cloudPen;
            case PenUI.PenType.Steel:
                return steelPen;
            case PenUI.PenType.Electric:
                return electricPen;
        }
        return null;
    }

    private void OnDisable()
    {
        print("Wtf");
    }

    private void OnDestroy()
    {
        // Scene changes destroy the owning Drawer; its static pen-selection subscription
        // must not keep old cursor handlers alive through later levels and refreshes.
        OnPenSelect -= SetPenProperties;
        EgakuSettings.Changed -= RefreshCursorForSettings;
        if (photonView.IsMine) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        if (scaledCursor != null) Destroy(scaledCursor);
        // Remove the static Unity event hook so a destroyed network Drawer cannot keep receiving scene loads.
        SceneManager.sceneLoaded -= ResetEraseDedupeForLoadedScene;
    }

    private void ResetEraseDedupeForLoadedScene(Scene loadedScene, LoadSceneMode mode)
    {
        // A refreshed level may reuse hierarchy paths and PhotonView IDs; clear per-room request guards so valid erases can run again.
        requestedEraseViewIds.Clear();
        requestedSceneErasePaths.Clear();
        processedEraseViewIds.Clear();
        processedSceneErasePaths.Clear();
    }


    private Vector3 lastErasePos;
    [SerializeField] private float minEraseDis;
    // Request guards suppress duplicate owner-to-Master erase RPCs and reset when a refreshed level reuses its scene targets.
    private readonly HashSet<int> requestedEraseViewIds = new HashSet<int>();
    private readonly HashSet<string> requestedSceneErasePaths = new HashSet<string>();
    private readonly List<DrawMesh> eraseMeshHits = new List<DrawMesh>();
    // The eraser is a visible brush, not an infinitely thin physics ray. Sampling
    // this radius along fast pointer movement prevents narrow strokes being skipped.
    [SerializeField, Min(0.01f)] private float eraseBrushRadius = 0.2f;

    void Update()
    {
        if (EgakuSettingsMenu.IsOpen) return;
        // Freeze drawing and erasing while Photon preserves the two-player session for reconnection.
        if (GameManager.InteractionsPausedForRecovery) return;

        if(!photonView.IsMine || currentPenType == PenUI.PenType.None)
            return;
        if (cursorUsedGamepad != GameplayInput.DrawerUsesGamepad)
        {
            // A disconnected controller can end a held stroke without sending RT
            // release. Finish it before changing cursor coordinates or icon mode.
            if (currentDrawer != null)
            {
                drawStrokeTotal -= currentDrawer.drawStrokes;
                currentDrawer.RequestFinishDraw();
                currentDrawer = null;
            }
            ApplyCursor(ActiveCursorTexture);
        }
        int brushStep = GameplayInput.BrushStep;
        if(brushStep != 0)
        {
            eraserMode = false;
            // TODO: hard code 4 length here
            if (brushStep > 0)
            {
                int tempIndex = (currentPenIndex - 1 + 4) % 4;
                while (tempIndex != currentPenIndex)
                {
                    //print(tempIndex + "  " + penStatus[tempIndex]);
                    if (penStatus[tempIndex] == true)
                    {
                        SetPenProperties(penProperties[tempIndex].penType);
                        StopAllCoroutines();
                        Color color = penProperties[tempIndex].material.color;

                        ChangeSliderColor(color.r, color.g, color.b, (int)penProperties[tempIndex].penType);
                        ClearCoroutineQueue();
                        UpdateSlider(1 - penProperties[tempIndex].currentStrokes * 1f / penProperties[tempIndex].maxStrokes);

                        break;
                    }
                    else
                    {
                        tempIndex--;
                        tempIndex = (tempIndex + 4) % 4;
                    }
                }

                if (tempIndex == currentPenIndex && currentPenType == PenUI.PenType.Eraser)
                {
                    SetPenProperties(penProperties[tempIndex].penType);
                    StopAllCoroutines();
                    Color color = penProperties[tempIndex].material.color;

                    ChangeSliderColor(color.r, color.g, color.b, (int)penProperties[tempIndex].penType);
                    ClearCoroutineQueue();
                    UpdateSlider(1 - penProperties[tempIndex].currentStrokes * 1f / penProperties[tempIndex].maxStrokes);
                }
            }
            if (brushStep < 0)
            {
                int tempIndex = (currentPenIndex + 1 + 4) % 4;
                while (tempIndex != currentPenIndex)
                {
                    //print(tempIndex + "  " + penStatus[tempIndex]);
                    if (penStatus[tempIndex] == true)
                    {
                        SetPenProperties(penProperties[tempIndex].penType);

                        StopAllCoroutines();
                        Color color = penProperties[tempIndex].material.color;
                        ChangeSliderColor(color.r, color.g, color.b, (int)penProperties[tempIndex].penType);
                        ClearCoroutineQueue();
                        UpdateSlider(1 - penProperties[tempIndex].currentStrokes * 1f / penProperties[tempIndex].maxStrokes);


                        break;
                    }
                    else
                    {
                        tempIndex++;
                        tempIndex = (tempIndex + 4) % 4;
                    }
                }
                
                
                if (tempIndex == currentPenIndex && currentPenType == PenUI.PenType.Eraser)
                {
                    SetPenProperties(penProperties[tempIndex].penType);
                    StopAllCoroutines();
                    Color color = penProperties[tempIndex].material.color;

                    ChangeSliderColor(color.r, color.g, color.b, (int)penProperties[tempIndex].penType);
                    ClearCoroutineQueue();
                    UpdateSlider(1 - penProperties[tempIndex].currentStrokes * 1f / penProperties[tempIndex].maxStrokes);
                }
            }
        }

        if (GameplayInput.EraserPressed)
        {
            if (eraserMode)
            {
                SetPenProperties(lastPenType);
            }
            else
            {
                SetPenProperties(PenUI.PenType.Eraser);
                Vector2 erasePosition = Camera.main.ScreenToWorldPoint(GameplayInput.PointerScreenPosition(false));
                if (CanUsePointer(erasePosition))
                    EraseDrawnObj(erasePosition);
            }
        }
        // Opening the persistent Settings button must not also create a network stroke.
        if (GameplayInput.DrawPressed &&
            !EgakuSettingsMenu.PointerIsOverOpenButton(GameplayInput.PointerScreenPosition(false)))
        {
            if (eraserMode) //&& EventSystem.current.IsPointerOverGameObject())
            {
                lastErasePos = GetMouseWorldPosition();
                //if(EventSystem.current.IsPointerOverGameObject())
                if (CanUsePointer(lastErasePos))
                    EraseDrawnObj(lastErasePos);
            }
            else if (currentDrawer == null && CanUsePointer(GetMouseWorldPosition()))
            {
                //currentDrawer = Instantiate(drawMeshPrefab);
                currentDrawer = PhotonNetwork.Instantiate(drawMeshPrefab.name, this.transform.position, this.transform.rotation).GetComponent<DrawMesh>();
                //currentDrawer = PhotonNetwork.Instantiate(drawMeshSpriteShapePrefab.name, this.transform.position, this.transform.rotation).GetComponent<DrawMesh>();
                
                Vector3 mousePos = GetMouseWorldPosition();
                currentDrawer.photonView.RPC("RPC_InitializedDrawProperty", RpcTarget.All, mousePos, currentPenType.ToString(), interactable);
            }
        }
        if (GameplayInput.DrawHeld)
        {
            Vector3 mousePos = GetMouseWorldPosition();
            if (!CanUsePointer(mousePos))
            {
                // End a stroke at the boundary rather than letting an off-level pointer
                // create a long segment when it returns to the visible drawing area.
                if (currentDrawer != null)
                {
                    drawStrokeTotal -= currentDrawer.drawStrokes;
                    currentDrawer.RequestFinishDraw();
                    currentDrawer = null;
                }
                lastErasePos = mousePos;
                return;
            }
            if (eraserMode)
            {
                float eraseDistance = Vector3.Distance(mousePos, lastErasePos);
                float eraseSampleDistance = Mathf.Max(0.01f, Mathf.Min(minEraseDis, eraseBrushRadius * 0.5f));
                if (eraseDistance >= eraseSampleDistance)
                {
                    EraseDrawnObjCast(lastErasePos, mousePos);
                    lastErasePos = mousePos;
                }

                return;
            }

            if(currentDrawer == null)
            {
                goto SkipDrawMesh;
            }


            if (currentDrawer.ValidateMouseMovement(mousePos))
            {
                //int strokeLeft = drawStrokeTotal - currentDrawer.drawStrokes;
                int strokeLeft = currentDrawer.currProperty.maxStrokes - currentDrawer.currProperty.currentStrokes;

                print("strokeLeft "  + strokeLeft);

                UpdateSlider(1 - currentDrawer.currProperty.currentStrokes * 1f / currentDrawer.currProperty.maxStrokes);


                Vector3 lastPos = currentDrawer.GetLastMousePosition();
                Vector3 direction = (mousePos - lastPos).normalized;
                float distance = Vector3.Distance(lastPos, mousePos);
                
                

                if (strokeLeft <= 0)
                {
                    print("stop drawing");
                    drawStrokeTotal -= currentDrawer.drawStrokes;
                    currentDrawer.RequestFinishDraw();
                    currentDrawer = null;
                }
                else
                {
                    if (currentDrawer)
                    {
                        currentDrawer.RequestStartDraw(mousePos);
                    }
                    //currentDrawer.photonView.RPC("RPC_DrawSpriteShape", RpcTarget.All, mousePos);
                    //currentDrawer.StartDraw();
                }
            }
        }
        SkipDrawMesh:
        if (GameplayInput.DrawReleased || (GameplayInput.DrawerUsesGamepad && GameplayInput.PadFor(false) == null && currentDrawer != null))
        {
            if (currentDrawer)
            {
                drawStrokeTotal -= currentDrawer.drawStrokes;
                currentDrawer.RequestFinishDraw();
                //currentDrawer.rb2d.bodyType = RigidbodyType2D.Kinematic;
            }
            currentDrawer = null;
        }
    }

    [PunRPC]
    public void ChangeSliderColor(float r, float g, float b, int penType)
    {
        inkSlider.transform.Find("Background").GetComponent<Image>().color = new Color(r, g, b, 0.5f);
        inkSlider.transform.Find("Fill Area/Fill").GetComponent<Image>().color = new Color(r, g, b, 1f);
        sliderPenType = (PenProperty.PenType)penType;
    }

    private PenUI.PenType lastPenType;
    private void SetPenProperties(PenUI.PenType penType)
    {
        switch (penType)
        {
            case PenUI.PenType.None:
                ApplyCursor(null);
                break;
            case PenUI.PenType.Wood:
                currentPenIndex = 0;
                ApplyCursor(woodCursorTexture);
                break;
            case PenUI.PenType.Cloud:
                currentPenIndex = 1;
                ApplyCursor(cloudCursorTexture);
                break;
            case PenUI.PenType.Steel:
                currentPenIndex = 2;
                ApplyCursor(steelCursorTexture);
                break;
            case PenUI.PenType.Electric:
                currentPenIndex = 3;
                ApplyCursor(electricCursorTexture);
                break;
            case PenUI.PenType.Eraser:
                lastPenType = currentPenType;
                eraserMode = true;
                ApplyCursor(eraserCursorTexture);
                break;
        }
        currentPenType = penType;
        
        if (penType != PenUI.PenType.Eraser)
        {
            eraserMode = false;
        }
    }
    private void SetPenProperties(PenProperty.PenType penType)
    {
        switch (penType)
        {
            case PenProperty.PenType.Wood:
                currentPenIndex = 0;
                currentPenType = PenUI.PenType.Wood;
                ApplyCursor(woodCursorTexture);
                break;
            case PenProperty.PenType.Cloud:
                currentPenIndex = 1;
                currentPenType = PenUI.PenType.Cloud;
                ApplyCursor(cloudCursorTexture);
                break;
            case PenProperty.PenType.Steel:
                currentPenIndex = 2;
                currentPenType = PenUI.PenType.Steel;
                ApplyCursor(steelCursorTexture);
                break;
            case PenProperty.PenType.Electric:
                currentPenIndex = 3;
                currentPenType = PenUI.PenType.Electric;
                ApplyCursor(electricCursorTexture);
                break;
        }
    }
    
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(GameplayInput.PointerScreenPosition(false));
        worldPosition.z = 0;
        return worldPosition;
    }

    private Texture2D scaledCursor;

    public void FinishStrokeForSettings()
    {
        // A local settings pause must finish an existing network stroke before input is blocked.
        if (!photonView.IsMine || currentDrawer == null) return;
        drawStrokeTotal -= currentDrawer.drawStrokes;
        currentDrawer.RequestFinishDraw();
        currentDrawer = null;
    }

    public void RefreshCursorForSettings()
    {
        if (photonView.IsMine) ApplyCursor(ActiveCursorTexture);
    }

    private void ApplyCursor(Texture2D texture)
    {
        ActiveCursorTexture = texture;
        cursorUsedGamepad = GameplayInput.DrawerUsesGamepad;
        // A gamepad Drawer has a separate on-screen pointer. Keep the physical
        // mouse ordinary so the local keyboard Runner's cursor is not a pen icon.
        if (scaledCursor != null)
        {
            Destroy(scaledCursor);
            scaledCursor = null;
        }
        Texture2D visibleTexture = cursorUsedGamepad || EgakuSettingsMenu.IsOpen ? null : texture;
        if (visibleTexture != null && !Mathf.Approximately(EgakuSettings.DrawerBrushCursorScale, 1f))
        {
            // The existing cursor textures are readable but have different source sizes.
            // Resample the selected texture locally; neither the pen nor its Photon state changes.
            int width = Mathf.Max(1, Mathf.RoundToInt(texture.width * EgakuSettings.DrawerBrushCursorScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(texture.height * EgakuSettings.DrawerBrushCursorScale));
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            scaledCursor = new Texture2D(width, height, TextureFormat.RGBA32, false);
            scaledCursor.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            scaledCursor.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            visibleTexture = scaledCursor;
        }
        Vector2 hotspot = visibleTexture != null ? new Vector2(0f, visibleTexture.height) : Vector2.zero;
        Cursor.SetCursor(visibleTexture, hotspot, CursorMode.Auto);
    }

    private bool CanUsePointer(Vector3 worldPosition)
    {
        return cameraController == null || cameraController.CanUsePointer(worldPosition);
    }

    [PunRPC]
    public void UpdateSlider(float val)
    {
        inkSlider.value = val;
    }
    
    private void EraseDrawnObj(Vector2 mousePos)
    {
        EraseAtPosition(mousePos);
    }

    private void EraseDrawnObjCast(Vector2 startPos, Vector2 endPos)
    {
        float distance = Vector2.Distance(startPos, endPos);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(eraseBrushRadius * 0.5f, 0.05f)));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 sample = Vector2.Lerp(startPos, endPos, i / (float)steps);
            EraseAtPosition(sample);
        }
    }

    private void EraseAtPosition(Vector2 position)
    {
        // Eraser drag sampling can straddle the edge even when the current cursor is inside.
        if (cameraController != null && !cameraController.CanEraseAt(position))
            return;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(position, eraseBrushRadius, LayerMask.GetMask("Draw"));
        foreach (Collider2D hitCollider in colliders)
        {
            if (hitCollider == null)
                continue;

            if (hitCollider.CompareTag("ClickToErase"))
            {
                Transform sceneTarget = hitCollider.transform.parent;
                if (sceneTarget != null)
                {
                    string path = GetSceneObjectPath(sceneTarget.gameObject);
                    if (requestedSceneErasePaths.Add(path))
                        photonView.RPC(nameof(RPC_RequestSceneObjectErase), RpcTarget.MasterClient, path);
                }
                continue;
            }

        }

        // Do not depend on Physics2D for strokes: the Drawer-side rigidbody is
        // intentionally unsimulated, so its attached PolygonCollider2D is not queried.
        DrawMesh.CollectEraserHits(position, eraseBrushRadius, eraseMeshHits);
        foreach (DrawMesh erasingMesh in eraseMeshHits)
        {
            if (erasingMesh.photonView != null && requestedEraseViewIds.Add(erasingMesh.photonView.ViewID))
                photonView.RPC(nameof(RPC_RequestErase), RpcTarget.MasterClient, erasingMesh.photonView.ViewID);
        }
    }

    public void ForceFinishDraw(DrawMesh drawMesh)
    {
        if (photonView.IsMine && currentDrawer == drawMesh)
        {
            drawStrokeTotal -= currentDrawer.drawStrokes;
            currentDrawer = null;
        }
    }

    private void RequestSceneObjectErase(GameObject target)
    {
        photonView.RPC(nameof(RPC_RequestSceneObjectErase), RpcTarget.MasterClient, GetSceneObjectPath(target));
    }

    [PunRPC]
    private void RPC_RequestErase(int viewId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || info.Sender == null || info.Sender.ActorNumber != photonView.OwnerActorNr || !processedEraseViewIds.Add(viewId))
            return;

        PhotonView targetView = PhotonView.Find(viewId);
        DrawMesh target = targetView != null ? targetView.GetComponent<DrawMesh>() : null;
        if (target == null || target.currProperty == null)
            return;

        Vector2 eraseCenter = target.col2d != null ? target.col2d.bounds.center : target.transform.position;
        photonView.RPC(nameof(RPC_DirectErase), RpcTarget.AllViaServer, (int)target.currProperty.penType,
            target.drawStrokes, eraseCenter, target.gameObject.tag);
        target.DestroyAfterMasterAuthorization();
    }

    [PunRPC]
    private void RPC_RequestSceneObjectErase(string path, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || info.Sender == null || info.Sender.ActorNumber != photonView.OwnerActorNr || !processedSceneErasePaths.Add(path))
            return;

        photonView.RPC(nameof(RPC_ApplySceneObjectErase), RpcTarget.AllViaServer, path);
    }

    [PunRPC]
    private void RPC_ApplySceneObjectErase(string path)
    {
        GameObject target = FindSceneObject(path);
        if (target != null)
        {
            AudioManager.PlayOne(AudioManager.ERASESFX);
            Destroy(target);
        }
    }

    [PunRPC]
    public void RPC_DirectErase(int penTypeValue, int stroke, Vector2 centerPos, string name)
    {
        PenProperty.PenType penType = (PenProperty.PenType)penTypeValue;
        PenProperty pen  = GetPenProperty(penType);
        pen.currentStrokes -= stroke;
        float value = 1 - pen.currentStrokes * 1.0f / pen.maxStrokes;

        AudioManager.PlayOne(AudioManager.ERASESFX);
        if (photonView.IsMine && sliderPenType == penType)
            EnqueueCoroutine(AddSliderValue(value, time));
        print("queue:  " + coroutineQueue.Count);
        //StartCoroutine(AddSliderValue(value, time));
        SpawnParticles(name, centerPos);
        //ParticleAttractor eraseEffect = PhotonNetwork.Instantiate("EraseEffect", new Vector3(centerPos.x, centerPos.y, 0), Quaternion.identity).GetComponent<ParticleAttractor>();
    }

    private PenProperty GetPenProperty(PenProperty.PenType penType)
    {
        switch (penType)
        {
            case PenProperty.PenType.Wood:
                return woodPen;
            case PenProperty.PenType.Cloud:
                return cloudPen;
            case PenProperty.PenType.Steel:
                return steelPen;
            case PenProperty.PenType.Electric:
                return electricPen;
        }
        return null;

    }

    private void SpawnParticles(string erasingTagName, Vector3 centerPos)
    {
        //TODO: Error prone here, should not be changing tag of holding object to holding, stay as what type it is
        if (erasingTagName == "Holding")
            erasingTagName = "Wood";
        Instantiate(Resources.Load("EraseEffect" + erasingTagName, typeof(GameObject)),  new Vector3(centerPos.x, centerPos.y, 0), Quaternion.identity);
        Instantiate(Resources.Load("EraseEffect", typeof(GameObject)),  new Vector3(centerPos.x, centerPos.y, -6), Quaternion.identity);
        //PhotonNetwork.Instantiate("EraseEffect" + erasingTagName, new Vector3(centerPos.x, centerPos.y, 0), Quaternion.identity).GetComponent<ParticleAttractor>();
    }

    IEnumerator AddSliderValue(float target, float time)
    {
        print("queue" + target +" " + inkSlider.value);
        float currentValue = inkSlider.value;
        float increment = (target- currentValue) / time / 50;
        while (currentValue <= target) {
            currentValue += increment;
            //print("value:" + currentValue);
            UpdateSlider(currentValue);
            yield return new WaitForSeconds(0.02f);
        }

    }

    private Queue<IEnumerator> coroutineQueue = new Queue<IEnumerator>();
    public bool isCoroutineRunning = false;

    public void EnqueueCoroutine(IEnumerator coroutine)
    {
        coroutineQueue.Enqueue(coroutine);

        if (!isCoroutineRunning)
            StartCoroutine(RunQueue());
    }

    [PunRPC]
    public void ClearCoroutineQueue()
    {
        coroutineQueue.Clear();
    }

    private static string GetSceneObjectPath(GameObject target)
    {
        List<int> indices = new List<int>();
        Transform current = target.transform;
        while (current.parent != null)
        {
            indices.Add(current.GetSiblingIndex());
            current = current.parent;
        }

        GameObject[] roots = current.gameObject.scene.GetRootGameObjects();
        indices.Add(Array.IndexOf(roots, current.gameObject));
        indices.Reverse();
        return string.Join("/", indices);
    }

    private static GameObject FindSceneObject(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length == 0 || !int.TryParse(parts[0], out int rootIndex))
            return null;

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        if (rootIndex < 0 || rootIndex >= roots.Length)
            return null;

        Transform current = roots[rootIndex].transform;
        for (int i = 1; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out int childIndex) || childIndex < 0 || childIndex >= current.childCount)
                return null;
            current = current.GetChild(childIndex);
        }

        return current.gameObject;
    }

    private IEnumerator RunQueue()
    {
        isCoroutineRunning = true;

        while (coroutineQueue.Count > 0)
        {
            print("running queue");
            yield return StartCoroutine(coroutineQueue.Dequeue());
        }

        isCoroutineRunning = false;
    }


}

[System.Serializable]
public class PenProperty
{
    public enum PenType
    {
        Wood,
        Cloud,
        Electric,
        Steel,
        Eraser
    };
    public PenType penType;
    public bool gravity;
    public bool trigger;
    public int mass;
    public float size;
    public Material material;
    public Material drawingMaterial;

    public int maxStrokes;
    public int currentStrokes;
}
