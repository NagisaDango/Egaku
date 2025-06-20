using System;
using Allan;
using Photon.Pun;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;


public enum BulletType
{
    Follow,
    Set
}

public class Bullet : MonoBehaviourPun
{
    [SerializeField] private BulletType type;
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 direction;
    [SerializeField] public float speed = 1;

    private bool initialized = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!initialized || !photonView.IsMine) return;

        if (type == BulletType.Follow)
        {
            transform.Translate(direction * Time.deltaTime * speed );
        }
        else if (type == BulletType.Set)
        {
            transform.Translate(direction.normalized * Time.deltaTime * speed);
        }

    }

    public void Init(BulletType _type, Transform _target, Vector2 _direction, float _speed)
    {
        type = _type;
        target = _target;
        direction = _direction;
        speed = _speed;

        if (type == BulletType.Follow)
        {
            direction = (target.position - transform.position).normalized;
        }
        initialized = true;
    }


    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.tag == "Player")
        {
            //print("Bullet hitting Player, Restarting game");
            collision.gameObject.GetComponent<Runner>().Revive();

        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("Platform"))
        {
            print("Bullet hitting Platform, Destroying self");
            BreakablePlatform glass = collision.gameObject.GetComponent<BreakablePlatform>();
            if (glass)
            {
                glass.HitGlass();
            }

        }
        else if(collision.gameObject.layer == LayerMask.NameToLayer("Draw"))
        {
            if (collision.gameObject.tag == "Cloud")
            {
                ChangeDirection(collision.contacts[0].normal.normalized);
                return;
            }
            PhotonNetwork.Destroy(collision.gameObject);
        }
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }

    }

    public void ChangeDirection(Vector2 normal)
    {
        //Debug.Log(direction + " before bounce");
        direction = Vector2.Reflect(direction, normal).normalized;
        //transform.Translate(direction.normalized * Time.deltaTime * speed);
        //Debug.Log(direction + " after bounce");
        //direction = dir;
    }


    private void OnTriggerExit2D(Collider2D collision)
    {
        if(collision.tag == "Bound")
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        Instantiate(Resources.Load("BulletEffect", typeof(GameObject)),  new Vector3(this.transform.position.x, transform.position.y, -6), Quaternion.identity);
    }
}

