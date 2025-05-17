using System;
using UnityEngine;

public class CloudFloat : MonoBehaviour
{
    public float amplitude = 0.5f; // 摆动幅度（上下移动的最大距离）
    public float speed = 2.0f;     // 摆动速度（控制摆动的快慢）
    public float bounceMagnitude = 50.0f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position; // 记录物体的起始世界位置
    }

    void Update()
    {
        // Mathf.Sin(时间 * 速度) 会在 -1 到 1 之间平滑变化
        // 乘以 amplitude 得到 Y 轴的偏移量
        float yOffset = Mathf.Sin(Time.time * speed) * amplitude;

        // 更新物体的位置，只改变 Y 轴
        transform.position = new Vector3(startPos.x, startPos.y + yOffset, startPos.z);
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if ((other.gameObject.tag == "Steel" || other.gameObject.tag == "Wood"))
        {
            Rigidbody2D rb = other.gameObject.GetComponent<Rigidbody2D>();
            //If the object is being hold by the player
            if (rb.mass < 5.0f)
            {
                return;
            }
            ContactPoint2D contact = other.contacts[0];
            Vector3 bounceDirection = - contact.normal;
            
            //Only bounce if going up
            if(bounceDirection.y > 0.15f)
                rb.AddForce(bounceDirection * bounceMagnitude, ForceMode2D.Impulse);
        }
    }
}
