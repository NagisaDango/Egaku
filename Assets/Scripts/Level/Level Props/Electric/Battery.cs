using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Allan;
using Photon.Pun;
using Unity.VisualScripting;
using UnityEngine;

public class Battery : MonoBehaviourPun, HoldableObject
{
    [SerializeField] private Vector2 originalPos;
    private Collider2D col;
    private Rigidbody2D rb;
    private bool simulatedStatus;
    private Vector2 lastContactPoint;
    private float ogMass;
    private RigidbodyType2D ogType;
    private bool ogSimulated;
    [SerializeField] public float radius;
    [SerializeField] private IElectricControl controlObj; 
    public float distance = 10f; // Define a max distance for visualization
    public LayerMask playerLayer; // Assign this in the Inspector
    private bool ownerTransfered = false;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        originalPos = transform.position;
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        simulatedStatus = rb.simulated;
        ogMass = rb.mass;
        
        if (!PhotonNetwork.OfflineMode && !GameManager.Instance.devSpawn && 
             (RolesManager.PlayerRole)(int)PhotonNetwork.CurrentRoom.CustomProperties["Role_" + PhotonNetwork.LocalPlayer.ActorNumber] != RolesManager.PlayerRole.Runner)
        {
        }
        else
        {
            Debug.Log($"This player ActorNum is {PhotonNetwork.LocalPlayer.ActorNumber}. Runner is {Runner.Instance.actorNum}");
            ownerTransfered = true;
        }
        ogType = rb.bodyType;
        ogSimulated = rb.simulated;
    }

    
    void Update()
    {
        if (!ownerTransfered)
        {
            if (Runner.Instance != null)
            {            
                this.rb.simulated = false;
                rb.bodyType = RigidbodyType2D.Kinematic;
                photonView.TransferOwnership(Runner.Instance.actorNum);
                ogType = rb.bodyType;
                ogSimulated = rb.simulated;
                ownerTransfered = true;
            }
        }
        
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
            //Debug.Log("CircleCast hit: " + hit.collider.name);
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

    public void DisconnectFromElectric()
    {
        Debug.Log(("Out from electric"));
        if(controlObj != null)
            controlObj.BatteryOut();
        rb.bodyType = ogType;
        rb.simulated = ogSimulated;
        col.isTrigger = false;
        controlObj = null;
    }

    public void ConnectToElectric(IElectricControl obj)
    {
        Debug.Log(("Into electric"));
        controlObj = obj;
        rb.bodyType = RigidbodyType2D.Static;
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("DeathDesuwa"))
        {
            this.transform.position = originalPos;
        }
    }

    private void OnCollisionStay2D(Collision2D other)
    {
        lastContactPoint = other.contacts[0].point;
    }
    
    public void Reset()
    {
        this.gameObject.tag = "Battery";
        this.transform.SetParent(null);
        ToggleRbSimulated();
        rb.mass = ogMass;
        rb.bodyType = ogType;
        lastContactPoint = Vector2.negativeInfinity;
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
        rb.simulated = ogSimulated;
    }

    //TODO: Right now can infinite jump
    public bool ValidateHold()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = LayerMask.GetMask("Draw", "Platform");
        List<ContactPoint2D> contactPoints = new List<ContactPoint2D>();

        if (col.GetContacts(filter, contactPoints) > 0)
        {
            foreach (ContactPoint2D point in contactPoints)
            {
                if (point.normal.y > 0.2f)
                {
                    return true;
                }
            }
        }        
        if (lastContactPoint != Vector2.negativeInfinity)
        {
            RaycastHit2D hit = Physics2D.Raycast(lastContactPoint, Vector2.up, 0.5f, LayerMask.GetMask( "Battery"));
            if (hit.collider != null)
            {
                print("Layer " + hit.collider.gameObject.name);
                return true;
            }
        }
        return false;
    }
    
    
}
