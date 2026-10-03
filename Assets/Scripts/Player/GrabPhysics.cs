using UnityEngine;

// Queries are owner-side only. Passive Photon bodies are deliberately absent from Physics2D.
public static class GrabPhysics
{
    private static readonly ContactPoint2D[] contacts = new ContactPoint2D[64];
    private static readonly ContactPoint2D[] pushContacts = new ContactPoint2D[64];
    private static readonly System.Collections.Generic.List<Collider2D> attached = new System.Collections.Generic.List<Collider2D>();
    private static readonly System.Collections.Generic.List<Collider2D> overlaps = new System.Collections.Generic.List<Collider2D>();

    public static bool InBuoyancy(Rigidbody2D body)
    {
        if (body == null || !body.simulated) return false;
        body.GetAttachedColliders(attached);
        var filter = new ContactFilter2D { useTriggers = true };
        foreach (var collider in attached)
        {
            collider.Overlap(filter, overlaps);
            foreach (var other in overlaps)
            {
                var effector = other.GetComponent<BuoyancyEffector2D>();
                if (other.usedByEffector && effector != null && effector.isActiveAndEnabled &&
                    (!effector.useColliderMask || (effector.colliderMask & (1 << collider.gameObject.layer)) != 0))
                    return true;
            }
        }
        return false;
    }
    private static readonly System.Collections.Generic.HashSet<Rigidbody2D> pushed = new System.Collections.Generic.HashSet<Rigidbody2D>();

    // Called only by the local Runner motor. Acceleration compensation makes heavy
    // wood movable without changing its mass, teleporting it or pushing through walls.
    public static void PushWood(Rigidbody2D runner, float input, float acceleration, float maxSpeed, float maxForce)
    {
        if (Mathf.Abs(input) < 0.001f) return;
        pushed.Clear();
        int count = runner.GetContacts(pushContacts);
        for (int i = 0; i < count; i++)
        {
            var contact = pushContacts[i];
            Collider2D other = OtherCollider(contact, runner);
            Rigidbody2D wood = other != null ? other.attachedRigidbody : null;
            if (wood == null || !wood.simulated || wood.bodyType != RigidbodyType2D.Dynamic ||
                !wood.CompareTag("Wood") || contact.normal.y >= 0.7f ||
                contact.normal.x * Mathf.Sign(input) >= -0.1f || !pushed.Add(wood)) continue;
            // An arch's inner surface pushes down and sideways, not horizontally.
            // Accept its opposing horizontal component while excluding supporting floors
            // and a flat ceiling (which has no horizontal obstruction to push against).
            var view = wood.GetComponent<Photon.Pun.PhotonView>();
            if (view != null && !view.IsMine) continue;
            if (BlocksDirection(wood, runner, input)) continue;
            float desired = Mathf.Abs(input) * maxSpeed;
            float missingSpeed = desired - wood.linearVelocity.x * Mathf.Sign(input);
            if (missingSpeed <= 0f) continue;
            float force = Mathf.Min(maxForce, wood.mass * Mathf.Min(acceleration, missingSpeed / Time.fixedDeltaTime));
            wood.AddForce(Vector2.right * (Mathf.Sign(input) * force));
        }
    }

    public static bool HasExternalSupport(Rigidbody2D body, Rigidbody2D runner)
    {
        if (body == null || !body.simulated) return false;
        int count = body.GetContacts(contacts);
        int layers = LayerMask.GetMask("Platform", "Draw", "Battery");
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = contacts[i];
            Collider2D other = OtherCollider(contact, body);
            if (other == null || other.isTrigger || (runner != null && other.attachedRigidbody == runner) ||
                other.attachedRigidbody == body || other.CompareTag("Holding") ||
                (layers & (1 << other.gameObject.layer)) == 0) continue;
            // Contact normals returned for this body point out of its supporting surface.
            // No saved contact point/raycast fallback: those can hit the carried board itself.
            if (contact.normal.y >= 0.4f) return true;
        }
        return false;
    }

    public static bool BlocksDirection(Rigidbody2D body, Rigidbody2D partner, float input)
    {
        if (body == null || !body.simulated || Mathf.Abs(input) < 0.001f) return false;
        int count = body.GetContacts(contacts);
        for (int i = 0; i < count; i++)
        {
            Collider2D other = OtherCollider(contacts[i], body);
            if (other == null || other.isTrigger || other.attachedRigidbody == partner) continue;
            // Floors and usable ramps must not be mistaken for walls. Moving bodies
            // remain pushable; only static/kinematic obstructions suppress the motor.
            if (other.attachedRigidbody != null && other.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
            if (Mathf.Abs(contacts[i].normal.y) < 0.4f && contacts[i].normal.x * Mathf.Sign(input) < -0.5f)
                return true;
        }
        return false;
    }

    private static Collider2D OtherCollider(ContactPoint2D contact, Rigidbody2D body)
    {
        return contact.collider != null && contact.collider.attachedRigidbody == body
            ? contact.otherCollider : contact.collider;
    }
}
