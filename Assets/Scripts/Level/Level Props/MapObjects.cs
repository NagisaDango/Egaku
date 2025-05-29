using System;
using UnityEngine;

public class MapObjects : MonoBehaviour
{
    [SerializeField] private Vector2 respawnPos;

    private void Start()
    {
        if (respawnPos == Vector2.zero)
        {
            respawnPos = transform.position;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("DeathDesuwa"))
        {
            Rigidbody2D rb;
            this.TryGetComponent<Rigidbody2D>(out rb);
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            this.transform.position = respawnPos;
        }
    }
}
