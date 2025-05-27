using UnityEngine;

public class ButtonTrigger : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.tag == "Player" || collision.gameObject.layer == LayerMask.NameToLayer("Draw"))
        {
            print("Button Trigger Pressed");


        }
    }
}
