using UnityEngine;

public class SimpleAnim : MonoBehaviour
{
    [SerializeField] private Vector3 startPos;
    [SerializeField] private Vector3 endPos;

    [SerializeField] private float time;
    [SerializeField] private bool autoStart;
    private bool started = false;
    private float startTime;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (autoStart)
        {
            started = true;
            startTime = Time.time;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (started)
        {
            this.transform.position = Vector3.Lerp(startPos, endPos,  (Time.time - startTime) / time);
        }
    }
}
