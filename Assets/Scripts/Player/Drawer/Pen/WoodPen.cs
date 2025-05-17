using System;
using UnityEngine;

public class WoodPen : MonoBehaviour
{
    public Runner holder;
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

    private void OnCollisionExit2D(Collision2D other)
    {
        if (holder)
        {
            if( other.gameObject.CompareTag("Cloud"))
                holder.SetExtraJumpForce(0);
            holder.validHoldJump = false;
        }
    }
}
