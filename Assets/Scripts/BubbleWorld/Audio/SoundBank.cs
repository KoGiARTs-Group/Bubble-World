using System;
using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "SoundBank", menuName = "BubbleWorld/Sound Bank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public sealed class SfxEntry
        {
            public string name;
            public AudioCue cue;
        }

        [Serializable]
        public sealed class BgmEntry
        {
            public StarterEnvironment environment;
            public AudioCue cue;
        }

        public BgmEntry[] bgmPerWorld;
        public SfxEntry[] sfx;
        public AudioCue cursorHover;
        public AudioCue cursorSelect;
        public AudioCue brushPlace;
        public AudioCue brushRemove;
        public AudioCue uiBack;
        public AudioCue cursorSwitch;
        public AudioCue livabilityAlert;

        public AudioCue FindSfx(string name)
        {
            if (sfx == null) return null;
            foreach (SfxEntry entry in sfx)
            {
                if (entry != null && entry.name == name)
                    return entry.cue;
            }
            return null;
        }

        public AudioCue FindBgm(StarterEnvironment environment)
        {
            if (bgmPerWorld == null) return null;
            foreach (BgmEntry entry in bgmPerWorld)
            {
                if (entry != null && entry.environment == environment)
                    return entry.cue;
            }
            return null;
        }
    }
}
