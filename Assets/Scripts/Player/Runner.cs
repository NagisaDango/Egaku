using System;
using System.Collections;
using Photon.Pun;
using Photon.Pun.Demo.PunBasics;
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
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (!photonView.IsMine)
        {
            Debug.Log("this player is not the runner, setting the rb to non physic");
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
        }

        InitInput();
        _RunnerMovement = new RunnerMovement(rb, 10f, maxSpeed);
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
        }

        if (Input.GetKeyDown(KeyCode.E) && inElectric && interactingObject != null)
        {
            MoveAlongElectric();
        }

        if (jumpAction.triggered)
        {
            jump = true;
        }

        if (splineAnimate != null && splineAnimate.NormalizedTime >= 1)
        {
            col.enabled = true;
            rb.simulated = true;
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
            _RunnerMovement.Jump(jumpForce);
            jump = false;
        }
    }

    private void MoveAlongElectric()
    {
        print("start electric");
        movingAlongElectric = true;
        splineAnimate = interactingObject.transform.parent.GetChild(0).GetComponent<SplineAnimate>();
        ElectricSpline splinePoints = interactingObject.transform.parent.GetComponent<ElectricSpline>();
        photonView.RPC("RPC_SetParent", RpcTarget.All,
            interactingObject.transform.parent.GetChild(0).gameObject.GetComponent<PhotonView>().ViewID);
        transform.localPosition = Vector3.zero;
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
        Spline spline = new Spline();
        if (interactingObject.tag.EndsWith("End"))
        {
            splineAnimate.Container.ReverseFlow(0);
            reversed = true;
        }
        splineAnimate.Restart(true);
    }

    public void SetRevivePos(Vector2 pos)
    {
        revivePos = pos;
    }

    private void Revive()
    {
        rb.linearVelocity = Vector2.zero;
        this.transform.position = revivePos;
    }
    
    [PunRPC]
    private void RPC_SetParent(int viewID)
    {
        if (viewID != -1) transform.SetParent(PhotonView.Find(viewID).transform, false);
        else transform.SetParent(null);
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
        if (other.tag.StartsWith("Electric"))
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