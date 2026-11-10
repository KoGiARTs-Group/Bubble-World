using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "AudioCue", menuName = "BubbleWorld/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        public bool loop;
    }
}
