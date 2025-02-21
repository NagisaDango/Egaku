using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class Drawer : MonoBehaviourPun
{
    public DrawMesh drawMeshPrefab;
    private DrawMesh currentDrawer;

    [SerializeField] private Material drawMaterial;
    [SerializeField] private bool eraserMode;
    [SerializeField] private bool interactable;

    public float drawSize;
    private int drawStrokeLimit = 300;
    private int drawStrokeTotal = 300;
    [SerializeField] private Slider inkSlider;

    private void Awake()
    {
        inkSlider = GameObject.Find("Canvas/Slider").GetComponent<Slider>();
    }

    void Update()
    {
        if(!photonView.IsMine)
            return;

        

        if (Input.GetMouseButtonDown(0))
        {

            if (eraserMode)
            {
                EraseDrawnObj();
            }
            else if (currentDrawer == null)
            {
                //currentDrawer = Instantiate(drawMeshPrefab);
                currentDrawer = PhotonNetwork.Instantiate(drawMeshPrefab.name, this.transform.position, this.transform.rotation).GetComponent<DrawMesh>();
                //currentDrawer.InitializedDrawProperty(drawMaterial, interactable);
                //currentDrawer.photonView.RPC("RPC_InitializedDrawProperty", RpcTarget.AllBuffered, drawMaterial.color.r, drawMaterial.color.g, drawMaterial.color.b, interactable);
                
                //***Hard code Wood for now
                Vector3 mousePos = GetMouseWorldPosition();
                currentDrawer.photonView.RPC("RPC_InitializedDrawProperty", RpcTarget.All, mousePos, "Wood", interactable);
            }
        }
        if (Input.GetMouseButton(0) && currentDrawer != null)
        {
            int djj = drawStrokeTotal - currentDrawer.drawStrokes;
            inkSlider.value = djj * 1.0f / drawStrokeLimit;

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
            if(currentDrawer) drawStrokeTotal -= currentDrawer.drawStrokes;
            currentDrawer = null;
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldPosition.z = 0;
        return worldPosition;
    }

    private void EraseDrawnObj()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero); // Small downward ray

        if (hit.collider != null && hit.collider.gameObject.layer == LayerMask.NameToLayer("Draw"))
        {
            Debug.Log("Hit: " + hit.collider.gameObject.name);
            drawStrokeTotal += hit.collider.gameObject.GetComponent<DrawMesh>().drawStrokes;
            inkSlider.value = drawStrokeTotal * 1.0f / drawStrokeLimit;
            PhotonNetwork.Destroy(hit.collider.gameObject);
        }
    }
}
