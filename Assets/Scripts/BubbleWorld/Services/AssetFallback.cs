using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public static class AssetFallback
    {
        private static readonly HashSet<string> loggedWarnings = new HashSet<string>();

        public static Mesh GetFallbackMesh(BuildTab tab)
        {
            switch (tab)
            {
                case BuildTab.Terrain:
                    return PrimitiveMeshUtility.Cube;
                case BuildTab.Water:
                    return PrimitiveMeshUtility.Cube;
                case BuildTab.Plants:
                    return PrimitiveMeshUtility.Capsule;
                case BuildTab.Resources:
                    return PrimitiveMeshUtility.Cylinder;
                case BuildTab.Climate:
                    return PrimitiveMeshUtility.Sphere;
                case BuildTab.Interventions:
                    return PrimitiveMeshUtility.Cube;
                default:
                    return PrimitiveMeshUtility.Cube;
            }
        }

        public static Vector3 GetFallbackScale(BuildTab tab, bool isObject)
        {
            return Vector3.one;
        }

        public static void LogWarningOnce(string itemId, TileVariant variant)
        {
            string key = itemId + ":" + variant;
            if (loggedWarnings.Add(key))
                Debug.LogWarningFormat("[AssetFallback] Missing visual for {0} variant {1}. Using fallback primitive.", itemId, variant);
        }

        public static bool HasSeenWarning(string itemId, TileVariant variant)
        {
            return loggedWarnings.Contains(itemId + ":" + variant);
        }
    }
}
