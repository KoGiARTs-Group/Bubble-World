using UnityEngine;
using UnityEngine.Rendering;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "EnvironmentProfile", menuName = "BubbleWorld/Environment Profile")]
    public sealed class EnvironmentProfile : ScriptableObject
    {
        public Material skyboxMaterial;
        public Color ambientColor = new Color(0.2f, 0.2f, 0.25f);
        public AmbientMode ambientMode = AmbientMode.Flat;
        public Color directionalColor = Color.white;
        public float directionalIntensity = 1f;
        public Vector3 directionalRotation = new Vector3(50f, -30f, 0f);
        public Color fogColor = Color.gray;
        public FogMode fogMode = FogMode.Exponential;
        public float fogDensity = 0.01f;
        public Color cameraClearColor = Color.black;
    }
}
