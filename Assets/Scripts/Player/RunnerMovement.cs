using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunnerMovement
{
    private float speed;
    private int maxSpeed;
    Rigidbody2D rb;
    RaycastHit2D hit;

    public RunnerMovement(Rigidbody2D rg2d, float speed, int maxSpeed)
    {
        rb = rg2d;
        this.speed = speed;
        this.maxSpeed = maxSpeed;
    }

    public void Update()
    {
        GroundDetect();
    }

    public void Move(Vector2 inputVector)
    {
        Vector2 axis = GetMoveAxis().normalized * inputVector;
        rb.velocity = new Vector2(axis.x * speed, axis.y * speed + rb.velocity.y);
    }

    public void Jump(int jumpForce)
    {
        if(GroundDetect())
            rb.velocity= new Vector2(rb.velocity.x, jumpForce);
    }

    private Vector2 GetMoveAxis()
    {
        if(GroundDetect())
        {
            return new Vector2(hit.normal.y, -hit.normal.x);
        }
        return Vector2.right; // Return some default direction if no ground is found
    }

    public bool GroundDetect()
    {
        hit = Physics2D.Raycast(rb.transform.position, Vector2.down, 1f, LayerMask.GetMask("Platform", "Draw")); 
        
        if (hit.collider == null)
        {
            rb.drag = 0;
            return false;
        }

        rb.drag = 5;
        return true;
    }
}
