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
    private DrawMesh currentDrawer;

    [SerializeField] private bool eraserMode;
    [SerializeField] private bool interactable;
    [SerializeField] private GameObject drawerPanelPrefab;

    public static Action<PenUI.PenType> OnPenSelect;
    private PenUI.PenType currentPenType;
    public float drawSize;
    private int drawStrokeLimit = 300;
    private int drawStrokeTotal = 300;
    [SerializeField] private Slider inkSlider;
    public float time = 3;

    private int actorNum;
    
    [Header("Pen")]
    [SerializeField] public PenProperty woodPen;
    [SerializeField] public PenProperty cloudPen;
    [SerializeField] public PenProperty steelPen;
    [SerializeField] public PenProperty electricPen;
    
    private void Awake()
    {
        inkSlider = GameObject.Find("Canvas/Slider").GetComponent<Slider>();
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Drawer is already active and set, destroying this drawer.");
            Destroy(this.gameObject);
        }
        if (photonView.IsMine)
        {
            actorNum = PhotonNetwork.LocalPlayer.ActorNumber;
            print("This is the draweer spawning UI");
            OnPenSelect += SetPenProperties;
            Instantiate(drawerPanelPrefab);
        }
    }
    
    void Update()
    {
        if(!photonView.IsMine)
            return;

        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            if (eraserMode)
            {
                photonView.RPC("EraseDrawnObj", RpcTarget.AllBuffered, Camera.main.ScreenToWorldPoint(Input.mousePosition));
                //EraseDrawnObj();
            }
            else if (currentDrawer == null)
            {
                //currentDrawer = Instantiate(drawMeshPrefab);
                currentDrawer = PhotonNetwork.Instantiate(drawMeshPrefab.name, this.transform.position, this.transform.rotation).GetComponent<DrawMesh>();
                
                Vector3 mousePos = GetMouseWorldPosition();
                currentDrawer.photonView.RPC("RPC_InitializedDrawProperty", RpcTarget.All, mousePos, currentPenType.ToString(), interactable);
            }
        }
        if (Input.GetMouseButton(0) && currentDrawer != null)
        {
            int djj = drawStrokeTotal - currentDrawer.drawStrokes;
            photonView.RPC("UpdateSlider", RpcTarget.All, djj * 1.0f / drawStrokeLimit);
            if (djj <= 0)
            {
                print("stop drawing");
                drawStrokeTotal -= currentDrawer.drawStrokes;
                currentDrawer = null;
            }
            else
            {
                Vector3 mousePos = GetMouseWorldPosition();
                currentDrawer.photonView.RPC("RPC_StartDraw", RpcTarget.All, mousePos);
                //currentDrawer.StartDraw();
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (currentDrawer)
            {
                drawStrokeTotal -= currentDrawer.drawStrokes;
                currentDrawer.photonView.RPC("RPC_FinishDraw", RpcTarget.All);
                currentDrawer.rb2d.bodyType = RigidbodyType2D.Kinematic;
            }
            currentDrawer = null;
        }
    }

    private void SetPenProperties(PenUI.PenType penType)
    {
        if (penType == PenUI.PenType.Eraser)
        {
            eraserMode = true;
        }
        else
        {
            currentPenType = penType;
            eraserMode = false;
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
    private void EraseDrawnObj(Vector3 mousePos)
    {
        //Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero); // Small downward ray

        if (hit.collider != null && hit.collider.gameObject.layer == LayerMask.NameToLayer("Draw"))
        {
            Debug.Log("Hit: " + hit.collider.gameObject.name);
            //hit.collider.gameObject.GetComponent<DrawMesh>().photonView.TransferOwnership(actorNum);
            DrawMesh erasingMesh = hit.collider.gameObject.GetComponent<DrawMesh>();
            drawStrokeTotal += erasingMesh.drawStrokes;
            float value = drawStrokeTotal * 1.0f / drawStrokeLimit;
            StartCoroutine(AddSliderValue(value, time));
            erasingMesh.earsingSelf = true;
            PhotonNetwork.Destroy(hit.collider.gameObject);
            ParticleAttractor eraseEffect = PhotonNetwork.Instantiate("EraseEffect", new Vector3(mousePos.x, mousePos.y, 0), Quaternion.identity).GetComponent<ParticleAttractor>();
        }
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
}

[System.Serializable]
public struct PenProperty
{
    public bool gravity;
    public bool trigger;
    public int mass;
    public float size;
    public int maxStrokes;
    public Material material;
}
