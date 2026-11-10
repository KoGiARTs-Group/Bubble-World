using UnityEngine;

namespace BubbleWorld
{
    /// <summary>
    /// Center character panel. Owns the 3D model container, its expression
    /// driver and lip-sync component, plus an independent background layer.
    /// The model stays centered while backgrounds can be swapped freely.
    /// </summary>
    public sealed class CharacterPanelController : MonoBehaviour
    {
        [Header("Model")]
        [SerializeField] private GameObject modelParent;
        [SerializeField] private SoulformExpressionDriver expressionDriver;
        [SerializeField] private SoulformLipSync lipSync;

        [Header("Background")]
        [SerializeField] private GameObject backgroundRoot;
        [SerializeField] private GameObject[] backgrounds;

        private SoulformModel boundModel;
        private int backgroundIndex;

        private void Awake()
        {
            if (backgroundRoot != null) backgroundRoot.SetActive(true);
            ShowBackground(0);
        }

        /// <summary>Programmatic fallback wiring for scenes without a controller.</summary>
        public void Configure(GameObject parent, SoulformExpressionDriver driver, SoulformLipSync lip)
        {
            if (modelParent == null && parent != null) modelParent = parent;
            if (expressionDriver == null && driver != null) expressionDriver = driver;
            if (lipSync == null && lip != null) lipSync = lip;
        }

        /// <summary>Reveals the model, links the soulform for lip-sync and sets emotion.</summary>
        public void SetupModel(SoulformModel model, string expression, string animation)
        {
            boundModel = model;
            if (modelParent != null) modelParent.SetActive(true);
            if (lipSync != null && model != null) lipSync.SetSoulform(model);
            UpdateEmotion(expression, animation);
        }

        public void UpdateEmotion(string expression, string animation)
        {
            if (expressionDriver != null && !string.IsNullOrEmpty(expression))
                expressionDriver.SetExpression(expression);
        }

        public void Speak(string text)
        {
            if (lipSync != null) lipSync.Speak(text);
        }

        public void StopSpeaking()
        {
            if (lipSync != null) lipSync.StopSpeaking();
        }

        /// <summary>Hides the model and stops lip-sync when the chat closes.</summary>
        public void CleanupModel()
        {
            if (lipSync != null) lipSync.StopSpeaking();
            if (modelParent != null) modelParent.SetActive(false);
            boundModel = null;
        }

        /// <summary>Cycles the environmental background (rain, dusk, cave, ...).</summary>
        public void CycleBackground()
        {
            if (backgrounds == null || backgrounds.Length == 0) return;
            ShowBackground((backgroundIndex + 1) % backgrounds.Length);
        }

        public void ShowBackground(int index)
        {
            if (backgrounds == null) return;
            backgroundIndex = Mathf.Clamp(index, 0, Mathf.Max(0, backgrounds.Length - 1));
            for (int i = 0; i < backgrounds.Length; i++)
            {
                if (backgrounds[i] != null) backgrounds[i].SetActive(i == backgroundIndex);
            }
        }
    }
}
