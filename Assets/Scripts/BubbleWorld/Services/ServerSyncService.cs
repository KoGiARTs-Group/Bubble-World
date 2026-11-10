using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace BubbleWorld
{
    public enum ServerStatus
    {
        Offline,
        Online,
        Maintenance
    }

    // World-state sync against the backend — the backend is the single source of
    // truth: PullLatestState() fetches the authoritative snapshot at startup,
    // PushLocalChanges() uploads the local save blob (BubbleWorldSaveData JSON)
    // after every save. Uses the same BackendGateway link resolution and shared
    // x-api-key as the brain routes; no login.
    public sealed class ServerSyncService
    {
        public const string DefaultWorldKey = "default";

        public ServerStatus GetServerStatus() => lastStatus;

        private ServerStatus lastStatus = ServerStatus.Offline;

        // Null when nothing is stored yet (version 0) or the backend is unreachable.
        public IEnumerator PullLatestState(Action<ServerStateData> onDone)
        {
            yield return BackendGateway.EnsureResolved();
            if (BackendGateway.ResolvedBase == null)
            {
                lastStatus = ServerStatus.Offline;
                onDone?.Invoke(null);
                yield break;
            }

            string url = BackendGateway.ResolvedBase + BackendGateway.SyncPath +
                "?worldKey=" + DefaultWorldKey;
            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("x-api-key", BackendGateway.ApiKey);
                request.timeout = BackendGateway.RequestTimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    lastStatus = ServerStatus.Offline;
                    onDone?.Invoke(null);
                    yield break;
                }

                lastStatus = ServerStatus.Online;
                SyncEnvelope envelope = JsonUtility.FromJson<SyncEnvelope>(request.downloadHandler.text);
                SyncData data = envelope?.data;
                if (data == null || string.IsNullOrEmpty(data.state))
                {
                    onDone?.Invoke(null); // reachable, but no snapshot stored yet
                    yield break;
                }

                onDone?.Invoke(new ServerStateData
                {
                    WorldId = data.worldKey,
                    SerializedState = data.state,
                    Version = data.version,
                    UpdatedAt = data.updatedAt
                });
            }
        }

        // True when the backend stored the snapshot (version incremented).
        public IEnumerator PushLocalChanges(string serializedState, Action<bool> onDone)
        {
            yield return BackendGateway.EnsureResolved();
            if (BackendGateway.ResolvedBase == null || string.IsNullOrEmpty(serializedState))
            {
                onDone?.Invoke(false);
                yield break;
            }

            string body = JsonUtility.ToJson(new SyncPutRequest
            {
                worldKey = DefaultWorldKey,
                state = serializedState
            });

            using (var request = new UnityWebRequest(BackendGateway.ResolvedBase + BackendGateway.SyncPath, "PUT"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-api-key", BackendGateway.ApiKey);
                request.timeout = BackendGateway.RequestTimeoutSeconds;
                yield return request.SendWebRequest();

                bool ok = request.result == UnityWebRequest.Result.Success;
                if (ok) lastStatus = ServerStatus.Online;
                else if (request.result == UnityWebRequest.Result.ConnectionError)
                    lastStatus = ServerStatus.Offline;
                onDone?.Invoke(ok);
            }
        }

        [Serializable] private class SyncEnvelope { public SyncData data; }
        [Serializable] private class SyncData
        {
            public string worldKey;
            public int version;
            public string state;
            public string updatedAt;
        }
        [Serializable] private class SyncPutRequest
        {
            public string worldKey;
            public string state;
        }
    }

    public sealed class ServerStateData
    {
        public string WorldId;
        public string SerializedState;
        public int Version;
        public string UpdatedAt;
    }
}
