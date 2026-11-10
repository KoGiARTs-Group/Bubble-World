using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private SoundBank soundBank;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private int sfxPoolSize = 4;

        private int currentTrack;
        private readonly List<AudioSource> sfxPool = new List<AudioSource>();

        private void Awake()
        {
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
            }
            if (sfxSource == null)
                sfxSource = gameObject.AddComponent<AudioSource>();

            for (int i = 0; i < sfxPoolSize; i++)
            {
                AudioSource pool = gameObject.AddComponent<AudioSource>();
                pool.playOnAwake = false;
                sfxPool.Add(pool);
            }
        }

        public void PlayBgm(int index)
        {
            if (soundBank == null || soundBank.bgmPerWorld == null) return;
            if (index < 0 || index >= soundBank.bgmPerWorld.Length) return;
            AudioCue cue = soundBank.bgmPerWorld[index].cue;
            if (cue == null || cue.clip == null) return;
            currentTrack = index;
            bgmSource.clip = cue.clip;
            bgmSource.volume = cue.volume;
            bgmSource.pitch = cue.pitch;
            bgmSource.Play();
        }

        public void PlayBgmForWorld(StarterEnvironment environment)
        {
            if (soundBank == null) return;
            AudioCue cue = soundBank.FindBgm(environment);
            if (cue == null || cue.clip == null) return;
            bgmSource.clip = cue.clip;
            bgmSource.volume = cue.volume;
            bgmSource.pitch = cue.pitch;
            bgmSource.Play();
        }

        public void StopBgm()
        {
            bgmSource.Stop();
        }

        public void PlaySfx(AudioCue cue)
        {
            if (cue == null || cue.clip == null) return;
            AudioSource src = GetPooledSource();
            if (src != null)
            {
                src.clip = cue.clip;
                src.volume = cue.volume;
                src.pitch = cue.pitch;
                src.loop = cue.loop;
                src.Play();
            }
        }

        public void PlaySfxOneShot(AudioCue cue)
        {
            if (cue == null || cue.clip == null) return;
            sfxSource.PlayOneShot(cue.clip, cue.volume);
        }

        public void PlayHover()
        {
            if (soundBank != null) PlaySfxOneShot(soundBank.cursorHover);
        }

        public void PlaySelect()
        {
            if (soundBank != null) PlaySfxOneShot(soundBank.cursorSelect);
        }

        public void PlayBrushPlace()
        {
            if (soundBank != null) PlaySfxOneShot(soundBank.brushPlace);
        }

        public void PlayBrushRemove()
        {
            if (soundBank != null) PlaySfxOneShot(soundBank.brushRemove);
        }

        public void PlayUiBack()
        {
            if (soundBank != null) PlaySfxOneShot(soundBank.uiBack);
        }

        public void PlaySwitch()
        {
            if (soundBank == null) return;
            PlaySfxOneShot(soundBank.cursorSwitch ?? soundBank.cursorSelect);
        }

        public void PlayLivabilityAlert()
        {
            if (soundBank != null) PlaySfx(soundBank.livabilityAlert);
        }

        public void PlaySfxByName(string name)
        {
            if (soundBank == null) return;
            PlaySfx(soundBank.FindSfx(name));
        }

        private AudioSource GetPooledSource()
        {
            foreach (AudioSource src in sfxPool)
            {
                if (!src.isPlaying) return src;
            }
            AudioSource extra = gameObject.AddComponent<AudioSource>();
            extra.playOnAwake = false;
            sfxPool.Add(extra);
            return extra;
        }
    }
}
