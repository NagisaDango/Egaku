using System;
using Photon.Pun;
using Photon.Pun.Demo.PunBasics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class Runner : MonoBehaviourPunCallbacks
{
    public static Runner Instance;
    public int actorNum;
    private RunnerMovement _RunnerMovement;
    private Rigidbody2D rb;
    public InputActionAsset _ActionMap;
    private InputAction moveAction;
    private InputAction jumpAction;
    public int jumpForce;
    public int maxSpeed;
    private GameObject runnerMouse;

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
        if (!photonView.IsMine)
        {
            Debug.Log("this player is not the runner, setting the rb to non physic");
            rb.isKinematic = true;  // Stop physics interactions
            rb.simulated = false;   // Turn off physics on non-owners
        }
        else
        {
            runnerMouse = PhotonNetwork.Instantiate("RunnerMouse", Camera.main.ScreenToWorldPoint(Input.mousePosition), Quaternion.identity);
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
        RunnerMouseUpdate();
        if (moveAction.ReadValue<Vector2>() != Vector2.zero)
        {
            Vector2 movement = moveAction.ReadValue<Vector2>();
            _RunnerMovement.Move(movement);
        }

        if (jumpAction.triggered)
        {
            jump = true;
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
        if(jump)
        {
            _RunnerMovement.Jump(jumpForce);
            jump = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag.StartsWith("Electric"))
        {
            if (true)//Input.GetKeyDown(KeyCode.E))
            {
                print("start electric");
                SplineAnimate splineAnimate = other.transform.parent.GetChild(0).GetComponent<SplineAnimate>();
                this.transform.SetParent(splineAnimate.gameObject.transform, false);
                if (splineAnimate == null) { Debug.LogWarning("No apline anim"); return; }

                SplineContainer splineContainer = splineAnimate.Container;
                if (splineContainer == null || splineContainer.Splines.Count == 0)
                {
                    Debug.LogWarning("No splines found in the container.");
                    return;
                }

                if (other.tag.EndsWith("Begin"))
                {
                    this.rb.simulated = false;
                    splineAnimate.Container.Spline = splineAnimate.Container.Splines[0];
                }
                else
                {
                    splineAnimate.Container.Spline = splineAnimate.Container.Splines[1];
                }
                splineAnimate.Play();
            }
        }
    }
}
