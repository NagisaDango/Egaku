using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

public class RunnerMovement
{
    private float speed;
    private int maxSpeed;
    private bool allowJump = true;
    Rigidbody2D rb;
    RaycastHit2D hit;
    private int extraJump;
    private float lastGroundTime;
    private float jumpBufferTime = 0.1f;

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
        //Debug.Log("Moving");
        Vector2 axis = GetMoveAxis().normalized * inputVector;
        rb.linearVelocity = new Vector2(axis.x * speed, axis.y * speed + rb.linearVelocity.y);
    }

    public void Jump(int jumpForce, bool directAllowance)
    {
        if (directAllowance || GroundDetect() || lastGroundTime > Time.time - jumpBufferTime)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce + extraJump);
            PhotonNetwork.RaiseEvent(AudioManager.PlayAudioEventCode, new object[] { AudioManager.JUMPSFX, false },
                new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
            PhotonNetwork.Instantiate("JumpVFX",rb.transform.position - new Vector3(0,0.6f,5), Quaternion.identity);
        }
    }

    private Vector2 GetMoveAxis()
    {
        if(GroundDetect())
        {
            return new Vector2(hit.normal.y, -hit.normal.x);
        }
        return Vector2.right; // Return some default direction if no ground is found
    }

    public void SetJumpAllowance(bool allow)
    {
        allowJump = allow;
    }
    
    public bool GroundDetect()
    {
        
        hit = Physics2D.Raycast(rb.transform.position - new Vector3(0,0.5f,0), Vector2.down, 1f, LayerMask.GetMask("Platform"));
        if (hit.collider != null)
        {
            rb.linearDamping = 5;
            lastGroundTime = Time.time;
            extraJump = 0;
            return true;
        }
        
        hit = Physics2D.Raycast(rb.transform.position - new Vector3(0,0.5f,0), Vector2.down, 1f, LayerMask.GetMask("Draw", "Battery")); 
        //RaycastHit2D[] hitAll = new RaycastHit2D[10];
        //var size = Physics2D.RaycastNonAlloc(rb.transform.position, Vector2.down, hitAll, 1.5f, LayerMask.GetMask("Platform", "Draw", "Battery")); 
        if (hit.collider == null || (hit.collider.isTrigger && !hit.collider.gameObject.CompareTag("Holding")))
        {
            rb.linearDamping = 0;
            return false;
        }
        if (hit.collider.gameObject.CompareTag("Holding"))
        {
            return false;
        }

        if (hit.collider.gameObject.CompareTag("Cloud"))
            extraJump = 10;
        else
            extraJump = 0;

        
        rb.linearDamping = 5;
        lastGroundTime = Time.time;
        return true;
    }
}
