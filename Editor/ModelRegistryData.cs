/*
 * /unity-adapter/Editor/ModelRegistryData.cs
 * 
 * Data structures for deserializing ModelRegistry.json
 * Contains model information from CDN including URLs, versions, and metadata
 */

using System;

namespace TournaHub.Editor
{
    /// <summary>
    /// Root object for ModelRegistry.json deserialization
    /// </summary>
    [Serializable]
    public class ModelRegistry
    {
        public string sdk_version;
        public ModelCategories models;
    }

    /// <summary>
    /// Contains model groups organized by target platform
    /// </summary>
    [Serializable]
    public class ModelCategories
    {
        public ModelGroup mobile_onnx;
        public ModelGroup pc_console_onnx;
        public ModelGroup pc_standalone_gguf;
    }

    /// <summary>
    /// Group of models for a specific platform (e.g., primary and lite variants)
    /// </summary>
    [Serializable]
    public class ModelGroup
    {
        public ModelInfo primary;
        public ModelInfo lite;
        
        // For GGUF models with quality tiers
        public ModelInfo high;
        public ModelInfo medium;
        public ModelInfo low;
    }

    /// <summary>
    /// Individual model information including download URL
    /// </summary>
    [Serializable]
    public class ModelInfo
    {
        public string name;
        public string url;
        public string version;
        
        /// <summary>
        /// File size in GB (optional, for display purposes)
        /// </summary>
        public float filesize_gb;
        
        /// <summary>
        /// Returns true if this is a direct file download (e.g., .gguf) vs a ZIP archive
        /// </summary>
        public bool IsDirectFile()
        {
            if (string.IsNullOrEmpty(url))
                return false;
                
            string urlLower = url.ToLower();
            return urlLower.EndsWith(".gguf") || 
                   urlLower.EndsWith(".onnx") || 
                   urlLower.EndsWith(".safetensors");
        }
        
        /// <summary>
        /// Returns the file extension (e.g., "gguf", "zip")
        /// </summary>
        public string GetFileExtension()
        {
            if (string.IsNullOrEmpty(url))
                return "unknown";
                
            int lastDot = url.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < url.Length - 1)
            {
                return url.Substring(lastDot + 1).ToLower();
            }
            return "unknown";
        }
    }
}
