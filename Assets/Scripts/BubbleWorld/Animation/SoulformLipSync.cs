using UnityEngine;

namespace BubbleWorld
{
    public sealed class SoulformLipSync : MonoBehaviour
    {
        private SoulformExpressionDriver expressionDriver;
        private SoulformModel soulform;

        [Tooltip("How long each viseme holds before advancing (seconds)")]
        public float visemeHoldDuration = 0.12f;

        [Tooltip("How long silence holds at end of speech (seconds)")]
        public float silenceDuration = 0.3f;

        private string currentText = "";
        private int textIndex;
        private float timer;
        private bool isSpeaking;

        private void Awake()
        {
            expressionDriver = GetComponent<SoulformExpressionDriver>();
        }

        public void SetSoulform(SoulformModel model)
        {
            soulform = model;
        }

        public void Speak(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                StopSpeaking();
                return;
            }

            currentText = text;
            textIndex = 0;
            timer = 0f;
            isSpeaking = true;
        }

        public void StopSpeaking()
        {
            isSpeaking = false;
            currentText = "";
            textIndex = 0;
            timer = 0f;
            if (expressionDriver != null)
                expressionDriver.SetViseme("");
        }

        private void Update()
        {
            if (!isSpeaking || expressionDriver == null)
                return;

            if (soulform != null)
            {
                string thought = soulform.CurrentThought;
                if (thought != currentText)
                {
                    Speak(thought);
                }
            }

            timer -= Time.deltaTime;
            if (timer > 0f)
                return;

            if (textIndex >= currentText.Length)
            {
                timer = silenceDuration;
                expressionDriver.SetViseme("");
                isSpeaking = false;
                return;
            }

            char c = currentText[textIndex];
            string viseme = CharToViseme(c);
            expressionDriver.SetViseme(viseme);
            timer = visemeHoldDuration;
            textIndex++;
        }

        private static string CharToViseme(char c)
        {
            switch (c)
            {
                case 'A': case 'a': case '@': return "AA";
                case 'E': case 'e': return "E";
                case 'I': case 'i': return "IH";
                case 'O': case 'o': return "OH";
                case 'U': case 'u': return "OU";
                case 'M': case 'B': case 'P': case 'm': case 'b': case 'p': return "PP";
                case 'F': case 'V': case 'f': case 'v': return "FF";
                case 'D': case 'T': case 'd': case 't': return "DD";
                case 'K': case 'G': case 'k': case 'g': return "KK";
                case 'S': case 'Z': case 's': case 'z': return "SS";
                case 'N': case 'L': case 'R':
                case 'n': case 'l': case 'r':
                case 'H': case 'h':
                case 'W': case 'w':
                case 'Y': case 'y': return "IH";
                default: return "";
            }
        }
    }
}
