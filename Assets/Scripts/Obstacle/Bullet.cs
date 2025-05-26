using Allan;
using UnityEngine;


public enum BulletType
{
    Follow,
    Set
}

public class Bullet : MonoBehaviour
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
        if (!initialized) return;

        if (type == BulletType.Follow)
        {
            transform.Translate((target.position-transform.position).normalized * Time.deltaTime * speed );
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
            //print("Bullet hitting Platform, Destroying self");
        }
        Destroy(gameObject);

    }


    private void OnTriggerExit2D(Collider2D collision)
    {
        if(collision.tag == "Bound")
        {
            Destroy(gameObject);
        }
    }

}

