using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    /// <summary>
    /// One-time installer for the Master Navigation "Link Companion App" button.
    /// Clones the Bubble Network button into the World column, reflows the
    /// column to four rows (the column card is 544×824, so the original
    /// three-row pitch would push a fourth row outside the card), raises the
    /// header icon to clear the first row, relabels the clone, and wires
    /// MasterNavigationScreenUI.worldLinkAppButton. Idempotent: re-running
    /// does nothing once the field is assigned.
    ///
    /// Run: menu BubbleWorld ▸ Install Link Companion App Button, or
    /// Unity -batchmode -quit -executeMethod BubbleWorld.LinkCompanionButtonInstaller.Install
    /// </summary>
    public static class LinkCompanionButtonInstaller
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ButtonName = "Link Companion App";
        private const string SourceLabel = "Bubble Network";

        // Row centers for the four-row World column (pitch 148). The card
        // interior is ±412 around the column center: rows span 170..-408.
        private static readonly float[] RowY = { 103f, -45f, -193f, -341f };
        private const float HeaderIconY = 296f;   // was 280; +16 clears row 1

        [MenuItem("BubbleWorld/Install Link Companion App Button")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var nav = Object.FindObjectOfType<MasterNavigationScreenUI>(true);
            if (nav == null)
                throw new System.InvalidOperationException(
                    "[LinkApp] MasterNavigationScreenUI not found in " + ScenePath);

            var so = new SerializedObject(nav);
            if (so.FindProperty("worldLinkAppButton").objectReferenceValue != null)
            {
                Debug.Log("[LinkApp] Already installed — nothing to do.");
                return;
            }

            var src = so.FindProperty("worldNetworkButton").objectReferenceValue as Button;
            if (src == null)
                throw new System.InvalidOperationException(
                    "[LinkApp] worldNetworkButton is not assigned; cannot clone.");

            // Clone into the same column as the fourth (bottom) row.
            var clone = Object.Instantiate(src.gameObject, src.transform.parent, false);
            clone.name = ButtonName;
            SetRow(clone.transform as RectTransform, RowY[3]);

            // Reflow the three existing rows into the four-row layout.
            SetRow(RectOf(so, "worldOverviewButton"), RowY[0]);
            SetRow(RectOf(so, "worldNetworkButton"), RowY[1]);
            SetRow(RectOf(so, "worldCreateButton"), RowY[2]);

            // Raise the column header icon so it clears the first row.
            Transform column = src.transform.parent;
            Transform icon = column.Find("Icon");
            if (icon is RectTransform iconRt)
                SetRow(iconRt, HeaderIconY);

            // Relabel the clone.
            bool labeled = false;
            foreach (var tmp in clone.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.text == SourceLabel)
                {
                    tmp.text = ButtonName;
                    labeled = true;
                    break;
                }
            }
            if (!labeled)
                Debug.LogWarning("[LinkApp] Clone label '" + SourceLabel + "' not found; check manually.");

            so.FindProperty("worldLinkAppButton").objectReferenceValue = clone.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new System.InvalidOperationException("[LinkApp] Failed to save " + ScenePath);

            Debug.Log("[LinkApp] Installed '" + ButtonName + "' button in " + ScenePath);
        }

        private static RectTransform RectOf(SerializedObject so, string field)
        {
            var b = so.FindProperty(field).objectReferenceValue as Button;
            return b != null ? b.transform as RectTransform : null;
        }

        private static void SetRow(RectTransform rt, float y)
        {
            if (rt == null) return;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }
    }
}
