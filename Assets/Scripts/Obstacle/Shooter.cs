using Allan;
using UnityEngine;


public class Shooter : MonoBehaviour
{
    public GameObject bulletPrefab;
    [SerializeField] private float interval = 2.0f;
    private float timer;
    public bool shooting;
    [SerializeField] private Transform spawnPos;

    [SerializeField] private Transform bulletParent;
    [SerializeField] private BulletType bulletType;
    [SerializeField] private Vector2 direction;
    [SerializeField] private float speed;
    [SerializeField] private float scale = 1;

    public Sprite saki_b;
    public Sprite saki_w;
    private SpriteRenderer sprd;
    public Color color_b;
    public Color color_w;


    private Transform runner;



    void Start()
    {
        sprd = transform.GetChild(0).GetComponent<SpriteRenderer>();
        sprd.color = color_b;
        runner = Runner.Instance.gameObject.transform;

    }

    // Update is called once per frame
    void Update()
    {
        if (shooting && runner)
        {
            //if ()
            
            timer += Time.deltaTime;
            if (timer > interval)
            {
                timer = 0;
                Bullet go = Instantiate(bulletPrefab, spawnPos.position, Quaternion.identity, bulletParent).GetComponent<Bullet>();


                go.Init(bulletType, runner, direction, speed);
                go.transform.localScale *= scale;
                //go.SetType(bulletType);

                //if (bulletType == BulletType.Follow)
                //    go.SetTarget(Runner.Instance.gameObject.transform);
                //else if (bulletType == BulletType.Set)
                //    go.SetDirection(direction);
            }
        }

    


    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            shooting = false;
            sprd.sprite = saki_w;
            sprd.color = color_w;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            shooting = true;
            sprd.sprite = saki_b;
            sprd.color = color_b;
        }
    }


}
