using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;

[RequireComponent(typeof(SpriteShapeController))]
public class EditableObj : MonoBehaviour, IPointerDownHandler
{
    SpriteShapeController controller;
    public GameObject pointPrefab;

    private void Start()
    {
        controller = GetComponent<SpriteShapeController>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            AddNewSplinePoint();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (this.transform.childCount > 0) return;

        GetComponent<Collider2D>().enabled = false;
        Debug.Log("Selecting: " + this.name);
        //controller = this.GetComponent<SpriteShapeController>();
        for (int i = 0; i < controller.spline.GetPointCount(); i++)
        {
            GameObject point = Instantiate(pointPrefab, Vector3.zero, Quaternion.identity, this.transform);
            point.transform.localPosition = controller.spline.GetPosition(i);
            //*** Using GetComponent here, change it to use LevelEditor to handle general event
            point.GetComponent<SplinePoint>().SetAssociateObj(this);
        }
    }

    public void AddNewSplinePoint()
    {
        Spline controlSpline = controller.spline;
        int splineCount = controlSpline.GetPointCount();

        Vector2 addingPos = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
        int minIndex = 0;
        float minDis = Vector2.Distance(controlSpline.GetPosition(0), addingPos);
        for (int i = 1; i < splineCount; i++)
        {
            float newDis = Vector2.Distance(controlSpline.GetPosition(i), addingPos);
            if (newDis < minDis)
            {
                minIndex = i;
                minDis = newDis;
            }
        }
        Vector2 minPoint = controlSpline.GetPosition(minIndex);
        Vector2 adjPointBefore = controlSpline.GetPosition((minIndex - 1 + splineCount) % splineCount);
        Vector2 adjPointAfter = controlSpline.GetPosition((minIndex + 1) % splineCount);

        int insertIndex = ValidateAddingPoint(addingPos, minPoint, adjPointBefore, adjPointAfter) ? minIndex : ((minIndex + 1) % splineCount);
        /*
        int insertIndex =
            Vector2.Distance(addingPos, adjPointBefore) <
            Vector2.Distance(addingPos, adjPointAfter)
            ? minIndex : ((minIndex + 1) % splineCount);
        */
        controlSpline.InsertPointAt(insertIndex, (Vector2)Camera.main.transform.InverseTransformPoint(addingPos));
        controlSpline.SetTangentMode(insertIndex, ShapeTangentMode.Continuous);

        GameObject point = Instantiate(pointPrefab, Vector3.zero, Quaternion.identity, this.transform);
        point.transform.position = (Vector2)addingPos;
        point.transform.SetSiblingIndex(insertIndex);
    }

    private bool ValidateAddingPoint(Vector2 addingPoint, Vector2 closetPoint, Vector2 validatingPoint, Vector2 testPoint)
    {
        float den = (addingPoint.x - validatingPoint.x) * (closetPoint.y - testPoint.y) - (addingPoint.y - validatingPoint.y) * (closetPoint.x - testPoint.x);

        float num1 = (validatingPoint.y - testPoint.y) * (closetPoint.x - testPoint.x) - (validatingPoint.x - testPoint.x) * (closetPoint.y - testPoint.y);
        float num2 = (validatingPoint.y - testPoint.y) * (addingPoint.x - validatingPoint.x) - (validatingPoint.x - testPoint.x) * (addingPoint.y - validatingPoint.y);
        
        float t1 = num1 / den;
        float t2 = num2 / den;

        return !((t1 >= 0 && t1 <= 1) && (t2 >= 0 && t2 <= 1));
    }

    public void DeletePoint(int index)
    {
        if (index > 0 && index < controller.spline.GetPointCount())
        {
            Destroy(transform.GetChild(index).gameObject);
            controller.spline.RemovePointAt(index);
        }
    }
}
