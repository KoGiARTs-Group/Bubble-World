using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class BubbleWorldGrid
    {
        private readonly List<GameObject> gridLines = new List<GameObject>();
        private Transform root;
        private Camera camera;
        private GridConfig config;
        private BubbleModel bubble;
        private EnvironmentVisualProfile environmentVisualProfile;
        private WorldLayerConfig layerConfig;
        private Material lineMaterial;
        private Vector3 focalPoint = Vector3.zero;
        private float currentDistance = 20f;
        private Camera3DView active3DView = Camera3DView.Angle45;
        private int viewAngle = 180;
        private float pitchAngle = 35f;
        private GameObject soulformVisual;
        private SoulformExpressionDriver expressionDriver;
        private SoulformAnimationDriver animationDriver;
        private Vector3 followOffset = new Vector3(0f, 3f, -4f);
        private GameObject cursorHighlight;
        private string currentHighlightId;
        private GridChunkManager chunkManager = new GridChunkManager();

        public GameObject soulformPrefab;

        public GridConfig Config => config;

        public BubbleWorldGrid(Transform root, Camera camera)
        {
            this.root = root;
            this.camera = camera;
        }

        public int ViewAngle => viewAngle;

        public void SetViewAngle(int degrees)
        {
            viewAngle = ((degrees % 360) + 360) % 360;
            if (active3DView == Camera3DView.Angle45)
                Apply3DView(Camera3DView.Angle45);
        }

        public void Orbit(float deltaX, float deltaY)
        {
            viewAngle = ((viewAngle + Mathf.RoundToInt(deltaX * 8f)) % 360 + 360) % 360;
            pitchAngle = Mathf.Clamp(pitchAngle + deltaY * 4f, 5f, 85f);
            if (active3DView == Camera3DView.Angle45)
                Apply3DView(Camera3DView.Angle45);
        }

        public void SetConfig(GridConfig newConfig)
        {
            config = newConfig;
        }

        public void SetEnvironmentVisualProfile(EnvironmentVisualProfile profile)
        {
            environmentVisualProfile = profile;
        }

        public void SetLayerConfig(WorldLayerConfig config)
        {
            layerConfig = config;
        }

        public void Show(BubbleModel model)
        {
            bubble = model;
            DestroyAll();
            chunkManager.Initialize(model, config, environmentVisualProfile, root);
            DrawGridLines();
            CenterCamera();
        }

        public void RefreshAll()
        {
            chunkManager.RebuildAll(bubble, config, environmentVisualProfile);
        }

        public void RefreshCells(HashSet<(int x, int y, int z)> dirtyCells)
        {
            chunkManager.RebuildRegion(dirtyCells, bubble, config, environmentVisualProfile);
        }

        public bool TryGetCellUnderPointer(out int x, out int y, out int z)
        {
            x = 0;
            y = 0;
            z = 0;
            if (bubble == null || camera == null) return false;

            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return false;
            Vector3 worldPos = ray.GetPoint(enter);

            return WorldToCell(worldPos, out x, out y, out z);
        }

        public bool CellUnderCursor(out int x, out int y, out int z)
        {
            return TryGetCellUnderPointer(out x, out y, out z);
        }

        public void UpdateCursorHighlight(BubblePlacementDefinition selectedItem)
        {
            UpdateCursorHighlight3D(selectedItem);
        }

        private void UpdateCursorHighlight3D(BubblePlacementDefinition selectedItem)
        {
            if (!CellUnderCursor(out int cx, out int cy, out int cz) || bubble == null || selectedItem == null)
            {
                if (cursorHighlight != null) cursorHighlight.SetActive(false);
                return;
            }

            if (CursorYOverride >= 0) cy = CursorYOverride;

            Vector3 worldPos = ToWorld(cx, cy, cz, LayerBand.GroundWaterBase);
            PlacementItem itemAsset = FindItemAsset(selectedItem.Id);
            GameObject highlightPrefab = ResolvePlacementItemPrefab(selectedItem.Id);

            string desiredId = BuildHighlightId(selectedItem.Id);

            if (cursorHighlight == null || currentHighlightId != desiredId)
            {
                if (cursorHighlight != null) Object.Destroy(cursorHighlight);
                cursorHighlight = new GameObject("CursorHighlight");
                cursorHighlight.transform.SetParent(root, false);
                currentHighlightId = desiredId;

                if (highlightPrefab != null)
                {
                    Vector3 soScale = itemAsset != null ? itemAsset.ModelScale3D : Vector3.one;
                    GameObject prefab = Object.Instantiate(highlightPrefab, cursorHighlight.transform);
                    prefab.transform.localPosition = Vector3.zero;
                    prefab.transform.localRotation = Quaternion.identity;
                    prefab.transform.localScale = soScale;
                    Material tintMat = GetHighlightMaterial();
                    foreach (MeshRenderer r in prefab.GetComponentsInChildren<MeshRenderer>())
                    {
                        r.sharedMaterial = tintMat;
                    }
                }
                else
                {
                    Mesh3DPrimitive primitive = itemAsset != null
                        ? itemAsset.FallbackPrimitive
                        : config.Default3DPrimitive;
                    MeshFilter filter = cursorHighlight.AddComponent<MeshFilter>();
                    MeshRenderer renderer = cursorHighlight.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = GetHighlightMaterial();

                    switch (primitive)
                    {
                        case Mesh3DPrimitive.Sphere:
                            filter.sharedMesh = PrimitiveMeshUtility.Sphere;
                            break;
                        case Mesh3DPrimitive.Cylinder:
                            filter.sharedMesh = PrimitiveMeshUtility.Cylinder;
                            break;
                        case Mesh3DPrimitive.Capsule:
                            filter.sharedMesh = PrimitiveMeshUtility.Capsule;
                            break;
                        case Mesh3DPrimitive.Plane:
                            filter.sharedMesh = PrimitiveMeshUtility.Plane;
                            break;
                        case Mesh3DPrimitive.Quad:
                            filter.sharedMesh = PrimitiveMeshUtility.Quad;
                            cursorHighlight.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                            break;
                        default:
                            filter.sharedMesh = PrimitiveMeshUtility.Cube;
                            break;
                    }
                    cursorHighlight.transform.localScale = Vector3.one * 0.68f;
                }
            }

            float highlightYOffset = highlightPrefab != null && itemAsset != null ? itemAsset.YOffset3D : 0f;
            cursorHighlight.transform.position = worldPos + new Vector3(0f, 0.2f + highlightYOffset, 0f);
            cursorHighlight.SetActive(true);
        }

        private Material GetHighlightMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader);
            Color tint = new Color(0f, 1f, 0f, 0.3f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            return mat;
        }

        private string BuildHighlightId(string itemId)
        {
            if (environmentVisualProfile != null)
            {
                GameObject p = environmentVisualProfile.GetPrefab(itemId, TileVariant.Center);
                if (p != null) return "profile_" + itemId;
            }
            PlacementItem itemAsset = FindItemAsset(itemId);
            if (itemAsset != null && itemAsset.Prefab3D != null)
                return "prefab_" + itemId;
            return itemAsset != null
                ? itemAsset.FallbackPrimitive.ToString()
                : config.Default3DPrimitive.ToString();
        }

        private GameObject ResolvePlacementItemPrefab(string itemId)
        {
            PlacementItem itemAsset = FindItemAsset(itemId);
            if (itemAsset != null && itemAsset.Prefab3D != null)
                return itemAsset.Prefab3D;
            return null;
        }

        private PlacementItem FindItemAsset(string id)
        {
            return BubblePlacementCatalog.FindItemAsset(id);
        }

        private Vector3 ToWorld(int x, int y, int z, LayerBand layer)
        {
            float offsetX = bubble.Width * 0.5f * config.TileSize - config.TileSize * 0.5f;
            float offsetZ = bubble.Height * 0.5f * config.TileSize - config.TileSize * 0.5f;
            float worldX = x * config.TileSize - offsetX;
            float worldZ = z * config.TileSize - offsetZ;
            float worldY = y * config.TileHeight + (int)layer * 0.6f;
            return new Vector3(worldX, worldY, worldZ);
        }

        public bool WorldToCell(Vector3 worldPos, out int x, out int y, out int z)
        {
            x = 0;
            y = 0;
            z = 0;
            if (bubble == null || config == null) return false;
            return WorldToCellStatic(worldPos, bubble.Width, bubble.Height, bubble.Depth,
                config.TileSize, config.TileHeight, out x, out y, out z);
        }

        public static bool WorldToCellStatic(
            Vector3 worldPos, int gridWidth, int gridHeight, int gridDepth,
            float tileSize, float tileHeight, out int x, out int y, out int z)
        {
            float offsetX = gridWidth * 0.5f * tileSize - tileSize * 0.5f;
            float offsetZ = gridHeight * 0.5f * tileSize - tileSize * 0.5f;
            x = Mathf.FloorToInt((worldPos.x + offsetX) / tileSize);
            z = Mathf.FloorToInt((worldPos.z + offsetZ) / tileSize);
            y = Mathf.FloorToInt(worldPos.y / tileHeight);
            return x >= 0 && y >= 0 && z >= 0 && x < gridWidth && y < gridDepth && z < gridHeight;
        }

        public void TickCamera()
        {
            if (camera == null || config == null) return;

            if (active3DView == Camera3DView.FollowSoulform && soulformVisual != null)
            {
                UpdateFollowSoulformCamera();
                return;
            }

            float speed = currentDistance * config.PanSpeed * Time.deltaTime;

            Vector3 rightFlat = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            Vector3 forwardFlat = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            if (forwardFlat.sqrMagnitude < 0.001f)
            {
                forwardFlat = -Vector3.ProjectOnPlane(camera.transform.up, Vector3.up).normalized;
                if (forwardFlat.sqrMagnitude < 0.001f) forwardFlat = Vector3.forward;
            }
            Vector3 move = (rightFlat * Input.GetAxisRaw("Horizontal") + forwardFlat * Input.GetAxisRaw("Vertical")) * speed;

            if (Input.GetMouseButton(2))
            {
                move += (-rightFlat * Input.GetAxisRaw("Mouse X") - forwardFlat * Input.GetAxisRaw("Mouse Y")) * speed * 2f;
            }

            focalPoint += move;
            camera.transform.position += move;
            ClampCamera();

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                Vector3 dir = (camera.transform.position - focalPoint);
                if (dir.sqrMagnitude < 0.0001f) dir = -camera.transform.forward;
                currentDistance = Mathf.Clamp(
                    currentDistance - wheel * config.ZoomStep,
                    config.MinZoom, config.MaxZoom);
                camera.transform.position = focalPoint + dir.normalized * currentDistance;
                camera.transform.LookAt(focalPoint, Vector3.up);
            }
        }

        public void Zoom(float delta)
        {
            if (camera == null || config == null) return;
            currentDistance = Mathf.Clamp(currentDistance + delta, config.MinZoom, config.MaxZoom);
            Vector3 dir = (camera.transform.position - focalPoint);
            if (dir.sqrMagnitude < 0.0001f) dir = -camera.transform.forward;
            camera.transform.position = focalPoint + dir.normalized * currentDistance;
            camera.transform.LookAt(focalPoint, Vector3.up);
        }

        public void CenterCamera()
        {
            if (camera == null || config == null) return;
            Apply3DView(config.Default3DView);
        }

        public void Set3DView(Camera3DView view)
        {
            if (camera == null || config == null) return;
            Apply3DView(view);
        }

        private void Apply3DView(Camera3DView view)
        {
            active3DView = view;

            if (view == Camera3DView.FollowSoulform && soulformVisual != null)
            {
                UpdateFollowSoulformCamera();
                return;
            }

            camera.orthographic = false;
            float fov = view == Camera3DView.Top ? config.CameraFOV : config.CameraFOV45;
            camera.fieldOfView = fov;
            active3DView = view;

            float gridRadius = bubble != null
                ? Mathf.Max(bubble.Width, bubble.Height) * 0.5f
                : 12f;
            float dist = gridRadius / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad);
            dist *= view == Camera3DView.Angle45 ? 1.25f : 1.0f;
            dist = Mathf.Clamp(dist, config.MinZoom, config.MaxZoom);
            currentDistance = dist;
            focalPoint = Vector3.zero;

            if (view == Camera3DView.Top)
            {
                Vector3 pos = focalPoint + new Vector3(0f, dist, 0f);
                camera.transform.position = pos;
                camera.transform.rotation = Quaternion.LookRotation(
                    (focalPoint - pos).normalized, Vector3.forward);
            }
            else
            {
                float rad = viewAngle * Mathf.Deg2Rad;
                float pitchRad = pitchAngle * Mathf.Deg2Rad;
                float hDist = dist * Mathf.Cos(pitchRad);
                Vector3 pos = focalPoint + new Vector3(
                    hDist * Mathf.Sin(rad), dist * Mathf.Sin(pitchRad), hDist * Mathf.Cos(rad));
                camera.transform.position = pos;
                camera.transform.rotation = Quaternion.LookRotation(
                    (focalPoint - pos).normalized, Vector3.up);
            }
        }

        private void DrawGridLines()
        {
            if (bubble == null || config == null || !config.ShowGridLines) return;

            Material mat = GetLineMaterial();
            int halfW = bubble.Width / 2;
            int halfH = bubble.Height / 2;

            for (int x = 0; x <= bubble.Width; x++)
            {
                float worldX = x - halfW;
                LineRenderer line = CreateLineObject("V" + x);
                line.positionCount = 2;
                line.SetPosition(0, new Vector3(worldX, 0f, -halfH));
                line.SetPosition(1, new Vector3(worldX, 0f, halfH));
                ConfigureLineRenderer(line, mat);
            }

            for (int z = 0; z <= bubble.Height; z++)
            {
                float worldZ = z - halfH;
                LineRenderer line = CreateLineObject("H" + z);
                line.positionCount = 2;
                line.SetPosition(0, new Vector3(-halfW, 0f, worldZ));
                line.SetPosition(1, new Vector3(halfW, 0f, worldZ));
                ConfigureLineRenderer(line, mat);
            }
        }

        private void ClampCamera()
        {
            if (config == null) return;
            Vector3 pos = camera.transform.position;
            Vector3 flat = pos - focalPoint;
            flat.y = 0f;
            if (Mathf.Abs(flat.x) > config.MaxWorldX)
                flat.x = Mathf.Sign(flat.x) * config.MaxWorldX;
            if (Mathf.Abs(flat.z) > config.MaxWorldZ)
                flat.z = Mathf.Sign(flat.z) * config.MaxWorldZ;
            pos = focalPoint + flat + new Vector3(0f, pos.y - focalPoint.y, 0f);
            camera.transform.position = pos;
        }

        private LineRenderer CreateLineObject(string name)
        {
            GameObject obj = new GameObject("GridLine_" + name);
            obj.transform.SetParent(root, false);
            gridLines.Add(obj);
            return obj.AddComponent<LineRenderer>();
        }

        private void ConfigureLineRenderer(LineRenderer line, Material mat)
        {
            line.material = mat;
            line.startColor = config.GridLineColor;
            line.endColor = config.GridLineColor;
            line.startWidth = config.GridLineWidth;
            line.endWidth = config.GridLineWidth;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sortingOrder = 1;
        }

        private Material GetLineMaterial()
        {
            if (lineMaterial == null)
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
            return lineMaterial;
        }

        private void DestroyAll()
        {
            chunkManager.Destroy();
            foreach (GameObject line in gridLines)
            {
                if (line != null) Object.Destroy(line);
            }
            gridLines.Clear();
            if (cursorHighlight != null)
            {
                Object.Destroy(cursorHighlight);
                cursorHighlight = null;
            }
        }

        public void SpawnSoulformVisual(int gridX, int gridY, int gridZ)
        {
            DestroySoulformVisual();
            Vector3 pos = ToWorld(gridX, gridY, gridZ, LayerBand.GroundWaterBase);

            if (soulformPrefab != null)
            {
                soulformVisual = Object.Instantiate(soulformPrefab, root, false);
                soulformVisual.name = "SoulformVisual";
                soulformVisual.transform.position = pos + Vector3.up * 0.5f;
            }
            else
            {
                soulformVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                soulformVisual.name = "SoulformVisual";
                soulformVisual.transform.SetParent(root, false);
                soulformVisual.transform.position = pos + Vector3.up * 0.5f;
                soulformVisual.transform.localScale = Vector3.one * 0.6f;
                var renderer = soulformVisual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.3f, 0.85f, 0.5f));
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.3f, 0.85f, 0.5f));
                    renderer.sharedMaterial = mat;
                }
            }

            expressionDriver = soulformVisual.GetComponent<SoulformExpressionDriver>();
            animationDriver = soulformVisual.GetComponent<SoulformAnimationDriver>();
        }

        public void SetSoulformPosition(int gridX, int gridY, int gridZ)
        {
            if (soulformVisual == null) return;
            Vector3 pos = ToWorld(gridX, gridY, gridZ, LayerBand.GroundWaterBase);
            soulformVisual.transform.position = Vector3.Lerp(
                soulformVisual.transform.position,
                pos + Vector3.up * 0.5f,
                Time.deltaTime * 5f);
        }

        public void DestroySoulformVisual()
        {
            if (soulformVisual != null)
            {
                Object.Destroy(soulformVisual);
                soulformVisual = null;
                expressionDriver = null;
                animationDriver = null;
            }
        }

        public GameObject SoulformVisual => soulformVisual;

        public void SetExpression(string expression)
        {
            if (expressionDriver != null)
                expressionDriver.SetExpression(expression);
        }

        public void SetAnimation(string animation)
        {
            if (animationDriver != null)
                animationDriver.PlayAnimation(animation);
        }

        public int CursorYOverride = -1;
        private bool hasCamera = true;

        private void UpdateFollowSoulformCamera()
        {
            if (camera == null || soulformVisual == null || !hasCamera) return;
            Vector3 targetPos = soulformVisual.transform.position + followOffset;
            camera.transform.position = Vector3.Lerp(camera.transform.position, targetPos, Time.deltaTime * 3f);
            camera.transform.LookAt(soulformVisual.transform.position);
            focalPoint = soulformVisual.transform.position;
        }

        public void SetFollowOffset(Vector3 offset)
        {
            followOffset = offset;
        }

        public void Destroy()
        {
            DestroyAll();
            DestroySoulformVisual();
            if (lineMaterial != null)
            {
                Object.Destroy(lineMaterial);
                lineMaterial = null;
            }
        }
    }
}
