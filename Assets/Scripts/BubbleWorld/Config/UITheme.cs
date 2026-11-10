using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "UITheme", menuName = "BubbleWorld/UI Theme")]
    public sealed class UITheme : ScriptableObject
    {
        [Header("Panel")]
        public Color PanelBackground = new Color(0.08f, 0.1f, 0.12f, 0.92f);
        public Color PanelAccent = new Color(0.03f, 0.06f, 0.055f, 0.88f);

        [Header("Button")]
        public Color ButtonNormal = new Color(0.88f, 0.91f, 0.85f, 0.94f);
        public Color ButtonHighlighted = new Color(0.94f, 0.96f, 0.92f, 0.96f);
        public Color ButtonDisabled = new Color(0.38f, 0.4f, 0.39f, 0.7f);
        public Sprite ButtonBackground;

        [Header("Text")]
        public Color TextPrimary = new Color(0.05f, 0.07f, 0.06f);
        public Color TextSecondary = new Color(0.78f, 0.8f, 0.78f);
        public Color TextOnDark = Color.white;
        public Color TextAccent = new Color(0.3f, 0.85f, 0.5f);

        [Header("Font Sizes")]
        public int TitleFontSize = 54;
        public int HeaderFontSize = 36;
        public int BodyFontSize = 18;
        public int SmallFontSize = 14;

        [Header("Screen")]
        public Color ScreenBackground = new Color(0.04f, 0.08f, 0.1f);

        public static UITheme CreateDefault()
        {
            UITheme theme = CreateInstance<UITheme>();
            return theme;
        }
    }
}
