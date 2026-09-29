using UnityEngine;

/// <summary>
/// Optional visual category for map terrain. Equal IDs share one outline silhouette;
/// differing IDs retain a boundary even when the renderers touch or overlap.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlatformOutlineGroup : MonoBehaviour
{
    // Existing unmarked ground uses 0 and BreakablePlatform glass uses 1. Put this on a
    // platform root to override that fallback without changing its physics or Photon setup.
    [Min(0)]
    [Tooltip("Renderers with the same ID merge into one outline. 0 = ground, 1 = glass by convention.")]
    [SerializeField] private int groupId;

    public int GroupId => groupId;
}
