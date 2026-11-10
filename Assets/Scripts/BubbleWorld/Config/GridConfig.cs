using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "GridConfig", menuName = "BubbleWorld/Grid Config")]
    public sealed class GridConfig : ScriptableObject
    {
        [Header("Grid")]
        public int Width = 60;
        public int Height = 60;
        public int Depth = 8;
        public float TileSize = 1f;
        public float TileHeight = 0.6f;

        [Header("Camera")]
        public bool UsePerspectiveCamera = true;
        [Tooltip("Field of view for 3D perspective camera (Top view).")]
        public float CameraFOV = 60f;
        [Tooltip("Field of view for 3D 45-degree view.")]
        public float CameraFOV45 = 75f;
        [Tooltip("Camera angle for 3D 45-degree view (X rotation in degrees).")]
        public float CameraTilt45X = 45f;
        [Tooltip("Camera angle for 3D top-down view (X rotation in degrees).")]
        public float CameraTiltTopX = 89.9f;
        [Tooltip("Camera angle for 3D view (Z rotation in degrees).")]
        public float CameraTiltZ = 0f;
        [Tooltip("Default view mode when entering 3D mode.")]
        public Camera3DView Default3DView = Camera3DView.Angle45;
        [Tooltip("Primitive mesh used when a PlacementItem has no Prefab3D assigned.")]
        public Mesh3DPrimitive Default3DPrimitive = Mesh3DPrimitive.Cube;

        [Header("Zoom & Pan")]
        public float MinZoom = 5f;
        public float MaxZoom = 20f;
        public float DefaultZoom = 14f;
        public float ZoomStep = 1f;
        public float PanSpeed = 0.8f;

        [Header("Grid Lines")]
        public bool ShowGridLines = true;
        public Color GridLineColor = new Color(1f, 1f, 1f, 0.08f);
        public float GridLineWidth = 0.03f;

        [Header("Bounds")]
        public float MinWorldX = -35f;
        public float MaxWorldX = 35f;
        public float MinWorldY = -15f;
        public float MaxWorldY = 15f;
        public float MinWorldZ = -35f;
        public float MaxWorldZ = 35f;

        public static GridConfig CreateDefault()
        {
            GridConfig config = CreateInstance<GridConfig>();
            config.UsePerspectiveCamera = true;
            return config;
        }
    }
}
