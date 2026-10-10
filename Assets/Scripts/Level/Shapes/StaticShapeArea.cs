using UnityEngine;

namespace Egaku.Shapes
{
    public enum StaticShapeUse { Platform, DrawProhibited }

    /// <summary>Use and appearance policy; the path and geometry algorithm know no roles or Photon state.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShapePath))]
    public sealed class StaticShapeArea : MonoBehaviour
    {
        public StaticShapeUse use;
        public Color fillColor = new Color(0.9333333f, 0.7137255f, 0.4901961f, 1f);
        public Material fillMaterial;
        public PhysicsMaterial2D physicsMaterial;
        public int sortingLayerId;
        public int sortingOrder;
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Queue rebuilding through the path: OnValidate itself must not create
            // assets or modify a collider during serialization/import.
            var path = GetComponent<ShapePath>();
            if (path != null) path.RequestRebuild();
        }
#endif
    }
}
