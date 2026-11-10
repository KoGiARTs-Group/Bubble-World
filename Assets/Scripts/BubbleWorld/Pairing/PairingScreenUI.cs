using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BubbleWorld
{
    /// <summary>
    /// "Link Companion App" overlay. Authored in the scene (Canvas ▸ Pairing
    /// Screen, matched to the project's UI theme) and bound through the
    /// serialized fields below — Assets/Editor/PairingScreenUIBinder wires
    /// them once. When those fields are empty (e.g. the component is added to
    /// a fresh GameObject at runtime), EnsureBuilt constructs the same overlay
    /// procedurally, so the screen works either way. Opened from
    /// Master Navigation → World → Link Companion App. Shows two QR codes side by side:
    /// (1) a static "Get the app" code pointing at GET /app/download, which the
    /// backend redirects to Google Play or the App Store based on the scanning
    /// device, and (2) the QR for a fresh device pairing session (5-minute
    /// expiry), with the short code echoed as manual text fallback, a live
    /// countdown, and refresh/close controls.
    /// Gamepad: Left/Right moves focus, Submit activates, Cancel (Escape/B)
    /// closes and revokes the pending session.
    /// </summary>
    public sealed class PairingScreenUI : MonoBehaviour
    {
        private const int PanelWidth = 900;
        private const int PanelHeight = 900;
        private const int MaxQrPixels = 340;   // per-QR cap; two codes sit side by side
        private const float ColumnX = 210f;    // column centers, ± from the panel center
        private const string DownloadPath = "/app/download";

        private readonly PairingService service = new PairingService();
        private PairingSession session;

        // Bound to the scene-authored panel; empty fields mean "build the
        // overlay in code" (see EnsureBuilt). Set up by the binder tool.
        [Header("Authored panel bindings (empty = built in code)")]
        [SerializeField] private RawImage qrImage;
        [SerializeField] private RawImage downloadQrImage;
        [SerializeField] private TextMeshProUGUI codeLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private TextMeshProUGUI errorLabel;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button closeButton;

        private GameObject root;
        private RectTransform qrRect;
        private RectTransform downloadQrRect;
        private Texture2D downloadQrTexture;   // static content, built once per run
        private AudioManager audioManager;

        private bool built;
        private bool busy;
        private int generation;    // invalidates in-flight requests after close/refresh
        private int lastShownSecond = -1;

        // Colors follow the project's UITheme palette.
        private static readonly Color PanelBg = new Color(0.08f, 0.10f, 0.12f, 0.97f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color TextOnDark = new Color(0.92f, 0.95f, 0.93f, 1f);
        private static readonly Color TextSecondary = new Color(0.70f, 0.74f, 0.72f, 1f);
        private static readonly Color Accent = new Color(0.30f, 0.85f, 0.50f, 1f);
        private static readonly Color ErrorColor = new Color(0.95f, 0.45f, 0.40f, 1f);
        private static readonly Color ButtonBg = new Color(0.88f, 0.91f, 0.85f, 0.94f);
        private static readonly Color ButtonText = new Color(0.05f, 0.07f, 0.06f, 1f);

        public void Open()
        {
            EnsureBuilt();
            if (audioManager == null) audioManager = FindObjectOfType<AudioManager>();
            root.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (!built || !root.activeSelf) return;

            generation++;               // drop any in-flight create callback
            busy = false;
            ReleaseSession(cancelOnServer: true);
            codeLabel.text = "Link code: —";
            statusLabel.text = "";
            ClearError();
            root.SetActive(false);
        }

        private void Refresh()
        {
            if (busy) return;
            StartCoroutine(CreateRoutine());
        }

        private System.Collections.IEnumerator CreateRoutine()
        {
            busy = true;
            int gen = ++generation;
            ClearError();
            statusLabel.text = "Requesting pairing code…";
            SetButtonsInteractable(false);

            // Resolve once up front: the download QR needs the phone-reachable
            // base before the session QR request goes out.
            yield return BackendGateway.EnsureResolved();
            if (gen != generation) yield break;   // closed or refreshed while probing
            EnsureDownloadQr();

            yield return service.CreateSession(
                s =>
                {
                    if (gen != generation) { s.Dispose(); return; }
                    ReleaseSession(cancelOnServer: true);
                    session = s;
                    ShowSession();
                    busy = false;
                    SetButtonsInteractable(true);
                },
                error =>
                {
                    if (gen != generation) return;
                    statusLabel.text = "Pairing unavailable";
                    errorLabel.text = error;
                    errorLabel.gameObject.SetActive(true);
                    busy = false;
                    SetButtonsInteractable(true);
                });
        }

        private void ShowSession()
        {
            qrImage.texture = session.QrTexture;
            if (session.QrTexture != null)
            {
                int size = session.QrTexture.width;
                int scale = Mathf.Clamp(MaxQrPixels / size, 6, 12);   // whole pixels per module
                qrRect.sizeDelta = new Vector2(size * scale, size * scale);
            }
            codeLabel.text = "Link code: " + session.ShortCode;
            lastShownSecond = -1;   // force the countdown line to repaint
        }

        // The download QR is static for a given resolved backend, so it is
        // built on the first successful probe and kept across open/close.
        private void EnsureDownloadQr()
        {
            if (downloadQrTexture != null || downloadQrImage == null) return;
            string baseUrl = BackendGateway.PublicBase;
            if (string.IsNullOrEmpty(baseUrl)) return;   // nothing reachable to point at

            downloadQrTexture = PairingService.BuildQrTexture(baseUrl + DownloadPath);
            if (downloadQrTexture == null) return;
            downloadQrImage.texture = downloadQrTexture;
            int size = downloadQrTexture.width;
            int scale = Mathf.Clamp(MaxQrPixels / size, 6, 12);   // whole pixels per module
            downloadQrRect.sizeDelta = new Vector2(size * scale, size * scale);
        }

        private void ReleaseSession(bool cancelOnServer)
        {
            if (session == null) return;
            if (cancelOnServer)
                StartCoroutine(service.CancelSession(session.SessionId));
            session.Dispose();
            session = null;
            qrImage.texture = null;
        }

        private void OnDestroy()
        {
            if (downloadQrTexture != null)
            {
                Destroy(downloadQrTexture);
                downloadQrTexture = null;
            }
        }

        private void Update()
        {
            if (!built || !root.activeSelf) return;

            if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(KeyCode.B))
            {
                PlaySound(audio => audio.PlayUiBack());
                Close();
                return;
            }

            if (session == null || busy) return;

            int remaining = session.SecondsRemaining;
            if (remaining == lastShownSecond) return;
            lastShownSecond = remaining;
            statusLabel.text = remaining > 0
                ? string.Format("Code expires in {0}:{1:00}", remaining / 60, remaining % 60)
                : "Code expired — choose Refresh Code";
        }

        // ---- Construction (runs once, on first Open) ----

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;

            if (qrImage != null)
            {
                // Scene-authored panel: this component sits on the overlay
                // itself, so the overlay doubles as the toggled root. Derive
                // the QR rects and wire the button handlers (a scene can't
                // carry the private click targets).
                root = gameObject;
                qrRect = qrImage.rectTransform;
                if (downloadQrImage != null)
                    downloadQrRect = downloadQrImage.rectTransform;
                if (refreshButton != null)
                {
                    refreshButton.onClick.RemoveAllListeners();
                    refreshButton.onClick.AddListener(OnRefreshClicked);
                }
                if (closeButton != null)
                {
                    closeButton.onClick.RemoveAllListeners();
                    closeButton.onClick.AddListener(OnCloseClicked);
                }
                return;
            }

            var canvasGo = new GameObject("Pairing Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;   // above the game HUD, below nothing
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            root = CreateObject("Dim", canvasGo.transform);
            Stretch(root.GetComponent<RectTransform>());
            root.AddComponent<Image>().color = DimColor;

            var panel = CreateObject("Panel", root.transform);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.AddComponent<Image>().color = PanelBg;

            var title = CreateText(panel.transform, "Link Companion App", 46, TextAlignmentOptions.Center);
            PlaceTop(title.rectTransform, 40, 64);
            title.color = TextOnDark;

            var subtitle = CreateText(panel.transform,
                "Scan with your phone camera", 22, TextAlignmentOptions.Center);
            PlaceTop(subtitle.rectTransform, 112, 40);
            subtitle.color = TextSecondary;

            var getCaption = CreateText(panel.transform, "1 · Get the app", 30, TextAlignmentOptions.Center);
            PlaceTop(getCaption.rectTransform, -ColumnX, 156, 36, 400f);
            getCaption.color = TextOnDark;

            var linkCaption = CreateText(panel.transform, "2 · Link this device", 30, TextAlignmentOptions.Center);
            PlaceTop(linkCaption.rectTransform, ColumnX, 156, 36, 400f);
            linkCaption.color = TextOnDark;

            var downloadHolder = CreateObject("Download QR", panel.transform);
            downloadQrRect = downloadHolder.GetComponent<RectTransform>();
            PlaceTop(downloadQrRect, -ColumnX, 200, MaxQrPixels, MaxQrPixels);
            downloadQrImage = downloadHolder.AddComponent<RawImage>();
            downloadQrImage.color = Color.white;
            downloadQrImage.raycastTarget = false;

            var qrHolder = CreateObject("QR", panel.transform);
            qrRect = qrHolder.GetComponent<RectTransform>();
            PlaceTop(qrRect, ColumnX, 200, MaxQrPixels, MaxQrPixels);
            qrImage = qrHolder.AddComponent<RawImage>();
            qrImage.color = Color.white;
            qrImage.raycastTarget = false;

            var downloadHint = CreateText(panel.transform,
                "Opens the Play Store or App Store", 18, TextAlignmentOptions.Center);
            PlaceTop(downloadHint.rectTransform, -ColumnX, 548, 40, 400f);
            downloadHint.color = TextSecondary;

            var linkHint = CreateText(panel.transform,
                "Scan from inside the companion app", 18, TextAlignmentOptions.Center);
            PlaceTop(linkHint.rectTransform, ColumnX, 548, 40, 400f);
            linkHint.color = TextSecondary;

            codeLabel = CreateText(panel.transform, "Link code: —", 42, TextAlignmentOptions.Center);
            PlaceBottom(codeLabel.rectTransform, 240, 54);
            codeLabel.color = Accent;

            statusLabel = CreateText(panel.transform, "", 26, TextAlignmentOptions.Center);
            PlaceBottom(statusLabel.rectTransform, 190, 44);
            statusLabel.color = TextSecondary;

            errorLabel = CreateText(panel.transform, "", 22, TextAlignmentOptions.Center);
            PlaceBottom(errorLabel.rectTransform, 140, 44);
            errorLabel.color = ErrorColor;
            errorLabel.gameObject.SetActive(false);

            refreshButton = CreateButton(panel.transform, "Refresh Code",
                new Vector2(-170, 45), OnRefreshClicked);
            closeButton = CreateButton(panel.transform, "Close",
                new Vector2(170, 45), OnCloseClicked);

            root.SetActive(false);
        }

        private void OnRefreshClicked()
        {
            PlaySound(audio => audio.PlaySelect());
            Refresh();
        }

        private void OnCloseClicked()
        {
            PlaySound(audio => audio.PlayUiBack());
            Close();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (refreshButton != null) refreshButton.interactable = interactable;
            if (closeButton != null) closeButton.interactable = interactable;
            if (interactable && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(refreshButton.gameObject);
        }

        private void ClearError()
        {
            if (errorLabel != null) errorLabel.gameObject.SetActive(false);
        }

        private void PlaySound(System.Action<AudioManager> play)
        {
            if (audioManager != null) play(audioManager);
        }

        // ---- Small uGUI helpers ----

        private static GameObject CreateObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // Position against the panel's top edge (pivot y = 1).
        private static void PlaceTop(RectTransform rt, float y, float height)
        {
            PlaceTop(rt, 0f, y, height, 840f);
        }

        private static void PlaceTop(RectTransform rt, float x, float y, float height, float width)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }

        // Position against the panel's bottom edge (pivot y = 0).
        private static void PlaceBottom(RectTransform rt, float y, float height)
        {
            PlaceBottom(rt, 0f, y, height, 840f);
        }

        private static void PlaceBottom(RectTransform rt, float x, float y, float height, float width)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static TextMeshProUGUI CreateText(
            Transform parent, string content, int fontSize, TextAlignmentOptions alignment)
        {
            var go = CreateObject("Text", parent);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;
            return text;
        }

        private Button CreateButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var go = CreateObject(label + " Button", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(300, 64);

            go.AddComponent<Image>().color = ButtonBg;
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            var text = CreateText(go.transform, label, 26, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.color = ButtonText;
            return button;
        }
    }
}
