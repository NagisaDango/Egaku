using System.Collections.Generic;
using UnityEngine;

public class ButtonTrigger : MonoBehaviour
{
    private bool pressed = false;
    public List<ButtonTrigger> otherRequire;
    [SerializeField] public GameObject controlling;
    public LayerMask layerMask;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.CompareTag("Player") || collision.gameObject.layer == layerMask)
        {
            pressed = true;
            if (otherRequire.Count > 0)
            {
                foreach (ButtonTrigger bt in otherRequire)
                {
                    if (bt.pressed == false)
                        return;
                }
            }
            controlling.GetComponent<IControlObj>().Activate();
        }
    }
}
