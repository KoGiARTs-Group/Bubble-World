using System;
using System.Reflection;
using System.Threading;
using UnityEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using UnityEngine.Windows.Speech;
#endif

namespace BubbleWorld
{
    /// <summary>
    /// Voice input (microphone dictation) and voice output (text-to-speech).
    /// Both are Windows-only and degrade gracefully: every entry point returns
    /// false / does nothing when the platform or OS speech services are missing,
    /// so the game always falls back to text-only chat.
    /// </summary>
    public static class VoiceService
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private static DictationRecognizer recognizer;
        private static bool dictationBroken;
#endif
        /// <summary>When true, Speak() does nothing (synced with the chat mute button).</summary>
        public static bool IsMuted { get; set; }

        /// <summary>True while text-to-speech audio is playing.</summary>
        public static bool IsSpeaking { get; private set; }

        /// <summary>True when this platform can capture microphone dictation.</summary>
        public static bool IsDictationAvailable
        {
            get
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                return !dictationBroken;
#else
                return false;
#endif
            }
        }

        /// <summary>Fired with the final transcription of one dictation segment.</summary>
        public static event Action<string> OnDictationResult;

        /// <summary>Fired with human-readable status text ("Listening...", errors).</summary>
        public static event Action<string> OnDictationStatus;

        /// <summary>Starts microphone capture. Returns false when unavailable.</summary>
        public static bool StartDictation()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (dictationBroken) return false;
            try
            {
                if (recognizer == null)
                {
                    recognizer = new DictationRecognizer();
                    recognizer.DictationResult += (text, confidence) => OnDictationResult?.Invoke(text);
                    recognizer.DictationComplete += cause =>
                    {
                        // Complete/Canceled = an ended phrase or our own Stop();
                        // anything else (timeout, mic loss) needs user feedback.
                        if (cause == DictationCompletionCause.Complete ||
                            cause == DictationCompletionCause.Canceled)
                            return;
                        OnDictationStatus?.Invoke("Voice input ended: " + cause);
                    };
                    recognizer.DictationError += (error, hresult) =>
                    {
                        // 0x80045509 = SPERR_SPEECH_PRIVACY_POLICY_NOT_ACCEPTED — permanent.
                        if (hresult == unchecked((int)0x80045509))
                            dictationBroken = true;
                        OnDictationStatus?.Invoke("Voice input unavailable: " + error);
                    };
                }

                if (recognizer.Status == SpeechSystemStatus.Running) return true;
                recognizer.Start();
                OnDictationStatus?.Invoke("Listening...");
                return true;
            }
            catch (Exception e)
            {
                dictationBroken = true;
                OnDictationStatus?.Invoke("Voice input unavailable");
                Debug.LogWarning("Dictation unavailable: " + e.Message);
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>Stops an active dictation session. Safe to call at any time.</summary>
        public static void StopDictation()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            try
            {
                if (recognizer != null && recognizer.Status == SpeechSystemStatus.Running)
                    recognizer.Stop();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Dictation stop failed: " + e.Message);
            }
#endif
        }

        /// <summary>
        /// Speaks text aloud through the operating system voice (SAPI).
        /// Returns false when TTS is unavailable, muted, or the text is empty.
        /// </summary>
        public static bool Speak(string text)
        {
            if (string.IsNullOrEmpty(text) || IsMuted) return false;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            try
            {
                var voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (voiceType == null) return false;

                string payload = StripLeadingTag(text);

                // Speech runs on a worker thread so a long sentence never hitches a frame.
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        IsSpeaking = true;
                        object voice = Activator.CreateInstance(voiceType);
                        voiceType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice,
                            new object[] { payload });
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("Text-to-speech failed: " + e.Message);
                    }
                    finally
                    {
                        IsSpeaking = false;
                    }
                });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Text-to-speech unavailable: " + e.Message);
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>Removes a leading [Emotion] tag so the OS voice never reads it aloud.</summary>
        private static string StripLeadingTag(string text)
        {
            if (text.Length > 0 && text[0] == '[')
            {
                int end = text.IndexOf(']');
                if (end > 0 && end < 12)
                    return text.Substring(end + 1).TrimStart();
            }
            return text;
        }
    }
}
