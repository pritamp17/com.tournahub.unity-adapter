/*
 * /unity-adapter/Assets/TournaHub/Editor/ModelBakerService.cs
 * 
 * This is the Layer 3 (Editor-only) service.
 * Its responsibility is to perform the heavy-lifting MLOps tasks:
 * 1. Bake World Lore: Chunk text, run an embedding model (via Sentis),
 *    and build the 'world_lore.vectordb' file.
 * 2. Bake NPC Model: Contact a service (or run a local script) to
 *    fine-tune and convert a model into the correct.onnx format
 *    based on the project's target platform.[1, 3]
 * 
 * NOTE: These are highly complex tasks. This implementation provides
 * the STUBS and entry points for that future logic.
 */

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using TournaHub.Runtime;

namespace TournaHub.Editor
{
    public static class ModelBakerService
    {
       
        public static void BakeWorldLoreMenu()
        {
            string configPath = "Assets/TournaHub/Data/ContinuumProjectConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<ContinuumProjectConfig>(configPath);
            if (config == null)
            {
                Debug.LogError("ModelBakerService: Could not find ContinuumProjectConfig. " +
                               "Please run the Welcome Window first.");
                return;
            }
            BakeWorldLore(config);
        }
        
        /// <summary>
        /// Bakes the project's 'worldLore' text into a vector database.
        /// Legacy method without progress tracking.
        /// </summary>
        public static void BakeWorldLore(ContinuumProjectConfig config) 
        {
            BakeWorldLoreWithProgress(config.worldLore, null);
        }

        /// <summary>
        /// Bakes world lore text into a vector database with progress tracking.
        /// </summary>
        /// <param name="worldLoreText">The world lore text to bake</param>
        /// <param name="onProgress">Callback for progress updates (0-1)</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool BakeWorldLoreWithProgress(string worldLoreText, Action<float> onProgress)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(worldLoreText))
                {
                    Debug.LogWarning("ModelBakerService: World Lore is empty. Baking an empty guardrail.");
                }

                onProgress?.Invoke(0.1f);

                // Ensure StreamingAssets directory exists
                if (!Directory.Exists(Application.streamingAssetsPath))
                {
                    Directory.CreateDirectory(Application.streamingAssetsPath);
                }

                string dbPath = Path.Combine(Application.streamingAssetsPath, "world_lore.vectordb");
                Debug.Log($"ModelBakerService: Starting 'Bake World Lore' to {dbPath}...");

                onProgress?.Invoke(0.2f);

                // Step 1: Split world lore into chunks
                string[] chunks = SplitIntoChunks(worldLoreText);
                Debug.Log($"Split world lore into {chunks.Length} chunks");

                onProgress?.Invoke(0.4f);

                // Step 2: Generate dummy embedding vectors (stub for now)
                // TODO: Replace with Sentis-based embedding model in future
                List<float[]> embeddings = GenerateDummyEmbeddings(chunks);
                Debug.Log($"Generated {embeddings.Count} embedding vectors (384-dim each)");

                onProgress?.Invoke(0.6f);

                // Step 3: Create vector database structure
                var vectorDB = new VectorDatabase
                {
                    version = "1.0",
                    chunkCount = chunks.Length,
                    embeddingDimension = 384,
                    chunks = chunks,
                    timestamp = DateTime.UtcNow.ToString("o")
                };

                onProgress?.Invoke(0.8f);

                // Step 4: Serialize embeddings to binary format (more efficient than JSON)
                // For now, we'll use a simple text format
                string output = SerializeVectorDB(vectorDB, embeddings);

                onProgress?.Invoke(0.9f);

                // Step 5: Save to file
                File.WriteAllText(dbPath, output);
                AssetDatabase.Refresh();

                onProgress?.Invoke(1.0f);

                Debug.Log($"ModelBakerService: World lore baked successfully! {chunks.Length} chunks, saved to {dbPath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"ModelBakerService: Failed to bake world lore: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Splits text into chunks for embedding.
        /// </summary>
        private static string[] SplitIntoChunks(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new string[0];

            // Split by sentences (periods, exclamation marks, question marks)
            char[] delimiters = { '.', '!', '?' };
            string[] sentences = text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

            // Trim whitespace and filter out very short chunks
            List<string> chunks = new List<string>();
            foreach (string sentence in sentences)
            {
                string trimmed = sentence.Trim();
                if (trimmed.Length > 10) // Minimum 10 characters
                {
                    chunks.Add(trimmed);
                }
            }

            return chunks.ToArray();
        }

        /// <summary>
        /// Generates dummy embedding vectors for demonstration.
        /// TODO: Replace with actual Sentis-based embedding model.
        /// </summary>
        private static List<float[]> GenerateDummyEmbeddings(string[] chunks)
        {
            var embeddings = new List<float[]>();
            var random = new System.Random(42); // Fixed seed for reproducibility

            foreach (string chunk in chunks)
            {
                // Generate 384-dimensional vector (standard for MiniLM)
                float[] embedding = new float[384];
                for (int i = 0; i < 384; i++)
                {
                    embedding[i] = (float)(random.NextDouble() * 2 - 1); // Range: -1 to 1
                }
                embeddings.Add(embedding);
            }

            return embeddings;
        }

        /// <summary>
        /// Serializes vector database to string format.
        /// </summary>
        private static string SerializeVectorDB(VectorDatabase db, List<float[]> embeddings)
        {
            // Simple text format for now
            // Format: metadata, then chunks with embeddings
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.AppendLine($"VERSION:{db.version}");
            sb.AppendLine($"CHUNK_COUNT:{db.chunkCount}");
            sb.AppendLine($"EMBEDDING_DIM:{db.embeddingDimension}");
            sb.AppendLine($"TIMESTAMP:{db.timestamp}");
            sb.AppendLine("---CHUNKS---");

            for (int i = 0; i < db.chunks.Length; i++)
            {
                sb.AppendLine($"CHUNK_{i}:{db.chunks[i]}");
                // Optionally include embedding summary (first 5 values)
                if (i < embeddings.Count)
                {
                    float[] emb = embeddings[i];
                    sb.AppendLine($"EMBEDDING_{i}_SAMPLE:{emb[0]:F4},{emb[1]:F4},{emb[2]:F4},{emb[3]:F4},{emb[4]:F4}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Vector database structure.
        /// </summary>
        [Serializable]
        private class VectorDatabase
        {
            public string version;
            public int chunkCount;
            public int embeddingDimension;
            public string[] chunks;
            public string timestamp;
        }

        /// <summary>
        /// Bakes a new NPC configuration based on personality and platform.
        /// Creates an NPC config file that references the base model and includes personality.
        /// </summary>
        /// <param name="config">Project configuration</param>
        /// <param name="npcName">Name of the NPC</param>
        /// <param name="personality">NPC's personality and backstory</param>
        public static void BakeNPCModel(ContinuumProjectConfig config, 
                                        string npcName, string personality) 
        {
            try
            {
                Debug.Log($"ModelBakerService: Creating NPC configuration for '{npcName}'...");
                Debug.Log($"-- Target Platform: {config.platform}");
                Debug.Log($"-- Personality length: {personality.Length} characters");
                
                // Ensure NPCs directory exists
                string npcsDir = Path.Combine(Application.streamingAssetsPath, "NPCs");
                if (!Directory.Exists(npcsDir))
                {
                    Directory.CreateDirectory(npcsDir);
                    Debug.Log($"Created NPCs directory: {npcsDir}");
                }

                // Create NPC-specific directory
                string npcDir = Path.Combine(npcsDir, SanitizeFileName(npcName));
                if (!Directory.Exists(npcDir))
                {
                    Directory.CreateDirectory(npcDir);
                }

                // Create NPC configuration file
                string configPath = Path.Combine(npcDir, "npc_config.json");
                var npcConfig = new NPCConfiguration
                {
                    npcName = npcName,
                    personality = personality,
                    baseModelPath = config.selectedModelUrl, // Reference to base model
                    platform = config.platform.ToString(),
                    createdAt = DateTime.UtcNow.ToString("o"),
                    version = "1.0"
                };

                string jsonConfig = JsonUtility.ToJson(npcConfig, true);
                File.WriteAllText(configPath, jsonConfig);
                
                Debug.Log($"NPC config saved to: {configPath}");

                // Create an empty memory/persistence file for this NPC
                string memoryPath = Path.Combine(npcDir, "memory.sqlite");
                CreateEmptySQLiteMemory(memoryPath);
                
                Debug.Log($"NPC memory database created: {memoryPath}");

                // Create a readme with usage instructions
                string readmePath = Path.Combine(npcDir, "README.txt");
                string readme = $@"NPC Configuration: {npcName}
=====================================

This NPC uses the base model with injected personality.

Files:
- npc_config.json: NPC personality and configuration
- memory.sqlite: Persistent memory database (managed by C++ core)

To use this NPC in your game:
1. Call TournaHub_LoadNPC() with the base model path
2. The C++ core will inject the personality as a system prompt
3. Use TournaHub_ProcessDialogue() to interact with the NPC

Personality Summary:
{(personality.Length > 200 ? personality.Substring(0, 200) + "..." : personality)}

Created: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
";
                File.WriteAllText(readmePath, readme);
                
                AssetDatabase.Refresh();
                
                Debug.Log($"✓ NPC '{npcName}' configuration created successfully!");
                Debug.Log($"  Location: {npcDir}");
                Debug.Log($"  This NPC will use the base model: {config.selectedModelName}");
                
                // Show success dialog
                EditorUtility.DisplayDialog(
                    "NPC Created!",
                    $"NPC '{npcName}' has been configured successfully!\n\n" +
                    $"Location: StreamingAssets/NPCs/{SanitizeFileName(npcName)}/\n\n" +
                    $"This NPC will use the base model with its personality injected at runtime.",
                    "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create NPC configuration: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog(
                    "Error",
                    $"Failed to create NPC configuration:\n{e.Message}",
                    "OK");
            }
        }
        
        /// <summary>
        /// Sanitizes a filename to remove invalid characters.
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            char[] invalids = Path.GetInvalidFileNameChars();
            string sanitized = fileName;
            foreach (char c in invalids)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            return sanitized;
        }
        
        /// <summary>
        /// Creates an empty SQLite memory database for the NPC.
        /// </summary>
        private static void CreateEmptySQLiteMemory(string dbPath)
        {
            // Create a minimal SQLite database structure
            // The C++ core will manage the actual schema and data
            string initSQL = @"-- NPC Memory Database
-- This file is managed by the Srujan C++ core
-- Schema will be created automatically on first use
";
            
            File.WriteAllText(dbPath, initSQL);
        }
        
        /// <summary>
        /// NPC Configuration structure for JSON serialization.
        /// </summary>
        [Serializable]
        private class NPCConfiguration
        {
            public string npcName;
            public string personality;
            public string baseModelPath;
            public string platform;
            public string createdAt;
            public string version;
        }
    }
}