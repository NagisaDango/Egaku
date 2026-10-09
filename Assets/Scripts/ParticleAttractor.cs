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
    // Each locally spawned effect follows its own scene UI; cached references expire on scene unload.
    private Slider inkSlider;
    private Camera gameplayCamera;
    public float disappearDistance = 0.1f; // Distance threshold for disappearing

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null) return;
        particles = new ParticleSystem.Particle[ps.main.maxParticles];
        SetTarget();
    }

    public void SetMaterial()
    {
        
    }

    bool SetTarget()
    {
        if (inkSlider == null || gameplayCamera == null)
        {
            var context = GameplaySceneContext.FindInScene(gameObject.scene);
            if (context == null) return false;
            inkSlider = context.inkSlider;
            gameplayCamera = context.gameplayCamera;
        }
        // Loading, leaving or waiting for the first Drawer HUD snapshot must not throw
        // or pull particles toward a stale target from the preceding scene.
        if (inkSlider == null || gameplayCamera == null || !inkSlider.gameObject.activeInHierarchy) return false;
        target = inkSlider.transform;
        var rect = (RectTransform)target;
        float fill = inkSlider.normalizedValue;
        if (inkSlider.direction == Slider.Direction.TopToBottom) fill = 1f - fill;
        var uiPoint = rect.TransformPoint(new Vector3(rect.rect.center.x,
            Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, fill), 0f));
        var canvas = inkSlider.GetComponentInParent<Canvas>();
        var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, uiPoint);
        // TransformPoint includes the authored slider scale. Project the screen target
        // onto this effect's world plane instead of using UI z as camera depth.
        float depth = Vector3.Dot(transform.position - gameplayCamera.transform.position, gameplayCamera.transform.forward);
        djj = gameplayCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));
        return true;
    }
    void LateUpdate()
    {
        if (ps == null || particles == null || !SetTarget()) return;

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
