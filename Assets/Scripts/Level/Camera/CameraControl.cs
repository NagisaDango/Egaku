using UnityEngine;

public class CameraControl : MonoBehaviour
{
    [SerializeField] private CameraBound _cameraBound;

    [SerializeField] private Camera cam;

    void Update()
    {
        transform.position = SetBoundedPos();
        if(cam.orthographicSize > MaxHeight())
        {
            cam.orthographicSize = MaxHeight();
        }
    }

    private float MaxHeight()
    {
        return Mathf.Min(_cameraBound._bounds.size.x / 2 / cam.aspect, _cameraBound._bounds.size.y / 2);
    }

    private Vector3 SetBoundedPos()
    {
        float h = cam.orthographicSize;
        float w = cam.orthographicSize * cam.aspect;
        return new Vector3(
            Mathf.Clamp(this.transform.position.x , _cameraBound._bounds.min.x + w, _cameraBound._bounds.max.x - w),
            Mathf.Clamp(this.transform.position.y, _cameraBound._bounds.min.y + h, _cameraBound._bounds.max.y - h),
            this.transform.position.z
            );
    }
}
