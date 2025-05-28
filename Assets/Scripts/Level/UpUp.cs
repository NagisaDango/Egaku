using UnityEngine;

public class UpUp : MonoBehaviour
{
    [SerializeField] private float speed;
    
    void Update()
    {
        transform.Translate(Vector3.up * speed * Time.deltaTime);
    }
}
