using System;
using System.Collections.Generic;
using UnityEngine;

namespace Egaku.Shapes
{
    public enum ShapePointMode { Corner, Smooth }
    public enum ShapePreset { Rectangle, Triangle, Slope, Circle, Ellipse }

    [Serializable]
    public sealed class ShapePoint
    {
        public Vector2 position;
        // Handles are offsets in the shape's local XY plane. Corner points may still
        // have independent handles, so a cusp can join a straight edge to a curve.
        public Vector2 incoming;
        public Vector2 outgoing;
        public ShapePointMode mode;
        public ShapePoint(Vector2 position) { this.position = position; }
    }

    /// <summary>Closed shape authoring data, independent of the area's gameplay use.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D))]
    public sealed class ShapePath : MonoBehaviour
    {
        public List<ShapePoint> points = new List<ShapePoint>();
        [Min(0.001f)] public float curveTolerance = 0.025f;
        [Min(0.001f)] public float minimumEdge = 0.002f;
        [Range(4, 12)] public int maximumSubdivisionDepth = 10;
        public bool snapToGrid = true;
        [Min(0.001f)] public float gridSize = 0.25f;
        // Status is derived in the Editor; the saved collider/mesh are used directly
        // in the Player. No gameplay hierarchy or geometry is created in Play Mode.
        [HideInInspector] public string validationMessage;

#if UNITY_EDITOR
        public static event Action<ShapePath> AuthoringChanged;
        public void RequestRebuild() { AuthoringChanged?.Invoke(this); }
        private void OnValidate() { AuthoringChanged?.Invoke(this); }
        private void OnEnable() { AuthoringChanged?.Invoke(this); }
#endif
    }
}
