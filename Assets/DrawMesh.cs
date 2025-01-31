using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class DrawMesh : MonoBehaviour
{
    [SerializeField] private Transform debugVisual1;
    [SerializeField] private Transform debugVisual2;


    [SerializeField] float minDistance = .1f;
    private Mesh mesh;
    private Vector3 lastMousePosition;

    void Start()
    {

    }



    private void Awake()
    {





    }



    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            mesh = new Mesh();

            Vector3[] vertices = new Vector3[4];
            Vector2[] uv = new Vector2[4];
            int[] triangles = new int[6];

            vertices[0] = GetMouseWorldPosition();
            vertices[1] = GetMouseWorldPosition();
            vertices[2] = GetMouseWorldPosition();
            vertices[3] = GetMouseWorldPosition();

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


            GetComponent<MeshFilter>().mesh = mesh;


            lastMousePosition = GetMouseWorldPosition();

        }

        if (Input.GetMouseButton(0))
        {
            if (Vector3.Distance(GetMouseWorldPosition(), lastMousePosition) > minDistance)
            {
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

                Vector3 mouseForwardVector = (GetMouseWorldPosition() - lastMousePosition).normalized;
                Vector3 normal2D = new Vector3(0, 0, -1f);
                float lineThickness = 1f;
                Vector3 newVertexUp = GetMouseWorldPosition() + Vector3.Cross(mouseForwardVector, normal2D) * lineThickness;
                Vector3 newVertexDown = GetMouseWorldPosition() + Vector3.Cross(mouseForwardVector, normal2D * -1f) * lineThickness;

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

                lastMousePosition = GetMouseWorldPosition();

            }



        }

        if (Input.GetMouseButtonUp(0))
        {
            EdgeCollider2D col = GetComponent<EdgeCollider2D>();
            Vector2[] v2 = System.Array.ConvertAll(mesh.vertices, v3 => new Vector2(v3.x, v3.y));
            col.points = GetEdge(v2);



        }
    }


    private Vector2[] GetEdge(Vector2[] array)
    {
        List<Vector2> v = new List<Vector2>();

        int index = 3;

        Stack<Vector2> stack = new Stack<Vector2>();


        for (int i = 0; i < 4; i++)
        {
            v.Add(array[i]);
        }

        for( int i = 4; i < array.Length; i++)
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

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldPosition.z = 0;
        return worldPosition;
    }
}
