using System;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class Drawer : MonoBehaviourPun
{
    public static Drawer Instance;
    public DrawMesh drawMeshPrefab;
    public DrawMesh drawMeshSpriteShapePrefab;
    private DrawMesh currentDrawer;

    [SerializeField] private bool eraserMode;
    [SerializeField] private bool interactable;
    [SerializeField] private GameObject drawerPanelPrefab;
    [Header("Cursor")]
    [SerializeField] private Texture2D woodCursorTexture;
    [SerializeField] private Texture2D cloudCursorTexture;
    [SerializeField] private Texture2D electricCursorTexture;
    [SerializeField] private Texture2D steelCursorTexture;
    [SerializeField] private Texture2D eraserCursorTexture;
    
    public static Action<PenUI.PenType> OnPenSelect;
    private PenUI.PenType currentPenType;
    private int currentPenIndex;
    public float drawSize;
    private int drawStrokeLimit = 300;
    private int drawStrokeTotal = 300;
    [SerializeField] private Slider inkSlider;
    public float time = 0.2f;

    private int actorNum;

    [Header("Pen")] 
    [SerializeField] public PenProperty woodPen;
    [SerializeField] public PenProperty cloudPen;
    [SerializeField] public PenProperty steelPen;
    [SerializeField] public PenProperty electricPen;
    public List<PenProperty> penProperties;
    public static bool[] penStatus = new bool[4];
    public static bool multipleEraseMode;
    private void Awake()
    {
        //DontDestroyOnLoad(this.gameObject);
        Instance = this;
        inkSlider = GameObject.Find("GameCanvas/Panel/Slider").GetComponent<Slider>();
        currentPenType = PenUI.PenType.None;
        penProperties = 
            new List<PenProperty>
            {
                woodPen, cloudPen, steelPen, electricPen
            };

        time = 0.2f;
    }
    
    private void Start()
    {
        //if (Instance == null)
        //{
        //    Instance = this;
        //}
        //else
        //{
        //    Debug.LogWarning("Drawer is already active and set, destroying this drawer.");
        //    Destroy(this.gameObject);
        //}
        if (photonView.IsMine)
        {
            actorNum = PhotonNetwork.LocalPlayer.ActorNumber;
            print("This is the draweer spawning UI");
            OnPenSelect += SetPenProperties;
            GameObject UI = Instantiate(drawerPanelPrefab).transform.GetChild(0).gameObject;
            GameObject.Find("LevelSetup").GetComponent<LevelSetup>().Init(UI.GetComponent<DrawerUICOntrol>());
        }
    }

    private void OnDisable()
    {
        print("Wtf");
    }
    
    

    void Update()
    {
        if(!photonView.IsMine || currentPenType == PenUI.PenType.None)
            return;
        if(Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            eraserMode = false;
            // TODO: hard code 4 length here
            if (Input.GetAxis("Mouse ScrollWheel") > 0)
            {
                int tempIndex = (currentPenIndex - 1 + 4) % 4;
                while (tempIndex != currentPenIndex)
                {
                    print(tempIndex + "  " + penStatus[tempIndex]);
                    if (penStatus[tempIndex] == true)
                    {
                        SetPenProperties(penProperties[tempIndex].penType);
                        break;
                    }
                    else
                    {
                        tempIndex--;
                        tempIndex = (tempIndex + 4) % 4;
                    }
                }
            }
            if (Input.GetAxis("Mouse ScrollWheel") < 0)
            {
                int tempIndex = (currentPenIndex + 1 + 4) % 4;
                while (tempIndex != currentPenIndex)
                {
                    print(tempIndex + "  " + penStatus[tempIndex]);
                    if (penStatus[tempIndex] == true)
                    {
                        SetPenProperties(penProperties[tempIndex].penType);
                        break;
                    }
                    else
                    {
                        tempIndex++;
                        tempIndex = (tempIndex + 4) % 4;
                    }
                }
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (eraserMode)
            {
                SetPenProperties(lastPenType);
                
            }
            else
            {
                SetPenProperties(PenUI.PenType.Eraser);
                RaycastHit2D hit = Physics2D.Raycast((Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition),
                    Vector2.zero, Mathf.Infinity, LayerMask.GetMask("Draw"));

                photonView.RPC("EraseDrawnObj", RpcTarget.All,
                    (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition));
                if (hit.collider != null)
                {
                    print(hit.collider.gameObject.name);
                    print("erase mode: " + multipleEraseMode);
                }
            }
        }
        if (Input.GetMouseButtonDown(0))//&& !EventSystem.current.IsPointerOverGameObject())
        {
            if (eraserMode) //&& EventSystem.current.IsPointerOverGameObject())
            {
                //if(EventSystem.current.IsPointerOverGameObject())
                RaycastHit2D hit = Physics2D.Raycast((Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero, Mathf.Infinity, LayerMask.GetMask("Draw"));
                
                photonView.RPC("EraseDrawnObj", RpcTarget.All,
                    (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition));
                if (hit.collider != null)
                {
                    print(hit.collider.gameObject.name);
                    print("erase mode: " + multipleEraseMode);
                    //if(!multipleEraseMode)
                        //SetPenProperties(lastPenType);
                }
                //EraseDrawnObj();
            }
            else if (currentDrawer == null)
            {
                //currentDrawer = Instantiate(drawMeshPrefab);
                currentDrawer = PhotonNetwork.Instantiate(drawMeshPrefab.name, this.transform.position, this.transform.rotation).GetComponent<DrawMesh>();
                //currentDrawer = PhotonNetwork.Instantiate(drawMeshSpriteShapePrefab.name, this.transform.position, this.transform.rotation).GetComponent<DrawMesh>();
                
                Vector3 mousePos = GetMouseWorldPosition();
                currentDrawer.photonView.RPC("RPC_InitializedDrawProperty", RpcTarget.All, mousePos, currentPenType.ToString(), interactable);
            }
        }
        if (Input.GetMouseButton(0) && currentDrawer != null)
        {
            Vector3 mousePos = GetMouseWorldPosition();
            if (currentDrawer.ValidateMouseMovement(mousePos))
            {
                int djj = drawStrokeTotal - currentDrawer.drawStrokes;
                photonView.RPC("UpdateSlider", RpcTarget.All, djj * 1.0f / drawStrokeLimit);

                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, 0, LayerMask.GetMask("DrawProhibited"));
                //hit = Physics2D.Raycast(mousePos, Vector2.zero);


                if (djj <= 0)
                {
                    print("stop drawing");
                    drawStrokeTotal -= currentDrawer.drawStrokes;
                    currentDrawer = null;
                }
                else if(hit.collider != null)
                {
                    print(hit.collider.name);
                    drawStrokeTotal -= currentDrawer.drawStrokes;
                    currentDrawer.photonView.RPC("RPC_FinishDraw", RpcTarget.All);
                    currentDrawer = null;

                }
                else
                {
                    currentDrawer.photonView.RPC("RPC_StartDraw", RpcTarget.All, mousePos);
                    //currentDrawer.photonView.RPC("RPC_DrawSpriteShape", RpcTarget.All, mousePos);
                    //currentDrawer.StartDraw();
                }



            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (currentDrawer)
            {
                drawStrokeTotal -= currentDrawer.drawStrokes;
                currentDrawer.photonView.RPC("RPC_FinishDraw", RpcTarget.All);
                //currentDrawer.rb2d.bodyType = RigidbodyType2D.Kinematic;
            }
            currentDrawer = null;
        }
    }
    private PenUI.PenType lastPenType;
    private void SetPenProperties(PenUI.PenType penType)
    {
        switch (penType)
        {
            case PenUI.PenType.None:
                Cursor.SetCursor(null, new Vector2(0, 0), CursorMode.Auto);
                break;
            case PenUI.PenType.Wood:
                currentPenIndex = 0;
                Cursor.SetCursor(woodCursorTexture, new Vector2(0, woodCursorTexture.height), CursorMode.Auto);
                break;
            case PenUI.PenType.Cloud:
                currentPenIndex = 1;
                Cursor.SetCursor(cloudCursorTexture, new Vector2(0, cloudCursorTexture.height), CursorMode.Auto);
                break;
            case PenUI.PenType.Steel:
                currentPenIndex = 2;
                Cursor.SetCursor(steelCursorTexture, new Vector2(0, steelCursorTexture.height), CursorMode.Auto);
                break;
            case PenUI.PenType.Electric:
                currentPenIndex = 3;
                Cursor.SetCursor(electricCursorTexture, new Vector2(0, electricCursorTexture.height), CursorMode.Auto);
                break;
            case PenUI.PenType.Eraser:
                lastPenType = currentPenType;
                eraserMode = true;
                Cursor.SetCursor(eraserCursorTexture, new Vector2(0, eraserCursorTexture.height), CursorMode.Auto);
                break;
        }
        currentPenType = penType;
        
        if (penType != PenUI.PenType.Eraser)
        {
            eraserMode = false;
        }
    }
    private void SetPenProperties(PenProperty.PenType penType)
    {
        switch (penType)
        {
            case PenProperty.PenType.Wood:
                currentPenIndex = 0;
                currentPenType = PenUI.PenType.Wood;
                Cursor.SetCursor(woodCursorTexture, new Vector2(0, woodCursorTexture.height), CursorMode.Auto);
                break;
            case PenProperty.PenType.Cloud:
                currentPenIndex = 1;
                currentPenType = PenUI.PenType.Cloud;
                Cursor.SetCursor(cloudCursorTexture, new Vector2(0, cloudCursorTexture.height), CursorMode.Auto);
                break;
            case PenProperty.PenType.Steel:
                currentPenIndex = 2;
                currentPenType = PenUI.PenType.Steel;
                Cursor.SetCursor(steelCursorTexture, new Vector2(0, steelCursorTexture.height), CursorMode.Auto);
                break;
            case PenProperty.PenType.Electric:
                currentPenIndex = 3;
                currentPenType = PenUI.PenType.Electric;
                Cursor.SetCursor(electricCursorTexture, new Vector2(0, electricCursorTexture.height), CursorMode.Auto);
                break;
        }
    }
    
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldPosition.z = 0;
        return worldPosition;
    }

    [PunRPC]
    private void UpdateSlider(float val)
    {
        inkSlider.value = val;
    }
    
    [PunRPC]
    private void EraseDrawnObj(Vector2 mousePos)
    {
        //Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, LayerMask.GetMask("Draw")); // Small downward ray

        if (hit.collider != null)// && hit.collider.gameObject.layer == LayerMask.NameToLayer("Draw"))
        {
            if(hit.collider.gameObject.tag == "ClickToErase")
            {
                Destroy(hit.collider.gameObject.transform.parent.gameObject);
                return;
            }

            Debug.Log("Hit: " + hit.collider.gameObject.name);
            //hit.collider.gameObject.GetComponent<DrawMesh>().photonView.TransferOwnership(actorNum);
            DrawMesh erasingMesh = hit.collider.gameObject.GetComponent<DrawMesh>();
            photonView.RPC("RPC_DirectErase", RpcTarget.All, erasingMesh.drawStrokes, mousePos, hit.collider.gameObject.tag);
            erasingMesh.earsingSelf = true;
            PhotonNetwork.Destroy(hit.collider.gameObject);
            /*
            drawStrokeTotal += erasingMesh.drawStrokes;
            float value = drawStrokeTotal * 1.0f / drawStrokeLimit;
            StartCoroutine(AddSliderValue(value, time));
            erasingMesh.earsingSelf = true;
            PhotonNetwork.Destroy(hit.collider.gameObject);
            SpawnParticles(hit.collider.gameObject.tag, mousePos);
            */
            //ParticleAttractor eraseEffect = PhotonNetwork.Instantiate("EraseEffect", new Vector3(mousePos.x, mousePos.y, 0), Quaternion.identity).GetComponent<ParticleAttractor>();
        }
    }

    [PunRPC]
    public void RPC_DirectErase(int drawStrokes, Vector2 centerPos, string name)
    {
        drawStrokeTotal += drawStrokes;
        float value = drawStrokeTotal * 1.0f / drawStrokeLimit;
        EnqueueCoroutine(AddSliderValue(value, time));
        //StartCoroutine(AddSliderValue(value, time));
        SpawnParticles(name, centerPos);
        //ParticleAttractor eraseEffect = PhotonNetwork.Instantiate("EraseEffect", new Vector3(centerPos.x, centerPos.y, 0), Quaternion.identity).GetComponent<ParticleAttractor>();
    }

    private void SpawnParticles(string erasingTagName, Vector3 centerPos)
    {
        //TODO: Error prone here, should not be changing tag of holding object to holding, stay as what type it is
        if (erasingTagName == "Holding")
            erasingTagName = "Wood";
        Instantiate(Resources.Load("EraseEffect" + erasingTagName, typeof(GameObject)),  new Vector3(centerPos.x, centerPos.y, 0), Quaternion.identity);
        Instantiate(Resources.Load("EraseEffect", typeof(GameObject)),  new Vector3(centerPos.x, centerPos.y, -6), Quaternion.identity);
        //PhotonNetwork.Instantiate("EraseEffect" + erasingTagName, new Vector3(centerPos.x, centerPos.y, 0), Quaternion.identity).GetComponent<ParticleAttractor>();
    }

    IEnumerator AddSliderValue(float target, float time)
    {
        float currentValue = inkSlider.value;
        float increment = (target- currentValue) / time / 50;
        while (currentValue <= target) {
            currentValue += increment;
            print("value:" + currentValue);
            photonView.RPC("UpdateSlider", RpcTarget.All, currentValue);
            yield return new WaitForSeconds(0.02f);
        }

    }

    private Queue<IEnumerator> coroutineQueue = new Queue<IEnumerator>();
    private bool isCoroutineRunning = false;

    public void EnqueueCoroutine(IEnumerator coroutine)
    {
        coroutineQueue.Enqueue(coroutine);

        if (!isCoroutineRunning)
            StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        isCoroutineRunning = true;

        while (coroutineQueue.Count > 0)
        {
            yield return StartCoroutine(coroutineQueue.Dequeue());
        }

        isCoroutineRunning = false;
    }


}

[System.Serializable]
public struct PenProperty
{
    public enum PenType
    {
        Wood,
        Cloud,
        Electric,
        Steel,
        Eraser
    };
    public PenType penType;
    public bool gravity;
    public bool trigger;
    public int mass;
    public float size;
    public int maxStrokes;
    public Material material;
}
