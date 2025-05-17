using System;
using Photon.Pun;
using System.Collections.Generic;
using System.Linq;
using Photon.Realtime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;

public class DrawMesh : MonoBehaviourPunCallbacks, IOnPhotonViewOwnerChange
{
    [SerializeField] float minDistance = .1f;
    [SerializeField] GameObject splinePrefab;
    
    private Vector3 lastMousePosition;
    public int drawStrokes = 0;
    private PenProperty currProperty;
    
    [Header("Properties set through Drawer PenProperty")]
    [SerializeField] public Rigidbody2D rb2d;
    [SerializeField] public Collider2D col2d;
    private Mesh mesh;
    private float drawSize = 1;
    private int maxStrokes;
    public bool earsingSelf;
    private List<Vector3> pointList;
    
    [PunRPC]
    //Initialize the properties
    void RPC_InitializedDrawProperty(Vector3 mousePos, string penType, bool interactable)//Material mat, bool interactable)
    {
        mesh = new Mesh();
        SetPenProperty(penType);
        if (penType == "Electric")
        {
            pointList = new List<Vector3>();
            pointList.Add(new Vector3(mousePos.x, mousePos.y, 0));
        }
        GetComponent<MeshRenderer>().material = currProperty.material;
        drawSize = currProperty.size;
        maxStrokes = currProperty.maxStrokes;
        
        Vector3 startPos = mousePos;
        lastMousePosition = startPos;
        
        Vector3[] vertices = new Vector3[4];
        Vector2[] uv = new Vector2[4];
        int[] triangles = new int[6];

        vertices[0] = startPos;
        vertices[1] = startPos;
        vertices[2] = startPos;
        vertices[3] = startPos;

        uv[0] = Vector2.zero;
        uv[1] = Vector2.zero;
        uv[2] = Vector2.zero;
        uv[3] = Vector2.zero;

        triangles[0] = 0;
        triangles[1] = 3;
        triangles[2] = 1;

        triangles[3] = 1;
        triangles[4] = 3;
        triangles[5] = 2;

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.MarkDynamic();

        if (interactable)
            InteractSetting();


        GetComponent<MeshFilter>().mesh = mesh;
        lastMousePosition = mousePos;
    }

    private void SetPenProperty(string penName)
    {
        switch (penName)
        {
            case "Wood":
                currProperty = Drawer.Instance.woodPen; break;
            case "Steel":
                currProperty = Drawer.Instance.steelPen; break;
            case "Cloud":
                currProperty = Drawer.Instance.cloudPen; break;
            case "Electric":
                currProperty = Drawer.Instance.electricPen; break;
            default:
                Debug.LogWarning(penName + " is not set in the drawer pen list"); break;
        }
    }
    
    //Set the generated body type rb2d to Dynamic 
    private void InteractSetting()
    {
        GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
    }

    public bool ValidateMouseMovement(Vector3 mousePos)
    {
        return Vector3.Distance(mousePos, lastMousePosition) > minDistance;
    }

    //RPC: Draw mesh according to the mosuePos
    [PunRPC]
    void RPC_StartDraw(Vector3 mousePos)
    {
        //GetMouseWorldPosition();
        if (((drawStrokes < maxStrokes) || maxStrokes <= 0))
        {
            drawStrokes++;
            if(pointList != null) pointList.Add(mousePos);
            //Debug.Log("draw times:" + i);
            Vector3[] vertices = new Vector3[mesh.vertices.Length + 2];
            Vector2[] uv = new Vector2[mesh.uv.Length + 2];
            int[] triangles = new int[mesh.triangles.Length + 6];
            
            mesh.vertices.CopyTo(vertices, 0);
            mesh.uv.CopyTo(uv, 0);
            mesh.triangles.CopyTo(triangles, 0);

            int vIndex = vertices.Length - 4;
            int vIndex0 = vIndex + 0;
            int vIndex1 = vIndex + 1;
            int vIndex2 = vIndex + 2;
            int vIndex3 = vIndex + 3;

            Vector3 mouseForwardVector = (mousePos - lastMousePosition).normalized;
            Vector3 normal2D = new Vector3(0, 0, -drawSize);
            float lineThickness = 1f;
            Vector3 newVertexUp = mousePos + Vector3.Cross(mouseForwardVector, normal2D) * lineThickness;
            Vector3 newVertexDown = mousePos + Vector3.Cross(mouseForwardVector, normal2D * -1f) * lineThickness;

            //debugVisual1.position = newVectexUp;
            //debugVisual2.position = newVectexDown;

            vertices[vIndex2] = newVertexUp;
            vertices[vIndex3] = newVertexDown;

            uv[vIndex2] = Vector2.zero;
            uv[vIndex3] = Vector2.zero;

            int tIndex = triangles.Length - 6;

            triangles[tIndex + 0] = vIndex0;
            triangles[tIndex + 1] = vIndex2;
            triangles[tIndex + 2] = vIndex1;

            triangles[tIndex + 3] = vIndex1;
            triangles[tIndex + 4] = vIndex2;
            triangles[tIndex + 5] = vIndex3;


            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;

            lastMousePosition = mousePos;

            //EdgeCollider2D col = GetComponent<EdgeCollider2D>();
            PolygonCollider2D col = GetComponent<PolygonCollider2D>();

            Vector2[] v2 = System.Array.ConvertAll(mesh.vertices, v3 => new Vector2(v3.x, v3.y));
            col.points = GetEdge(v2);
        }
    }

    [PunRPC]
    private void RPC_FinishDraw()
    {
        if(drawStrokes <= 0)
            PhotonNetwork.Destroy(gameObject);
        else
        {
            if (currProperty.gravity) rb2d.bodyType = RigidbodyType2D.Dynamic;
            if (currProperty.mass > 0) rb2d.mass = currProperty.mass;
            //***!!! if currproperty is trigger just remove the collider for now.
            if (currProperty.trigger)  //Destroy(col2d);
                col2d.isTrigger = true;

            this.gameObject.tag = SetUpObjectTag(currProperty.penType);
            photonView.TransferOwnership(Runner.Instance.actorNum);

            //Electric
            if (pointList != null && currProperty.penType == PenProperty.PenType.Electric)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    GameObject splineInstance = PhotonNetwork.Instantiate(splinePrefab.name, Vector3.zero, Quaternion.identity);
                    splineInstance.transform.GetChild(1).transform.position = pointList[0];
                    splineInstance.transform.GetChild(2).transform.position = pointList[pointList.Count - 1];
                    photonView.RPC("RPC_SetupSpline", RpcTarget.AllBuffered, splineInstance.GetComponent<PhotonView>().ViewID);
                }
            }
            else if (currProperty.penType == PenProperty.PenType.Cloud)
            {
                this.AddComponent<CloudFloat>();
            }
            else if (currProperty.penType == PenProperty.PenType.Wood)
            {
                this.AddComponent<WoodPen>();
            }
        }
    }

    private string SetUpObjectTag(PenProperty.PenType penType)
    {
        switch (penType)
        {
            case PenProperty.PenType.Electric:
                return "Electric";
            case PenProperty.PenType.Wood:
                return "Wood";
            case PenProperty.PenType.Steel:
                return "Steel";
            case PenProperty.PenType.Cloud:
                return "Cloud";
            default:
                return "Error";
        }
    }
    
    [PunRPC]
    private void RPC_SetupSpline(int splineViewID)
    {
        PhotonView splinePhotonView = PhotonView.Find(splineViewID);
        if (splinePhotonView != null)
        {
            splinePhotonView.gameObject.transform.SetParent(this.transform);
            ElectricSpline listHolder = splinePhotonView.GetComponent<ElectricSpline>();
            SplineContainer container = splinePhotonView.GetComponent<SplineContainer>();
            if (container != null)
            {
                Spline spline = new Spline(); 
                container.AddSpline(spline);
                foreach (Vector3 point in pointList)
                {
                    spline.Add(new BezierKnot(point));
                }
            }
        }
    }
    
    //Get the edges of the mesh
    private Vector2[] GetEdge(Vector2[] array)
    {
        List<Vector2> v = new List<Vector2>();

        int index = 3;

        Stack<Vector2> stack = new Stack<Vector2>();


        for (int i = 0; i <= index; i++)
        {
            v.Add(array[i]);
        }

        for( int i = index + 1; i < array.Length; i++)
        {
            if(i % 2 == 0)
            {
                v.Add(array[i]);
            }
            else
            {
                if (i == array.Length - 1)  
                {
                    v.Add(array[i]);
                    break;
                }
                stack.Push(array[i]);
                print(array[i]);
            }

        }

        int stackCount = stack.Count;
        for(int i = 0; i < stackCount; i++)
        {
            v.Add(stack.Pop());
        }
        v.Add(array[0]);


        return v.ToArray();
    }
    
    public void OnOwnerChange(Player newOwner, Player previousOwner)
    {
        if (earsingSelf)
        {
            PhotonNetwork.Destroy(this.gameObject);
        }
    }

    public void SelfDestroy()
    {
        Drawer.Instance.photonView.RPC("RPC_DirectErase", RpcTarget.AllBuffered, drawStrokes, (Vector2)col2d.bounds.center);
        PhotonNetwork.Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag == "DeathDesuwa")
        {
            Drawer.Instance.photonView.RPC("RPC_DirectErase", RpcTarget.AllBuffered, drawStrokes, (Vector2)col2d.bounds.center);
            //ParticleAttractor eraseEffect = PhotonNetwork.Instantiate("EraseEffect", new Vector3(col2d.bounds.center.x, col2d.bounds.center.y, 0), Quaternion.identity).GetComponent<ParticleAttractor>();
            PhotonNetwork.Destroy(gameObject);
        }
    }
}


