using System;
using UnityEngine;
using UnityEngine.U2D;

public class PlatformEditMode : MonoBehaviour, IMode
{
    SpriteShapeController controller;
    public GameObject pointPrefab;

    /// <summary>
    /// Spawn the node icons for adjusting spline points
    /// </summary>
    public void Initialize()
    {
        if(this.transform.childCount > 0) return;
        this.gameObject.layer = LayerMask.NameToLayer("Editing");
        controller = this.GetComponent<SpriteShapeController>();
        for (int i = 0; i < controller.spline.GetPointCount(); i++)
        {
            GameObject point = Instantiate(pointPrefab, Vector3.zero, Quaternion.identity, this.transform);
            point.transform.localPosition = controller.spline.GetPosition(i);
            //*** Using GetComponent here, change it to use LevelEditor to handle general event
            point.GetComponent<SplinePoint>().SetAssociateObj(this);
            
        }
    }

    public void ModeUpdate()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            AddNewSplinePoint();
        }
    }
    
    /// <summary>
    /// Deleting useless stuff
    /// </summary>
    public void Dispose()
    {
        print("Disposing");
        this.gameObject.layer = LayerMask.NameToLayer("Default");
        //***Optimization: Object pool
        foreach (Transform child in this.transform)
        {
            GameObject.Destroy(child.gameObject);
        }
    }
    
    /// <summary>
    /// Insert a new point to the spline
    /// </summary>
    private void AddNewSplinePoint()
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
        
        print((Vector2)Camera.main.transform.InverseTransformPoint(addingPos));
        controlSpline.InsertPointAt(insertIndex, (Vector2)(Camera.main.transform.InverseTransformPoint(addingPos) - this.transform.localPosition));
        controlSpline.SetTangentMode(insertIndex, ShapeTangentMode.Continuous);

        GameObject point = Instantiate(pointPrefab, Vector3.zero, Quaternion.identity, this.transform);
        point.transform.position = (Vector2)addingPos;
        point.GetComponent<SplinePoint>().SetAssociateObj(this);
        point.transform.SetSiblingIndex(insertIndex);
    }
    
    /// <summary>
    /// Check if the vertex form by validating point and closetPoint is proper (By forming a segment with closet point and test point, and another segment from adding point to validating point and see if two lines intersect, if intersect, not valid, else valid)
    /// </summary>
    /// <param name="addingPoint">The point that is going to be add to spline</param>
    /// <param name="closetPoint">The closest point to the adding point</param>
    /// <param name="validatingPoint">One of the adjacent point next to closest point</param>
    /// <param name="testPoint">Another adjacent point next to closest point</param>
    /// <returns></returns>
    private bool ValidateAddingPoint(Vector2 addingPoint, Vector2 closetPoint, Vector2 validatingPoint, Vector2 testPoint)
    {
        float den = (addingPoint.x - validatingPoint.x) * (closetPoint.y - testPoint.y) - (addingPoint.y - validatingPoint.y) * (closetPoint.x - testPoint.x);

        float num1 = (validatingPoint.y - testPoint.y) * (closetPoint.x - testPoint.x) - (validatingPoint.x - testPoint.x) * (closetPoint.y - testPoint.y);
        float num2 = (validatingPoint.y - testPoint.y) * (addingPoint.x - validatingPoint.x) - (validatingPoint.x - testPoint.x) * (addingPoint.y - validatingPoint.y);
        
        float t1 = num1 / den;
        float t2 = num2 / den;

        return !((t1 >= 0 && t1 <= 1) && (t2 >= 0 && t2 <= 1));
    }
    
    /// <summary>
    /// Remove a point from the spline
    /// </summary>
    /// <param name="index">removing index</param>
    public void DeletePoint(int index)
    {
        int splineCount = controller.spline.GetPointCount();
        if (index >= 0 && index < splineCount && splineCount > 2)
        {
            Destroy(transform.GetChild(index).gameObject);
            controller.spline.RemovePointAt(index);
        }
    }
    
}
