using System;

namespace BubbleWorld
{
    public enum SpeakerType
    {
        Player,
        Soulform
    }

    [Serializable]
    public sealed class ChatMessage
    {
        public SpeakerType Speaker;
        public string Text;
        public string Timestamp;
        public string Emotion;
        public string Expression;
        public string Animation;
        public string PortraitId;
        public bool HasVoice;
        public string VoiceClipId;

        public ChatMessage(SpeakerType speaker, string text, string timestamp = null, string emotion = null, string expression = null, string animation = null)
        {
            Speaker = speaker;
            Text = text;
            Timestamp = string.IsNullOrEmpty(timestamp) ? DateTime.Now.ToString("h:mm tt") : timestamp;
            Emotion = emotion;
            Expression = expression;
            Animation = animation;
            PortraitId = null;
            HasVoice = false;
            VoiceClipId = null;
        }
    }
}
