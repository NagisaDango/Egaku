using Photon.Pun;
using Photon.Pun.Demo.PunBasics;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private void FixedUpdate()
    {
        if(jump)
        {
            _RunnerMovement.Jump(jumpForce);
            jump = false;
        }
    }
}
