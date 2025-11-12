/*
 * /unity-adapter/Assets/TournaHub/Editor/ContinuumWelcomeWindow.cs
 * 
 * This file creates the "smart on-boarding" pop-up window.
 * It uses [InitializeOnLoad] to check if this is the first time
 * the SDK has been imported, and if so, shows this window.
 */

using UnityEngine;
using UnityEditor;
using System.IO;
using TournaHub.Runtime;

namespace TournaHub.Editor
{
    // This static class runs on editor launch
    [InitializeOnLoad]
    public class WelcomeScreenActivator
    {
        private const string WelcomeFlagKey = "TournaHub_WelcomeScreenShown";

        static WelcomeScreenActivator()
        {
            // Check EditorPrefs to see if we've shown this window *ever*
            if (!EditorPrefs.GetBool(WelcomeFlagKey, false))
            {
                // Use delayCall to wait for the editor to be fully ready
                EditorApplication.delayCall += ShowWelcomeWindow;
            }
        }

        private static void ShowWelcomeWindow()
        {
            ContinuumWelcomeWindow.ShowWindow();
            // Set the flag so we never show this again
            EditorPrefs.SetBool(WelcomeFlagKey, true);
        }
    }

    // This is the EditorWindow GUI itself
    public class ContinuumWelcomeWindow : EditorWindow
    {
        private ContinuumProjectConfig _config;
        private const string ConfigName = "ContinuumProjectConfig.asset";

        [MenuItem("TournaHub/Welcome & Setup")]
        public static void ShowWindow()
        {
            // Show the window, non-modal
            GetWindow<ContinuumWelcomeWindow>(true, "Welcome to Continuum AI");
        }

        void OnEnable()
        {
            // Find or create the main project config asset 
            FindOrCreateProjectConfig();
        }

        private void FindOrCreateProjectConfig()
        {
            // Ensure the /Data/ directory exists
            string dataPath = "Assets/TournaHub/Data";
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            string configAssetPath = $"{dataPath}/{ConfigName}";
            _config = AssetDatabase.LoadAssetAtPath<ContinuumProjectConfig>(configAssetPath);

            if (_config == null)
            {
                // Create a new config asset
                _config = CreateInstance<ContinuumProjectConfig>();
                AssetDatabase.CreateAsset(_config, configAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Continuum: Created new Project Config asset.");
            }
        }

        void OnGUI()
        {
            if (_config == null)
            {
                EditorGUILayout.HelpBox("Could not find or create Project Config.", 
                                        MessageType.Error);
                return;
            }
            
            // Mark the object as "dirty" so changes are saved
            Undo.RecordObject(_config, "Modify Continuum Config");

            // --- Window GUI ---
            EditorGUILayout.LabelField("Welcome to the Continuum AI SDK!", 
                                       EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This is your one-time setup. Please configure " + 
                                    "your project's core settings below.", 
                                    MessageType.Info);

            EditorGUILayout.Space();

            // 1. Ask for Target Platform [1, 3]
            _config.platform = (ContinuumProjectConfig.TargetPlatform)EditorGUILayout.EnumPopup(
                "Target Platform", _config.platform);
            
            EditorGUILayout.HelpBox(
                "This choice controls which AI models the 'Model Baker' will use. " +
                "'Mobile' uses smaller, faster models (like Phi-3). " +
                "'PC/Console' uses larger, higher-quality models (like Llama 3).", 
                MessageType.None);

            EditorGUILayout.Space();

            // 2. Ask for World Lore 
            EditorGUILayout.LabelField("Project-Wide World Lore (RAG Guardrail)", 
                                       EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Enter all static lore, story, and rules for your " + 
                                    "game here. This will be 'baked' into a guardrail " +
                                    "to keep all NPCs factually consistent.", 
                                    MessageType.None);
            
            _config.worldLore = EditorGUILayout.TextArea(_config.worldLore, 
                                                         GUILayout.Height(200));

            EditorGUILayout.Space();

            // 3. Bake World Lore Button
            if (GUILayout.Button("Bake World Lore to Database", 
                                 GUILayout.Height(40)))
            {
                // Call the Layer 3 ModelBakerService 
                ModelBakerService.BakeWorldLore(_config);
            }

            // Apply changes to the ScriptableObject
            if (GUI.changed)
            {
                EditorUtility.SetDirty(_config);
            }
        }
    }
}