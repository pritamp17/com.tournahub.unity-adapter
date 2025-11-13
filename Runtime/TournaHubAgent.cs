/*
 * /unity-adapter/Assets/TournaHub/Runtime/TournaHubAgent.cs
 * 
 * This is the primary, developer-facing MonoBehaviour.
 * Developers drag this onto their NPC GameObject.
 * 
 * It provides the "friendly" C# API (Say, ObserveEvent) 
 * and manages the C++ handle for its specific NPC instance.
 */

using UnityEngine;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TournaHub.Runtime
{
   
    public class TournaHubAgent : MonoBehaviour
    {
       
       
        public string modelFileName;

       
        public string memoryFileName;

        // The opaque handle (e.g., 123) returned by the C++ core [2]
        private int _npcId = -1;

        // Reusable buffer for C++ string marshalling
        private StringBuilder _responseBuffer;
        private const int ResponseBufferSize = 2048; // 2KB buffer

        async void Start()
        {
            _responseBuffer = new StringBuilder(ResponseBufferSize);

            // --- 1. Resolve Model Path ---
            // The model is a read-only asset, loaded from StreamingAssets.
            string modelPath = Path.Combine(Application.streamingAssetsPath, 
                                            modelFileName);

            // --- 2. Resolve Memory Path ---
            // The memory file is read/write, so it MUST live in
            // persistentDataPath.
            string memoryPath = Path.Combine(Application.persistentDataPath, 
                                             memoryFileName);
            
            // --- 3. Load the NPC (on a background thread) ---
            // TournaHub_LoadNPC is a blocking C++ call.
            // We run it on a background thread to avoid freezing Unity.
            Debug.Log($"TournaHubAgent: Loading NPC {name}...");
            _npcId = await Task.Run(() => 
                UnityBridge.TournaHub_LoadNPC(modelPath, memoryPath)
            );

            if (_npcId > 0)
            {
                Debug.Log($"TournaHubAgent: NPC '{name}' loaded successfully. (ID: {_npcId})");
            }
            else
            {
                Debug.LogError($"TournaHubAgent: Failed to load NPC '{name}'. " +
                               $"Check model path: {modelPath} " +
                               $"and memory path: {memoryPath}");
            }
        }

        /// <summary>
        /// Sends dialogue to the NPC and gets a response.
        /// </summary>
        /// <param name="playerInput">The text said by the player.</param>
        /// <returns>The NPC's dialogue response.</returns>
        public async Task<string> Say(string playerInput) 
        {
            if (_npcId <= 0) return "[NPC is not initialized]";

            // Run the blocking C++ call on a background thread
            string response = await Task.Run(() =>
            {
                _responseBuffer.Clear();
                int bytesWritten = UnityBridge.TournaHub_ProcessDialogue(
                    _npcId, playerInput, _responseBuffer, _responseBuffer.Capacity);
                
                return _responseBuffer.ToString(0, bytesWritten);
            });

            return response;
        }

        /// <summary>
        /// Makes the NPC observe a game event (fire-and-forget helper).
        /// </summary>
        /// <param name="eventName">e.g., "PlayerSpotted"</param>
        /// <param name="eventDetails">e.g., "Player stole bread"</param>
        public void ObserveEvent(string eventName, string eventDetails) 
        {
            // Format the simple data into the JSON the C++ core expects
            string eventJson = $"{{ \"event\": \"{eventName}\", \"details\": \"{eventDetails}\" }}";
            
            // Call the async version but don't wait for it
            _ = ObserveEventAsync(eventJson);
        }

        /// <summary>
        /// Makes the NPC observe a game event and returns a potential reaction.
        /// </summary>
        /// <param name="eventJson">A JSON string describing the event.</param>
        /// <returns>An optional dialogue reaction from the NPC.</returns>
        public async Task<string> ObserveEventAsync(string eventJson) 
        {
            if (_npcId <= 0) return string.Empty;

            string response = await Task.Run(() =>
            {
                _responseBuffer.Clear();
                int bytesWritten = UnityBridge.TournaHub_ObserveGameEvent(
                    _npcId, eventJson, _responseBuffer, _responseBuffer.Capacity);

                return (bytesWritten > 0) 
                   ? _responseBuffer.ToString(0, bytesWritten) 
                    : string.Empty;
            });

            return response;
        }

        void OnDestroy()
        {
            // --- Clean up the C++ instance ---
            if (_npcId > 0)
            {
                // This frees the memory in the C++ core.
                UnityBridge.TournaHub_UnloadNPC(_npcId);
                Debug.Log($"TournaHubAgent: Unloaded NPC '{name}' (ID: {_npcId})");
            }
        }
    }
}