using Allan;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class LevelTransition : MonoBehaviourPunCallbacks
{
    public float outTime;
    public float inTime;

    // One accepted transition per scene prevents duplicate destination/skip events and RPC echoes.
    private bool endRequested;
    private bool ending;
    private Material matTransition;
    public Image image;

    public Canvas canvas;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        // A missing visual is reported, but must not block the persistent departure controller.
        if (image == null || image.material == null)
        {
            Debug.LogWarning("Transition mask is missing; scene flow will continue without the visual.", this);
            return;
        }
        matTransition = Instantiate(image.material);
        image.material = matTransition;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public override void OnEnable()
    {
        base.OnEnable();
        EventHandler.ReachDestinationEvent += LoadEnd;
        EventHandler.LevelStartEvent += LoadLevelStart;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        EventHandler.ReachDestinationEvent -= LoadEnd;
        EventHandler.LevelStartEvent -= LoadLevelStart;
    }

    public void LoadEnd()
    {
        if (endRequested || ending || GameManager.InteractionsPausedForRecovery) return;
        endRequested = true;
        photonView.RPC("LoadLevelEnd", RpcTarget.All);
    }
       

    [PunRPC]
    public void LoadLevelEnd()
    {
        if (ending || GameManager.InteractionsPausedForRecovery) return;
        ending = true;
        StopAllCoroutines();
        if (GameManager.Instance != null) GameManager.Instance.BeginLevelDeparture(this);
    }

    public void LoadLevelStart()
    {
        // A delayed readiness event must not reopen a mask already closing for departure.
        if (ending) return;
        StopAllCoroutines();
        print("Enter LoadLevelStart");
        StartCoroutine(ShowTransitionStartScene());
    }

    IEnumerator ShowTransitionEndScene()
    {
        if (matTransition == null) yield break;
        // The role instance is explicit; remote/local ownership does not change the visual's target.
        Vector2 uv = GetCenter(Runner.Instance != null ? Runner.Instance.transform.position : (Vector3?)null);
        yield return AnimateRadius(GetOpenRadius(uv, (float)Screen.width / Screen.height), 0f, outTime);
    }

    public void PlayClosingVisual()
    {
        // Disabled presentation is a valid fallback; do not start a coroutine on an inactive GameObject.
        if (isActiveAndEnabled) StartCoroutine(ShowTransitionEndScene());
    }

    public void FinishClosingVisual()
    {
        StopAllCoroutines();
        if (matTransition != null) matTransition.SetFloat("_Radius", 0f);
    }

    IEnumerator ShowTransitionStartScene()
    {
        if (matTransition == null) yield break;
        LevelSetup setup = LevelSetup.FindInScene(gameObject.scene);
        Vector2 uv = GetCenter(setup != null ? (Vector3)setup.GetRevivePos() : (Vector3?)null);
        yield return AnimateRadius(0f, GetOpenRadius(uv, (float)Screen.width / Screen.height), inTime);
    }

    private Vector2 GetCenter(Vector3? position)
    {
        // Missing camera/target or a collapsed Canvas uses a centered wipe instead of throwing.
        Vector2 uv = new Vector2(0.5f, 0.5f);
        var context = GameplaySceneContext.FindInScene(gameObject.scene);
        Camera viewCamera = context != null ? context.gameplayCamera : Camera.main;
        if (canvas != null && viewCamera != null && position.HasValue)
        {
            var rect = canvas.GetComponent<RectTransform>();
            Vector2 localPoint;
            if (rect != null && rect.rect.width > 0f && rect.rect.height > 0f &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,
                    RectTransformUtility.WorldToScreenPoint(viewCamera, position.Value),
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out localPoint))
                uv = (localPoint + rect.rect.size * 0.5f) / rect.rect.size;
        }
        matTransition.SetVector("_Center", new Vector4(uv.x, uv.y, 0, 0));
        return uv;
    }

    private void OnDestroy()
    {
        // Each scene owns its cloned mask material; never destroy the authored shared asset.
        if (matTransition != null) Destroy(matTransition);
    }

    private static float GetOpenRadius(Vector2 center, float aspect)
    {
        // Match CircleMask's aspect-corrected distance, including off-screen centers.
        // A small margin keeps even the farthest corner outside the opaque mask.
        float x = Mathf.Max(Mathf.Abs(center.x), Mathf.Abs(1f - center.x)) * aspect;
        float y = Mathf.Max(Mathf.Abs(center.y), Mathf.Abs(1f - center.y));
        return Mathf.Sqrt(x * x + y * y) + 0.01f;
    }

    private IEnumerator AnimateRadius(float from, float to, float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            matTransition.SetFloat("_Radius", Mathf.Lerp(from, to, elapsedTime / duration));
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        // Frame steps rarely land exactly on duration. Explicitly finish even for zero duration,
        // before loading another scene, so closing leaves no hole and opening leaves no border.
        matTransition.SetFloat("_Radius", to);
    }

}
