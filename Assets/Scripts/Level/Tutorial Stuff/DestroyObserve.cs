using System;
using UnityEngine;

public class DestroyObserve : MonoBehaviour
{
    public event Action _OnDestroy;
    private void OnDestroy()
    {
        _OnDestroy?.Invoke();
    }
}
