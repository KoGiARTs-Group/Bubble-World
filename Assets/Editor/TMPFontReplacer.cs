using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TMPFontReplacer : EditorWindow
{
    private Font sourceFont;
    private TMP_FontAsset targetFontAsset;

    [MenuItem("Tools/TMP/Replace Scene TMP Fonts")]
    static void Open()
    {
        GetWindow<TMPFontReplacer>("TMP Font Replacer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Replace All TextMeshProUGUI Fonts", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        sourceFont = (Font)EditorGUILayout.ObjectField(
            "Unity Font",
            sourceFont,
            typeof(Font),
            false);

        targetFontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "TMP Font Asset",
            targetFontAsset,
            typeof(TMP_FontAsset),
            false);

        EditorGUILayout.HelpBox(
            "Assign either a Unity Font or a TMP Font Asset.\n" +
            "If only a Unity Font is assigned, a TMP Font Asset will be created automatically if needed.",
            MessageType.Info);

        GUI.enabled = sourceFont != null || targetFontAsset != null;

        if (GUILayout.Button("Replace Fonts"))
        {
            ReplaceFonts();
        }

        GUI.enabled = true;
    }

    void ReplaceFonts()
    {
        TMP_FontAsset fontAsset = targetFontAsset;

        if (fontAsset == null)
        {
            fontAsset = GetOrCreateTMPFontAsset(sourceFont);

            if (fontAsset == null)
            {
                Debug.LogError("Could not create TMP Font Asset.");
                return;
            }
        }

        TextMeshProUGUI[] texts = FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int count = 0;

        foreach (var text in texts)
        {
            if (text == null)
                continue;

            Undo.RecordObject(text, "Replace TMP Font");

            text.font = fontAsset;

            EditorUtility.SetDirty(text);
            count++;
        }

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene());

        Debug.Log($"Replaced font on {count} TextMeshProUGUI components.");
    }

    TMP_FontAsset GetOrCreateTMPFontAsset(Font font)
    {
        if (font == null)
            return null;

        string fontPath = AssetDatabase.GetAssetPath(font);

        if (string.IsNullOrEmpty(fontPath))
            return null;

        string directory = Path.GetDirectoryName(fontPath);
        string fileName = Path.GetFileNameWithoutExtension(fontPath);

        string assetPath = directory + "/" + fileName + " SDF.asset";

        TMP_FontAsset existing =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);

        if (existing != null)
            return existing;

        // Create TMP Font Asset
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);

        AssetDatabase.CreateAsset(fontAsset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created TMP Font Asset:\n{assetPath}");

        return fontAsset;
    }
}