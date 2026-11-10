using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace BubbleWorld
{
    public sealed class PairingSession
    {
        public string SessionId;
        public string ShortCode;
        public string DeepLink;
        public DateTime ExpiresUtc;
        public Texture2D QrTexture;

        public bool IsExpired => DateTime.UtcNow >= ExpiresUtc;

        public int SecondsRemaining =>
            Mathf.Max(0, (int)(ExpiresUtc - DateTime.UtcNow).TotalSeconds);

        public void Dispose()
        {
            if (QrTexture != null)
            {
                UnityEngine.Object.Destroy(QrTexture);
                QrTexture = null;
            }
        }
    }

    // Pairing session lifecycle against the backend's device endpoints —
    // shared x-api-key + worldKey instead of a login, the same trust model as
    // world sync. The QR payload is the deepLink the backend hands out; the
    // short code doubles as a manual fallback for the companion app.
    public sealed class PairingService
    {
        public const string DeviceSessionPath = "/api/v1/pairing/device/session";

        // Same world the sync service pushes under.
        public string WorldKey { get; } = ServerSyncService.DefaultWorldKey;

        public IEnumerator CreateSession(Action<PairingSession> onDone, Action<string> onError)
        {
            yield return BackendGateway.EnsureResolved();
            if (BackendGateway.ResolvedBase == null)
            {
                onError?.Invoke("Backend unreachable — start the server or check the connection.");
                yield break;
            }

            string body = JsonUtility.ToJson(new DeviceSessionRequest { worldKey = WorldKey });
            using (var request = new UnityWebRequest(BackendGateway.ResolvedBase + DeviceSessionPath, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-api-key", BackendGateway.ApiKey);
                request.timeout = BackendGateway.RequestTimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(Describe(request));
                    yield break;
                }

                DeviceSessionEnvelope envelope =
                    JsonUtility.FromJson<DeviceSessionEnvelope>(request.downloadHandler.text);
                if (envelope == null || envelope.data == null ||
                    string.IsNullOrEmpty(envelope.data.deepLink))
                {
                    onError?.Invoke("Malformed pairing response from the backend.");
                    yield break;
                }

                onDone?.Invoke(BuildSession(envelope.data));
            }
        }

        // Best-effort: closing the overlay cancels the pending session so the
        // short code stops being claimable immediately (it would expire in
        // five minutes anyway).
        public IEnumerator CancelSession(string sessionId)
        {
            if (BackendGateway.ResolvedBase == null || string.IsNullOrEmpty(sessionId))
                yield break;

            string url = BackendGateway.ResolvedBase + DeviceSessionPath + "/" + sessionId +
                "?worldKey=" + UnityWebRequest.EscapeURL(WorldKey);
            using (var request = UnityWebRequest.Delete(url))
            {
                request.SetRequestHeader("x-api-key", BackendGateway.ApiKey);
                request.timeout = BackendGateway.RequestTimeoutSeconds;
                yield return request.SendWebRequest();
            }
        }

        private static PairingSession BuildSession(DeviceSessionData data)
        {
            DateTime expiresUtc;
            if (!DateTime.TryParse(data.expiresAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out expiresUtc))
                expiresUtc = DateTime.UtcNow.AddMinutes(5);
            if (expiresUtc.Kind != DateTimeKind.Utc)
                expiresUtc = expiresUtc.ToUniversalTime();

            return new PairingSession
            {
                SessionId = data.sessionId,
                ShortCode = data.shortCode,
                DeepLink = data.deepLink,
                ExpiresUtc = expiresUtc,
                QrTexture = BuildQrTexture(data.deepLink),
            };
        }

        // Renders the QR matrix to a pixel-art texture: point filtered, black
        // modules on white, y-flipped (matrix row 0 is the symbol's top, Unity
        // textures grow upward), quiet zone included by the encoder.
        public static Texture2D BuildQrTexture(string text)
        {
            QrCodeEncoder.QrCode qr;
            try
            {
                qr = QrCodeEncoder.Encode(text);
            }
            catch (Exception e)
            {
                Debug.LogError("[Pairing] QR encode failed: " + e.Message);
                return null;
            }

            int size = qr.Size;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    byte v = qr.Modules[r, c] ? (byte)0 : (byte)255;
                    int y = size - 1 - r;   // matrix top → texture top
                    pixels[y * size + c] = new Color32(v, v, v, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        private static string Describe(UnityWebRequest request)
        {
            long code = request.responseCode;
            if (code == 401) return "Backend rejected the shared API key (401).";
            if (code >= 500) return "Backend error " + code + ".";
            if (request.result == UnityWebRequest.Result.ConnectionError)
                return "Backend unreachable (" + request.error + ").";
            return "Pairing request failed (" + code + ").";
        }

        [Serializable] private class DeviceSessionRequest { public string worldKey; }
        [Serializable] private class DeviceSessionEnvelope { public DeviceSessionData data; }
        [Serializable] private class DeviceSessionData
        {
            public string sessionId;
            public string shortCode;
            public string deepLink;
            public string expiresAt;
        }
    }
}
