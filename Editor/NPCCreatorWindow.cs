/*
 * /unity-adapter/Assets/TournaHub/Editor/NPCCreatorWindow.cs
 * 
 * This is the Layer 3 (Editor-only) GUI for creating new NPCs.
 * It reads the project config to make smart suggestions and
 * calls the ModelBakerService to do the actual work.
 */

using UnityEngine;
using UnityEditor;
using TournaHub.Runtime;

namespace TournaHub.Editor
{
    public class NPCCreatorWindow : EditorWindow
    {
        private ContinuumProjectConfig _config;
        private string _npcName = "NewNPC";
        private string _npcPersonality = "A brave guard who is suspicious of strangers...";
        
        private string _modelLabel = "Model (Phi-3-mini)"; // Dynamically set

        [MenuItem("TournaHub/Create NPC")]
        public static void ShowWindow()
        {
            GetWindow<NPCCreatorWindow>(false, "NPC Creator");
        }

        void OnEnable()
        {
            // Load the project config
            string configPath = "Assets/TournaHub/Data/ContinuumProjectConfig.asset";
            _config = AssetDatabase.LoadAssetAtPath<ContinuumProjectConfig>(configPath);

            if (_config!= null)
            {
                // Read the config to intelligently update the UI 
                UpdateModelLabel();
            }
        }

        private void UpdateModelLabel()
        {
            if (_config == null) return;
            
            // This is the "platform-aware" logic [1, 3]
            switch (_config.platform)
            {
                case ContinuumProjectConfig.TargetPlatform.Mobile:
                    _modelLabel = "Base Model: Phi-3-mini (ONNX)";
                    break;
                case ContinuumProjectConfig.TargetPlatform.PC_CONSOLE:
                    _modelLabel = "Base Model: Llama-3-8B (ONNX)";
                    break;
            }
        }

        void OnGUI()
        {
            if (_config == null)
            {
                EditorGUILayout.HelpBox("Could not find ContinuumProjectConfig. " +
                               "Please run 'TournaHub/Welcome & Setup' first.", 
                               MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Create New NPC", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // --- Show Read-Only Project State ---
            EditorGUILayout.LabelField("Project Platform", _config.platform.ToString());
            EditorGUILayout.LabelField(_modelLabel);

            EditorGUILayout.Space();

            // --- Get NPC-Specific Info ---
            _npcName = EditorGUILayout.TextField("NPC Name", _npcName);
            
            EditorGUILayout.LabelField("NPC Personality & Backstory");
            _npcPersonality = EditorGUILayout.TextArea(_npcPersonality, 
                                                       GUILayout.Height(150));
            
            EditorGUILayout.Space();

            // --- Bake Button ---
            if (GUILayout.Button($"Bake '{_npcName}' Model", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("Confirm Bake",
                    "This will call the ModelBakerService to create a new " +
                    ".onnx model file. (Currently stubbed)\n\n" +
                    $"Model will be saved to StreamingAssets/{_npcName}.onnx", 
                    "Bake", "Cancel"))
                {
                    // Call the Layer 3 service 
                    ModelBakerService.BakeNPCModel(_config, _npcName, _npcPersonality);
                }
            }
        }
    }
}