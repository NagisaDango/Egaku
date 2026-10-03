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
        StartCoroutine(ShowTransitionEndScene());
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
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, GameObject.FindGameObjectWithTag("Player").transform.position);
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out localPoint);
        Vector2 size = canvasRect.rect.size;
        Vector2 uv = (localPoint + size * 0.5f) / size;
        matTransition.SetVector("_Center", new Vector4(uv.x, uv.y, 0, 0));

        yield return AnimateRadius(GetOpenRadius(uv, (float)Screen.width / Screen.height), 0f, outTime);

        // The manager alone decides next/finish; animation has no progression state of its own.
        GameManager.Instance.OnReachDestination();


    }

    IEnumerator ShowTransitionStartScene()
    {
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, (Vector3)LevelSetup.FindInScene(gameObject.scene).GetRevivePos());
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out localPoint);
        Vector2 size = canvasRect.rect.size;
        Vector2 uv = (localPoint + size * 0.5f) / size;



        matTransition.SetVector("_Center", new Vector4(uv.x, uv.y, 0, 0));
        yield return AnimateRadius(0f, GetOpenRadius(uv, (float)Screen.width / Screen.height), inTime);
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
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Frame steps rarely land exactly on duration. Explicitly finish even for zero duration,
        // before loading another scene, so closing leaves no hole and opening leaves no border.
        matTransition.SetFloat("_Radius", to);
    }

}
