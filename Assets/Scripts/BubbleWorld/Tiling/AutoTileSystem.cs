using System.Collections.Generic;

namespace BubbleWorld
{
    public static class AutoTileSystem
    {
        private static readonly (int dx, int dy)[] Offsets =
        {
            (0, 1),   // N  → bit 7
            (1, 1),   // NE → bit 6
            (1, 0),   // E  → bit 5
            (1, -1),  // SE → bit 4
            (0, -1),  // S  → bit 3
            (-1, -1), // SW → bit 2
            (-1, 0),  // W  → bit 1
            (-1, 1),  // NW → bit 0
        };

        private static readonly int N = 7, NE = 6, E = 5, SE = 4;
        private static readonly int S = 3, SW = 2, W = 1, NW = 0;

        private static int B(int bit) => 1 << bit;

        private static readonly Dictionary<int, TileVariant> MaskToVariant = BuildMaskMap();

        public static (int matchMask, int heightBoundaryMask) ComputeMask(BubbleModel model, int x, int y, int z)
        {
            BubbleCell center = model.Get(x, y, z);
            if (center == null) return (0, 0);

            int matchMask = 0;
            int heightBoundaryMask = 0;

            for (int i = 0; i < 8; i++)
            {
                int nx = x + Offsets[i].dx;
                int nz = z + Offsets[i].dy;

                if (!model.IsInside(nx, y, nz))
                {
                    matchMask |= B(7 - i);
                    continue;
                }

                BubbleCell neighbor = model.Get(nx, y, nz);
                if (neighbor == null) continue;

                if (TerrainMatches(center, neighbor))
                {
                    if (center.TileHeight == neighbor.TileHeight)
                        matchMask |= B(7 - i);
                    else
                        heightBoundaryMask |= B(7 - i);
                }
            }

            return (matchMask, heightBoundaryMask);
        }

        private static bool TerrainMatches(BubbleCell center, BubbleCell neighbor)
        {
            return SameLayerMatch(center, neighbor) || GroundLayerMatch(center, neighbor)
                || WaterNeighborMatch(neighbor) || GroundTerrainNeighborMatch(center, neighbor);
        }

        public static int ComputeGroundMask(BubbleModel model, int x, int y, int z, string groundItemId)
        {
            BubblePlacementDefinition groundDef = BubblePlacementCatalog.Find(groundItemId);
            if (groundDef == null) return 0;

            int mask = 0;

            for (int i = 0; i < 8; i++)
            {
                int nx = x + Offsets[i].dx;
                int nz = z + Offsets[i].dy;

                if (!model.IsInside(nx, y, nz))
                {
                    mask |= B(7 - i);
                    continue;
                }

                BubbleCell neighbor = model.Get(nx, y, nz);
                if (neighbor == null) continue;

                if (GroundItemMatches(neighbor, groundItemId))
                    mask |= B(7 - i);
            }

            return mask;
        }

        private static bool SameLayerMatch(BubbleCell center, BubbleCell neighbor)
        {
            if (neighbor.Layer != center.Layer) return false;
            return SameTileCategory(center, neighbor);
        }

        private static bool GroundLayerMatch(BubbleCell center, BubbleCell neighbor)
        {
            if (string.IsNullOrEmpty(neighbor.GroundItemId)) return false;
            string nCat = EffectiveTileCategory(neighbor.GroundItemId);
            return SameTileCategoryById(center.ItemId, center.TileCategory, neighbor.GroundItemId, nCat);
        }

        private static bool GroundItemMatches(BubbleCell neighbor, string groundItemId)
        {
            string nId = !string.IsNullOrEmpty(neighbor.GroundItemId) ? neighbor.GroundItemId : neighbor.ItemId;
            string nCat = !string.IsNullOrEmpty(neighbor.GroundItemId)
                ? EffectiveTileCategory(neighbor.GroundItemId)
                : neighbor.TileCategory;
            return SameTileCategoryById(groundItemId, EffectiveTileCategory(groundItemId), nId, nCat);
        }

        private static bool WaterNeighborMatch(BubbleCell neighbor)
        {
            return neighbor.ItemId == "fresh_water" || neighbor.ItemId == "shore";
        }

        private static bool GroundTerrainNeighborMatch(BubbleCell center, BubbleCell neighbor)
        {
            var centerDef = BubblePlacementCatalog.Find(center.ItemId);
            var neighborDef = BubblePlacementCatalog.Find(neighbor.ItemId);
            if (centerDef == null || neighborDef == null) return false;
            if (centerDef.Tab != BuildTab.Terrain || neighborDef.Tab != BuildTab.Terrain)
                return false;
            return SameTileCategory(center, neighbor);
        }

        private static string EffectiveTileCategory(string itemId)
        {
            var def = BubblePlacementCatalog.Find(itemId);
            return def?.TileCategory ?? "";
        }

        private static bool SameTileCategoryById(string idA, string catA, string idB, string catB)
        {
            if (!string.IsNullOrEmpty(catA) && !string.IsNullOrEmpty(catB))
                return catA == catB;
            return idA == idB;
        }

        public static TileVariant GetVariant(int mask)
        {
            if (MaskToVariant.TryGetValue(mask, out TileVariant variant))
                return variant;
            return ComputeVariant(mask);
        }

        private static TileVariant ComputeVariant(int mask)
        {
            bool hasN = (mask & B(N)) != 0;
            bool hasS = (mask & B(S)) != 0;
            bool hasE = (mask & B(E)) != 0;
            bool hasW = (mask & B(W)) != 0;
            bool hasNE = (mask & B(NE)) != 0;
            bool hasNW = (mask & B(NW)) != 0;
            bool hasSE = (mask & B(SE)) != 0;
            bool hasSW = (mask & B(SW)) != 0;

            int cardinalCount = (hasN ? 1 : 0) + (hasS ? 1 : 0) + (hasE ? 1 : 0) + (hasW ? 1 : 0);

            if (cardinalCount == 4)
            {
                if (!hasNE && hasNW && hasSE && hasSW) return TileVariant.InnerCornerSW;
                if (!hasNW && hasNE && hasSE && hasSW) return TileVariant.InnerCornerSE;
                if (!hasSE && hasNE && hasNW && hasSW) return TileVariant.InnerCornerNW;
                if (!hasSW && hasNE && hasNW && hasSE) return TileVariant.InnerCornerNE;
                return TileVariant.Center;
            }

            if (cardinalCount == 3)
            {
                if (!hasN) return TileVariant.EdgeNorth;
                if (!hasS) return TileVariant.EdgeSouth;
                if (!hasE) return TileVariant.EdgeEast;
                if (!hasW) return TileVariant.EdgeWest;
            }

            if (cardinalCount == 2)
            {
                if (hasN && hasE)
                {
                    if (hasNE && !hasS && !hasW && !hasNW && !hasSE && !hasSW)
                        return TileVariant.OuterCornerSW;
                    return TileVariant.Isolated;
                }
                if (hasE && hasS)
                {
                    if (hasSE && !hasN && !hasW && !hasNE && !hasNW && !hasSW)
                        return TileVariant.OuterCornerNW;
                    return TileVariant.Isolated;
                }
                if (hasS && hasW)
                {
                    if (hasSW && !hasN && !hasE && !hasNE && !hasNW && !hasSE)
                        return TileVariant.OuterCornerNE;
                    return TileVariant.Isolated;
                }
                if (hasW && hasN)
                {
                    if (hasNW && !hasS && !hasE && !hasNE && !hasSE && !hasSW)
                        return TileVariant.OuterCornerSE;
                    return TileVariant.Isolated;
                }
                if (hasN && hasS)
                {
                    if (hasE) return TileVariant.EdgeWest;
                    if (hasW) return TileVariant.EdgeEast;
                    return TileVariant.EdgeWest;
                }
                if (hasE && hasW)
                {
                    if (hasN) return TileVariant.EdgeSouth;
                    if (hasS) return TileVariant.EdgeNorth;
                    return TileVariant.EdgeNorth;
                }
            }

            if (cardinalCount == 1)
            {
                if (hasN) return TileVariant.EdgeSouth;
                if (hasS) return TileVariant.EdgeNorth;
                if (hasE) return TileVariant.EdgeWest;
                if (hasW) return TileVariant.EdgeEast;
            }

            return TileVariant.Isolated;
        }

        public static void ApplyToCell(BubbleModel model, int x, int y, int z)
        {
            BubbleCell cell = model.Get(x, y, z);
            if (cell == null) return;
            BubblePlacementDefinition def = BubblePlacementCatalog.Find(cell.ItemId);
            if (def != null && def.FixedVariant.HasValue)
            {
                cell.AutoTileVariant = def.FixedVariant.Value;
                return;
            }
            if (cell.Composition.FluidData.FluidType != FluidType.None)
            {
                WaterFlowVisualSystem.ApplyToCell(cell);
                return;
            }
            var masks = ComputeMask(model, x, y, z);
            TileVariant baseVariant = GetVariant(masks.matchMask);
            if (IsEdgeVariant(baseVariant))
            {
                cell.AutoTileVariant = def != null ? ApplyEdgeStyle(cell.ItemId, baseVariant, masks.heightBoundaryMask, masks.matchMask) : baseVariant;
            }
            else
            {
                cell.AutoTileVariant = baseVariant;
                if (masks.heightBoundaryMask != 0 || cell.TileHeight == HeightMode.Half)
                    cell.AutoTileVariant = GetHeightAwareVariant(masks.heightBoundaryMask, cell.TileHeight, baseVariant);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void DebugPrintMask(string label, int mask)
        {
            string bits = "";
            for (int i = 7; i >= 0; i--)
                bits += ((mask >> i) & 1) == 1 ? "1" : "0";
            int cardinalBits = mask & 0b10101010;
            int cardinalCount = CountBits(cardinalBits);
            string isOuterCorner = "";
            var v = GetVariant(mask);
            if (v == TileVariant.OuterCornerNE || v == TileVariant.OuterCornerNW ||
                v == TileVariant.OuterCornerSE || v == TileVariant.OuterCornerSW)
                isOuterCorner = " ** OUTER CORNER **";
            else if (v == TileVariant.InnerCornerNE || v == TileVariant.InnerCornerNW ||
                     v == TileVariant.InnerCornerSE || v == TileVariant.InnerCornerSW)
                isOuterCorner = " ** INNER CORNER **";
            UnityEngine.Debug.Log($"[AutoTile] {label} mask={mask} ({bits}) cardinals={cardinalCount} variant={v}{isOuterCorner}");
        }

        public static TileVariant GetHeightAwareVariant(int heightBoundaryMask, HeightMode cellHeight, TileVariant baseVariant)
        {
            if (heightBoundaryMask == 0)
                return ToHalfVariant(baseVariant);

            if (cellHeight == HeightMode.Full)
                return ToFullToHalfVariant(baseVariant);

            return ToHalfVariant(baseVariant);
        }

        private static TileVariant ToHalfVariant(TileVariant v)
        {
            switch (v)
            {
                case TileVariant.Center:        return TileVariant.HalfCenter;
                case TileVariant.EdgeNorth:     return TileVariant.HalfEdgeNorth;
                case TileVariant.EdgeSouth:     return TileVariant.HalfEdgeSouth;
                case TileVariant.EdgeEast:      return TileVariant.HalfEdgeEast;
                case TileVariant.EdgeWest:      return TileVariant.HalfEdgeWest;
                case TileVariant.OuterCornerNE: return TileVariant.HalfOuterCornerNE;
                case TileVariant.OuterCornerNW: return TileVariant.HalfOuterCornerNW;
                case TileVariant.OuterCornerSE: return TileVariant.HalfOuterCornerSE;
                case TileVariant.OuterCornerSW: return TileVariant.HalfOuterCornerSW;
                case TileVariant.InnerCornerNE: return TileVariant.HalfInnerCornerNE;
                case TileVariant.InnerCornerNW: return TileVariant.HalfInnerCornerNW;
                case TileVariant.InnerCornerSE: return TileVariant.HalfInnerCornerSE;
                case TileVariant.InnerCornerSW: return TileVariant.HalfInnerCornerSW;
                case TileVariant.Isolated:      return TileVariant.HalfIsolated;
                default:                        return v;
            }
        }

        private static TileVariant ToFullToHalfVariant(TileVariant v)
        {
            switch (v)
            {
                case TileVariant.EdgeNorth:     return TileVariant.FullToHalfEdgeNorth;
                case TileVariant.EdgeSouth:     return TileVariant.FullToHalfEdgeSouth;
                case TileVariant.EdgeEast:      return TileVariant.FullToHalfEdgeEast;
                case TileVariant.EdgeWest:      return TileVariant.FullToHalfEdgeWest;
                case TileVariant.OuterCornerNE: return TileVariant.FullToHalfOuterCornerNE;
                case TileVariant.OuterCornerNW: return TileVariant.FullToHalfOuterCornerNW;
                case TileVariant.OuterCornerSE: return TileVariant.FullToHalfOuterCornerSE;
                case TileVariant.OuterCornerSW: return TileVariant.FullToHalfOuterCornerSW;
                case TileVariant.InnerCornerNE: return TileVariant.FullToHalfInnerCornerNE;
                case TileVariant.InnerCornerNW: return TileVariant.FullToHalfInnerCornerNW;
                case TileVariant.InnerCornerSE: return TileVariant.FullToHalfInnerCornerSE;
                case TileVariant.InnerCornerSW: return TileVariant.FullToHalfInnerCornerSW;
                default:                        return v;
            }
        }

        private static TileVariant ToRoundedEdgeVariant(TileVariant v)
        {
            switch (v)
            {
                case TileVariant.EdgeNorth:            return TileVariant.RoundedEdgeNorth;
                case TileVariant.EdgeSouth:            return TileVariant.RoundedEdgeSouth;
                case TileVariant.EdgeEast:             return TileVariant.RoundedEdgeEast;
                case TileVariant.EdgeWest:             return TileVariant.RoundedEdgeWest;
                case TileVariant.FullToHalfEdgeNorth:  return TileVariant.RoundedEdgeNorth;
                case TileVariant.FullToHalfEdgeSouth:  return TileVariant.RoundedEdgeSouth;
                case TileVariant.FullToHalfEdgeEast:   return TileVariant.RoundedEdgeEast;
                case TileVariant.FullToHalfEdgeWest:   return TileVariant.RoundedEdgeWest;
                default:                               return v;
            }
        }

        private static TileVariant ToHalfVerticalVariant(TileVariant v)
        {
            switch (v)
            {
                case TileVariant.EdgeNorth:            return TileVariant.HalfVerticalBack;
                case TileVariant.EdgeSouth:            return TileVariant.HalfVerticalFront;
                case TileVariant.EdgeEast:             return TileVariant.HalfVerticalRight;
                case TileVariant.EdgeWest:             return TileVariant.HalfVerticalLeft;
                case TileVariant.FullToHalfEdgeNorth:  return TileVariant.HalfVerticalBack;
                case TileVariant.FullToHalfEdgeSouth:  return TileVariant.HalfVerticalFront;
                case TileVariant.FullToHalfEdgeEast:   return TileVariant.HalfVerticalRight;
                case TileVariant.FullToHalfEdgeWest:   return TileVariant.HalfVerticalLeft;
                default:                               return v;
            }
        }

        private static TileVariant ApplyEdgeStyle(string itemId, TileVariant v, int heightBoundaryMask, int mask)
        {
            TerrainType tt = TerrainType.Find(itemId);
            if (tt == null) return v;

            if (HasHeightBoundary(v, heightBoundaryMask))
            {
                if (tt.HasVariantOverride(TileVariant.HalfVerticalFront)
                    || tt.HasVariantOverride(TileVariant.HalfVerticalBack)
                    || tt.HasVariantOverride(TileVariant.HalfVerticalLeft)
                    || tt.HasVariantOverride(TileVariant.HalfVerticalRight))
                {
                    var half = ToHalfVerticalVariant(v);
                    if (half != v) return half;
                }
            }

            int cardinalCount = CountBits(mask & 0b10101010);
            if (cardinalCount >= 2)
            {
                var rounded = ToRoundedEdgeVariant(v);
                if (rounded != v && tt.HasVariantOverride(rounded))
                    return rounded;
            }

            return v;
        }

        private static bool IsEdgeVariant(TileVariant v)
        {
            return v == TileVariant.EdgeNorth || v == TileVariant.EdgeSouth
                || v == TileVariant.EdgeEast || v == TileVariant.EdgeWest;
        }

        private static bool HasHeightBoundary(TileVariant v, int heightBoundaryMask)
        {
            switch (v)
            {
                case TileVariant.EdgeNorth: return (heightBoundaryMask & B(N)) != 0;
                case TileVariant.EdgeSouth: return (heightBoundaryMask & B(S)) != 0;
                case TileVariant.EdgeEast:  return (heightBoundaryMask & B(E)) != 0;
                case TileVariant.EdgeWest:  return (heightBoundaryMask & B(W)) != 0;
                default: return false;
            }
        }

        private static bool SameTileCategory(BubbleCell a, BubbleCell b)
        {
            if (!string.IsNullOrEmpty(a.TileCategory) && !string.IsNullOrEmpty(b.TileCategory))
                return a.TileCategory == b.TileCategory;
            return a.ItemId == b.ItemId;
        }

        private static int CountBits(int v)
        {
            int c = 0;
            while (v != 0) { c += v & 1; v >>= 1; }
            return c;
        }

        private static bool IsValidOuterCornerMask(int mask)
        {
            bool hasN = (mask & B(N)) != 0;
            bool hasS = (mask & B(S)) != 0;
            bool hasE = (mask & B(E)) != 0;
            bool hasW = (mask & B(W)) != 0;
            bool hasNE = (mask & B(NE)) != 0;
            bool hasNW = (mask & B(NW)) != 0;
            bool hasSE = (mask & B(SE)) != 0;
            bool hasSW = (mask & B(SW)) != 0;

            int cardinalCount = (hasN ? 1 : 0) + (hasS ? 1 : 0) + (hasE ? 1 : 0) + (hasW ? 1 : 0);

            if (cardinalCount != 2) return false;

            if (hasN && hasE)
                return hasNE && !hasS && !hasW && !hasNW && !hasSE && !hasSW;
            if (hasE && hasS)
                return hasSE && !hasN && !hasW && !hasNE && !hasNW && !hasSW;
            if (hasS && hasW)
                return hasSW && !hasN && !hasE && !hasNE && !hasNW && !hasSE;
            if (hasW && hasN)
                return hasNW && !hasS && !hasE && !hasNE && !hasSE && !hasSW;

            return false;
        }

        private static Dictionary<int, TileVariant> BuildMaskMap()
        {
            var map = new Dictionary<int, TileVariant>(256);
            for (int mask = 0; mask < 256; mask++)
                map[mask] = ComputeVariant(mask);
            return map;
        }
    }
}
