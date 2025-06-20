using System;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Net;
using static UnityEngine.Rendering.DebugUI;

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
    public int drawStrokeLimit = 300;
    private int drawStrokeTotal;
    [SerializeField] private Slider inkSlider;
    public PenProperty.PenType sliderPenType;
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

        drawStrokeTotal = drawStrokeLimit;
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

        Color color = FindPenProperty(currentPenType).material.color;
        //sliderPenType = FindPenProperty(currentPenType).penType;
        photonView.RPC("ChangeSliderColor", RpcTarget.All, color.r, color.g, color.b, (int)FindPenProperty(currentPenType).penType);
        //ChangeSliderColor(FindPenProperty(currentPenType).material.color);
    }


    public PenProperty FindPenProperty(PenUI.PenType currentPenType)
    {
        switch (currentPenType)
        {
            case PenUI.PenType.Wood:
                return woodPen;
            case PenUI.PenType.Cloud:
                return cloudPen;
            case PenUI.PenType.Steel:
                return steelPen;
            case PenUI.PenType.Electric:
                return electricPen;
        }
        return null;
    }

    private void OnDisable()
    {
        print("Wtf");
    }

    private Vector3 lastErasePos;
    [SerializeField] private float minEraseDis;

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
                    //print(tempIndex + "  " + penStatus[tempIndex]);
                    if (penStatus[tempIndex] == true)
                    {
                        SetPenProperties(penProperties[tempIndex].penType);
                        StopAllCoroutines();
                        Color color = penProperties[tempIndex].material.color;

                        photonView.RPC("ChangeSliderColor", RpcTarget.All, color.r, color.g, color.b, (int)(penProperties[tempIndex].penType));
                        photonView.RPC("ClearCoroutineQueue", RpcTarget.All);

                        photonView.RPC("UpdateSlider", RpcTarget.All, 1 - penProperties[tempIndex].currentStrokes * 1f / penProperties[tempIndex].maxStrokes);

                        break;
                    }
                    else
                    {
                        tempIndex--;
                        tempIndex = (tempIndex + 4) % 4;
                    }
                }

                if (tempIndex == currentPenIndex && currentPenType == PenUI.PenType.Eraser)
                {
                    SetPenProperties(penProperties[tempIndex].penType);
                    StopAllCoroutines();
                    Color color = penProperties[tempIndex].material.color;

                    photonView.RPC("ChangeSliderColor", RpcTarget.All, color.r, color.g, color.b, (int)(penProperties[tempIndex].penType));
                    photonView.RPC("ClearCoroutineQueue", RpcTarget.All);

                    photonView.RPC("UpdateSlider", RpcTarget.All, 1 - penProperties[tempIndex].currentStrokes * 1f / penProperties[tempIndex].maxStrokes);
                }
            }
            if (Input.GetAxis("Mouse ScrollWheel") < 0)
            {
                int tempIndex = (currentPenIndex + 1 + 4) % 4;
                while (tempIndex != currentPenIndex)
                {
                    //print(tempIndex + "  " + penStatus[tempIndex]);
                    if (penStatus[tempIndex] == true)
                    {
                        SetPenProperties(penProperties[tempIndex].penType);

                        StopAllCoroutines();
                        Color color = penProperties[tempIndex].material.color;
                        photonView.RPC("ChangeSliderColor", RpcTarget.All, color.r, color.g, color.b, (int)(penProperties[tempIndex].penType));
                        photonView.RPC("ClearCoroutineQueue", RpcTarget.All);

                        photonView.RPC("UpdateSlider", RpcTarget.All, 1 - penProperties[tempIndex].currentStrokes * 1f / penProperties[tempIndex].maxStrokes);


                        break;
                    }
                    else
                    {
                        tempIndex++;
                        tempIndex = (tempIndex + 4) % 4;
                    }
                }
                
                
                if (tempIndex == currentPenIndex && currentPenType == PenUI.PenType.Eraser)
                {
                    SetPenProperties(penProperties[tempIndex].penType);
                    StopAllCoroutines();
                    Color color = penProperties[tempIndex].material.color;

                    photonView.RPC("ChangeSliderColor", RpcTarget.All, color.r, color.g, color.b, (int)(penProperties[tempIndex].penType));
                    photonView.RPC("ClearCoroutineQueue", RpcTarget.All);

                    photonView.RPC("UpdateSlider", RpcTarget.All, 1 - penProperties[tempIndex].currentStrokes * 1f / penProperties[tempIndex].maxStrokes);
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
                lastErasePos = GetMouseWorldPosition();
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
        if (Input.GetMouseButton(0))
        {
            Vector3 mousePos = GetMouseWorldPosition();
            if (eraserMode)
            {
                if (Vector3.Distance(mousePos, lastErasePos) >= minEraseDis)
                {
                    Vector3 direction = (mousePos - lastErasePos).normalized;
                    float distance = Vector3.Distance(lastErasePos, mousePos);
                    photonView.RPC("EraseDrawnObjCast", RpcTarget.All, (Vector2)lastErasePos, (Vector2)direction, distance, (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition));
                    lastErasePos = mousePos;
                }

                return;
            }

            if(currentDrawer == null)
            {
                goto SkipDrawMesh;
            }


            if (currentDrawer.ValidateMouseMovement(mousePos))
            {
                //int strokeLeft = drawStrokeTotal - currentDrawer.drawStrokes;
                int strokeLeft = currentDrawer.currProperty.maxStrokes - currentDrawer.currProperty.currentStrokes;

                print("strokeLeft "  + strokeLeft);

                photonView.RPC("UpdateSlider", RpcTarget.All, 1 - currentDrawer.currProperty.currentStrokes * 1f / currentDrawer.currProperty.maxStrokes);


                Vector3 lastPos = currentDrawer.GetLastMousePosition();
                Vector3 direction = (mousePos - lastPos).normalized;
                float distance = Vector3.Distance(lastPos, mousePos);
                
                

                if (strokeLeft <= 0)
                {
                    print("stop drawing");
                    drawStrokeTotal -= currentDrawer.drawStrokes;
                    currentDrawer.photonView.RPC("RPC_FinishDraw", RpcTarget.All);
                    currentDrawer = null;
                }
                else
                {
                    if (currentDrawer)
                    {
                        currentDrawer.photonView.RPC("RPC_StartDraw", RpcTarget.All, mousePos);
                    }
                    //currentDrawer.photonView.RPC("RPC_DrawSpriteShape", RpcTarget.All, mousePos);
                    //currentDrawer.StartDraw();
                }
            }
        }
        SkipDrawMesh:
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

    [PunRPC]
    private void ChangeSliderColor(float r, float g, float b, int penType)
    {
        inkSlider.transform.Find("Background").GetComponent<Image>().color = new Color(r, g, b, 0.5f);
        inkSlider.transform.Find("Fill Area/Fill").GetComponent<Image>().color = new Color(r, g, b, 1f);
        sliderPenType = (PenProperty.PenType)penType;
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
            AudioManager.PlayOne(AudioManager.ERASESFX);
            DrawMesh erasingMesh = hit.collider.gameObject.GetComponent<DrawMesh>();

            photonView.RPC("RPC_DirectErase", RpcTarget.All, erasingMesh.currProperty.penType, erasingMesh.drawStrokes, mousePos, hit.collider.gameObject.tag, sliderPenType == erasingMesh.currProperty.penType);
            erasingMesh.earsingSelf = true;
            PhotonNetwork.Destroy(hit.collider.gameObject);
        }
    }
    
    [PunRPC]
    private void EraseDrawnObjCast(Vector2 startPos, Vector2 direction, float distance, Vector2 mousePos)
    {
        //Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        //RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, LayerMask.GetMask("Draw")); // Small downward ray
        RaycastHit2D[] hits = Physics2D.RaycastAll(startPos, direction, distance, LayerMask.GetMask("Draw"));
        if (hits.Length > 0)// && hit.collider.gameObject.layer == LayerMask.NameToLayer("Draw"))
        {
            foreach (RaycastHit2D hit in hits)
            {
                if(hit.collider.gameObject.tag == "ClickToErase")
                {
                    Destroy(hit.collider.gameObject.transform.parent.gameObject);
                    return;
                }

                Debug.Log("Hit: " + hit.collider.gameObject.name);
                //hit.collider.gameObject.GetComponent<DrawMesh>().photonView.TransferOwnership(actorNum);
                AudioManager.PlayOne(AudioManager.ERASESFX);
                DrawMesh erasingMesh = hit.collider.gameObject.GetComponent<DrawMesh>();

                photonView.RPC("RPC_DirectErase", RpcTarget.All, erasingMesh.currProperty.penType, erasingMesh.drawStrokes, mousePos, hit.collider.gameObject.tag, sliderPenType == erasingMesh.currProperty.penType);

                erasingMesh.earsingSelf = true;
                PhotonNetwork.Destroy(hit.collider.gameObject);
            }
        }
    }

    [PunRPC]
    private void RPC_ForceFinishDraw()
    {
        if (currentDrawer)
        {
            drawStrokeTotal -= currentDrawer.drawStrokes;
            //currentDrawer.photonView.RPC("RPC_FinishDraw", RpcTarget.All);
            if (currentDrawer.drawStrokes <= 0)
            {
                PhotonNetwork.Destroy(currentDrawer.gameObject);
            }
            currentDrawer = null;
        }
    }
    

    [PunRPC]
    private void EraseDrawnObj(RaycastHit2D hit, Vector2 mousePos)
    {
        if (hit.collider != null)
        {
            if (hit.collider.gameObject.tag == "ClickToErase")
            {
                Destroy(hit.collider.gameObject.transform.parent.gameObject);
                return;
            }

            Debug.Log("Hit: " + hit.collider.gameObject.name);
            AudioManager.PlayOne(AudioManager.ERASESFX);
            DrawMesh erasingMesh = hit.collider.gameObject.GetComponent<DrawMesh>();
         
            photonView.RPC("RPC_DirectErase", RpcTarget.All, erasingMesh.currProperty.penType, erasingMesh.drawStrokes, mousePos, hit.collider.gameObject.tag, sliderPenType == erasingMesh.currProperty.penType);
            erasingMesh.earsingSelf = true;
            PhotonNetwork.Destroy(hit.collider.gameObject);
        }
    }

    //private DrawMesh erasedDrawMesh;

    [PunRPC]
    public void RPC_DirectErase(PenProperty.PenType penType, int stroke, Vector2 centerPos, string name, bool updateSlider)
    {
        PenProperty pen  = GetPenProperty(penType);
        pen.currentStrokes -= stroke;
        float value = 1 - pen.currentStrokes * 1.0f / pen.maxStrokes;

        if (updateSlider)
            EnqueueCoroutine(AddSliderValue(value, time));
        print("queue:  " + coroutineQueue.Count);
        //StartCoroutine(AddSliderValue(value, time));
        SpawnParticles(name, centerPos);
        //ParticleAttractor eraseEffect = PhotonNetwork.Instantiate("EraseEffect", new Vector3(centerPos.x, centerPos.y, 0), Quaternion.identity).GetComponent<ParticleAttractor>();
    }

    private PenProperty GetPenProperty(PenProperty.PenType penType)
    {
        switch (penType)
        {
            case PenProperty.PenType.Wood:
                return woodPen;
            case PenProperty.PenType.Cloud:
                return cloudPen;
            case PenProperty.PenType.Steel:
                return steelPen;
            case PenProperty.PenType.Electric:
                return electricPen;
        }
        return null;

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
        print("queue" + target +" " + inkSlider.value);
        float currentValue = inkSlider.value;
        float increment = (target- currentValue) / time / 50;
        while (currentValue <= target) {
            currentValue += increment;
            //print("value:" + currentValue);
            photonView.RPC("UpdateSlider", RpcTarget.All, currentValue);
            yield return new WaitForSeconds(0.02f);
        }

    }

    private Queue<IEnumerator> coroutineQueue = new Queue<IEnumerator>();
    public bool isCoroutineRunning = false;

    public void EnqueueCoroutine(IEnumerator coroutine)
    {
        coroutineQueue.Enqueue(coroutine);

        if (!isCoroutineRunning)
            StartCoroutine(RunQueue());
    }

    [PunRPC]
    public void ClearCoroutineQueue()
    {
        coroutineQueue.Clear();
    }

    private IEnumerator RunQueue()
    {
        isCoroutineRunning = true;

        while (coroutineQueue.Count > 0)
        {
            print("running queue");
            yield return StartCoroutine(coroutineQueue.Dequeue());
        }

        isCoroutineRunning = false;
    }


}

[System.Serializable]
public class PenProperty
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
    public Material material;
    public Material drawingMaterial;

    public int maxStrokes;
    public int currentStrokes;
}
