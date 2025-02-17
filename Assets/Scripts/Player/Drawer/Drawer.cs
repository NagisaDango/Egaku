using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Drawer : MonoBehaviourPun
{
    public DrawMesh drawMeshPrefab;
    private DrawMesh currentDrawer;

    [SerializeField] private Material drawMaterial;
    [SerializeField] private bool eraserMode;
    [SerializeField] private bool interactable;

    public float drawSize;

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
                currentDrawer.InitializedDrawProperty(drawMaterial, interactable);
            }
        }
        if (Input.GetMouseButton(0) && currentDrawer != null)
        {
            currentDrawer.StartDraw();
        }
        if (Input.GetMouseButtonUp(0))
        {
            currentDrawer = null;
        }
    }

    private void EraseDrawnObj()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero); // Small downward ray

        if (hit.collider != null && hit.collider.gameObject.layer == LayerMask.NameToLayer("Draw"))
        {
            Debug.Log("Hit: " + hit.collider.gameObject.name);
            Destroy(hit.collider.gameObject);
        }
    }
}
