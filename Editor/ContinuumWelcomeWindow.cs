/*
 * /unity-adapter/Assets/TournaHub/Editor/ContinuumWelcomeWindow.cs
 * 
 * This file creates the "smart on-boarding" pop-up window with:
 * - Modern Discord-themed UI
 * - Model selection from ModelRegistry.json
 * - Download progress tracking
 * - World lore baking workflow
 */

using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TournaHub.Runtime;

namespace TournaHub.Editor
{
    // Discord-themed color palette
    public static class ContinuumColors
    {
        public static Color DiscordBlue = new Color(88f/255f, 101f/255f, 242f/255f);
        public static Color DarkBackground = new Color(0.15f, 0.15f, 0.15f);
        public static Color CardBackground = new Color(0.2f, 0.2f, 0.2f);
        public static Color LightText = new Color(0.9f, 0.9f, 0.9f);
        public static Color ErrorRed = new Color(0.9f, 0.2f, 0.2f);
        public static Color SuccessGreen = new Color(0.2f, 0.8f, 0.3f);
    }

    // Window state machine
    public enum WelcomeWindowState
    {
        Configuration,    // User selecting platform/model/world lore
        AlreadyConfigured, // Model already setup, allow lore update only
        Downloading,      // Download in progress
        Extracting,       // Extracting ZIP file
        Baking,          // Baking world lore embeddings
        Complete,        // All operations complete
        Error            // Error occurred
    }

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

        // State management
        private WelcomeWindowState _currentState = WelcomeWindowState.Configuration;
        private float _downloadProgress = 0f;
        private long _bytesDownloaded = 0;
        private long _totalBytes = 0;
        private string _statusMessage = "";
        private string _errorMessage = "";

        // Model registry data
        private ModelRegistry _modelRegistry;
        private int _selectedModelIndex = 0;
        private List<ModelInfo> _availableModels = new List<ModelInfo>();
        private string _selectedModelGroup = ""; // Track which group the model is from (for display)

        // Download coroutine tracking
        private IEnumerator _downloadCoroutine;
        private bool _cancelRequested = false;

        // Custom styles
        private GUIStyle _headerStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _secondaryButtonStyle;
        private GUIStyle _centeredLabelStyle;
        private bool _stylesInitialized = false;

        private Vector2 _scrollPosition;
        
        // Spinner animation
        private float _spinnerRotation = 0f;
        private Texture2D _spinnerTexture;

        [MenuItem("TournaHub/Welcome & Setup")]
        public static void ShowWindow()
        {
            // Show the window with fixed size
            ContinuumWelcomeWindow window = GetWindow<ContinuumWelcomeWindow>(true, "Welcome to Continuum AI", true);
            window.minSize = new Vector2(600, 700);
            window.maxSize = new Vector2(600, 700);
        }

        void OnEnable()
        {
            // Find or create the main project config asset 
            FindOrCreateProjectConfig();
            
            // Load model registry
            LoadModelRegistry();
            
            // Create spinner texture
            CreateSpinnerTexture();
            
            // Check if already configured
            CheckIfAlreadyConfigured();

            // Subscribe to editor update for coroutine execution
            EditorApplication.update += UpdateCoroutine;
        }

        void OnDisable()
        {
            // Unsubscribe from editor update
            EditorApplication.update -= UpdateCoroutine;
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

        private void LoadModelRegistry()
        {
            try
            {
                // Load ModelRegistry.json from Resources
                TextAsset registryAsset = Resources.Load<TextAsset>("ModelRegistry");
                if (registryAsset != null)
                {
                    _modelRegistry = JsonUtility.FromJson<ModelRegistry>(registryAsset.text);
                    UpdateAvailableModels();
                    Debug.Log($"Loaded ModelRegistry.json, SDK version: {_modelRegistry.sdk_version}");
                }
                else
                {
                    Debug.LogError("ModelRegistry.json not found in Resources folder!");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load ModelRegistry.json: {e.Message}");
            }
        }

        private void CheckIfAlreadyConfigured()
        {
            // Check if a model has already been downloaded and configured
            bool hasModel = !string.IsNullOrEmpty(_config.selectedModelName) && 
                           !string.IsNullOrEmpty(_config.selectedModelUrl);
            
            // Check if world lore database exists
            string dbPath = Path.Combine(Application.streamingAssetsPath, "world_lore.vectordb");
            bool hasWorldLoreDB = File.Exists(dbPath);
            
            if (hasModel && hasWorldLoreDB)
            {
                Debug.Log($"[WelcomeWindow] Already configured with model: {_config.selectedModelName}");
                _currentState = WelcomeWindowState.AlreadyConfigured;
            }
            else
            {
                _currentState = WelcomeWindowState.Configuration;
            }
        }
        
        private void UpdateAvailableModels()
        {
            _availableModels.Clear();

            if (_modelRegistry == null || _modelRegistry.models == null)
                return;

            // Get models based on selected platform
            switch (_config.platform)
            {
                case ContinuumProjectConfig.TargetPlatform.Mobile:
                    if (_modelRegistry.models.mobile_onnx != null)
                    {
                        if (_modelRegistry.models.mobile_onnx.primary != null)
                            _availableModels.Add(_modelRegistry.models.mobile_onnx.primary);
                        if (_modelRegistry.models.mobile_onnx.lite != null)
                            _availableModels.Add(_modelRegistry.models.mobile_onnx.lite);
                    }
                    break;

                case ContinuumProjectConfig.TargetPlatform.PC_CONSOLE:
                case ContinuumProjectConfig.TargetPlatform.PC:
                case ContinuumProjectConfig.TargetPlatform.CONSOLE:
                    // For Unity/Unreal: only ONNX models
                    if (_modelRegistry.models.pc_console_onnx != null)
                    {
                        if (_modelRegistry.models.pc_console_onnx.primary != null)
                            _availableModels.Add(_modelRegistry.models.pc_console_onnx.primary);
                    }
                    
                    // For other frameworks: GGUF models
                    if (_modelRegistry.models.pc_standalone_gguf != null)
                    {
                        if (_modelRegistry.models.pc_standalone_gguf.high != null)
                            _availableModels.Add(_modelRegistry.models.pc_standalone_gguf.high);
                        if (_modelRegistry.models.pc_standalone_gguf.medium != null)
                            _availableModels.Add(_modelRegistry.models.pc_standalone_gguf.medium);
                        if (_modelRegistry.models.pc_standalone_gguf.low != null)
                            _availableModels.Add(_modelRegistry.models.pc_standalone_gguf.low);
                    }
                    break;
            }

            // Reset selection if out of bounds
            if (_selectedModelIndex >= _availableModels.Count)
            {
                _selectedModelIndex = 0;
            }
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized)
                return;

            // Header style (Discord blue background)
            _headerStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, ContinuumColors.DiscordBlue) },
                padding = new RectOffset(15, 15, 15, 15),
                margin = new RectOffset(0, 0, 0, 10)
            };

            // Card style (dark background)
            _cardStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, ContinuumColors.CardBackground) },
                padding = new RectOffset(15, 15, 15, 15),
                margin = new RectOffset(0, 0, 5, 5)
            };

            // Title style
            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };

            // Primary button (Discord blue)
            _primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = MakeTex(2, 2, ContinuumColors.DiscordBlue), textColor = Color.white },
                hover = { background = MakeTex(2, 2, ContinuumColors.DiscordBlue * 1.2f), textColor = Color.white },
                active = { background = MakeTex(2, 2, ContinuumColors.DiscordBlue * 0.8f), textColor = Color.white },
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 10, 10)
            };

            // Secondary button
            _secondaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                padding = new RectOffset(10, 10, 8, 8)
            };

            // Centered label
            _centeredLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
        
        private void CreateSpinnerTexture()
        {
            // Create a custom spinner texture based on your website design
            int size = 64;
            _spinnerTexture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];
            
            // Create a triangular spinner shape
            Vector2 center = new Vector2(size / 2f, size / 2f);
            Color spinnerColor = ContinuumColors.DiscordBlue;
            Color transparent = new Color(0, 0, 0, 0);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float dist = Vector2.Distance(pos, center);
                    
                    // Create a triangular pattern
                    float angle = Mathf.Atan2(y - center.y, x - center.x);
                    float normalizedAngle = (angle + Mathf.PI) / (2 * Mathf.PI);
                    
                    // Create triangle segments
                    bool inTriangle = (normalizedAngle < 0.33f || normalizedAngle > 0.66f) && dist < size / 2.5f && dist > size / 4f;
                    
                    if (inTriangle)
                    {
                        float alpha = 1f - (dist / (size / 2.5f));
                        pixels[y * size + x] = new Color(spinnerColor.r, spinnerColor.g, spinnerColor.b, alpha);
                    }
                    else
                    {
                        pixels[y * size + x] = transparent;
                    }
                }
            }
            
            _spinnerTexture.SetPixels(pixels);
            _spinnerTexture.Apply();
        }

        void OnGUI()
        {
            InitializeStyles();

            if (_config == null)
            {
                EditorGUILayout.HelpBox("Could not find or create Project Config.", 
                                        MessageType.Error);
                return;
            }

            // Scroll view for entire window
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Header section
            DrawHeader();

            GUILayout.Space(10);

            // State-based rendering
            switch (_currentState)
            {
                case WelcomeWindowState.Configuration:
                    DrawConfigurationUI();
                    break;
                    
                case WelcomeWindowState.AlreadyConfigured:
                    DrawAlreadyConfiguredUI();
                    break;

                case WelcomeWindowState.Downloading:
                case WelcomeWindowState.Extracting:
                case WelcomeWindowState.Baking:
                    DrawProgressUI();
                    break;

                case WelcomeWindowState.Error:
                    DrawErrorUI();
                    break;

                case WelcomeWindowState.Complete:
                    DrawCompleteUI();
                    break;
            }

            EditorGUILayout.EndScrollView();

            // Apply changes to the ScriptableObject
            if (GUI.changed)
            {
                EditorUtility.SetDirty(_config);
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(_headerStyle);
            GUILayout.Label("Welcome to Continuum AI", _titleStyle);
            GUILayout.Label("Intelligent NPC Creation for Unity", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawConfigurationUI()
        {
            // Platform Selection Card
            EditorGUILayout.BeginVertical(_cardStyle);
            GUILayout.Label("1. Select Target Platform", EditorStyles.boldLabel);
            GUILayout.Space(5);

            ContinuumProjectConfig.TargetPlatform previousPlatform = _config.platform;
            _config.platform = (ContinuumProjectConfig.TargetPlatform)EditorGUILayout.EnumPopup(
                "Target Platform", _config.platform);

            // Update available models if platform changed
            if (previousPlatform != _config.platform)
            {
                UpdateAvailableModels();
            }

            EditorGUILayout.HelpBox(
                "This choice controls which AI models will be available. " +
                "'Mobile' uses smaller, faster models. " +
                "'PC/Console' uses larger, higher-quality models.", 
                MessageType.Info);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Model Selection Card
            if (_availableModels.Count > 0)
            {
                EditorGUILayout.BeginVertical(_cardStyle);
                GUILayout.Label("2. Select AI Model", EditorStyles.boldLabel);
                GUILayout.Space(5);

                // Display model selection
                string[] modelNames = new string[_availableModels.Count];
                for (int i = 0; i < _availableModels.Count; i++)
                {
                    ModelInfo model = _availableModels[i];
                    modelNames[i] = $"{model.name} (v{model.version})";
                }

                _selectedModelIndex = EditorGUILayout.Popup("Model", _selectedModelIndex, modelNames);

                // Show model info
                if (_selectedModelIndex < _availableModels.Count)
                {
                    ModelInfo selectedModel = _availableModels[_selectedModelIndex];
                    
                    string fileType = selectedModel.GetFileExtension().ToUpper();
                    string sizeInfo = selectedModel.filesize_gb > 0 
                        ? $"\nSize: ~{selectedModel.filesize_gb:F2} GB" 
                        : "";
                    string formatInfo = selectedModel.IsDirectFile() 
                        ? $"\nFormat: Direct {fileType} file" 
                        : $"\nFormat: ZIP archive";
                    
                    EditorGUILayout.HelpBox(
                        $"Selected: {selectedModel.name}\n" +
                        $"Version: {selectedModel.version ?? "1.0"}" +
                        sizeInfo +
                        formatInfo +
                        $"\nThis model will be downloaded from our CDN.",
                        MessageType.None);
                }

                EditorGUILayout.EndVertical();

                GUILayout.Space(10);
            }

            // World Lore Card
            EditorGUILayout.BeginVertical(_cardStyle);
            GUILayout.Label("3. Define World Lore (RAG Guardrail)", EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Enter all static lore, story, and rules for your game here. " +
                "This will be 'baked' into a vector database to keep all NPCs factually consistent.", 
                MessageType.Info);

            _config.worldLore = EditorGUILayout.TextArea(_config.worldLore, 
                                                         GUILayout.Height(150));

            // Character count
            GUILayout.Label($"Characters: {(_config.worldLore != null ? _config.worldLore.Length : 0)}", 
                          EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // Action Button
            GUI.enabled = _availableModels.Count > 0 && !string.IsNullOrWhiteSpace(_config.worldLore);
            
            if (GUILayout.Button("Confirm & Download Model", _primaryButtonStyle, GUILayout.Height(45)))
            {
                OnConfirmAndDownload();
            }

            GUI.enabled = true;

            if (_availableModels.Count == 0)
            {
                GUILayout.Space(5);
                EditorGUILayout.HelpBox("No models available for selected platform. Check ModelRegistry.json.", 
                                      MessageType.Warning);
            }
        }

        private void DrawProgressUI()
        {
            EditorGUILayout.BeginVertical(_cardStyle);

            GUILayout.Label("Processing...", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Progress bar
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = ContinuumColors.DiscordBlue;
            
            Rect progressRect = GUILayoutUtility.GetRect(18, 30);
            EditorGUI.ProgressBar(progressRect, _downloadProgress, _statusMessage);
            
            GUI.backgroundColor = originalColor;

            GUILayout.Space(10);

            // Percentage display (instead of bytes)
            if (_currentState == WelcomeWindowState.Downloading)
            {
                int percentage = Mathf.RoundToInt(_downloadProgress * 100);
                string percentText = $"{percentage}% / 100%";
                
                GUIStyle percentStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    normal = { textColor = ContinuumColors.DiscordBlue }
                };
                
                GUILayout.Label(percentText, percentStyle);
                GUILayout.Space(15);
            }
            
            // Animated spinner at bottom
            DrawSpinner();
            
            GUILayout.Space(15);

            // Cancel button (only during download)
            if (_currentState == WelcomeWindowState.Downloading)
            {
                if (GUILayout.Button("Cancel", _secondaryButtonStyle, GUILayout.Height(30)))
                {
                    CancelDownload();
                }
            }

            EditorGUILayout.EndVertical();
        }
        
        private void DrawSpinner()
        {
            if (_spinnerTexture == null)
                return;
                
            // Update rotation
            _spinnerRotation += 2f; // Rotation speed
            if (_spinnerRotation >= 360f)
                _spinnerRotation -= 360f;
            
            // Calculate spinner rect (centered, small size)
            float spinnerSize = 48f;
            Rect spinnerRect = GUILayoutUtility.GetRect(spinnerSize, spinnerSize);
            spinnerRect.x = (spinnerRect.width - spinnerSize) / 2f + spinnerRect.x;
            spinnerRect.width = spinnerSize;
            spinnerRect.height = spinnerSize;
            
            // Draw rotated spinner
            Matrix4x4 matrixBackup = GUI.matrix;
            GUIUtility.RotateAroundPivot(_spinnerRotation, spinnerRect.center);
            
            GUI.DrawTexture(spinnerRect, _spinnerTexture);
            
            GUI.matrix = matrixBackup;
            
            // Force repaint for smooth animation
            Repaint();
        }

        private void DrawErrorUI()
        {
            EditorGUILayout.BeginVertical(_cardStyle);

            EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);

            GUILayout.Space(15);

            if (GUILayout.Button("Retry", _primaryButtonStyle, GUILayout.Height(35)))
            {
                ResetToConfiguration();
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Back to Configuration", _secondaryButtonStyle, GUILayout.Height(30)))
            {
                ResetToConfiguration();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAlreadyConfiguredUI()
        {
            // Warning card
            EditorGUILayout.BeginVertical(_cardStyle);
            
            EditorGUILayout.HelpBox(
                "⚠ You have already configured a model for this project!",
                MessageType.Warning);
            
            GUILayout.Space(10);
            
            // Current configuration summary
            GUILayout.Label("Current Configuration:", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            EditorGUILayout.LabelField("Platform:", _config.platform.ToString());
            EditorGUILayout.LabelField("Model:", _config.selectedModelName ?? "Unknown");
            EditorGUILayout.LabelField("World Lore:", $"{(_config.worldLore != null ? _config.worldLore.Length : 0)} characters");
            
            GUILayout.Space(15);
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // World Lore Update Card
            EditorGUILayout.BeginVertical(_cardStyle);
            
            GUILayout.Label("Update World Lore", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            EditorGUILayout.HelpBox(
                "You can update your game's world lore and re-bake the guardrail database. " +
                "This will not re-download the model.",
                MessageType.Info);
            
            _config.worldLore = EditorGUILayout.TextArea(_config.worldLore, 
                                                         GUILayout.Height(150));
            
            // Character count
            GUILayout.Label($"Characters: {(_config.worldLore != null ? _config.worldLore.Length : 0)}", 
                          EditorStyles.miniLabel);
            
            GUILayout.Space(15);
            
            // Update World Lore button
            GUI.enabled = !string.IsNullOrWhiteSpace(_config.worldLore);
            
            if (GUILayout.Button("Update & Re-bake World Lore", _primaryButtonStyle, GUILayout.Height(45)))
            {
                OnUpdateWorldLore();
            }
            
            GUI.enabled = true;
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // Actions card
            EditorGUILayout.BeginVertical(_cardStyle);
            
            GUILayout.Label("Other Actions", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            if (GUILayout.Button("Open NPC Creator", _secondaryButtonStyle, GUILayout.Height(35)))
            {
                NPCCreatorWindow.ShowWindow();
                Close();
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("Reconfigure (Download New Model)", _secondaryButtonStyle, GUILayout.Height(35)))
            {
                if (EditorUtility.DisplayDialog(
                    "Reconfigure Model?",
                    "This will allow you to download a different model. Your current configuration will be overwritten.\n\nAre you sure?",
                    "Yes, Reconfigure",
                    "Cancel"))
                {
                    _currentState = WelcomeWindowState.Configuration;
                    UpdateAvailableModels();
                }
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("Close", _secondaryButtonStyle, GUILayout.Height(30)))
            {
                Close();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void OnUpdateWorldLore()
        {
            if (string.IsNullOrWhiteSpace(_config.worldLore))
            {
                EditorUtility.DisplayDialog("Error", "World lore is empty. Please enter some world lore.", "OK");
                return;
            }
            
            // Save the updated world lore
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            
            // Start baking process (skip download and extraction)
            _currentState = WelcomeWindowState.Baking;
            _statusMessage = "Baking world lore embeddings...";
            _downloadProgress = 0f;
            
            _downloadCoroutine = BakeWorldLoreOnly();
        }
        
        private IEnumerator BakeWorldLoreOnly()
        {
            // Just bake world lore without downloading
            bool bakeSuccess = ModelBakerService.BakeWorldLoreWithProgress(
                _config.worldLore,
                (progress) =>
                {
                    _downloadProgress = progress;
                    Repaint();
                }
            );

            if (!bakeSuccess)
            {
                HandleError("Failed to bake world lore. Check console for details.");
                yield break;
            }

            // Complete!
            _currentState = WelcomeWindowState.Complete;
            _downloadProgress = 1f;
            Repaint();

            Debug.Log("World lore updated and re-baked successfully!");
            
            // After a short delay, go back to already configured state
            yield return null;
            yield return null;
            yield return null;
            
            // Show success message then return to already configured
            EditorUtility.DisplayDialog(
                "Success!",
                "World lore has been updated and re-baked successfully!",
                "OK");
            
            _currentState = WelcomeWindowState.AlreadyConfigured;
        }
        
        private void DrawCompleteUI()
        {
            EditorGUILayout.BeginVertical(_cardStyle);

            EditorGUILayout.HelpBox(
                "✓ Model downloaded and world lore baked successfully!", 
                MessageType.Info);

            GUILayout.Space(15);

            GUILayout.Label("Configuration Summary:", EditorStyles.boldLabel);
            GUILayout.Label($"Platform: {_config.platform}", EditorStyles.label);
            GUILayout.Label($"Model: {_config.selectedModelName}", EditorStyles.label);
            GUILayout.Label($"World Lore: {(_config.worldLore != null ? _config.worldLore.Length : 0)} characters", EditorStyles.label);

            GUILayout.Space(20);

            if (GUILayout.Button("Open NPC Creator", _primaryButtonStyle, GUILayout.Height(40)))
            {
                NPCCreatorWindow.ShowWindow();
                Close();
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Configure Another Platform", _secondaryButtonStyle, GUILayout.Height(30)))
            {
                ResetToConfiguration();
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Close", _secondaryButtonStyle, GUILayout.Height(30)))
            {
                Close();
            }

            EditorGUILayout.EndVertical();
        }

        private void OnConfirmAndDownload()
        {
            // Validate selections
            if (_availableModels.Count == 0 || _selectedModelIndex >= _availableModels.Count)
            {
                EditorUtility.DisplayDialog("Error", "No model selected. Please select a model.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(_config.worldLore))
            {
                EditorUtility.DisplayDialog("Error", "World lore is empty. Please enter some world lore.", "OK");
                return;
            }

            // Get selected model info
            ModelInfo selectedModel = _availableModels[_selectedModelIndex];
            Debug.Log($"[WelcomeWindow] Selected model: {selectedModel.name}");
            Debug.Log($"[WelcomeWindow] Model URL: {selectedModel.url}");
            Debug.Log($"[WelcomeWindow] Model version: {selectedModel.version}");

            // Validate disk space
            if (!ModelDownloadService.ValidateDiskSpace())
            {
                EditorUtility.DisplayDialog("Insufficient Disk Space", 
                    "Not enough disk space to download and extract the model. Please free up space and try again.", 
                    "OK");
                return;
            }

            // Save model selection to config
            _config.selectedModelName = selectedModel.name;
            _config.selectedModelUrl = selectedModel.url;
            _config.selectedModelVersion = selectedModel.version;
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();

            // Start download process
            _currentState = WelcomeWindowState.Downloading;
            _statusMessage = $"Downloading {selectedModel.name}...";
            _downloadProgress = 0f;
            _cancelRequested = false;

            _downloadCoroutine = DownloadAndBakeWorkflow(selectedModel);
        }

        private IEnumerator DownloadAndBakeWorkflow(ModelInfo modelInfo)
        {
            // Determine if this is a direct file or ZIP
            bool isDirectFile = modelInfo.IsDirectFile();
            string fileExtension = modelInfo.GetFileExtension();
            
            Debug.Log($"[WelcomeWindow] Download workflow started. Is direct file: {isDirectFile}, Extension: {fileExtension}");
            
            // Step 1: Download model
            string tempPath = Path.Combine(Application.temporaryCachePath, 
                isDirectFile ? $"model_download.{fileExtension}" : "model_download.zip");
            bool downloadSuccess = false;
            string downloadError = null;

            IEnumerator downloadCoroutine = ModelDownloadService.DownloadModel(
                modelInfo.url,
                tempPath,
                (progress, downloaded, total) =>
                {
                    _downloadProgress = progress;
                    _bytesDownloaded = downloaded;
                    _totalBytes = total;
                    Repaint(); // Force UI update
                },
                (success, error) =>
                {
                    downloadSuccess = success;
                    downloadError = error;
                }
            );

            // Execute download coroutine
            while (downloadCoroutine.MoveNext())
            {
                if (_cancelRequested)
                {
                    ModelDownloadService.CleanupTempFiles(tempPath);
                    ResetToConfiguration();
                    yield break;
                }
                yield return null;
            }

            // Check download result
            if (!downloadSuccess)
            {
                HandleError(downloadError ?? "Unknown download error");
                ModelDownloadService.CleanupTempFiles(tempPath);
                yield break;
            }

            // Step 2: Extract or move model based on file type
            string finalPath = ModelDownloadService.GetModelStoragePath(_config.platform.ToString());
            
            if (isDirectFile)
            {
                // Direct file (e.g., .gguf) - just move it to final location
                _currentState = WelcomeWindowState.Extracting;
                _statusMessage = "Moving model file to storage...";
                _downloadProgress = 0.5f;
                Repaint();
                
                Debug.Log($"[WelcomeWindow] Direct file download, moving to: {finalPath}");
                
                try
                {
                    // Ensure directory exists
                    if (!Directory.Exists(finalPath))
                    {
                        Directory.CreateDirectory(finalPath);
                    }
                    
                    // Get filename from URL
                    string fileName = Path.GetFileName(modelInfo.url);
                    string destinationPath = Path.Combine(finalPath, fileName);
                    
                    // Move file
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath); // Replace existing
                    }
                    File.Move(tempPath, destinationPath);
                    
                    Debug.Log($"[WelcomeWindow] Model file moved to: {destinationPath}");
                    _downloadProgress = 1.0f;
                    Repaint();
                }
                catch (Exception e)
                {
                    HandleError($"Failed to move model file: {e.Message}");
                    ModelDownloadService.CleanupTempFiles(tempPath);
                    yield break;
                }
            }
            else
            {
                // ZIP file - extract it
                _currentState = WelcomeWindowState.Extracting;
                _statusMessage = "Extracting model files...";
                _downloadProgress = 0f;
                Repaint();
                
                Debug.Log($"[WelcomeWindow] ZIP file, extracting to: {finalPath}");

                string extractError;
                bool extractSuccess = ModelDownloadService.ExtractZip(tempPath, finalPath, out extractError);

                if (!extractSuccess)
                {
                    HandleError(extractError ?? "Failed to extract model files");
                    ModelDownloadService.CleanupTempFiles(tempPath);
                    yield break;
                }

                // Clean up temp ZIP file
                ModelDownloadService.CleanupTempFiles(tempPath);
            }

            // Step 3: Bake world lore
            _currentState = WelcomeWindowState.Baking;
            _statusMessage = "Baking world lore embeddings...";
            _downloadProgress = 0f;
            Repaint();

            bool bakeSuccess = ModelBakerService.BakeWorldLoreWithProgress(
                _config.worldLore,
                (progress) =>
                {
                    _downloadProgress = progress;
                    Repaint();
                }
            );

            if (!bakeSuccess)
            {
                HandleError("Failed to bake world lore. Check console for details.");
                yield break;
            }

            // Step 4: Complete!
            _currentState = WelcomeWindowState.Complete;
            _downloadProgress = 1f;
            Repaint();

            Debug.Log("Continuum setup complete!");
        }

        private void UpdateCoroutine()
        {
            // Execute one step of the coroutine per editor update
            if (_downloadCoroutine != null)
            {
                try
                {
                    if (!_downloadCoroutine.MoveNext())
                    {
                        _downloadCoroutine = null;
                    }
                    else
                    {
                        // Force repaint during active operations for smooth progress updates
                        Repaint();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in download coroutine: {e.Message}\n{e.StackTrace}");
                    HandleError($"Unexpected error: {e.Message}");
                    _downloadCoroutine = null;
                }
            }
        }

        private void CancelDownload()
        {
            _cancelRequested = true;
            _downloadCoroutine = null;
            Debug.Log("Download cancelled by user");
        }

        private void HandleError(string error)
        {
            _currentState = WelcomeWindowState.Error;

            // Parse error and provide user-friendly messages
            if (error.Contains("network") || error.Contains("connection") || error.Contains("Network"))
            {
                _errorMessage = "Network error: Could not connect to download server. Please check your internet connection and try again.";
            }
            else if (error.Contains("disk space") || error.Contains("Disk"))
            {
                _errorMessage = "Insufficient disk space. Please free up space and try again.";
            }
            else if (error.Contains("timeout"))
            {
                _errorMessage = "Download timed out. Please try again.";
            }
            else if (error.Contains("zip") || error.Contains("extract") || error.Contains("ZIP"))
            {
                _errorMessage = "Failed to extract model files. The download may be corrupted. Please try again.";
            }
            else
            {
                _errorMessage = $"An error occurred: {error}";
            }

            Debug.LogError($"Continuum setup failed: {error}");
            Repaint();
        }

        private void ResetToConfiguration()
        {
            _currentState = WelcomeWindowState.Configuration;
            _downloadProgress = 0f;
            _bytesDownloaded = 0;
            _totalBytes = 0;
            _statusMessage = "";
            _errorMessage = "";
            _downloadCoroutine = null;
            _cancelRequested = false;
            Repaint();
        }
    }
}
