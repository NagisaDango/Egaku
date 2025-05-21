public interface HoldableObject
{
    public bool ValidateHold();
    public void Reset();
    public void ToggleCollider(bool state);
}
