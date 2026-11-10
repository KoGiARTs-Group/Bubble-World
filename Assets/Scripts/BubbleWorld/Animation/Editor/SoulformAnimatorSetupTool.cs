using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;

namespace BubbleWorld
{
    public sealed class SoulformAnimatorSetupTool : EditorWindow
    {
        private string controllerName = "SoulformAnimatorController";
        private string savePath = "Assets/Animations";

        [MenuItem("Tools/BubbleWorld/Create Soulform Animator Controller")]
        private static void ShowWindow()
        {
            GetWindow<SoulformAnimatorSetupTool>("Soulform Animator Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("Create Soulform Animator Controller", EditorStyles.boldLabel);
            GUILayout.Space(10);

            controllerName = EditorGUILayout.TextField("Controller Name", controllerName);
            savePath = EditorGUILayout.TextField("Save Path", savePath);

            GUILayout.Space(10);

            if (GUILayout.Button("Create Controller", GUILayout.Height(30)))
            {
                CreateAnimatorController();
            }

            GUILayout.Space(20);
            GUILayout.Label("Animation Parameters (triggers):", EditorStyles.boldLabel);
            GUILayout.Label("Idle, Wave, Nod, ShakeHead, LookAround,");
            GUILayout.Label("Point, Sit, Walk, Celebrate, Defeated");
        }

        private void CreateAnimatorController()
        {
            string fullPath = Path.Combine(Application.dataPath, savePath.Replace("Assets/", ""));

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                AssetDatabase.Refresh();
            }

            string assetPath = Path.Combine(savePath, controllerName + ".controller");
            string fullAssetPath = "Assets/" + assetPath.Replace("Assets/", "");

            // Check if already exists
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(fullAssetPath) != null)
            {
                if (!EditorUtility.DisplayDialog("Overwrite?",
                        $"Controller already exists at {fullAssetPath}. Overwrite?",
                        "Yes", "Cancel"))
                    return;

                AssetDatabase.DeleteAsset(fullAssetPath);
                AssetDatabase.Refresh();
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(fullAssetPath);
            if (controller == null)
            {
                Debug.LogError("Failed to create Animator Controller");
                return;
            }

            // Remove default state
            var stateMachine = controller.layers[0].stateMachine;
            stateMachine.states = new ChildAnimatorState[0];

            string[] animationNames = new string[]
            {
                "Idle", "Wave", "Nod", "ShakeHead", "LookAround",
                "Point", "Sit", "Walk", "Celebrate", "Defeated"
            };

            // Add trigger parameters and states
            foreach (var name in animationNames)
            {
                controller.AddParameter(name, AnimatorControllerParameterType.Trigger);

                var state = stateMachine.AddState(name);
                state.motion = null; // User assigns their clips
                state.writeDefaultValues = false;
            }

            // Add transitions from Any State to each state, triggered by the matching param
            foreach (var name in animationNames)
            {
                var state = stateMachine.states[System.Array.FindIndex(
                    stateMachine.states, s => s.state.name == name)].state;

                var transition = stateMachine.AddAnyStateTransition(state);
                transition.hasExitTime = false;
                transition.exitTime = 0f;
                transition.duration = 0.1f;
                transition.hasFixedDuration = true;

                // Add the trigger as a condition
                transition.AddCondition(AnimatorConditionMode.If, 0f, name);
            }

            // Set "Idle" as default state
            stateMachine.defaultState = System.Array.Find(
                stateMachine.states, s => s.state.name == "Idle").state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Soulform Animator Controller created at: {fullAssetPath}\n" +
                      "Assign your animation clips to each state in the Animator window.");
        }
    }
}
