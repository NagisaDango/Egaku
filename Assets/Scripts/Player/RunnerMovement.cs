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
    // Allow a failed takeoff to recover only after the solver has had time to separate contacts.
    [Min(0.02f)] public float blockedJumpRecoveryTime = 0.12f;
    [Min(0f)] public float pushAcceleration = 35f;
    [Min(0f)] public float pushMaxSpeed = 6f;
    [Min(0f)] public float pushMaxForce = 1500f;
}

[Serializable]
public sealed class RunnerGrabTuning
{
    // These values affect only the local Runner's held movement, never object mass or Photon state.
    public bool enabled = true;
    [Min(0f)] public float maxSpeed = 8f;
    [Min(0f)] public float groundAcceleration = 60f;
    [Min(0f)] public float groundBraking = 90f;
    [Min(0f)] public float groundTurnAcceleration = 90f;
    [Range(0f, 1f)] public float airControlMultiplier = 0.7f;
    [Min(0f)] public float transitionTime = 0.1f;
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
    private float takeoffTime;
    private Rigidbody2D groundBody;
    private readonly RunnerGrabTuning grabTuning;
    private float grabBlend;
    // Preserve the load's native water response; matching Runner gravity is for dry jumps only.
    private Rigidbody2D gravityLoad;
    private float loadGravity;
    private bool supportHasContact;
    public bool Grounded { get; private set; }
    public float TargetSpeed { get; private set; }
    public bool Blocked { get; private set; }

    public RunnerMovement(Rigidbody2D rb, Collider2D playerCollider, RunnerMovementTuning tuning,
        RunnerGrabTuning grabTuning = null)
    {
        this.rb = rb;
        this.playerCollider = playerCollider;
        this.tuning = tuning;
        this.grabTuning = grabTuning ?? new RunnerGrabTuning();
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
        grabBlend = 0f;
        gravityLoad = null;
        rb.gravityScale = normalGravityScale;
    }

    public bool FixedStep(float horizontalInput, float time, float maxSpeed, int jumpForce,
        Rigidbody2D heldBody = null)
    {
        if (heldBody != gravityLoad)
        {
            gravityLoad = heldBody;
            if (heldBody != null) loadGravity = heldBody.gravityScale;
        }
        bool jumped = false;
        int groundBonus;
        Vector2 groundNormal;
        bool grounded = DetectGround(heldBody, out groundBonus, out groundNormal);
        // One support rule for all surfaces; releasing a body immediately makes it
        // eligible again. Only the currently connected load needs external support.
        bool supported = grounded;
        Grounded = supported;
        if (supported)
        {
            lastGroundedTime = time;
            lastGroundJumpBonus = groundBonus;
            // A jump can still touch the floor for one physics step after takeoff.
            // A low ceiling can prevent *any* loss of support. Recover on settled
            // external support, never at an airborne apex or from our carried body.
            float supportY = groundBody != null ? groundBody.GetPointVelocity(rb.position).y : 0f;
            bool settledOnSupport = rb.linearVelocity.y - supportY <= 0.1f;
            bool blockedTakeoff = waitingToLeaveGround && time - takeoffTime >= tuning.blockedJumpRecoveryTime &&
                settledOnSupport &&
                supportHasContact;
            // A body within probe distance is not a landing while Runner is still
            // separating upward from it. Rising platforms are valid at equal velocity.
            if ((!waitingToLeaveGround && settledOnSupport) || blockedTakeoff)
            {
                waitingToLeaveGround = false;
                jumpConsumed = false;
                jumpStarted = false;
            }
        }
        else
        {
            waitingToLeaveGround = false;
        }

        bool inCoyoteTime = time - lastGroundedTime <= tuning.coyoteTime;
        if (HasBufferedJump(time) && !jumpConsumed && inCoyoteTime)
        {
            // Keep cloud's bonus during the coyote window, but never after that window expires.
            int cloudBonus = inCoyoteTime ? lastGroundJumpBonus : 0;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce + cloudBonus);
            // Launch the connected pair together instead of asking the joint to lift
            // a stationary load, which changes takeoff speed with grab direction.
            if (heldBody != null && heldBody.simulated)
                heldBody.linearVelocity = new Vector2(heldBody.linearVelocity.x, rb.linearVelocity.y);
            takeoffTime = time;
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
                if (heldBody != null && heldBody.simulated)
                    heldBody.linearVelocity = new Vector2(heldBody.linearVelocity.x, rb.linearVelocity.y);
                releasePending = false;
            }
            else if (!HasBufferedJump(time))
            {
                releasePending = false;
            }
        }

        // Preserve vertical velocity from jumping, buoyancy, platforms and collisions.
        float input = Mathf.Clamp(horizontalInput, -1f, 1f);
        bool grabbing = heldBody != null && grabTuning.enabled;
        grabBlend = Mathf.MoveTowards(grabBlend, grabbing ? 1f : 0f,
            grabTuning.transitionTime > 0f ? Time.fixedDeltaTime / grabTuning.transitionTime : 1f);
        float targetSpeed = input * Mathf.Lerp(maxSpeed, grabTuning.maxSpeed, grabBlend);
        float currentSpeed = rb.linearVelocity.x;
        float acceleration;
        if (Mathf.Abs(input) < 0.001f)
            acceleration = grounded ? tuning.groundBraking : tuning.airBraking;
        else if (currentSpeed * input < 0f)
            acceleration = grounded ? tuning.groundTurnAcceleration : tuning.airTurnAcceleration;
        else
            acceleration = grounded ? tuning.groundAcceleration : tuning.airAcceleration;

        float heldAcceleration = Mathf.Abs(input) < 0.001f
            ? (supported ? grabTuning.groundBraking : tuning.airBraking * grabTuning.airControlMultiplier)
            : currentSpeed * input < 0f
                ? (supported ? grabTuning.groundTurnAcceleration : tuning.airTurnAcceleration * grabTuning.airControlMultiplier)
                : (supported ? grabTuning.groundAcceleration : tuning.airAcceleration * grabTuning.airControlMultiplier);
        acceleration = Mathf.Lerp(acceleration, heldAcceleration, grabBlend);
        // Do not repeatedly drive a rigid joint into a wall. Only the blocked direction
        // is suppressed, so reversing out remains responsive without auto-release.
        Blocked = grabbing && (GrabPhysics.BlocksDirection(rb, heldBody, input) ||
                               GrabPhysics.BlocksDirection(heldBody, rb, input));
        if (Blocked) targetSpeed = 0f;
        TargetSpeed = targetSpeed;

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

        // Accelerate the connected load by the same motor delta without reducing its
        // mass/density. Preserve collision and water velocities rather than overwriting them.
        if (heldBody != null && heldBody.simulated)
            heldBody.AddForce(Vector2.right * ((nextX - currentSpeed) * heldBody.mass), ForceMode2D.Impulse);
        rb.linearVelocity = new Vector2(nextX, nextY);
        rb.gravityScale = rb.linearVelocity.y < 0f
            ? normalGravityScale * tuning.fallGravityMultiplier
            : normalGravityScale;
        // BuoyancyEffector2D also responds to the body's gravity scale. Applying the
        // Runner's 5x gravity to wet wood amplifies its water forces through the joint.
        if (heldBody != null && heldBody.simulated)
            heldBody.gravityScale = GrabPhysics.InBuoyancy(heldBody) ? loadGravity : rb.gravityScale;
        if (heldBody == null && grounded)
            GrabPhysics.PushWood(rb, input, tuning.pushAcceleration, tuning.pushMaxSpeed, tuning.pushMaxForce);
        return jumped;
    }

    private bool DetectGround(Rigidbody2D heldBody, out int jumpBonus, out Vector2 groundNormal)
    {
        jumpBonus = 0;
        groundNormal = Vector2.up;
        groundBody = null;
        supportHasContact = false;
        int hitCount = playerCollider.Cast(Vector2.down, groundFilter, groundHits, tuning.groundProbeDistance);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D surface = groundHits[i].collider;
            if (surface == null || surface.isTrigger ||
                groundHits[i].normal.y < MinimumGroundNormalY)
                continue;
            Rigidbody2D surfaceBody = surface.attachedRigidbody;
            if (surfaceBody != null && surfaceBody == heldBody &&
                !GrabPhysics.HasExternalSupport(surfaceBody, rb))
                continue;

            jumpBonus = surface.CompareTag("Cloud") ? CloudJumpBonus : 0;
            groundNormal = groundHits[i].normal;
            groundBody = surfaceBody;
            // Proximity permits ordinary landing/coyote behavior, but failed takeoff
            // recovery requires real foot contact, measured against this same surface.
            supportHasContact = playerCollider.IsTouching(surface) ||
                (surfaceBody != null && surfaceBody == heldBody && GrabPhysics.HasExternalSupport(surfaceBody, rb));
            return true;
        }

        return false;
    }
}
