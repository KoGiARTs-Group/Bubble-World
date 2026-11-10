using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    public sealed class CompanySplashScreenUI : MonoBehaviour
    {
        [SerializeField] private string companyName = "BubbleWorld Studio";
        [SerializeField] private string tagline = "A world inside every bubble";
        [SerializeField] private string continueHint = "Press any key or tap to continue";
        [SerializeField] private float duration = 3f;

        private Action onContinue;
        private float elapsed;
        private bool finished;
        private bool built;

        public void Show(Action continueAction)
        {
            onContinue = continueAction;
            finished = false;
            elapsed = 0f;
            EnsureBuilt();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;

            var bg = CreateObject("Background", transform);
            var bgRect = (RectTransform)bg.transform;
            Stretch(bgRect);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.02f, 0.05f, 0.07f, 1f);
            var bgButton = bg.AddComponent<Button>();
            bgButton.transition = Selectable.Transition.None;
            bgButton.onClick.AddListener(() => Continue());

            var title = CreateText("Company Title", transform, companyName, 84, TextAlignmentOptions.Center);
            PositionCenter(title.rectTransform, 40f);
            title.color = new Color(0.55f, 0.9f, 1f, 1f);

            var subtitle = CreateText("Tagline", transform, tagline, 34, TextAlignmentOptions.Center);
            PositionCenter(subtitle.rectTransform, -40f);
            subtitle.color = new Color(0.85f, 0.85f, 0.85f, 1f);

            var hint = CreateText("Hint", transform, continueHint, 24, TextAlignmentOptions.Center);
            PositionCenter(hint.rectTransform, -380f);
            hint.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        }

        private void Update()
        {
            if (finished) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= duration || Input.anyKeyDown || Input.GetMouseButtonDown(0))
                Continue();
        }

        private void Continue()
        {
            if (finished) return;
            finished = true;
            Hide();
            onContinue?.Invoke();
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string content,
            int fontSize, TextAlignmentOptions alignment)
        {
            var go = CreateObject(name, parent);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;
            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(1600f, 140f);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void PositionCenter(RectTransform rect, float y)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
        }
    }
}
