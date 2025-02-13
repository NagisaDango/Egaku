using UnityEngine;
using UnityEngine.InputSystem;

public class Runner : MonoBehaviour
{
    private RunnerMovement _RunnerMovement;
    private Rigidbody2D rb;
    public InputActionAsset _ActionMap;
    private InputAction moveAction;
    private InputAction jumpAction;
    public int jumpForce;
    public int maxSpeed;

    private void Start()
    {
        InitInput();
        rb = GetComponent<Rigidbody2D>();
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
