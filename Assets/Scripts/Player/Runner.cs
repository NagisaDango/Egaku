using System;
using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Pun.Demo.PunBasics;
using Photon.Realtime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class Runner : MonoBehaviourPunCallbacks
{
    // Ignore tiny network velocity changes so the remote face keeps its last direction at rest.
    private const float RemoteFacingVelocityThreshold = 0.05f;

    [Header("Player components")]
    [Tooltip("The local player instance. Use this to know if the local player is represented in the Scene")]
    public static GameObject LocalPlayerInstance;
    public static Runner Instance;
    private RunnerMovement _RunnerMovement;
    private Rigidbody2D rb;
    private Collider2D col;

    [Header("Runtime Player Data")]
    public int actorNum;
    private Vector2 revivePos;

    [Header("Input")]
    public InputActionAsset _ActionMap;
    private InputAction moveAction;
    private InputAction jumpAction;
    private PlayerInput playerInput;
    private Gamepad pairedGamepad;

    [Header("Player Status")]
    GameObject interactingObject;
    private bool movingAlongElectric;
    bool inElectric = false;
    private bool reversed = false;
    private SplineAnimate splineAnimate;

    public bool validHoldJump;
    private int extraJumpForce;
    private float horizontalInput;

    [Header("Editable Data")]
    public int jumpForce;
    public int maxSpeed;
    [SerializeField] private RunnerMovementTuning movementTuning = new RunnerMovementTuning();

    [Header("Hold Object")]
    [SerializeField] private FixedJoint2D fixedJoint2D;
    [SerializeField] private LayerMask batteryLayer;
    GameObject holdGO;
    private bool holding;
    private HoldableObject holdingObject;
    private int holdingObjectID = -1;

    [Header("Appearance")]
    private GameObject runnerMouse;
    public Transform face;
    [SerializeField] private SpriteRenderer leftEye;
    [SerializeField] private SpriteRenderer rightEye;
    [SerializeField] private SpriteRenderer mouth;
    [SerializeField] private SpriteRenderer color;
    [SerializeField, Min(0.1f)] private float respawnDrawDuration = 1.2f;

    // This is local instance state only. Scene refresh creates a new Runner, so an
    // interrupted reveal cannot carry its input or damage lock into the rebuilt level.
    private RunnerRespawnVisual respawnVisual;
    private bool respawnRevealing;
    private float respawnRevealStartTime;
    private Vector3 respawnRevealPosition;
    private TrailRenderer[] respawnTrails;
    private bool[] previousTrailEmission;

    [SerializeField] private Sprite holdEyeLeft;
    [SerializeField] private Sprite holdEyeRight;
    [SerializeField] private Sprite ogEyes;

    #region Unity Execution Events
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Drawer is already active and set, destroying this runner.");
            Destroy(this.gameObject);
        }

        // #Important
        // used in GameManager.cs: we keep track of the localPlayer instance to prevent instantiation when levels are synchronized
        if (photonView.IsMine)
        {
            PlayerManager.LocalPlayerInstance = this.gameObject;
            photonView.RPC("RPC_InitDataSync", RpcTarget.AllBuffered, PhotonNetwork.LocalPlayer.ActorNumber);
            actorNum = PhotonNetwork.LocalPlayer.ActorNumber;
        }
        // #Critical
        // we flag as don't destroy on load so that instance survives level synchronization, thus giving a seamless experience when levels load.
        //DontDestroyOnLoad(this.gameObject);
    }

    [PunRPC]
    private void RPC_InitDataSync(int num)
    {
        actorNum = num;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        LevelSetup LevelM = GameObject.Find("LevelSetup").GetComponent<LevelSetup>();
        if (LevelM != null)
            LevelM.SetUpCamera(this);
        if (!photonView.IsMine)
        {
            Debug.Log("this player is not the runner, setting the rb to non physic");
            rb.bodyType = RigidbodyType2D.Kinematic; // Stop physics interactions
            rb.simulated = false; // Turn off physics on non-owners
            // Remote copies never read a local keyboard or claim the local gamepad.
            PlayerInput remoteInput = GetComponent<PlayerInput>();
            if (remoteInput != null) remoteInput.enabled = false;
        }
        else
        {
            runnerMouse = PhotonNetwork.Instantiate("RunnerMouse", Camera.main.ScreenToWorldPoint(Input.mousePosition),
                Quaternion.identity);
            GameObject fog = GameObject.Find("FogCanvas/Fog");
            if (fog != null)
                fog.SetActive(false);
            photonView.RPC("RPC_SetUpAppearance", RpcTarget.AllBuffered, PhotonNetwork.LocalPlayer.CustomProperties["Eyes"], PhotonNetwork.LocalPlayer.CustomProperties["Mouth"], PhotonNetwork.LocalPlayer.CustomProperties["Color"]);
        }

        if (photonView.IsMine)
            InitInput();
        _RunnerMovement = new RunnerMovement(rb, col, movementTuning);
        if (respawnVisual == null)
            respawnVisual = new RunnerRespawnVisual(gameObject);
    }

    private bool holdingShift;
    private void Update()
    {
        if (respawnRevealing)
        {
            if (Allan.GameManager.InteractionsPausedForRecovery)
                FinishRespawnReveal();
            else
                UpdateRespawnReveal();
        }

        // Freeze local gameplay input while Photon preserves the two-player session for reconnection.
        if (Allan.GameManager.InteractionsPausedForRecovery || EgakuSettingsMenu.IsOpen)
        {
            // Recovery must not replay a buffered jump or held direction when local input resumes.
            ClearMovementInput();
            return;
        }

        if (!photonView.IsMine)
        {
            return;
        }

        // Online input may change devices mid-level; local co-op still pairs one
        // device per role. Clear buffered movement whenever that pairing changes.
        bool inputDeviceChanged = ConfigureRunnerInput();

        if (runnerMouse)
            RunnerMouseUpdate();
        if (respawnRevealing)
        {
            // Do not process movement, grabbing, or wire input until every body and
            // facial sprite has completed the same drawing pass.
            ClearMovementInput();
            return;
        }
        if (movingAlongElectric || !rb.simulated)
        {
            ClearMovementInput();
        }
        else
        {
            // Read even zero input so FixedUpdate can brake after the player releases a direction.
            horizontalInput = moveAction != null ? Mathf.Clamp(moveAction.ReadValue<Vector2>().x, -1f, 1f) : 0f;
            if (horizontalInput > 0.001f)
                face.localScale = Vector3.one;
            else if (horizontalInput < -0.001f)
                face.localScale = new Vector3(-1, 1, 1);

            // A press that selects a new scheme can precede its action rebind in
            // this frame; keep that first jump instead of requiring a second press.
            bool firstPressOnNewDevice = inputDeviceChanged &&
                (GameplayInput.RunnerUsesGamepad
                    ? GameplayInput.PadFor(true) != null && GameplayInput.PadFor(true).buttonSouth.wasPressedThisFrame
                    : Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
            if ((jumpAction != null && jumpAction.WasPressedThisFrame()) || firstPressOnNewDevice)
                _RunnerMovement.QueueJump(Time.time);
            if (jumpAction != null && jumpAction.WasReleasedThisFrame())
                _RunnerMovement.ReleaseJump();
        }

        if (GameplayInput.WirePressed && inElectric && interactingObject != null)
        {
            if(DetectElectricField())
                MoveAlongElectric();
        }

        if (GameplayInput.GrabPressed && !holding)
        {
            if (holdingObjectID != -1)
            {
                photonView.RPC("RPC_SetHoldingEyes", RpcTarget.All);
                holdGO = PhotonView.Find(holdingObjectID).gameObject;
                holdingObject = holdGO.GetComponent<HoldableObject>();
                if (holdGO.CompareTag("Wood") || holdingObject is WoodPen)
                {
                    photonView.RPC("RPC_HoldWood", RpcTarget.All, holdingObjectID);
                }
                else if (holdGO.CompareTag("Battery") || holdingObject is Battery)
                {
                    photonView.RPC("RPC_HoldBattery", RpcTarget.All, holdingObjectID);
                        print("Getting battery from gate");
                        Battery battery = holdingObject as Battery;
                        battery.gameObject.tag = "Holding";
                        battery.DisconnectFromElectric();
                        Vector3 temp = holdGO.transform.localPosition;
                        holdGO.transform.localPosition = temp.normalized;
                }
            }
        }

        if (holding && GameplayInput.GrabReleased)
        {
            photonView.RPC("RPC_Release", RpcTarget.All);
            holding = false;
        }

        if (movingAlongElectric)
        {
            if (!DetectElectricField())
            {
                col.enabled = true;
                rb.simulated = true;
                if (holdingObject != null)
                {
                    holdingObject.ToggleCollider(true);
                    holdingObject.ToggleRbSimulated();
                }
                if (reversed)
                {
                    splineAnimate.Container.ReverseFlow(0);
                    reversed = false;
                }
                splineAnimate = null;
                movingAlongElectric = false;
                photonView.RPC("RPC_SetParent", RpcTarget.AllBuffered, -1);
            }
        }
        
        if (splineAnimate != null && splineAnimate.NormalizedTime >= 1)
        {
            col.enabled = true;
            rb.simulated = true;
            if (holdingObject != null)
            {
                holdingObject.ToggleCollider(true);
                holdingObject.ToggleRbSimulated();
            }
            if (reversed)
            {
                splineAnimate.Container.ReverseFlow(0);
                reversed = false;
            }
            splineAnimate = null;
            movingAlongElectric = false;
            photonView.RPC("RPC_SetParent", RpcTarget.AllBuffered, -1);
        }

    }

    private void LateUpdate()
    {
        if (!photonView.IsMine && respawnRevealing)
        {
            // PhotonTransformView can still interpolate a pre-death pose after the reliable
            // respawn RPC arrives. Pin only the passive copy during the reveal so the
            // partially drawn body stays at the checkpoint until fresh poses arrive.
            transform.position = respawnRevealPosition;
        }

        if (!photonView.IsMine && rb != null && !respawnRevealing)
        {
            // PhotonRigidbody2DView already receives this velocity on the passive copy.
            // Derive facing locally without adding a visual RPC or changing its stream.
            float remoteVelocityX = rb.linearVelocity.x;
            if (remoteVelocityX > RemoteFacingVelocityThreshold)
                face.localScale = Vector3.one;
            else if (remoteVelocityX < -RemoteFacingVelocityThreshold)
                face.localScale = new Vector3(-1, 1, 1);
        }

        // PhotonTransformView updates the remote body in Update; apply face rotation afterwards.
        AdjustFaceRotation();
    }

    private void FixedUpdate()
    {
        // Only the Runner owner's body simulates movement; the other client follows Photon state.
        if (!photonView.IsMine || _RunnerMovement == null)
            return;

        if (Allan.GameManager.InteractionsPausedForRecovery || EgakuSettingsMenu.IsOpen ||
            respawnRevealing || movingAlongElectric || !rb.simulated)
        {
            ClearMovementInput();
            return;
        }

        // ValidateHold may set the wood jump bonus, so read extraJumpForce afterwards.
        bool directAllowance = _RunnerMovement.HasBufferedJump(Time.fixedTime) &&
                               holdingObject != null && holdingObject.ValidateHold();
        if (_RunnerMovement.FixedStep(horizontalInput, Time.fixedTime, maxSpeed,
                jumpForce + extraJumpForce, directAllowance))
        {
            validHoldJump = false;
            // Keep the existing network-visible jump effects tied to an actual owner-side jump.
            PhotonNetwork.RaiseEvent(AudioManager.PlayAudioEventCode,
                new object[] { AudioManager.JUMPSFX, false },
                new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
            PhotonNetwork.Instantiate("JumpVFX", rb.transform.position - new Vector3(0, 0.6f, 5),
                Quaternion.identity);
        }
    }

    private void ClearMovementInput()
    {
        horizontalInput = 0f;
        if (_RunnerMovement != null)
            _RunnerMovement.ClearBufferedInput();
    }
    #endregion

    #region Appearance
    [PunRPC]
    private void RPC_SetUpAppearance(int eyeType, int mouthType, Vector3 playerColor)
    {
        leftEye.sprite = Resources.Load<Sprite>("Eyes/" + eyeType);
        rightEye.sprite = Resources.Load<Sprite>("Eyes/" + eyeType);
        ogEyes = leftEye.sprite;
        mouth.sprite = Resources.Load<Sprite>("Mouth/" + mouthType);
        color.color = new Color(playerColor.x, playerColor.y, playerColor.z);
    }

    [PunRPC]
    private void RPC_SetHoldingEyes()
    {
        leftEye.sprite = holdEyeLeft;
        rightEye.sprite = holdEyeRight;
    }
    
    private void ResetAppearance()
    {
        leftEye.sprite = ogEyes;
        rightEye.sprite = ogEyes;
    }

    public void AdjustFaceRotation()
    {
        // Keep eyes and mouth upright in world space as the parent body rotates.
        face.rotation = Quaternion.identity;
    }

    [PunRPC]
    public void AdjustScale(Vector2 movement)
    {
        if (movement.x > 0)
            face.localScale = Vector3.one;
        else
            face.localScale = new Vector3(-1, 1, 1);
    }

    private void RunnerMouseUpdate()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(GameplayInput.PointerScreenPosition(true));
        mousePos.z = 0;
        runnerMouse.transform.position = mousePos;
    }
    #endregion

    private void InitInput()
    {
        playerInput = GetComponent<PlayerInput>();
        if (PhotonNetwork.OfflineMode && Allan.GameManager.IsLocalMultiplayer)
        {
            // InputUser owns the local Runner action copy. Disable PlayerInput's
            // private user so it cannot pair the Drawer's second controller.
            if (playerInput != null) playerInput.enabled = false;
            InputActionAsset actions = InputDeviceRouter.ActionsFor(InputDeviceRouter.Role.Runner);
            moveAction = actions.FindAction("Runner/Move");
            jumpAction = actions.FindAction("Runner/Jump");
            return;
        }
        // PlayerInput owns a private action copy. Explicit pairing prevents a
        // local Drawer's device from also moving Runner.
        playerInput.neverAutoSwitchControlSchemes = true;
        ConfigureRunnerInput(true);
        moveAction = playerInput.actions.FindAction("Runner/Move");
        jumpAction = playerInput.actions.FindAction("Runner/Jump");
    }

    private bool ConfigureRunnerInput(bool force = false)
    {
        if (PhotonNetwork.OfflineMode && Allan.GameManager.IsLocalMultiplayer)
        {
            Gamepad assigned = InputDeviceRouter.GamepadFor(InputDeviceRouter.Role.Runner);
            if (pairedGamepad == assigned && !force) return false;
            ClearMovementInput();
            pairedGamepad = assigned;
            return true;
        }
        bool useGamepad = GameplayInput.RunnerUsesGamepad;
        Gamepad pad = GameplayInput.PadFor(true);
        if (useGamepad && pad == null)
        {
            // Offline co-op keeps its role assignment while unplugged. Stop the
            // old action state and wait for a replacement controller to appear.
            if (pairedGamepad != null) ClearMovementInput();
            pairedGamepad = null;
            playerInput.actions.bindingMask = InputBinding.MaskByGroup("Gamepad");
            return false;
        }

        if (!force && (useGamepad
            ? pairedGamepad == pad && playerInput.currentControlScheme == "Gamepad"
            : pairedGamepad == null && playerInput.currentControlScheme == "Keyboard&Mouse"))
            return false;

        ClearMovementInput();
        if (useGamepad)
        {
            playerInput.actions.bindingMask = InputBinding.MaskByGroup("Gamepad");
            playerInput.SwitchCurrentControlScheme("Gamepad", pad);
            pairedGamepad = pad;
        }
        else
        {
            playerInput.actions.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
            if (Keyboard.current != null && Mouse.current != null)
                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", Keyboard.current, Mouse.current);
            pairedGamepad = null;
        }
        return true;
    }

    private void MoveAlongElectric()
    {
        if (movingAlongElectric) return;
        print("start electric");
        print(interactingObject);
        splineAnimate = interactingObject.transform.parent.GetChild(0).GetComponent<SplineAnimate>();
        ElectricSpline splinePoints = interactingObject.transform.parent.GetComponent<ElectricSpline>();
        transform.SetParent(interactingObject.transform.parent.GetChild(0));
        transform.localPosition = Vector3.zero;
        movingAlongElectric = true;
        ClearMovementInput();
        if (splineAnimate == null)
        {
            Debug.LogWarning("No spline anim");
            return;
        }

        SplineContainer splineContainer = splineAnimate.Container;
        if (splineContainer == null || splineContainer.Splines.Count == 0)
        {
            Debug.LogWarning("No splines found in the container.");
            return;
        }

        rb.simulated = false;
        col.enabled = false;
        if (holdingObject != null)
        {
            holdingObject.ToggleCollider(false);
            holdingObject.ToggleRbSimulated(false);
        }

        Spline spline = new Spline();
        if (interactingObject.tag.EndsWith("End") && !reversed)
        {
            splineAnimate.Container.ReverseFlow(0);
            reversed = true;
        }
        else
            reversed = false;
        splineAnimate.Restart(true);
        interactingObject = null;
    }

    private bool DetectElectricField()
    {
        //setting 200 as the longest radius just cuz no battery can exceed that
        //可優化: 現在為每次調用，改為當超出第一次檢測時得到的radius後再重新Raycast檢測
        if (holdingObject is Battery)
        {
            return true;
        }
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, 50, Vector2.zero, 0, batteryLayer);
        if (hits.Length == 0)
        {
            Debug.Log("Currently Not in any electric zone, cannot or stop travel thru electric spline");
            return false;
        }

        // Iterate through all the colliders hit
        foreach (RaycastHit2D hit in hits)
        {
            Battery batteryComponent = hit.collider.gameObject.GetComponent<Battery>();

            // Check if the hit object has a Battery component
            if (batteryComponent != null)
            {
                // Check if the distance to this battery is within its specific radius
                if (Vector2.Distance(this.transform.position, hit.collider.transform.position) <= batteryComponent.radius)
                {
                    // Found a battery whose field we are inside
                    return true;
                }
            }
        }

        // If we've checked all hits and none were within their battery's radius
        Debug.Log("Detected batteries, but not within any of their effective radii.");
        return false;
        
        /*
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, 50, Vector2.zero, 0, batteryLayer); 
        if (hit.collider == null)
        {
            Debug.Log("Currently Not in any electric zone, cannot or stop travel thru electric spline");
        }
        else
        {
            if(Vector2.Distance(this.transform.position, hit.collider.transform.position) <= 
                hit.collider.gameObject.GetComponent<Battery>().radius)
            {
                return true;
            }
        }
        return false;
        */
    }

    public void SetRevivePos(Vector2 pos)
    {
        revivePos = pos;
    }

    
    public void Revive()
    {
        // Only the Runner owner handles a death. This also rejects repeated water,
        // hazard, or bullet contacts while the new body is being drawn.
        if (!photonView.IsMine || respawnRevealing || Allan.GameManager.InteractionsPausedForRecovery)
            return;

        photonView.RPC("RPC_Release", RpcTarget.All);
        ClearMovementInput();
        if (_RunnerMovement != null)
            _RunnerMovement.ResetAfterRespawn();
        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.SetRotation(0f);
        photonView.transform.position = revivePos;
        respawnRevealPosition = revivePos;
        BeginRespawnReveal();
        // One unbuffered event per death keeps both screens' visual sequence aligned
        // without adding an observed field or sending per-frame animation progress.
        photonView.RPC(nameof(RPC_BeginRespawnReveal), RpcTarget.Others, revivePos, PhotonNetwork.Time);
    }

    [PunRPC]
    private void RPC_BeginRespawnReveal(Vector2 checkpoint, double startedAt)
    {
        // The Runner owner alone decides whether a death happened. A late packet for an
        // old scene must not create a visual lock during a recovery refresh.
        if (photonView.IsMine || respawnRevealing || Allan.GameManager.InteractionsPausedForRecovery)
            return;

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        respawnRevealPosition = checkpoint;
        transform.position = respawnRevealPosition;
        transform.rotation = Quaternion.identity;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        BeginRespawnReveal();
        if (respawnRevealing)
        {
            // PhotonNetwork.Time is shared across clients. Account for packet travel so
            // the remote drawing finishes at roughly the owner's finish time.
            float elapsed = Mathf.Max(0f, (float)(PhotonNetwork.Time - startedAt));
            respawnRevealStartTime = Time.time - elapsed;
            UpdateRespawnReveal();
        }
    }

    private void BeginRespawnReveal()
    {
        // Clear the old movement trail before revealing at the checkpoint, or the
        // teleport itself draws a line across the level.
        respawnTrails = GetComponentsInChildren<TrailRenderer>();
        previousTrailEmission = new bool[respawnTrails.Length];
        for (int i = 0; i < respawnTrails.Length; i++)
        {
            previousTrailEmission[i] = respawnTrails[i].emitting;
            respawnTrails[i].emitting = false;
            respawnTrails[i].Clear();
        }

        if (respawnVisual == null)
            respawnVisual = new RunnerRespawnVisual(gameObject);
        if (!respawnVisual.Begin())
        {
            FinishRespawnReveal();
            return;
        }

        respawnRevealing = true;
        respawnRevealStartTime = Time.time;
    }

    private void UpdateRespawnReveal()
    {
        float progress = Mathf.Clamp01((Time.time - respawnRevealStartTime) /
                                        Mathf.Max(0.1f, respawnDrawDuration));
        respawnVisual.SetProgress(progress);
        if (progress >= 1f)
            FinishRespawnReveal();
    }

    private void FinishRespawnReveal()
    {
        // Restore local physics only after the visual lock ends. Remote copies remain
        // passive under their existing Photon Rigidbody configuration.
        respawnRevealing = false;
        respawnVisual?.End();
        if (respawnTrails != null)
        {
            for (int i = 0; i < respawnTrails.Length; i++)
            {
                if (respawnTrails[i] != null)
                    respawnTrails[i].emitting = previousTrailEmission[i];
            }
        }
        respawnTrails = null;
        previousTrailEmission = null;
        if (photonView.IsMine && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = true;
            ClearMovementInput();
        }
    }

    private void OnDestroy()
    {
        // A scene refresh may destroy the Runner halfway through the reveal. Dispose
        // its temporary material; the next scene's Runner starts with no lock state.
        respawnVisual?.End();
        if (Instance == this)
            Instance = null;
    }

    [PunRPC]
    private void RPC_Disable()
    {
        this.transform.gameObject.SetActive(false);
    }

    [PunRPC]
    private void RPC_Enable()
    {
        this.transform.gameObject.SetActive(true);
    }

    [PunRPC]
    private void RPC_SetParent(int viewID)
    {
        if (viewID != -1)
        {
            //transform.SetParent(PhotonView.Find(viewID).transform, true);
            //transform.localPosition = Vector3.zero;

        }
        else transform.SetParent(null);
    }


    #region Hold Objects
    [PunRPC]
    private void RPC_HoldWood(int viewID)
    {
        if (viewID != -1)
        {
            if(holdGO == null)
                holdGO = PhotonView.Find(viewID).gameObject;
            //_RunnerMovement.SetJumpAllowance(false);
            holdGO.tag = "Holding";
            holdGO.transform.SetParent(this.transform);
            Rigidbody2D holdingRb = holdGO.GetComponent<Rigidbody2D>();
            holdGO.GetComponent<WoodPen>().holder = this;
            holdingRb.mass = 1;
            holding = true;
            fixedJoint2D.connectedBody = holdingRb;
        }
    }
    private bool inBattery = false;

    private void TakeOutBattery(GameObject battery)
    {
        holdingObjectID = battery.GetPhotonView().ViewID;
    }

    [PunRPC]
    private void RPC_HoldBattery(int viewID)
    {
        if (holdGO == null)
            holdGO = PhotonView.Find(viewID).gameObject;
        holdGO.tag = "Holding";
        holdGO.transform.SetParent(this.transform);
        Rigidbody2D holdingRb = holdGO.GetComponent<Rigidbody2D>();
        //holdGO.GetComponent<WoodPen>().holder = this;
        holdingRb.mass = 1;
        holding = true;
        fixedJoint2D.connectedBody = holdingRb;
    }

    public void HoldingObjLost()
    {
        validHoldJump = false;
        extraJumpForce = 0;
        holding = false;
        holdGO = null;
        holdingObject = null;
        fixedJoint2D.connectedBody = rb;
    }

    [PunRPC]
    private void RPC_Release()
    {
        ResetAppearance();
        // TODO: only setting to wood here cuz its the only one that can be hold, might want to change, have a buffer holding the original tag name
        if (holdGO != null)
        {
            holdGO.transform.SetParent(null);
        }
        
        if (holdingObject != null)
        {
            holdingObject.ToggleCollider(true);
            holdingObject.Reset();
            holdingObject = null;

            fixedJoint2D.connectedBody = rb;
        }
        validHoldJump = false;
        extraJumpForce = 0;
        holding = false;
        holdGO = null;
    }
    #endregion


    public void SetExtraJumpForce(int _extraJumpForce)
    {
        extraJumpForce = _extraJumpForce;
    }

    #region Collision / Trigger Detection
    private void OnCollisionStay2D(Collision2D other)
    {
        if (other.gameObject.tag == "Wood" || other.gameObject.tag == "Battery")
        {
            holdingObjectID = other.gameObject.GetPhotonView().ViewID;
        }
    }


    private void OnCollisionExit2D(Collision2D other)
    {
        if(!holding)
            holdingObjectID = -1;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!photonView.IsMine || respawnRevealing)
            return;
        if (other.tag == "DeathDesuwa")
        {
            Revive();
        }
        if(other.tag == "Water")
        {
            Revive();
        }
    }
    
    
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Battery") && holdingObjectID == -1)
        {
            holdingObjectID = other.gameObject.GetPhotonView().ViewID;
            inBattery = true;
        }
        
        if (other.tag.StartsWith("Electric") && !other.CompareTag("Electric"))
        {
            inElectric = true;
            interactingObject = other.gameObject;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {        
        if (other.CompareTag("Battery"))
        {
            // TODO: Might cause error here since everytime passby would be set to -1, even when holding other object
            holdingObjectID = -1;
            inBattery = false;
        }
        if (other.tag.StartsWith("Electric") && !movingAlongElectric)
        {
            inElectric = false;
            interactingObject = null;
        }
    }
    #endregion
}
