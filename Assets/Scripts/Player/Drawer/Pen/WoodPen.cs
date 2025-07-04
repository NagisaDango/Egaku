using System;
using System.Collections.Generic;
using UnityEngine;

public class WoodPen : MonoBehaviour, HoldableObject
{
    private Collider2D col;
    private Rigidbody2D rb;
    private Vector2 lastContactPoint { get; set; }
    public Runner holder;
    private HoldableObject _holdableObjectImplementation;
    private bool simulatedStatus;
    
    /*
    private void OnCollisionStay2D(Collision2D other)
    {
        if (holder)
        {
            if(other.gameObject.CompareTag("Cloud"))
                holder.SetExtraJumpForce(15);
            print("Layer " + LayerMask.LayerToName(other.gameObject.layer));
            if (LayerMask.LayerToName(other.gameObject.layer) == "Draw" || LayerMask.LayerToName(other.gameObject.layer) == "Platform")
            {
                ContactPoint2D contact = other.contacts[0];
                lastContactPoint = contact;
                Vector3 direction =  contact.normal;
                print("Hold noraml: " + direction);
                if (direction.y > 0.2f)
                {
                    holder.validHoldJump = true;
                }
                else
                {
                    holder.validHoldJump = false;
                }
            }
        }
    }
    */
    // 檢測holder跳躍狀態，如果是因為跳躍的離開則不檢測
    private void OnCollisionStay2D(Collision2D other)
    {
        lastContactPoint = other.contacts[0].point;
    }

    public void Reset()
    {
        if(holder)
            holder.HoldingObjLost();
        holder = null;
        this.gameObject.tag = "Wood";
        rb.mass = 20;
        this.transform.SetParent(null);
        lastContactPoint = Vector2.negativeInfinity;
        rb.simulated = simulatedStatus;
    }

    private void OnDestroy()
    {
        if(holder)
            holder.HoldingObjLost();
    }

    private void Start()
    {
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        simulatedStatus = rb.simulated;
    }

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
                    holder.SetExtraJumpForce(15);
                    return true;
                }
            }
        }

        if (lastContactPoint != Vector2.negativeInfinity)
        {
            RaycastHit2D hit = Physics2D.Raycast(lastContactPoint, Vector2.up, 0.5f, LayerMask.GetMask( "Draw"));
            if (hit.collider != null)
            {
                print("Layer " + hit.collider.gameObject.name);
                if (holder && hit.collider.gameObject == this.gameObject)
                {
                    holder.SetExtraJumpForce(15);
                    return true;
                }
            }
        }
        
        holder.SetExtraJumpForce(0);
        holder.validHoldJump = false;
        return false;
    }

    public void ToggleCollider(bool status)
    {
        if(col)
            col.enabled = status;
    }

    public void ToggleRbSimulated(bool status)
    {
        if(rb)
            rb.simulated = status;
    }

    public void ToggleRbSimulated()
    {
        rb.simulated = simulatedStatus;
    }
}
