using UnityEngine;

/// <summary>Authored prefab rig: stationary endpoints and track, one physics platform.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class SlidePlatformRig : MonoBehaviour
{
    [HideInInspector] public Transform endpointA;
    [HideInInspector] public Transform endpointB;
    [HideInInspector] public Transform track;
    [HideInInspector] public Rigidbody2D platform;
    [Range(0, 1)] public float startingPosition = 0.5f;

    // Cache only editor inputs. Derived transforms must not produce perpetual
    // dirty scenes or fight Undo; Play Mode never rewrites the moving body.
    private Vector3 lastA, lastB;
    private float lastStart;
    private Vector2 lastExtents;
    private bool initialized;

    private bool HasValidEndpoints => endpointA != null && endpointB != null && track != null && platform != null &&
        Finite(endpointA.position) && Finite(endpointB.position) &&
        Mathf.Abs(endpointA.position.z - endpointB.position.z) < 0.001f &&
        Vector2.Distance(endpointA.position, endpointB.position) >= 0.01f;

    public bool IsValid => HasValidEndpoints && TryGetPivotRange(out _, out _);

    private Vector2 PlatformExtents()
    {
        if (platform == null) return Vector2.zero;
        float low = 0, high = 0;
        var sprite = platform.GetComponent<SpriteRenderer>();
        if (sprite != null) { low = sprite.localBounds.min.x; high = sprite.localBounds.max.x; }
        var box = platform.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            // Include both visible and physical edges, including off-centre
            // colliders and rounded edges; the larger envelope owns the limit.
            low = Mathf.Min(low, box.offset.x - box.size.x * .5f - box.edgeRadius);
            high = Mathf.Max(high, box.offset.x + box.size.x * .5f + box.edgeRadius);
        }
        float scale = platform.transform.lossyScale.x;
        float a = low * scale, b = high * scale;
        return new Vector2(Mathf.Min(a, b), Mathf.Max(a, b));
    }

    public bool TryGetPivotRange(out float min, out float max)
    {
        var extents = PlatformExtents();
        // Distances along A -> B. Endpoints bound the outer envelope, while the
        // joint still constrains the pivot using this smaller travel interval.
        min = -extents.x;
        max = HasValidEndpoints ? Vector2.Distance(endpointA.position, endpointB.position) - extents.y : 0;
        return HasValidEndpoints && Finite(new Vector3(min, max, 0)) && max >= min;
    }

    private void Awake()
    {
        if (Application.IsPlaying(gameObject)) ConfigurePhysics();
    }

    private void LateUpdate()
    {
        UpdateAuthoringIfNeeded();
    }

    public void UpdateAuthoringIfNeeded()
    {
        if (Application.IsPlaying(gameObject)) return;
        if (endpointA == null || endpointB == null) return;
        if (!initialized || lastA != endpointA.position || lastB != endpointB.position || lastStart != startingPosition || lastExtents != PlatformExtents())
            RefreshAuthoring();
    }

    private void OnValidate() { initialized = false; }

    public void RefreshAuthoring()
    {
        if (Application.IsPlaying(gameObject)) return;
        if (!HasValidEndpoints) { ConfigurePhysics(); return; }
        var renderer = track.GetComponent<SpriteRenderer>();
#if UNITY_EDITOR
        var derived = new Object[] { track, renderer, platform.transform, platform, platform.GetComponent<SliderJoint2D>() };
        var before = new string[derived.Length];
        for (int i = 0; i < derived.Length; i++)
            if (derived[i] != null) before[i] = UnityEditor.EditorJsonUtility.ToJson(derived[i]);
#endif
        var a = endpointA.position;
        var b = endpointB.position;
        // The artwork's long X axis follows the rail. Its physical orientation
        // remains fixed after Play starts, while translation is free along it.
        float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        var rotation = Quaternion.Euler(0, 0, angle);
        var midpoint = (a + b) * 0.5f;
        track.SetPositionAndRotation(midpoint, rotation);
        // Track is a sliced unit square sprite. Using renderer size rather than
        // transform scale keeps the authored hierarchy editable at unit scale.
        if (renderer != null) renderer.size = new Vector2(Vector2.Distance(a, b) / Mathf.Max(0.0001f, Mathf.Abs(track.lossyScale.x)), renderer.size.y);
        var direction = (b - a).normalized;
        bool fits = TryGetPivotRange(out float min, out float max);
        // A rail shorter than the platform cannot contain it. Keep a useful
        // centred preview and freeze physics; the Inspector explains the issue.
        var position = fits ? a + direction * Mathf.Lerp(min, max, Mathf.Clamp01(startingPosition)) : midpoint;
        platform.transform.SetPositionAndRotation(position, rotation);
        // In Edit Mode the physics body's cached pose can lag behind Transform
        // edits. Set it explicitly before computing the joint's local angle.
        platform.position = platform.transform.position;
        platform.rotation = angle;
        ConfigurePhysics();
        lastA = a; lastB = b; lastStart = startingPosition; lastExtents = PlatformExtents(); initialized = true;
#if UNITY_EDITOR
        // Derived values are serialized too: endpoint edits must persist when
        // saving a scene/prefab instance without needing a separate Apply step.
        if (!Application.IsPlaying(gameObject))
        {
            for (int i = 0; i < derived.Length; i++)
            {
                var obj = derived[i];
                // Reloading unchanged saved data must not dirty the scene.
                if (obj == null || before[i] == UnityEditor.EditorJsonUtility.ToJson(obj)) continue;
                UnityEditor.EditorUtility.SetDirty(obj);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
            }
        }
#endif
    }

    public bool ConfigurePhysics()
    {
        if (platform == null) return false;
        var joint = platform.GetComponent<SliderJoint2D>();
        if (joint == null) return false;
        if (!IsValid)
        {
            // Degenerate rails should hold still instead of releasing a freely
            // falling platform. Valid geometry restores the usual constraints.
            joint.enabled = false;
            platform.constraints = RigidbodyConstraints2D.FreezeAll;
            return false;
        }
        Vector2 a = endpointA.position, b = endpointB.position;
        var delta = b - a;
        TryGetPivotRange(out float min, out float max);
        float halfTravel = (max - min) * .5f;
        platform.constraints = RigidbodyConstraints2D.FreezeRotation;
        joint.connectedBody = null;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector2.zero;
        joint.connectedAnchor = a + delta.normalized * ((min + max) * .5f);
        joint.autoConfigureAngle = false;
        joint.angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - platform.rotation;
        joint.useMotor = false;
        joint.useLimits = true;
        joint.limits = new JointTranslationLimits2D { min = -halfTravel, max = halfTravel };
        joint.enabled = true;
        // Do not change simulated, bodyType or authority. A future network
        // adapter can retain sole responsibility for deciding who simulates.
        return true;
    }

    private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
        !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
}
