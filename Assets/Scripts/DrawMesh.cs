using System;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Realtime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.U2D;
using Spline = UnityEngine.Splines.Spline;
using Allan;

public class DrawMesh : MonoBehaviourPunCallbacks, IOnPhotonViewOwnerChange
{
    // Keep only live stroke meshes here so the eraser can query visible geometry even when its local physics collider is inactive.
    private static readonly HashSet<DrawMesh> activeDrawMeshes = new HashSet<DrawMesh>();

    [SerializeField] float minDistance = .1f;
    [SerializeField] float lineThickness = 1f;
    [SerializeField] float curveThresholdAngle = 90f;


    [SerializeField] GameObject splinePrefab;
    
    private Vector3 lastMousePosition;
    private Vector3 lastMouseDir;

    public int drawStrokes = 0;
    public PenProperty currProperty;
    
    [Header("Properties set through Drawer PenProperty")]
    [SerializeField] public Rigidbody2D rb2d;
    [SerializeField] public Collider2D col2d;
    private Mesh mesh;
    private float drawSize = 1;
    private int maxStrokes;
    private List<Vector2> pointList;
    // Only the DrawMesh owner may request finalization. These guards make finish
    // and destruction idempotent when input, collision, and physics overlap.
    private bool finishRequested;
    private bool finishApplied;
    private bool destroyRequestSent;
    private bool destroyDispatchSent;
    private bool destroyApplied;

    public override void OnEnable()
    {
        base.OnEnable();
        activeDrawMeshes.Add(this);
    }

    public override void OnDisable()
    {
        activeDrawMeshes.Remove(this);
        base.OnDisable();
    }

    private void OnDestroy()
    {
        activeDrawMeshes.Remove(this);
    }

    /// <summary>
    /// Finds visible strokes on this client without querying the physics scene. The
    /// Drawer copy intentionally has Rigidbody2D.simulated disabled, so its collider
    /// is absent from Physics2D queries even though its locally rebuilt mesh is visible.
    /// </summary>
    public static void CollectEraserHits(Vector2 center, float radius, List<DrawMesh> results)
    {
        results.Clear();
        foreach (DrawMesh drawMesh in activeDrawMeshes)
        {
            if (drawMesh != null && drawMesh.OverlapsEraser(center, radius))
                results.Add(drawMesh);
        }
    }

    private bool OverlapsEraser(Vector2 center, float radius)
    {
        if (drawStrokes <= 0 || currProperty == null || photonView == null || _vertices == null || _triangles == null || _triangles.Length < 3)
            return false;

        Renderer meshRenderer = GetComponent<Renderer>();
        if (meshRenderer == null)
            return false;

        Bounds broadPhaseBounds = meshRenderer.bounds;
        broadPhaseBounds.Expand(radius * 2f);
        if (!broadPhaseBounds.Contains(center))
            return false;

        for (int i = 0; i + 2 < _triangles.Length; i += 3)
        {
            int indexA = _triangles[i];
            int indexB = _triangles[i + 1];
            int indexC = _triangles[i + 2];
            if (indexA < 0 || indexB < 0 || indexC < 0 ||
                indexA >= _vertices.Length || indexB >= _vertices.Length || indexC >= _vertices.Length)
                continue;

            Vector2 a = transform.TransformPoint(_vertices[indexA]);
            Vector2 b = transform.TransformPoint(_vertices[indexB]);
            Vector2 c = transform.TransformPoint(_vertices[indexC]);
            if (PointInTriangle(center, a, b, c) ||
                DistanceToSegmentSquared(center, a, b) <= radius * radius ||
                DistanceToSegmentSquared(center, b, c) <= radius * radius ||
                DistanceToSegmentSquared(center, c, a) <= radius * radius)
                return true;
        }

        return false;
    }

    private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        const float epsilon = 0.00001f;
        if (Mathf.Abs(Cross(b - a, c - a)) <= epsilon)
            return false;

        float crossAB = Cross(b - a, point - a);
        float crossBC = Cross(c - b, point - b);
        float crossCA = Cross(a - c, point - c);
        bool hasNegative = crossAB < -epsilon || crossBC < -epsilon || crossCA < -epsilon;
        bool hasPositive = crossAB > epsilon || crossBC > epsilon || crossCA > epsilon;
        return !(hasNegative && hasPositive);
    }

    private static float DistanceToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= Mathf.Epsilon)
            return (point - start).sqrMagnitude;

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        return (point - (start + segment * t)).sqrMagnitude;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    public Vector3[] _vertices;
    public Vector2[] _uv;
    public int[] _triangles;




    [SerializeField] private SpriteShapeController spriteShapeController;
    
    [PunRPC]
//Initialize the properties
    void RPC_InitializedDrawProperty(Vector3 mousePos, string penType, bool interactable)
    {
        mesh = new Mesh();
        SetPenProperty(penType);
    
        pointList = new List<Vector2>();
        pointList.Add(new Vector2(mousePos.x, mousePos.y)); // If used for other logic

        GetComponent<MeshRenderer>().material = currProperty.drawingMaterial;
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


        // --- MODIFIED UV INITIALIZATION ---
        // The "end" of this degenerate quad (vertices 2 and 3) will have V=1.
        // The "start" (vertices 0 and 1) will have V=0.
        uv[0] = new Vector2(0, 0); // Corresponds to vertices[0]
        uv[1] = new Vector2(1, 0); // Corresponds to vertices[1]
        uv[2] = new Vector2(0, 1); // Corresponds to vertices[2] (used as vIndex0 in first segment)
        uv[3] = new Vector2(1, 1); // Corresponds to vertices[3] (used as vIndex1 in first segment)
        // --- END MODIFIED UV INITIALIZATION ---

        triangles[0] = 0;
        triangles[1] = 3; // Original: 0-3-1
        triangles[2] = 1;

        triangles[3] = 1;
        triangles[4] = 3; // Original: 1-3-2
        triangles[5] = 2;

        _vertices = vertices;
        _uv = uv;
        _triangles = triangles;


        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.MarkDynamic();

        if (interactable)
            InteractSetting();
        
        
        if (photonView.IsMine && !GameManager.Instance.devSpawn)
        {
            GetComponent<Rigidbody2D>().simulated = false;
        }

        GetComponent<MeshFilter>().mesh = mesh;
        // lastMousePosition = mousePos; // Already set
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

    public Vector3 GetLastMousePosition()
    {
        return lastMousePosition;
    }

    public void RequestStartDraw(Vector3 mousePos)
    {
        if (!photonView.IsMine || finishRequested)
            return;

        Vector3 direction = (mousePos - lastMousePosition).normalized;
        float distance = Vector3.Distance(lastMousePosition, mousePos);
        if (!_DrawPathValidate(lastMousePosition, direction, distance))
            return;

        photonView.RPC(nameof(RPC_StartDraw), RpcTarget.All, mousePos);
    }

    //RPC: Draw mesh according to the mosuePos
   [PunRPC]
    void RPC_StartDraw(Vector3 mousePos)
    {
        float distance = Vector3.Distance(lastMousePosition, mousePos);
        if (((drawStrokes < maxStrokes) || maxStrokes <= 0) && !finishApplied)
        {
            if (((drawStrokes < maxStrokes) || maxStrokes <= 0))
            {
                drawStrokes++;
                currProperty.currentStrokes++;
                if (pointList != null) pointList.Add(mousePos); // Assuming pointList is for other logic like ElectricSpline
    
                // RPC_StartDraw already arrives once on every client. Playing locally
                // avoids each receiver raising the same room-wide audio event again.
                AudioManager.PlayOne(AudioManager.DRAWSFX);
    
                Vector3[] vertices = new Vector3[mesh.vertices.Length + 2];
                Vector2[] uv = new Vector2[mesh.uv.Length + 2]; // UV array also needs to be expanded
                int[] triangles = new int[mesh.triangles.Length + 6];
            
                mesh.vertices.CopyTo(vertices, 0);
                mesh.uv.CopyTo(uv, 0); // Copy existing UVs
                mesh.triangles.CopyTo(triangles, 0);
    
                // vIndex0 and vIndex1 point to the last two vertices of the previous segment
                // Their indices in the 'vertices' array (which is mesh.vertices.Length + 2 long)
                // are effectively mesh.vertices.Length - 2 and mesh.vertices.Length - 1 if we consider the old mesh state.
                // However, the script uses indices relative to the *new, expanded* 'vertices' array length
                // to access the *copied old* vertices.
                int vIndex = vertices.Length - 4; // Base index for the previous quad's end vertices in the *new* array
                int vIndex0 = vIndex + 0; // Previous segment's "up" (relative to its forward direction)
                int vIndex1 = vIndex + 1; // Previous segment's "down"
                int vIndex2 = vIndex + 2; // New segment's "up"
                int vIndex3 = vIndex + 3; // New segment's "down"
    
                Vector3 mouseForwardVector = (mousePos - lastMousePosition).normalized;
                // If mouseForwardVector is zero (mouse hasn't moved enough), handle to avoid issues
                if (mouseForwardVector == Vector3.zero) {
                    mouseForwardVector = Vector3.right; // Or some other default, or skip drawing this segment
                }
    
                Vector3 normal2D = new Vector3(0, 0, -drawSize); // Used to get a perpendicular vector in XY plane
                //float lineThickness = 1f; // You can adjust this thickness
                Vector3 newVertexUp = mousePos + Vector3.Cross(mouseForwardVector, normal2D).normalized * lineThickness;
                Vector3 newVertexDown = mousePos + Vector3.Cross(mouseForwardVector, normal2D * -1f).normalized * lineThickness;
    
                if(vIndex == 2)
                {
                    print("start of mesh");
                    vertices[vIndex0] = vertices[vIndex0] + Vector3.Cross(mouseForwardVector, normal2D).normalized * lineThickness;
                    vertices[vIndex1] = vertices[vIndex1] + Vector3.Cross(mouseForwardVector, normal2D * -1f).normalized * lineThickness;
    
                }
    
    
                float angle = 180;
                if(lastMouseDir != null && lastMouseDir != Vector3.zero)
                {
                    float dot = Vector2.Dot(lastMouseDir.normalized, mouseForwardVector.normalized);
                    angle = 180 - Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
                    print("DrawMesh-- " + angle);
                }
    
                if (angle < curveThresholdAngle)
                {
                    float cross = -lastMouseDir.x * mouseForwardVector.y - -lastMouseDir.y * mouseForwardVector.x;

                    if (cross < 0)
                    {
                        var temp = vIndex1;
                        vIndex1 = vIndex0;
                        vIndex0 = temp;
                    }

    
    
                    int curves = 5;
    
                    int newVerticesSize = vertices.Length + curves + 2 ;
    
                    Vector3[] new_vertices = new Vector3[newVerticesSize];
                    Array.Copy(vertices, new_vertices, vertices.Length);
                    vertices = new_vertices;
    
    
                    int vIndexAfterCurve = vertices.Length - curves + 1 - 4;

                    vertices[newVerticesSize - 4] = newVertexUp - mouseForwardVector * distance;
                    vertices[newVerticesSize - 3] = newVertexDown - mouseForwardVector * distance;
                    vertices[newVerticesSize - 2] = newVertexUp;
                    vertices[newVerticesSize - 1] = newVertexDown;

                    vertices[vIndex2] = lastMousePosition;




                    Vector3 vStart = vertices[vIndex0] - vertices[vIndex2];
                    Vector3 vEnd = (cross > 0 ? vertices[newVerticesSize - 4] : vertices[newVerticesSize - 3]) - vertices[vIndex2];

                    float totalAngle = Vector3.SignedAngle(vStart, vEnd, Vector3.forward);
                    float angleStep = totalAngle / curves;


                    for (int i = 1; i < curves; i++)
                    {
                        float angleIncrement = angleStep * i;
                        //Vector3 dir = Quaternion.AngleAxis(cross > 0 ? angleIncrement : -angleIncrement, Vector3.forward) * vStart;
                        Vector3 dir = Quaternion.AngleAxis(angleIncrement, Vector3.forward) * vStart;

                        vertices[vIndexAfterCurve + i - 1] = vertices[vIndex2] + dir;
                    }


                    int newTriangleSize = triangles.Length + 3 * curves;
                    int[] new_triangles = new int[newTriangleSize];
                    Array.Copy(triangles, new_triangles, triangles.Length);
                    triangles = new_triangles;
    
                    int tIndexAfterCurve = triangles.Length - 3 * curves - 6;
    
                    triangles[tIndexAfterCurve + 0] = vIndex2;
                    triangles[tIndexAfterCurve + 1] = cross > 0 ? vIndex0 : vIndexAfterCurve;
                    triangles[tIndexAfterCurve + 2] = cross > 0 ? vIndexAfterCurve : vIndex0;


                    for (int i = 1; i < curves; i++)
                    {
                        triangles[tIndexAfterCurve + i * 3 + 0] = vIndex2;
                        triangles[tIndexAfterCurve + i * 3 + 1] = cross > 0 ? vIndexAfterCurve + i - 1 : i == curves - 1 ? vIndexAfterCurve + i + 1 : vIndexAfterCurve + i;
                        triangles[tIndexAfterCurve + i * 3 + 2] = cross > 0 ? vIndexAfterCurve + i : vIndexAfterCurve + i -1;
                    }


                    int tIndex = triangles.Length - 6;
                    // Triangle 1:
                    triangles[tIndex + 0] = newVerticesSize - 3;
                    triangles[tIndex + 1] = newVerticesSize - 2;
                    triangles[tIndex + 2] = newVerticesSize - 1;

                    // Triangle 2: 
                    triangles[tIndex + 3] = newVerticesSize - 3;
                    triangles[tIndex + 4] = newVerticesSize - 4;
                    triangles[tIndex + 5] = newVerticesSize - 2;


                    int newUVSize = uv.Length + curves + 2;
    
                    Vector2[] new_uv = new Vector2[newUVSize];
                    Array.Copy(uv, new_uv, uv.Length);
                    uv = new_uv;
                    float previousV = uv[vIndex0].y;
                    uv[vIndex2] = new Vector2(0, previousV + 1.0f);
                    uv[vIndex3] = new Vector2(1, previousV + 1.0f);
                    uv[vIndex3 + 1] = new Vector2(1, previousV + 1.0f);
                    uv[vIndex3 + 2] = new Vector2(1, previousV + 1.0f);
                    uv[vIndex3 + 3] = new Vector2(1, previousV + 1.0f);
                    uv[vIndex3 + 4] = new Vector2(1, previousV + 1.0f);
                    uv[vIndex3 + 5] = new Vector2(1, previousV + 1.0f);
    
                }
                else
                {
                    vertices[vIndex2] = newVertexUp;
                    vertices[vIndex3] = newVertexDown;
    
                    // --- NEW UV CALCULATION ---
                    // Assumes uv[vIndex0] and uv[vIndex1] hold the UVs of the end of the last segment.
                    // U goes from 0 to 1 across the width.
                    // V increments by 1 for each new segment, allowing textures to tile along the length.
                    float previousV = uv[vIndex0].y; // Get V from one of the previous segment's end points
                                                     // (assuming U-coordinates differ but V-coordinates are the same for that edge)
    
                    uv[vIndex2] = new Vector2(0, previousV + 1.0f); // New "up" vertex UV
                    uv[vIndex3] = new Vector2(1, previousV + 1.0f); // New "down" vertex UV
                                                                    // --- END NEW UV CALCULATION ---
    
                    int tIndex = triangles.Length - 6;
    
                    // Triangles for the new quad. Ensure Counter-Clockwise (CCW) winding for front faces.
                    // Quad formed by (vIndex0, vIndex1) and (vIndex2, vIndex3)
                    // vIndex0 --- vIndex2
                    //   |           |
                    // vIndex1 --- vIndex3
    
                    // Triangle 1: (vIndex0, vIndex2, vIndex1)
                    triangles[tIndex + 0] = vIndex0;
                    triangles[tIndex + 1] = vIndex2;
                    triangles[tIndex + 2] = vIndex1;
    
                    // Triangle 2: (vIndex1, vIndex2, vIndex3)
                    triangles[tIndex + 3] = vIndex1;
                    triangles[tIndex + 4] = vIndex2;
                    triangles[tIndex + 5] = vIndex3;
                }
    
                _vertices = vertices;
                _uv = uv;
                _triangles = triangles;
    
    
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.triangles = triangles;
    
                // --- RECALCULATE NORMALS AND BOUNDS ---
                mesh.RecalculateNormals(); // Crucial for lighting
                mesh.RecalculateBounds();  // Good for culling and other calculations
                // --- END RECALCULATION ---
    
                lastMousePosition = mousePos;
                lastMouseDir = mouseForwardVector;
    
                PolygonCollider2D polyCol = GetComponent<PolygonCollider2D>();
                if (polyCol != null) // Check if the component exists
                {
                    // Ensure GetEdge returns valid points for PolygonCollider2D
                    // The GetEdge method might need review if it's causing issues or not matching the visual mesh.
                    Vector2[] colliderPoints = System.Array.ConvertAll(mesh.vertices, v3 => new Vector2(v3.x, v3.y));
                    polyCol.points = GetEdge(colliderPoints);
                }
    
    
                //ClearDebugMeshes();
                //InstantiateDebugMeshes(vertices);
            }
    
        }
    }
    
    private bool finished = false;
    
    private bool _DrawPathValidate(Vector2 startPos, Vector2 direction, float distance)
    {
        if (currProperty.penType == PenProperty.PenType.Electric)
        { 
            RaycastHit2D[] hit = Physics2D.RaycastAll(startPos, direction, distance, LayerMask.GetMask("DrawProhibited"));
            if (hit.Length > 0)
                return false;
            return true;
        }
        
        if (finished)
            return false;
        RaycastHit2D[] hits = Physics2D.RaycastAll(startPos, direction, distance,  LayerMask.GetMask("DrawProhibited","Draw", "Platform"));
        if (hits.Length > 0 )// && hit.collider.gameObject.layer == LayerMask.NameToLayer("Draw"))
        {
            foreach (RaycastHit2D hit in hits)
            {
                if (!hit.collider.gameObject.CompareTag("Electric"))
                {
                    finished = true;
                    RequestFinishDraw();
                    Drawer.Instance.ForceFinishDraw(this);
                    return false;
                }
            }
        }
        return true;
    }

    public void RequestFinishDraw()
    {
        if (!photonView.IsMine || finishRequested)
            return;

        finishRequested = true;
        photonView.RPC(nameof(RPC_FinishDraw), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_FinishDraw()
    {
        if (finishApplied)
            return;

        finishApplied = true;
        finished = true;
        lastMouseDir = Vector3.zero;
        if (drawStrokes <= 0 || pointList == null || pointList.Count == 0)
        {
            // The creating Drawer owns this network object; the Master cannot destroy it by role alone.
            if (photonView.IsMine)
                PhotonNetwork.Destroy(gameObject);
        }
        else
        {
            //currProperty.currentStrokes += drawStrokes;
            GetComponent<MeshRenderer>().material = currProperty.material;

            //Debug.LogError("vertices" + mesh.vertices.Length + "triangle" + mesh.triangles.Length + "uv" + mesh.uv.Length);
            rb2d.centerOfMass = col2d.bounds.center;
            if (currProperty.gravity) rb2d.bodyType = RigidbodyType2D.Dynamic;
            if (currProperty.mass > 0) rb2d.mass = currProperty.mass;
            //***!!! if currproperty is trigger just remove the collider for now.
            if (currProperty.trigger)  //Destroy(col2d);
                col2d.isTrigger = true;
                
            if (PhotonNetwork.OfflineMode ||  GameManager.Instance.devSpawn || (RolesManager.PlayerRole)(int)PhotonNetwork.LocalPlayer.CustomProperties["Role"] != RolesManager.PlayerRole.Drawer)
            {
                GetComponent<Rigidbody2D>().simulated = true;
            }
            
            this.gameObject.tag = SetUpObjectTag(currProperty.penType);
            if (photonView.IsMine && PhotonNetwork.LocalPlayer.ActorNumber != Runner.Instance.actorNum)
            {
                Debug.Log($"Transferring drawn object ownership to Runner actor {Runner.Instance.actorNum}." );
                photonView.TransferOwnership(Runner.Instance.actorNum);
            }
            //Electric
            if (currProperty.penType == PenProperty.PenType.Electric)
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

    [PunRPC]
    private void RPC_DestroySelf()
    {
        if (photonView.IsMine)
            PhotonNetwork.Destroy(this.gameObject);
    }

    [PunRPC]
    private void RPC_CutDownMesh(int uv, int vert, int tri)
    {
        if (uv < mesh.uv.Length || vert < mesh.vertices.Length || tri < mesh.triangles.Length)
        {
            //Not efficient create new array and copies element
            Vector3[] vertices = new Vector3[vert];
            Vector2[] uv_arr = new Vector2[uv]; // UV array also needs to be expanded
            int[] triangles = new int[tri];
            
            Array.Copy(mesh.triangles, triangles, tri);
            Array.Copy(mesh.uv, uv_arr, uv);
            Array.Copy(mesh.vertices, vertices, vert);
            
            mesh.vertices = vertices;
            mesh.uv = uv_arr;
            mesh.triangles = triangles;
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

        int index = 1;

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
                //print(array[i]);
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
        // Ownership changes are state transitions only. Destruction is serialized
        // through the Master Client and must never be inferred from a transfer.
    }

    public void SelfDestroy()
    {
        RequestAuthoritativeDestroy();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("DeathDesuwa") && photonView.IsMine)
        {
            print("Wood into death");
            RequestAuthoritativeDestroy();
        }
    }

    private void RequestAuthoritativeDestroy()
    {
        if (!photonView.IsMine || destroyRequestSent || currProperty == null)
            return;

        destroyRequestSent = true;
        photonView.RPC(nameof(RPC_RequestSelfDestroy), RpcTarget.MasterClient);
    }

    [PunRPC]
    private void RPC_RequestSelfDestroy(PhotonMessageInfo info)
    {
        bool validOwner = PhotonNetwork.OfflineMode ||
            (info.Sender != null && info.Sender.ActorNumber == photonView.OwnerActorNr);
        if (!PhotonNetwork.IsMasterClient || !validOwner || destroyApplied || currProperty == null)
            return;

        Drawer drawer = Drawer.Instance;
        if (drawer != null)
        {
            drawer.photonView.RPC(nameof(Drawer.RPC_DirectErase), RpcTarget.AllViaServer,
                (int)currProperty.penType, drawStrokes, (Vector2)col2d.bounds.center, gameObject.tag);
        }
        DestroyAfterMasterAuthorization();
    }

    /// <summary>Routes Master-approved deletion through the PhotonView owner, who has legal destroy authority.</summary>
    public void DestroyAfterMasterAuthorization()
    {
        if (!PhotonNetwork.IsMasterClient || destroyApplied || destroyDispatchSent)
            return;

        if (photonView.IsMine)
        {
            destroyDispatchSent = true;
            destroyApplied = true;
            PhotonNetwork.Destroy(gameObject);
            return;
        }

        destroyDispatchSent = true;
        StartCoroutine(DispatchMasterApprovedDestroy());
    }

    // Ownership may be moving to the Runner or falling back to the Master while
    // an owner is inactive. Broadcast the approved command so the current PUN
    // controller can act, retrying briefly while its local controller cache updates.
    private IEnumerator DispatchMasterApprovedDestroy()
    {
        const int maxAttempts = 10;
        const float retryDelay = 0.1f;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (!PhotonNetwork.IsMasterClient || destroyApplied || this == null)
                yield break;

            if (photonView.IsMine)
            {
                destroyApplied = true;
                PhotonNetwork.Destroy(gameObject);
                yield break;
            }

            photonView.RPC(nameof(RPC_DestroyAfterMasterAuthorization), RpcTarget.AllViaServer);
            yield return new WaitForSeconds(retryDelay);
        }

        if (this != null && !destroyApplied)
            Debug.LogWarning($"Master-approved erase for DrawMesh {photonView.ViewID} was not acknowledged by its current controller.");
    }

    [PunRPC]
    private void RPC_DestroyAfterMasterAuthorization(PhotonMessageInfo info)
    {
        if (!photonView.IsMine || destroyApplied || info.Sender == null || PhotonNetwork.MasterClient == null ||
            info.Sender.ActorNumber != PhotonNetwork.MasterClient.ActorNumber)
            return;

        destroyApplied = true;
        PhotonNetwork.Destroy(gameObject);
    }

    private List<GameObject> instantiatedDebugMeshes = new List<GameObject>();
    private void ClearDebugMeshes()
    {
        foreach (GameObject go in instantiatedDebugMeshes)
        {
            if (go != null)
            {
                Destroy(go);
            }
        }
        instantiatedDebugMeshes.Clear();
    }
    [SerializeField] private GameObject debugMesh;
    private void InstantiateDebugMeshes(Vector3[] vertices)
    {
        if (debugMesh == null)
        {
            Debug.LogWarning("Debug Mesh prefab is not assigned. Skipping debug mesh instantiation.");
            return;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            // Instantiate at vertex position with current GameObject's rotation
            GameObject newDebugMesh = Instantiate(debugMesh, transform.TransformPoint(vertices[i]), transform.rotation);
            newDebugMesh.name = $"Vertex_{i}"; // Name it with its index
            newDebugMesh.transform.SetParent(transform); // Parent to this GameObject for organization
            instantiatedDebugMeshes.Add(newDebugMesh);
        }
    }


    //public void OnDestroy()
    //{
    //    currProperty.currentStrokes -= drawStrokes;
    //}

}


