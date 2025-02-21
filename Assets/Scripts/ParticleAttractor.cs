using UnityEngine;

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

    void SetTarget()
    {
        print($"Setting Target cause: djj not set: {djj == Vector3.zero} or target not set: {target == null}" );
        target = GameObject.Find("Canvas/Slider").transform;
        djj = Camera.main.ScreenToWorldPoint(target.position);
    }

    void LateUpdate()
    {
        if (djj == Vector3.zero || target == null)
        {
            SetTarget();
        }

        int numParticlesAlive = ps.GetParticles(particles);

        for (int i = 0; i < numParticlesAlive; i++)
        {
            // Calculate direction to move the particle
            Vector3 direction = (djj - particles[i].position).normalized;
            particles[i].velocity += direction * Time.deltaTime * 15f;

            // Check if the particle is close enough to disappear
            if(Mathf.Abs(particles[i].position.x - djj.x) < 1 && Mathf.Abs(particles[i].position.y - djj.y) < 10)
            {
                particles[i].remainingLifetime = 0;
            }
        }

        ps.SetParticles(particles, numParticlesAlive);
    }
}