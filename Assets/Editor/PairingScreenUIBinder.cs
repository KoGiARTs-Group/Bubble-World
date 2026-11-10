using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    /// <summary>
    /// One-time binder for the scene-authored pairing overlay
    /// (Canvas ▸ Pairing Screen): maps the panel's RawImages, buttons, and
    /// TMP labels onto PairingScreenUI's serialized fields so the script uses
    /// the authored UI instead of building one in code. Idempotent — re-running
    /// does nothing once qrImage is assigned.
    ///
    /// Run: menu BubbleWorld ▸ Bind Pairing Screen UI References, or
    /// Unity -batchmode -quit -executeMethod BubbleWorld.PairingScreenUIBinder.Bind
    /// </summary>
    public static class PairingScreenUIBinder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("BubbleWorld/Bind Pairing Screen UI References")]
        public static void Bind()
        {
            // Prefer the already-open scene so an in-editor run never risks
            // unsaved changes; only load it when the editor sits elsewhere.
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var screen = Object.FindObjectOfType<PairingScreenUI>(true);
            if (screen == null)
                throw new System.InvalidOperationException(
                    "[PairingBind] PairingScreenUI not found — attach it to Canvas ▸ Pairing Screen first.");

            var so = new SerializedObject(screen);
            if (so.FindProperty("qrImage").objectReferenceValue != null)
            {
                Debug.Log("[PairingBind] Already bound — nothing to do.");
                return;
            }

            var rawImages = screen.GetComponentsInChildren<RawImage>(true);
            var buttons = screen.GetComponentsInChildren<Button>(true);

            RawImage pair = FindByName(rawImages, "QR");
            RawImage download = FindByName(rawImages, "Download QR");
            Button refresh = FindByName(buttons, "Refresh Code Button");
            Button close = FindByName(buttons, "Close Button");

            TextMeshProUGUI code = null, status = null, error = null;
            foreach (var t in screen.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string s = (t.text ?? "").TrimStart();
                if (code == null && s.StartsWith("Link code")) code = t;
                else if (status == null &&
                         (s.StartsWith("Code expires") || s.StartsWith("Requesting pairing") ||
                          s.StartsWith("Pairing unavailable"))) status = t;
                else if (error == null && string.IsNullOrWhiteSpace(t.text)) error = t;
            }

            var missing = new System.Collections.Generic.List<string>();
            if (pair == null) missing.Add("RawImage named 'QR'");
            if (download == null) missing.Add("RawImage named 'Download QR'");
            if (refresh == null) missing.Add("Button named 'Refresh Code Button'");
            if (close == null) missing.Add("Button named 'Close Button'");
            if (code == null) missing.Add("TMP label starting with 'Link code'");
            if (status == null) missing.Add("TMP status label (starts with 'Code expires' / 'Requesting pairing' / 'Pairing unavailable')");
            if (error == null) missing.Add("empty TMP label (error line)");
            if (missing.Count > 0)
                throw new System.InvalidOperationException(
                    "[PairingBind] Could not locate: " + string.Join("; ", missing));

            so.FindProperty("qrImage").objectReferenceValue = pair;
            so.FindProperty("downloadQrImage").objectReferenceValue = download;
            so.FindProperty("refreshButton").objectReferenceValue = refresh;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("codeLabel").objectReferenceValue = code;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("errorLabel").objectReferenceValue = error;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
            if (!EditorSceneManager.SaveScene(screen.gameObject.scene))
                throw new System.InvalidOperationException("[PairingBind] Failed to save " + ScenePath);

            Debug.Log("[PairingBind] Bound PairingScreenUI to the authored panel in " + ScenePath +
                      $" (QR={pair.name}, download={download.name}, refresh={refresh.name}, close={close.name}).");
        }

        private static T FindByName<T>(T[] items, string name) where T : Component
        {
            return System.Array.Find(items, item => item != null && item.name == name);
        }
    }
}
