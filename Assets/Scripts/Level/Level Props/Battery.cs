using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

public class Battery : MonoBehaviour, HoldableObject
{
    private Collider2D col;
    private Rigidbody2D rb;
    private bool simulatedStatus;
    private float ogMass;
    [SerializeField] public float radius;
    [SerializeField] private GameObject controlObj; 
    public float distance = 10f; // Define a max distance for visualization
    public LayerMask playerLayer; // Assign this in the Inspector


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        simulatedStatus = rb.simulated;
        ogMass = rb.mass;
    }

    void Update()
    {
        // Your actual CircleCast logic
        RaycastHit2D hit = Physics2D.CircleCast(
            transform.position,
            radius,
            Vector2.zero, // It's good practice to normalize direction vectors
        distance, // Use the defined distance or Mathf.Infinity for your actual cast
            playerLayer // Use the LayerMask variable
        );

        if (hit.collider != null)
        {
            Debug.Log("CircleCast hit: " + hit.collider.name);
        }
    }

    void OnDrawGizmos()
    {
        if (radius <= 0) return; // Don't draw if radius is invalid

        Vector2 direction = Vector2.one.normalized; // Ensure this matches your cast direction
        Vector3 castOrigin = transform.position;

        // Store the original Gizmos color
        Color originalColor = Gizmos.color;

        // --- Draw the initial circle ---
        Gizmos.color = Color.yellow; // Color for the starting circle
        DrawWireDisk(castOrigin, radius, 32);

        // --- Perform a CircleCast to get hit information for Gizmos ---
        // Note: It's generally better to use the actual cast's distance here if it's not Mathf.Infinity
        // For Mathf.Infinity, you'll need to cap the Gizmo drawing distance.
        float gizmoCastDistance = (distance == Mathf.Infinity) ? 100f : distance; // Cap Gizmo line

        RaycastHit2D hitInfo = Physics2D.CircleCast(
            castOrigin,
            radius,
            direction,
            gizmoCastDistance,
            playerLayer // Use the LayerMask variable, or the problematic LayerMask.NameToLayer for direct testing
        );

        if (hitInfo.collider != null)
        {
            // --- Draw the line to the hit point ---
            Gizmos.color = Color.red; // Color for the line to the hit
            Gizmos.DrawLine(castOrigin, hitInfo.point);

            // --- Draw the circle at the hit point ---
            // The center of the circle at the hit point is along the ray,
            // at a distance of hitInfo.distance from the origin, plus the radius pushed back along the normal.
            // More simply, it's the hit centroid.
            Vector3 hitCircleCenter = castOrigin + (Vector3)direction * hitInfo.distance;
            Gizmos.color = Color.red; // Color for the circle at the hit
            DrawWireDisk(hitCircleCenter, radius, 32);

            // --- Draw the remainder of the cast line if it didn't hit at max distance ---
            if (hitInfo.distance < gizmoCastDistance)
            {
                Gizmos.color = Color.green; // Color for the line after hit (if applicable)
                Gizmos.DrawLine(hitCircleCenter, castOrigin + (Vector3)direction * gizmoCastDistance);
            }

        }
        else
        {
            // --- Draw the full cast line if no hit ---
            Gizmos.color = Color.green; // Color for the line if no hit
            Gizmos.DrawLine(castOrigin, castOrigin + (Vector3)direction * gizmoCastDistance);

            // --- Draw the circle at the end of the cast distance if no hit ---
            DrawWireDisk(castOrigin + (Vector3)direction * gizmoCastDistance, radius, 32);
        }

        // Restore the original Gizmos color
        Gizmos.color = originalColor;
    }

    // Helper function to draw a 2D wire disk (circle) for Gizmos
    void DrawWireDisk(Vector3 position, float radius, int segments)
    {
        float angle = 0f;
        Vector3 lastPoint = Vector3.zero;
        Vector3 thisPoint = Vector3.zero;

        for (int i = 0; i < segments + 1; i++)
        {
            thisPoint.x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
            thisPoint.y = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
            thisPoint.z = 0; // Assuming 2D plane, adjust if necessary

            if (i > 0)
            {
                Gizmos.DrawLine(position + lastPoint, position + thisPoint);
            }

            lastPoint = thisPoint;
            angle += 360f / segments;
        }
    }

    
    public void Reset()
    {
        this.gameObject.tag = "Battery";
        this.transform.SetParent(null);
        rb.mass = ogMass;
    }

    public void ToggleCollider(bool status)
    {
        //rb.simulated = status;
        col.enabled = status;
    }

    public void ToggleRbSimulated(bool status)
    {
        rb.simulated = status;
    }

    public void ToggleRbSimulated()
    {
        rb.simulated = simulatedStatus;
    }

    //TODO: Right now can infinite jump
    public bool ValidateHold()
    {
        return true;
    }
}
