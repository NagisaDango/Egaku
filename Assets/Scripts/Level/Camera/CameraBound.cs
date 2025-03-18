using UnityEngine;

public class CameraBound : MonoBehaviour
{
    private Camera _camera;
    private Collider2D _collider;
    public Bounds _bounds;

    private void Awake()
    {
        _camera = Camera.main;
        _collider = GetComponent<Collider2D>();
        _bounds = _collider.bounds;
    }

    //TODO: In update nowchange 
    private void Update()
    {
        UpdateBounds();
    }

    public void UpdateBounds()
    {
        _bounds = _collider.bounds;
    }
}
