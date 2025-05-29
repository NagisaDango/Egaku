using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Threading.Tasks;

public class ParticleAttractor : MonoBehaviour
{
    public static Transform target;
    private static Vector3 djj = Vector3.zero;
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;
    public float disappearDistance = 0.1f; // Distance threshold for disappearing

    void Start()
    {
        if (djj == Vector3.zero || target == null)
        {
            SetTarget();
        }
        ps = GetComponent<ParticleSystem>();
        particles = new ParticleSystem.Particle[ps.main.maxParticles];
    }

    public void SetMaterial()
    {
        
    }

    void SetTarget()
    {
        //print($"Setting Target cause: djj not set: {djj == Vector3.zero} or target not set: {target == null}" );
        target = GameObject.Find("GameCanvas/Panel/Slider").transform;
        //Vector3 xjj = Camera.main.ScreenToWorldPoint(target.position);
        float h = target.GetComponent<RectTransform>().rect.height;
        Vector3 xjj = target.position - new Vector3(0, h/2,0) + target.gameObject.GetComponent<UnityEngine.UI.Slider>().value * new Vector3(0, h, 0);
        djj = Camera.main.ScreenToWorldPoint(xjj);

        //print(xjj +"-----"+djj);
        //djj = Camera.main.ScreenToWorldPoint(target.position);
    }
    void LateUpdate()
    {
        if (djj == Vector3.zero || target == null)
        {
            //SetTarget();
        }
        SetTarget();

        int numParticlesAlive = ps.GetParticles(particles);

        for (int i = 0; i < numParticlesAlive; i++)
        {
            // Calculate direction to move the particle
            Vector3 direction = (djj - particles[i].position).normalized;
            particles[i].velocity = direction * 30f;

            // Check if the particle is close enough to disappear
            if(Mathf.Abs(particles[i].position.x - djj.x) < 0.01)
            {
                particles[i].remainingLifetime = 0;

                //DeleteParticle();
            }
        }

        ps.SetParticles(particles, numParticlesAlive);
    }

    //[PunRPC]
    //private async void CallAfterDelayAsync()
    //{
    //    await Task.Delay(3000); // milliseconds
    //    Destroy(this.gameObject);
    //}

    private async void DeleteParticle()
    {
        await Task.Delay(10000); // milliseconds
        Destroy(this.gameObject);
    }
}