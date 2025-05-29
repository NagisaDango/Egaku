using UnityEngine;

public class RotateCloud : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public float speed = 1.0f;


    // Update is called once per frame
    void FixedUpdate()
    {
        transform.Rotate(Vector3.forward, speed);
    }
}
