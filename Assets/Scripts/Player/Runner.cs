using System;
using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Pun.Demo.PunBasics;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class Runner : MonoBehaviourPunCallbacks
{
    public static Runner Instance;
    public int actorNum;
    private RunnerMovement _RunnerMovement;
    private Rigidbody2D rb;
    private Collider2D col;
    public InputActionAsset _ActionMap;
    private InputAction moveAction;
    private InputAction jumpAction;
    public int jumpForce;
    public int maxSpeed;
    private GameObject runnerMouse;
    private SplineAnimate splineAnimate;
    bool inElectric = false;
    GameObject interactingObject;
    private bool movingAlongElectric;
    private bool reversed = false;
    private Vector2 revivePos;
    private int extraJumpForce;
    public bool validHoldJump;
    [SerializeField] private FixedJoint2D fixedJoint2D;

    public Transform face;
    
    [Header("Appearance")]
    [SerializeField] private SpriteRenderer leftEye;
    [SerializeField] private SpriteRenderer rightEye;
    [SerializeField] private SpriteRenderer mouth;
    [SerializeField] private SpriteRenderer color;
    


    [Tooltip("The local player instance. Use this to know if the local player is represented in the Scene")]
    public static GameObject LocalPlayerInstance;

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
            if(fog != null)
                fog.SetActive(false);
            photonView.RPC("RPC_SetUpAppearance", RpcTarget.AllBuffered, PhotonNetwork.LocalPlayer.CustomProperties["Eyes"],PhotonNetwork.LocalPlayer.CustomProperties["Mouth"],PhotonNetwork.LocalPlayer.CustomProperties["Color"]);
        }

        InitInput();
        _RunnerMovement = new RunnerMovement(rb, 10f, maxSpeed);
    }
    
    
    [PunRPC]
    private void RPC_SetUpAppearance(int eyeType, int mouthType, Vector3 playerColor)
    {
        leftEye.sprite = Resources.Load<Sprite>("Eyes/" + eyeType);
        rightEye.sprite = Resources.Load<Sprite>("Eyes/" + eyeType);
        mouth.sprite = Resources.Load<Sprite>("Mouth/" + mouthType);
        color.color = new Color(playerColor.x, playerColor.y, playerColor.z);
    }

    private void InitInput()
    {
        moveAction = _ActionMap.FindAction("Move");
        jumpAction = _ActionMap.FindAction("Jump");
    }

    private bool jump;

    private void Update()
    {
        //***need fix calling in update
        if (!photonView.IsMine)
        {
            return;
        }

        _RunnerMovement.Update();
        if(runnerMouse)
            RunnerMouseUpdate();
        if (moveAction.ReadValue<Vector2>() != Vector2.zero)
        {
            Vector2 movement = moveAction.ReadValue<Vector2>();
            _RunnerMovement.Move(movement);
            if (movement.x > 0)
                face.localScale = Vector3.one;
            else
                face.localScale = new Vector3(-1, 1, 1);
        }

        if (Input.GetKeyDown(KeyCode.E) && inElectric && interactingObject != null)
        {
            MoveAlongElectric();
        }

        if (jumpAction.triggered)
        {
            jump = true;
        }
        
        
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            if(holdingObjectID != -1)
                photonView.RPC("RPC_HoldWood", RpcTarget.All, holdingObjectID);
        }

        if (holding && Input.GetKeyUp(KeyCode.LeftShift))
        {
            photonView.RPC("RPC_ReleaseWood", RpcTarget.All);
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

        face.rotation = Quaternion.Euler(0, 0, -transform.rotation.z);
    }

    private void RunnerMouseUpdate()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        runnerMouse.transform.position = mousePos;
    }

    private void FixedUpdate()
    {
        if (jump)
        {
            if(holdingObject!=null)
                _RunnerMovement.Jump(jumpForce + extraJumpForce, holdingObject.ValidateHold());
            else
                _RunnerMovement.Jump(jumpForce + extraJumpForce, false);
            //AudioManager.m_photonView.RPC("RPC_PlayOne", RpcTarget.All, AudioManager.JUMPSFX, false);
            //AudioManager.PlayOne(AudioManager.JUMPSFX, false);
            
            jump = false;
            validHoldJump = false;
        }
    }

    private void MoveAlongElectric()
    {
        if(movingAlongElectric) return;
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

    public void SetRevivePos(Vector2 pos)
    {
        revivePos = pos;
    }

    
    private void Revive()
    {
        photonView.RPC("RPC_ReleaseWood", RpcTarget.All);
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

    private bool holding;
    private HoldableObject holdingObject;
    [PunRPC]
    private void RPC_HoldWood(int viewID)
    {
        if (viewID != -1)
        {
            //_RunnerMovement.SetJumpAllowance(false);
            GameObject holdGO = PhotonView.Find(viewID).gameObject;
            holdingObject = holdGO.GetComponent<HoldableObject>();
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
        //fixedJoint2D.gameObject.layer = LayerMask.NameToLayer("Draw");
        _RunnerMovement.SetJumpAllowance(true);
        // TODO: only setting to wood here cuz its the only one that can be hold, might want to change, have a buffer holding the original tag name
        if (holdingObject != null)
        {
            holdingObject.ToggleCollider(true);
            holdingObject.Reset();
            holdingObject = null;
            
            fixedJoint2D.connectedBody.gameObject.tag = "Wood";
            fixedJoint2D.connectedBody.gameObject.GetComponent<WoodPen>().holder = null;
            fixedJoint2D.connectedBody.mass = 20;
            fixedJoint2D.connectedBody.gameObject.transform.SetParent(null);
            fixedJoint2D.connectedBody = rb;
        }
        validHoldJump = false;
        extraJumpForce = 0;
        holding = false;
    }
    private int holdingObjectID;
    private void OnCollisionStay2D(Collision2D other)
    {
        if (other.gameObject.tag == "Wood")
        {
            holdingObjectID = other.gameObject.GetPhotonView().ViewID;
        }
    }

    public void SetExtraJumpForce(int _extraJumpForce)
    {
        extraJumpForce = _extraJumpForce;
    }

    private void OnCollisionExit2D(Collision2D other)
    {
        holdingObjectID = -1;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag == "DeathDesuwa")
        {
            Revive();
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.tag.StartsWith("Electric") && !other.CompareTag("Electric"))
        {
            inElectric = true;
            interactingObject = other.gameObject;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.tag.StartsWith("Electric") && !movingAlongElectric)
        {
            inElectric = false;
            interactingObject = null;
        }
    }
}