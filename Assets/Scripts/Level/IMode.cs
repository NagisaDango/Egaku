using UnityEngine;

public interface IMode
{
    public void Initialize();
    public void ModeUpdate();
    public void Dispose();
}
