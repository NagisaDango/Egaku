using System;
using UnityEngine;

[Serializable]
public sealed class RunnerMovementTuning
{
    [Min(0f)] public float groundAcceleration = 70f;
    [Min(0f)] public float groundBraking = 100f;
    [Min(0f)] public float groundTurnAcceleration = 110f;
    [Min(0f)] public float airAcceleration = 35f;
    [Min(0f)] public float airBraking = 45f;
    [Min(0f)] public float airTurnAcceleration = 45f;
    [Min(0f)] public float groundProbeDistance = 0.08f;
    [Range(0f, 0.5f)] public float coyoteTime = 0.1f;
    [Range(0f, 0.5f)] public float jumpBufferTime = 0.1f;
    [Range(0f, 1f)] public float jumpReleaseMultiplier = 0.5f;
    [Min(1f)] public float fallGravityMultiplier = 1.5f;
}

// Runner owns this controller locally; remote copies follow the existing Photon observed components.
public sealed class RunnerMovement
{
    // Drawn polygon segments can briefly exceed 60 degrees near a curved ramp's ends.
    private const float MinimumGroundNormalY = 0.4f;
    private const int CloudJumpBonus = 10;

    private readonly Rigidbody2D rb;
    private readonly Collider2D playerCollider;
    private readonly RunnerMovementTuning tuning;
    private readonly ContactFilter2D groundFilter;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
    private readonly float normalGravityScale;

    private float lastGroundedTime = float.NegativeInfinity;
    private float bufferedJumpUntil = float.NegativeInfinity;
    private int lastGroundJumpBonus;
    private bool jumpConsumed;
    private bool waitingToLeaveGround;
    private bool jumpStarted;
    private bool releasePending;

    public RunnerMovement(Rigidbody2D rb, Collider2D playerCollider, RunnerMovementTuning tuning)
    {
        this.rb = rb;
        this.playerCollider = playerCollider;
        this.tuning = tuning;
        normalGravityScale = rb.gravityScale;

        // Only collidable gameplay surfaces may renew grounded state; visual and trigger areas cannot.
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(LayerMask.GetMask("Platform", "Draw", "Battery"));
        groundFilter.useTriggers = false;
    }

    public void QueueJump(float time)
    {
        bufferedJumpUntil = time + tuning.jumpBufferTime;
        releasePending = false;
    }

    public void ReleaseJump()
    {
        // A press and release between physics steps must still create a short buffered jump.
        releasePending = true;
    }

    public bool HasBufferedJump(float time)
    {
        return time <= bufferedJumpUntil;
    }

    public void ClearBufferedInput()
    {
        bufferedJumpUntil = float.NegativeInfinity;
        releasePending = false;
    }

    public void ResetAfterRespawn()
    {
        ClearBufferedInput();
        lastGroundedTime = float.NegativeInfinity;
        lastGroundJumpBonus = 0;
        jumpConsumed = false;
        waitingToLeaveGround = false;
        jumpStarted = false;
        rb.gravityScale = normalGravityScale;
    }

    public bool FixedStep(float horizontalInput, float time, float maxSpeed, int jumpForce, bool directJumpAllowance)
    {
        bool jumped = false;
        int groundBonus;
        Vector2 groundNormal;
        bool grounded = DetectGround(out groundBonus, out groundNormal);
        if (grounded)
        {
            lastGroundedTime = time;
            lastGroundJumpBonus = groundBonus;
            // A jump can still touch the floor for one physics step after takeoff.
            if (!waitingToLeaveGround)
            {
                jumpConsumed = false;
                jumpStarted = false;
            }
        }
        else
        {
            waitingToLeaveGround = false;
        }

        bool inCoyoteTime = time - lastGroundedTime <= tuning.coyoteTime;
        if (HasBufferedJump(time) && !jumpConsumed && (inCoyoteTime || directJumpAllowance))
        {
            // Keep cloud's bonus during the coyote window, but never after that window expires.
            int cloudBonus = inCoyoteTime ? lastGroundJumpBonus : 0;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce + cloudBonus);
            bufferedJumpUntil = float.NegativeInfinity;
            jumpConsumed = true;
            waitingToLeaveGround = true;
            jumpStarted = true;
            jumped = true;
        }

        if (releasePending)
        {
            if (jumpStarted && rb.linearVelocity.y > 0f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x,
                    rb.linearVelocity.y * tuning.jumpReleaseMultiplier);
                releasePending = false;
            }
            else if (!HasBufferedJump(time))
            {
                releasePending = false;
            }
        }

        // Preserve vertical velocity from jumping, buoyancy, platforms and collisions.
        float input = Mathf.Clamp(horizontalInput, -1f, 1f);
        float targetSpeed = input * maxSpeed;
        float currentSpeed = rb.linearVelocity.x;
        float acceleration;
        if (Mathf.Abs(input) < 0.001f)
            acceleration = grounded ? tuning.groundBraking : tuning.airBraking;
        else if (currentSpeed * input < 0f)
            acceleration = grounded ? tuning.groundTurnAcceleration : tuning.airTurnAcceleration;
        else
            acceleration = grounded ? tuning.groundAcceleration : tuning.airAcceleration;

        float nextX = Mathf.MoveTowards(currentSpeed, targetSpeed,
            acceleration * Time.fixedDeltaTime);
        float nextY = rb.linearVelocity.y;
        if (grounded && !jumped && !jumpStarted && Mathf.Abs(input) >= 0.001f)
        {
            // Horizontal-only velocity pushes the box into a drawn ramp. Follow its
            // uphill tangent while grounded, without suppressing a jump or a faster lift.
            float uphillY = -nextX * groundNormal.x / groundNormal.y;
            if (uphillY > nextY && uphillY > 0f)
                nextY = uphillY;
        }

        rb.linearVelocity = new Vector2(nextX, nextY);
        rb.gravityScale = rb.linearVelocity.y < 0f
            ? normalGravityScale * tuning.fallGravityMultiplier
            : normalGravityScale;
        return jumped;
    }

    private bool DetectGround(out int jumpBonus, out Vector2 groundNormal)
    {
        jumpBonus = 0;
        groundNormal = Vector2.up;
        int hitCount = playerCollider.Cast(Vector2.down, groundFilter, groundHits, tuning.groundProbeDistance);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D surface = groundHits[i].collider;
            if (surface == null || surface.isTrigger || surface.CompareTag("Holding") ||
                groundHits[i].normal.y < MinimumGroundNormalY)
                continue;

            jumpBonus = surface.CompareTag("Cloud") ? CloudJumpBonus : 0;
            groundNormal = groundHits[i].normal;
            return true;
        }

        return false;
    }
}
