using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "TerrainType", menuName = "BubbleWorld/Terrain Type")]
    public sealed class TerrainType : ScriptableObject
    {
        private static Dictionary<string, TerrainType> loaded = new Dictionary<string, TerrainType>();

        public static TerrainType Find(string id)
        {
            loaded.TryGetValue(id, out var tt);
            return tt;
        }

        void OnEnable() { if (!string.IsNullOrEmpty(Id)) loaded[Id] = this; }
        void OnDisable() { if (!string.IsNullOrEmpty(Id)) loaded.Remove(Id); }

        public bool HasVariantOverride(TileVariant variant)
        {
            if (explicitOverrides == null) return false;
            for (int i = 0; i < explicitOverrides.Length; i++)
                if (explicitOverrides[i].variant == variant) return true;
            return false;
        }

        [Header("Identity")]
        [Tooltip("Must match the PlacementItem.Id this TerrainType belongs to (e.g. 'grass', 'dirt', 'sand', 'snow').")]
        public string Id;
        [Tooltip("Display name shown in inspectors (e.g. 'Grass', 'Dirt', 'Snow').")]
        public string DisplayName;

        [Header("Variant Prefabs — one prefab per slot, rotated automatically")]
        [Tooltip("CENTER — TileVariant.Center\n  Mesh 14 (Full Cube)\n  Rotation: 0°\n  Used at center of contiguous areas and as fallback when no other variant applies.")]
        public GameObject centerPrefab;
        [Tooltip("EDGES — TileVariant.EdgeNorth / EdgeEast / EdgeSouth / EdgeWest\n  Mesh 2 (Half Height Forward Ramp), Mesh 4 (Full Height Backward Ramp), or Mesh 13 (Full Height Forward Ramp)\n  One prefab rotated 0°/90°/180°/270° covers all 4 cardinal edges.")]
        public GameObject edgePrefab;
        [Tooltip("OUTER CORNERS — TileVariant.OuterCornerNE / OuterCornerSE / OuterCornerSW / OuterCornerNW\n  Mesh 3 (Rounded Corner)\n  One prefab rotated 0°/90°/180°/270° covers all 4 outer corners.")]
        public GameObject outerCornerPrefab;
        [Tooltip("INNER CORNERS — TileVariant.InnerCornerNE / InnerCornerSE / InnerCornerSW / InnerCornerNW\n  Mesh 3 (Rounded Corner) or Mesh 6 (Wedge, full height) / Mesh 15 (Wedge, half height)\n  One prefab rotated 0°/90°/180°/270° covers all 4 inner corners.")]
        public GameObject innerCornerPrefab;
        [Tooltip("ISOLATED — TileVariant.Isolated\n  Mesh 14 (Full Cube) or Mesh 1 (Half Bottom Cube)\n  Single tile with no same-type neighbors. Falls back to centerPrefab if null.")]
        public GameObject isolatedPrefab;

        [Header("Materials")]
        [Tooltip("Default material applied to all variant prefabs. Can be overridden per variant in Explicit Overrides below.")]
        public Material defaultMaterial;

        [Header("Explicit Overrides — assign one TileVariant per entry (no rotation)")]
        [Tooltip("Override a specific TileVariant with a unique mesh that CANNOT be derived from the rotation system. Rotation forced to 0° — pre-rotate the prefab asset itself if needed.\n  • TileVariant.Center         → Mesh 1 (Half Bottom Cube) if you want a thinner center tile\n  • TileVariant.EdgeNorth      → pre-rotated ramp if rotation doesn't match\n  • TileVariant.OuterCornerNE  → Mesh 3 (Rounded Corner) if auto-rotation wrong\n  • TileVariant.InnerCornerNE  → Mesh 6 (Wedge full) or Mesh 15 (Wedge half)\n  • TileVariant.Isolated       → Mesh 1 (Half Bottom Cube) or Mesh 5 (Rounded Edge)\n  • TileVariant.WaterStraight  → Mesh 7 (Water flow straight)\n  • TileVariant.WaterCorner    → Mesh 8 (Water flow corner)\n  • TileVariant.WaterTee       → Mesh 9 (Water flow T-junction)\n  • TileVariant.WaterCross     → Mesh 10 (Water flow cross)")]
        public TerrainVariantEntry[] explicitOverrides;

        public bool TryGetEntry(TileVariant variant, out GameObject prefab, out Material material, out int rotationY)
        {
            for (int i = 0; i < explicitOverrides?.Length; i++)
            {
                if (explicitOverrides[i].variant == variant)
                {
                    prefab = explicitOverrides[i].prefab;
                    material = explicitOverrides[i].material;
                    rotationY = 0;
                    return prefab != null;
                }
            }

            rotationY = VariantRotation(variant);
            return PickVariantPrefab(variant, out prefab, out material);
        }

        private bool PickVariantPrefab(TileVariant variant, out GameObject prefab, out Material material)
        {
            switch (VariantGroup(variant))
            {
                case VariantGroupKind.Center:    prefab = centerPrefab;       break;
                case VariantGroupKind.Edge:      prefab = edgePrefab;         break;
                case VariantGroupKind.OuterCorner: prefab = outerCornerPrefab; break;
                case VariantGroupKind.InnerCorner: prefab = innerCornerPrefab; break;
                case VariantGroupKind.Isolated:  prefab = isolatedPrefab;     break;
                default:                         prefab = null;               break;
            }

            material = defaultMaterial;
            return prefab != null;
        }

        private static int VariantRotation(TileVariant variant)
        {
            switch (variant)
            {
                case TileVariant.Center:       return 0;
                case TileVariant.EdgeNorth:    return 0;
                case TileVariant.EdgeEast:     return 90;
                case TileVariant.EdgeSouth:    return 180;
                case TileVariant.EdgeWest:     return 270;
                case TileVariant.OuterCornerNE: return 0;
                case TileVariant.OuterCornerSE: return 90;
                case TileVariant.OuterCornerSW: return 180;
                case TileVariant.OuterCornerNW: return 270;
                case TileVariant.InnerCornerNE: return 0;
                case TileVariant.InnerCornerSE: return 90;
                case TileVariant.InnerCornerSW: return 180;
                case TileVariant.InnerCornerNW: return 270;
                case TileVariant.Isolated:     return 0;
                case TileVariant.FullToHalfEdgeNorth:    return 0;
                case TileVariant.FullToHalfEdgeEast:     return 90;
                case TileVariant.FullToHalfEdgeSouth:    return 180;
                case TileVariant.FullToHalfEdgeWest:     return 270;
                case TileVariant.FullToHalfOuterCornerNE: return 180;
                case TileVariant.FullToHalfOuterCornerSE: return 270;
                case TileVariant.FullToHalfOuterCornerSW: return 0;
                case TileVariant.FullToHalfOuterCornerNW: return 90;
                case TileVariant.FullToHalfInnerCornerNE: return 0;
                case TileVariant.FullToHalfInnerCornerSE: return 270;
                case TileVariant.FullToHalfInnerCornerSW: return 180;
                case TileVariant.FullToHalfInnerCornerNW: return 90;
                case TileVariant.HalfVerticalFront:
                case TileVariant.HalfVerticalBack:
                case TileVariant.HalfVerticalLeft:
                case TileVariant.HalfVerticalRight:  return 0;
                case TileVariant.RoundedEdgeNorth:   return 180;
                case TileVariant.RoundedEdgeSouth:   return 0;
                case TileVariant.RoundedEdgeEast:    return 270;
                case TileVariant.RoundedEdgeWest:    return 90;
                default:                       return 0;
            }
        }

        private static VariantGroupKind VariantGroup(TileVariant variant)
        {
            switch (variant)
            {
                case TileVariant.Center:  return VariantGroupKind.Center;
                case TileVariant.EdgeNorth:
                case TileVariant.EdgeEast:
                case TileVariant.EdgeSouth:
                case TileVariant.EdgeWest: return VariantGroupKind.Edge;
                case TileVariant.OuterCornerNE:
                case TileVariant.OuterCornerSE:
                case TileVariant.OuterCornerSW:
                case TileVariant.OuterCornerNW: return VariantGroupKind.OuterCorner;
                case TileVariant.InnerCornerNE:
                case TileVariant.InnerCornerSE:
                case TileVariant.InnerCornerSW:
                case TileVariant.InnerCornerNW: return VariantGroupKind.InnerCorner;
                case TileVariant.Isolated: return VariantGroupKind.Isolated;
                case TileVariant.FullToHalfEdgeNorth:
                case TileVariant.FullToHalfEdgeEast:
                case TileVariant.FullToHalfEdgeSouth:
                case TileVariant.FullToHalfEdgeWest: return VariantGroupKind.Edge;
                case TileVariant.FullToHalfOuterCornerNE:
                case TileVariant.FullToHalfOuterCornerSE:
                case TileVariant.FullToHalfOuterCornerSW:
                case TileVariant.FullToHalfOuterCornerNW: return VariantGroupKind.OuterCorner;
                case TileVariant.FullToHalfInnerCornerNE:
                case TileVariant.FullToHalfInnerCornerSE:
                case TileVariant.FullToHalfInnerCornerSW:
                case TileVariant.FullToHalfInnerCornerNW: return VariantGroupKind.InnerCorner;
                case TileVariant.HalfVerticalFront:
                case TileVariant.HalfVerticalBack:
                case TileVariant.HalfVerticalLeft:
                case TileVariant.HalfVerticalRight: return VariantGroupKind.Center;
                case TileVariant.RoundedEdgeNorth:
                case TileVariant.RoundedEdgeSouth:
                case TileVariant.RoundedEdgeEast:
                case TileVariant.RoundedEdgeWest: return VariantGroupKind.Edge;
                default: return VariantGroupKind.Center;
            }
        }

        private enum VariantGroupKind { Center, Edge, OuterCorner, InnerCorner, Isolated }
    }

    [System.Serializable]
    public struct TerrainVariantEntry
    {
        [Tooltip("Which auto-tile variant this override applies to.\n\nGround: Center, EdgeNorth/East/South/West, OuterCornerNE/SE/SW/NW, InnerCornerNE/SE/SW/NW, Isolated\nWater: WaterStraight, WaterCorner, WaterTee, WaterCross")]
        public TileVariant variant;
        [Tooltip("Unique prefab for this variant (rotationY forced to 0 — rotate mesh in prefab itself if needed).")]
        public GameObject prefab;
        [Tooltip("Optional material override for this variant only. Leave null to use TerrainType.defaultMaterial.")]
        public Material material;
    }
}
