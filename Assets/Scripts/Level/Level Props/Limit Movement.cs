using System;
using UnityEngine;

public class LimitMovement : MonoBehaviour
{
    private enum Limitation
    {
        X,
        Y,
        Z
    }

    [SerializeField] private float min;
    [SerializeField] private float max;

    // Update is called once per frame
    void Update()
    {
        transform.position = new Vector3 (Math.Clamp(this.transform.position.x, min, max), transform.position.y, transform.position.z);
    }
}
