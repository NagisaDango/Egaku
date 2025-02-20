using UnityEngine;

public class ParticleAttractor : MonoBehaviour
{
    public Transform target;
    private Vector3 djj;
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;
    public float disappearDistance = 0.1f; // Distance threshold for disappearing

    void Start()
    {
        djj = Camera.main.ScreenToWorldPoint(target.position);
        ps = GetComponent<ParticleSystem>();
        particles = new ParticleSystem.Particle[ps.main.maxParticles];
    }

    void LateUpdate()
    {
        if (ps == null || target == null) return;

        int numParticlesAlive = ps.GetParticles(particles);

        for (int i = 0; i < numParticlesAlive; i++)
        {
            // Calculate direction to move the particle
            Vector3 direction = (djj - particles[i].position).normalized;
            particles[i].velocity += direction * Time.deltaTime * 10f;

            // Check if the particle is close enough to disappear
            if (Vector3.Distance(particles[i].position, djj) < disappearDistance)
            {
                particles[i].remainingLifetime = 0;
            }
        }

        ps.SetParticles(particles, numParticlesAlive);
    }
}