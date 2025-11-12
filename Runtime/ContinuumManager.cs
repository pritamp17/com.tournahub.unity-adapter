/*
 * /unity-adapter/Assets/TournaHub/Runtime/ContinuumManager.cs
 * 
 * This is the project-wide singleton. It must exist once in the
 * first scene of the game.
 * 
 * Its job is to:
 * 1. Initialize the Srujan Core (Layer 1) on game start.
 * 2. Load the project-wide "world_lore.vectordb" (RAG guardrail).
 */

using UnityEngine;
using System.IO;

namespace TournaHub.Runtime
{
   
    public class ContinuumManager : MonoBehaviour
    {
        public static ContinuumManager Instance { get; private set; }

       
        public ContinuumProjectConfig projectConfig;

       
        public string worldLoreDatabaseFile = "world_lore.vectordb";

        void Awake()
        {
            // --- Standard Singleton Pattern ---
            if (Instance!= null && Instance!= this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this.gameObject);

            // --- Initialize Srujan Core (Layer 1) ---
            // We pass the persistentDataPath as the config/log directory.
            // This is the only safe, writable location on all platforms.
            string configPath = Application.persistentDataPath;
            UnityBridge.TournaHub_Initialize(configPath);
        }

        void Start()
        {
            if (projectConfig == null)
            {
                Debug.LogError("ContinuumManager: 'Project Config' is not set! " +
                               "Please create one via the Welcome Window.");
                return;
            }

            // --- Load World Context (RAG Guardrail) ---
            // The 'world_lore.vectordb' is a read-only asset.
            // Read-only assets must be placed in /Assets/StreamingAssets/.
            // We pass the direct path to the C++ core.
            // Note: This path is read-only at runtime.
            string loreDbPath = Path.Combine(Application.streamingAssetsPath, 
                                             worldLoreDatabaseFile);

            // On Android/WebGL, this path is a URL. The C++ core
            // must be able to handle this.
            // A production-ready manager would copy the DB from StreamingAssets
            // (a URL on Android) to persistentDataPath (a file) on first launch.
            // For simplicity, we pass the direct path.
            if (Application.platform == RuntimePlatform.Android)
            {
                Debug.LogWarning("ContinuumManager: On Android, the DB is in a " + 
                                 "compressed.apk. A real implementation would " +
                                 "use UnityWebRequest to copy it to persistentDataPath " +
                                 "before calling LoadWorldContext.");
                // Example of the *real* path on Android:
                // string realPath = Path.Combine(Application.persistentDataPath, worldLoreDatabaseFile);
                // if (!File.Exists(realPath)) {
                //    StartCoroutine(CopyDbToPersistentPath(loreDbPath, realPath));
                // } else {
                //    UnityBridge.TournaHub_LoadWorldContext(realPath);
                // }
            }
            else
            {
                if (File.Exists(loreDbPath))
                {
                    UnityBridge.TournaHub_LoadWorldContext(loreDbPath);
                    Debug.Log("ContinuumManager: World Context loaded.");
                }
                else
                {
                    Debug.LogError($"ContinuumManager: Could not find " + 
                                   $"{worldLoreDatabaseFile} at {loreDbPath}. " +
                                   "Was it 'baked' by the ModelBakerService?");
                }
            }
        }
        
        // Example coroutine for Android/WebGL file handling
        // IEnumerator CopyDbToPersistentPath(string fromPath, string toPath) {... }
    }
}