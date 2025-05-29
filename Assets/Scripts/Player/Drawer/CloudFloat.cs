using System;
using Photon.Pun;
using UnityEngine;

public class CloudFloat : MonoBehaviour
{
    public float amplitude = 0.5f; // 摆动幅度（上下移动的最大距离）
    public float speed = 2.0f;     // 摆动速度（控制摆动的快慢）
    public float bounceMagnitude = 200.0f;
    public float bounceMagnitudeSteel = 500.0f;


    private Vector3 startPos;
    private PhotonView photonView;
    private float timeSinceLastValidSurface = 0f;

    void Start()
    {
        photonView = GetComponent<PhotonView>();
        startPos = transform.position; // 记录物体的起始世界位置
    }

    void Update()
    {
        if (photonView.IsMine)
        {
            // Mathf.Sin(时间 * 速度) 会在 -1 到 1 之间平滑变化
            // 乘以 amplitude 得到 Y 轴的偏移量
            float yOffset = Mathf.Sin(Time.time * speed) * amplitude;

            // 更新物体的位置，只改变 Y 轴
            transform.position = new Vector3(startPos.x, startPos.y + yOffset, startPos.z);
            
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if ((other.gameObject.tag == "Steel" || other.gameObject.tag == "Wood" || other.gameObject.tag == "Bullet" || other.gameObject.tag == "Battery") && photonView.IsMine)
        {
            print(other.gameObject.tag  + " bouce tag");
            Rigidbody2D rb = other.gameObject.GetComponent<Rigidbody2D>();
            //If the object is being hold by the player
            ContactPoint2D contact = other.contacts[0];
            Vector3 bounceDirection = - contact.normal;
            AudioManager.PlayOne(AudioManager.CLOUDBOUNCESFX);
            if (other.gameObject.tag == "Bullet")
            {
                other.gameObject.GetComponent<Bullet>().ChangeDirection(bounceDirection.normalized);
                return;
            }
            if (rb.mass < 5.0f)
            {
                return;
            }
            
            //Only bounce if going up
            //if(bounceDirection.y > 0.15f)
            //    rb.AddForce(bounceDirection * bounceMagnitude, ForceMode2D.Impulse);



            if (other.gameObject.tag == "Steel")
            {
                if (bounceDirection.y > 0.15f)
                    rb.AddForce(bounceDirection * bounceMagnitudeSteel, ForceMode2D.Impulse);
                this.GetComponent<DrawMesh>().SelfDestroy();
            }
            else
            {
                
                if (bounceDirection.y > 0.15f)
                    rb.AddForce(bounceDirection * bounceMagnitude, ForceMode2D.Impulse);
            }
        }
    }
}
