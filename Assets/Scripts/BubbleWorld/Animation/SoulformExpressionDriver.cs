using UnityEngine;

namespace BubbleWorld
{
    public sealed class SoulformExpressionDriver : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer meshRenderer;

        [Tooltip("Facial expression blendshapes (mutually exclusive — one active at a time)")]
        public ExpressionMapping[] expressionMappings = new ExpressionMapping[]
        {
            new ExpressionMapping { expressionName = "Happy",      blendshapeName = "happy",      weight = 100f },
            new ExpressionMapping { expressionName = "Sad",        blendshapeName = "sad",        weight = 100f },
            new ExpressionMapping { expressionName = "Excited",    blendshapeName = "excited",    weight = 100f },
            new ExpressionMapping { expressionName = "Surprised",  blendshapeName = "jaw_open",   weight = 100f },
            new ExpressionMapping { expressionName = "Scared",     blendshapeName = "scared",     weight = 100f },
            new ExpressionMapping { expressionName = "Confused",   blendshapeName = "confused",   weight = 100f },
            new ExpressionMapping { expressionName = "Angry",      blendshapeName = "angry",      weight = 100f },
            new ExpressionMapping { expressionName = "JawOpen",    blendshapeName = "jaw_open",   weight = 100f },
            new ExpressionMapping { expressionName = "Blink",      blendshapeName = "Blink",      weight = 100f },
        };

        [Tooltip("Viseme blendshapes for lip-sync (stack on top of expressions, driven by LipSync)")]
        public ExpressionMapping[] visemeMappings = new ExpressionMapping[]
        {
            new ExpressionMapping { expressionName = "PP",  blendshapeName = "PP",  weight = 100f },
            new ExpressionMapping { expressionName = "AA",  blendshapeName = "aa",  weight = 100f },
            new ExpressionMapping { expressionName = "E",   blendshapeName = "E",   weight = 100f },
            new ExpressionMapping { expressionName = "IH",  blendshapeName = "ih",  weight = 100f },
            new ExpressionMapping { expressionName = "OH",  blendshapeName = "oh",  weight = 100f },
            new ExpressionMapping { expressionName = "OU",  blendshapeName = "ou",  weight = 100f },
            new ExpressionMapping { expressionName = "FF",  blendshapeName = "FF",  weight = 100f },
            new ExpressionMapping { expressionName = "DD",  blendshapeName = "DD",  weight = 100f },
            new ExpressionMapping { expressionName = "KK",  blendshapeName = "KK",  weight = 100f },
            new ExpressionMapping { expressionName = "SS",  blendshapeName = "SS",  weight = 100f },
        };

        private string currentExpression = "Neutral";
        private string currentViseme = "";

        private void Awake()
        {
            if (meshRenderer == null)
                meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        public void SetExpression(string expressionName)
        {
            if (string.IsNullOrEmpty(expressionName))
                expressionName = "Neutral";

            if (expressionName == currentExpression)
                return;

            if (meshRenderer == null)
            {
                Debug.LogWarning("SoulformExpressionDriver: no SkinnedMeshRenderer assigned");
                currentExpression = expressionName;
                return;
            }

            ResetMappedBlendShapes(expressionMappings);

            for (int i = 0; i < expressionMappings.Length; i++)
            {
                var mapping = expressionMappings[i];
                if (!string.Equals(mapping.expressionName, expressionName, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrEmpty(mapping.blendshapeName))
                    break;

                int blendIndex = meshRenderer.sharedMesh.GetBlendShapeIndex(mapping.blendshapeName);
                if (blendIndex < 0)
                {
                    Debug.LogWarning($"Blendshape '{mapping.blendshapeName}' not found on mesh");
                    break;
                }

                meshRenderer.SetBlendShapeWeight(blendIndex, mapping.weight);
                break;
            }

            currentExpression = expressionName;
        }

        public void SetViseme(string visemeName)
        {
            if (string.IsNullOrEmpty(visemeName))
                visemeName = "";

            if (visemeName == currentViseme)
                return;

            if (meshRenderer == null)
                return;

            ResetMappedBlendShapes(visemeMappings);

            if (string.IsNullOrEmpty(visemeName))
            {
                currentViseme = "";
                return;
            }

            for (int i = 0; i < visemeMappings.Length; i++)
            {
                var mapping = visemeMappings[i];
                if (!string.Equals(mapping.expressionName, visemeName, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrEmpty(mapping.blendshapeName))
                    break;

                int blendIndex = meshRenderer.sharedMesh.GetBlendShapeIndex(mapping.blendshapeName);
                if (blendIndex < 0)
                {
                    Debug.LogWarning($"Viseme blendshape '{mapping.blendshapeName}' not found on mesh");
                    break;
                }

                meshRenderer.SetBlendShapeWeight(blendIndex, mapping.weight);
                break;
            }

            currentViseme = visemeName;
        }

        public string GetCurrentExpression()
        {
            return currentExpression;
        }

        public string GetCurrentViseme()
        {
            return currentViseme;
        }

        public void ResetAllBlendShapes()
        {
            if (meshRenderer == null || meshRenderer.sharedMesh == null)
                return;

            for (int i = 0; i < meshRenderer.sharedMesh.blendShapeCount; i++)
                meshRenderer.SetBlendShapeWeight(i, 0f);

            currentExpression = "Neutral";
            currentViseme = "";
        }

        private void ResetMappedBlendShapes(ExpressionMapping[] mappings)
        {
            if (meshRenderer == null || meshRenderer.sharedMesh == null)
                return;

            for (int i = 0; i < mappings.Length; i++)
            {
                if (string.IsNullOrEmpty(mappings[i].blendshapeName))
                    continue;

                int blendIndex = meshRenderer.sharedMesh.GetBlendShapeIndex(mappings[i].blendshapeName);
                if (blendIndex >= 0)
                    meshRenderer.SetBlendShapeWeight(blendIndex, 0f);
            }
        }
    }

    [System.Serializable]
    public sealed class ExpressionMapping
    {
        public string expressionName = "Neutral";
        public string blendshapeName = "Neutral";
        [Range(0f, 100f)]
        public float weight = 100f;
    }
}
