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

    [Header("Player Status")]
    GameObject interactingObject;
    private bool movingAlongElectric;
    bool inElectric = false;
    private bool reversed = false;
    private SplineAnimate splineAnimate;

    public bool validHoldJump;
    private int extraJumpForce;
    private bool jump;

    [Header("Editable Data")]
    public int jumpForce;
    public int maxSpeed;

    [Header("Hold Object")]
    [SerializeField] private FixedJoint2D fixedJoint2D;
    [SerializeField] private LayerMask batteryLayer;
    GameObject holdGO;
    private bool holding;
    private HoldableObject holdingObject;
    private int holdingObjectID;

    [Header("Appearance")]
    private GameObject runnerMouse;
    public Transform face;
    [SerializeField] private SpriteRenderer leftEye;
    [SerializeField] private SpriteRenderer rightEye;
    [SerializeField] private SpriteRenderer mouth;
    [SerializeField] private SpriteRenderer color;

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
            actorNum = PhotonNetwork.LocalPlayer.ActorNumber;
        }
        // #Critical
        // we flag as don't destroy on load so that instance survives level synchronization, thus giving a seamless experience when levels load.
        //DontDestroyOnLoad(this.gameObject);
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
            //rb.bodyType = RigidbodyType2D.Dynamic;
            rb.isKinematic = true; // Stop physics interactions
            rb.simulated = false; // Turn off physics on non-owners
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

        InitInput();
        _RunnerMovement = new RunnerMovement(rb, 10f, maxSpeed);
    }

    private void Update()
    {
        AdjustFaceRotation();
        //***need fix calling in update
        if (!photonView.IsMine)
        {
            return;
        }

        _RunnerMovement.Update();
        if (runnerMouse)
            RunnerMouseUpdate();
        //print("Run update");
        if (moveAction.ReadValue<Vector2>() != Vector2.zero)
        {
            Vector2 movement = moveAction.ReadValue<Vector2>();
            
            //print("Run input update: " + movement);
            _RunnerMovement.Move(new Vector2(movement.x, 0));
            if (movement.x > 0)
                face.localScale = Vector3.one;
            else
                face.localScale = new Vector3(-1, 1, 1);
            //photonView.RPC("AdjustScale", RpcTarget.All, movement);
        }

        if (Input.GetKeyDown(KeyCode.E) && inElectric && interactingObject != null)
        {
            if(DetectElectricField())
                MoveAlongElectric();
        }

        if (jumpAction.triggered)
        {
            jump = true;
        }


        if (Input.GetKeyDown(KeyCode.LeftShift))
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
                    photonView.RPC("RPC_HoldBattery", RpcTarget.All);
                    if (inBattery)
                    {
                        print("Getting battery from gate");
                        inBattery = false;
                        Battery battery = holdingObject as Battery;
                        battery.DisconnectFromElectric();
                        Vector3 temp = holdGO.transform.localPosition;
                        holdGO.transform.localPosition = temp.normalized;
                    }
                }
            }
        }

        if (holding && Input.GetKeyUp(KeyCode.LeftShift))
        {
            photonView.RPC("RPC_Release", RpcTarget.All);
            /*
            if (holdingObject is WoodPen)
                photonView.RPC("RPC_ReleaseWood", RpcTarget.All);
            else if (holdingObject is Battery)
                photonView.RPC("RPC_ReleaseBattery", RpcTarget.All);
            */
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

        //face.rotation = Quaternion.Euler(0, 0, -transform.rotation.z);
        //photonView.RPC("AdjustFaceRotation", RpcTarget.All);
    }

    private void FixedUpdate()
    {
        if (jump)
        {
            if (holdingObject != null)
                _RunnerMovement.Jump(jumpForce + extraJumpForce, holdingObject.ValidateHold());
            else
                _RunnerMovement.Jump(jumpForce + extraJumpForce, false);
            //AudioManager.m_photonView.RPC("RPC_PlayOne", RpcTarget.All, AudioManager.JUMPSFX, false);
            //AudioManager.PlayOne(AudioManager.JUMPSFX, false);

            jump = false;
            validHoldJump = false;
        }
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
        face.rotation = Quaternion.Euler(0, 0, -transform.rotation.z);
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
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        runnerMouse.transform.position = mousePos;
    }
    #endregion

    private void InitInput()
    {
        moveAction = _ActionMap.FindAction("Move");
        jumpAction = _ActionMap.FindAction("Jump");
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
        //photonView.RPC("RPC_Release", RpcTarget.All);
        rb.linearVelocity = Vector2.zero;
        //this.transform.position = revivePos;
        photonView.transform.position = revivePos;
        //Below are for fog of war to prevent teleport and clear the pathway
        //photonView.RPC("RPC_Enable", RpcTarget.Others);
        //photonView.RPC("RPC_Disable", RpcTarget.Others);
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

    [PunRPC]
    private void RPC_ReleaseWood()
    {
        ResetAppearance();
        //fixedJoint2D.gameObject.layer = LayerMask.NameToLayer("Draw");
        //_RunnerMovement.SetJumpAllowance(true);
        // TODO: only setting to wood here cuz its the only one that can be hold, might want to change, have a buffer holding the original tag name
        if (holdingObject != null)
        {
            holdingObject.ToggleCollider(true);
            holdingObject.Reset();
            holdingObject = null;

            fixedJoint2D.connectedBody.gameObject.transform.SetParent(null);
            fixedJoint2D.connectedBody = rb;
        }
        validHoldJump = false;
        extraJumpForce = 0;
        holding = false;
    }
    
    
    private bool inBattery = false;

    private void TakeOutBattery(GameObject battery)
    {
        holdingObjectID = battery.GetPhotonView().ViewID;
    }

    [PunRPC]
    private void RPC_HoldBattery()
    {
        holdGO.tag = "Holding";
        holdGO.transform.SetParent(this.transform);
        Rigidbody2D holdingRb = holdGO.GetComponent<Rigidbody2D>();
        //holdGO.GetComponent<WoodPen>().holder = this;
        holdingRb.mass = 1;
        holding = true;
        fixedJoint2D.connectedBody = holdingRb;
    }

    [PunRPC]
    private void RPC_ReleaseBattery()
    {
        ResetAppearance();
        // TODO: only setting to wood here cuz its the only one that can be hold, might want to change, have a buffer holding the original tag name
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
    }
    
    [PunRPC]
    private void RPC_Release()
    {
        ResetAppearance();
        // TODO: only setting to wood here cuz its the only one that can be hold, might want to change, have a buffer holding the original tag name
        if (holdingObject != null && holdGO != null)
        {
            holdingObject.ToggleCollider(true);
            holdingObject.Reset();
            holdingObject = null;

            fixedJoint2D.connectedBody = rb;
        }
        validHoldJump = false;
        extraJumpForce = 0;
        holding = false;
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
        holdingObjectID = -1;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag == "DeathDesuwa")
        {
            //holdingObjectID = other.gameObject.GetPhotonView().ViewID;
            photonView.RPC("RPC_Release", RpcTarget.All);
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