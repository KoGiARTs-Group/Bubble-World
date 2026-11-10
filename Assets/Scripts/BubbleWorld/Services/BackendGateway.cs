using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace BubbleWorld
{
    // The one place in the client that knows how to reach the BubbleWorld backend.
    // Owns the local/remote link pair (from SoulformBrainConfig), availability
    // probing, and the runtime config the backend serves — the backend is the
    // single source of truth for anything tunable (tick cadence, feature flags);
    // the ScriptableObject only carries connection links.
    //
    // Resolution: GET <link>/api/v1/ai/status on local first, then remote
    // (2s each); first 200 wins and its served config is applied. The result is
    // cached for the session; Invalidate() drops it on connection errors so the
    // next call fails over to the other link. Failed probes are not cached, so a
    // backend started later is picked up on the next attempt.
    public static class BackendGateway
    {
        public const string StatusPath = "/api/v1/ai/status";
        public const string BrainPath = "/api/v1/ai/chat";
        public const string SyncPath = "/api/v1/sync/state";
        private const int ProbeTimeoutSeconds = 2;

        public static string ApiKey { get; private set; } = "";
        public static int RequestTimeoutSeconds { get; private set; } = 30;

        // Null until a probe succeeds (or when no links are configured).
        public static string ResolvedBase { get; private set; }

        // Backend-served runtime config (0 / false until a status fetch succeeds).
        public static float ServerTickSeconds { get; private set; }
        public static bool WorldSyncEnabled { get; private set; }

        // Phone-reachable base for QR payloads (the download QR a phone must
        // open): backend-served PUBLIC_BASE_URL wins — it knows its LAN or
        // deployed host — then the deployed link, then whatever link we
        // connected with. None are stored with a trailing slash.
        public static string PublicBase
        {
            get
            {
                if (!string.IsNullOrEmpty(servedPublicBaseUrl)) return servedPublicBaseUrl;
                if (!string.IsNullOrEmpty(remoteUrl)) return remoteUrl;
                return ResolvedBase;
            }
        }

        public static bool IsConfigured =>
            !string.IsNullOrEmpty(localUrl) || !string.IsNullOrEmpty(remoteUrl);

        private static string localUrl;
        private static string remoteUrl;
        private static string servedPublicBaseUrl;

        public static void Configure(SoulformBrainConfig config)
        {
            if (config == null) return;
            localUrl = string.IsNullOrEmpty(config.LocalBackendUrl)
                ? null : config.LocalBackendUrl.TrimEnd('/');
            remoteUrl = string.IsNullOrEmpty(config.RemoteBackendUrl)
                ? null : config.RemoteBackendUrl.TrimEnd('/');
            ApiKey = config.ApiKey ?? "";
            RequestTimeoutSeconds = config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 30;
            if (localUrl == null && remoteUrl == null)
            {
                // Links removed → never resolve; brain falls back to legacy paths.
                ResolvedBase = null;
            }
        }

        // Connection error on a real request → drop the cache so the next
        // EnsureResolved re-probes (fails over local ↔ remote).
        public static void Invalidate() => ResolvedBase = null;

        public static IEnumerator EnsureResolved()
        {
            if (!IsConfigured || ResolvedBase != null) yield break;

            foreach (string candidate in Candidates())
            {
                using (var request = UnityWebRequest.Get(candidate + StatusPath))
                {
                    request.timeout = ProbeTimeoutSeconds;
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success) continue;

                    ResolvedBase = candidate;
                    ApplyStatusConfig(request.downloadHandler.text);
                    Debug.Log("[BackendGateway] Resolved backend: " + candidate);
                    yield break;
                }
            }
        }

        private static IEnumerable<string> Candidates()
        {
            if (localUrl != null) yield return localUrl;
            if (remoteUrl != null &&
                !string.Equals(remoteUrl, localUrl, StringComparison.OrdinalIgnoreCase))
                yield return remoteUrl;
        }

        [Serializable] private class StatusEnvelope { public StatusData data; }
        [Serializable] private class StatusData
        {
            public StatusBrain brain;
            public StatusFeatures features;
            public string publicBaseUrl;
        }
        [Serializable] private class StatusBrain { public float tickSeconds; }
        [Serializable] private class StatusFeatures { public bool worldSync; }

        private static void ApplyStatusConfig(string json)
        {
            try
            {
                StatusEnvelope env = JsonUtility.FromJson<StatusEnvelope>(json);
                if (env?.data?.brain != null && env.data.brain.tickSeconds > 0f)
                    ServerTickSeconds = env.data.brain.tickSeconds;
                if (env?.data?.features != null)
                    WorldSyncEnabled = env.data.features.worldSync;
                if (env?.data != null)
                    servedPublicBaseUrl = string.IsNullOrEmpty(env.data.publicBaseUrl)
                        ? null : env.data.publicBaseUrl.TrimEnd('/');
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BackendGateway] Could not parse served config: " + e.Message);
            }
        }
    }
}
