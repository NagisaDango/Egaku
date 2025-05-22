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

    private void OnEnable()
    {
        EventHandler.ReachDestinationEvent += LoadEnd;
        EventHandler.LevelStartEvent += LoadLevelStart;
    }

    private void OnDisable()
    {
        EventHandler.ReachDestinationEvent -= LoadEnd;
        EventHandler.LevelStartEvent -= LoadLevelStart;


    }

    public void LoadEnd()
    {
        photonView.RPC("LoadLevelEnd", RpcTarget.All);
    }
       

    [PunRPC]
    public void LoadLevelEnd()
    {
        print("Enter LoadLevelEnd");
        StartCoroutine(ShowTransitionEndScene());
    }

    public void LoadLevelStart()
    {
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

        float elapsedTime = 0f;

        while (elapsedTime < outTime)
        {
            var p = elapsedTime / outTime;
            p = Mathf.Clamp01(p);
            var r = (1 - p) * 1.5;

            matTransition.SetFloat("_Radius", (float)r);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        GameManager.Instance.OnReachDestination();


    }

    IEnumerator ShowTransitionStartScene()
    {
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, (Vector3)GameObject.Find("LevelSetup").GetComponent<LevelSetup>().GetRevivePos());
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out localPoint);
        Vector2 size = canvasRect.rect.size;
        Vector2 uv = (localPoint + size * 0.5f) / size;



        float elapsedTime = 0f;

        while (elapsedTime < outTime)
        {
            var p = elapsedTime / outTime;
            p = Mathf.Clamp01(p);
            var r = p * 1.5;

            matTransition.SetFloat("_Radius", (float)r);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

}
