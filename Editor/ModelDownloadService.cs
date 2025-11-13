/*
 * /unity-adapter/Editor/ModelDownloadService.cs
 * 
 * Service for downloading and extracting model files from CDN
 * Provides progress tracking and error handling for model downloads
 */

using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;

namespace TournaHub.Editor
{
    /// <summary>
    /// Handles model downloads from CDN with progress tracking and extraction
    /// </summary>
    public static class ModelDownloadService
    {
        private const long DEFAULT_ESTIMATED_SIZE = 5L * 1024 * 1024 * 1024; // 5 GB default

        /// <summary>
        /// Downloads a model file from the specified URL with progress tracking
        /// </summary>
        /// <param name="url">CDN URL of the model ZIP file</param>
        /// <param name="outputPath">Local path to save the downloaded file</param>
        /// <param name="onProgress">Callback invoked with (progress 0-1, bytesDownloaded, totalBytes)</param>
        /// <param name="onComplete">Callback invoked with (success, errorMessage)</param>
        /// <returns>IEnumerator for coroutine execution</returns>
        public static IEnumerator DownloadModel(
            string url, 
            string outputPath, 
            Action<float, long, long> onProgress, 
            Action<bool, string> onComplete)
        {
            Debug.Log($"[ModelDownload] Starting download from: {url}");
            Debug.Log($"[ModelDownload] Output path: {outputPath}");
            
            // Ensure output directory exists
            string directory = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    Debug.Log($"[ModelDownload] Created directory: {directory}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ModelDownload] Failed to create directory: {e.Message}");
                    onComplete?.Invoke(false, $"Failed to create directory: {e.Message}");
                    yield break;
                }
            }

            // Create web request
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                // Use DownloadHandlerFile to stream directly to disk (memory efficient)
                request.downloadHandler = new DownloadHandlerFile(outputPath);
                request.timeout = 300; // 5 minute timeout
                
                Debug.Log($"[ModelDownload] Request configured, starting download...");

                // Start the download
                var operation = request.SendWebRequest();
                Debug.Log($"[ModelDownload] SendWebRequest initiated");

                // Track progress
                while (!operation.isDone)
                {
                    float progress = request.downloadProgress;
                    ulong downloaded = request.downloadedBytes;
                    
                    // Try to get content length from headers
                    string contentLength = request.GetResponseHeader("Content-Length");
                    long totalBytes = 0;
                    if (!string.IsNullOrEmpty(contentLength))
                    {
                        long.TryParse(contentLength, out totalBytes);
                    }
                    
                    onProgress?.Invoke(progress, (long)downloaded, totalBytes);
                    
                    yield return null; // Wait one frame
                }

                // Check for errors
                Debug.Log($"[ModelDownload] Request completed. Result: {request.result}");
                Debug.Log($"[ModelDownload] Response code: {request.responseCode}");
                Debug.Log($"[ModelDownload] Error (if any): {request.error}");
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMsg = $"Download failed: {request.error}";
                    
                    // Categorize errors for better user messaging
                    if (request.result == UnityWebRequest.Result.ConnectionError)
                    {
                        errorMsg = "Network connection error. Please check your internet connection.";
                        Debug.LogError($"[ModelDownload] Connection Error: {request.error}");
                    }
                    else if (request.result == UnityWebRequest.Result.ProtocolError)
                    {
                        errorMsg = $"Server error (HTTP {request.responseCode}). The model may not be available.";
                        Debug.LogError($"[ModelDownload] Protocol Error: HTTP {request.responseCode} - {request.error}");
                    }
                    else if (request.result == UnityWebRequest.Result.DataProcessingError)
                    {
                        errorMsg = "Failed to save downloaded file. Check disk space and permissions.";
                        Debug.LogError($"[ModelDownload] Data Processing Error: {request.error}");
                    }
                    
                    // Clean up partial download
                    try
                    {
                        if (File.Exists(outputPath))
                        {
                            File.Delete(outputPath);
                        }
                    }
                    catch { /* Ignore cleanup errors */ }
                    
                    onComplete?.Invoke(false, errorMsg);
                    yield break;
                }

                // Final progress update
                onProgress?.Invoke(1.0f, (long)request.downloadedBytes, (long)request.downloadedBytes);
                
                // Success
                onComplete?.Invoke(true, null);
            }
        }

        /// <summary>
        /// Extracts a ZIP file to the specified directory
        /// </summary>
        /// <param name="zipPath">Path to the ZIP file</param>
        /// <param name="extractPath">Directory to extract contents to</param>
        /// <returns>True if extraction succeeded, false otherwise</returns>
        public static bool ExtractZip(string zipPath, string extractPath, out string errorMessage)
        {
            errorMessage = null;
            
            try
            {
                // Ensure extract directory exists
                if (!Directory.Exists(extractPath))
                {
                    Directory.CreateDirectory(extractPath);
                }

                // Extract the ZIP file
                ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);
                
                Debug.Log($"Successfully extracted {zipPath} to {extractPath}");
                return true;
            }
            catch (InvalidDataException e)
            {
                errorMessage = $"ZIP file is corrupted or invalid: {e.Message}";
                Debug.LogError(errorMessage);
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                errorMessage = $"Permission denied when extracting files: {e.Message}";
                Debug.LogError(errorMessage);
                return false;
            }
            catch (IOException e)
            {
                errorMessage = $"I/O error during extraction: {e.Message}";
                Debug.LogError(errorMessage);
                return false;
            }
            catch (Exception e)
            {
                errorMessage = $"Unexpected error during extraction: {e.Message}";
                Debug.LogError(errorMessage);
                return false;
            }
        }

        /// <summary>
        /// Validates that sufficient disk space is available for the download and extraction
        /// </summary>
        /// <param name="requiredBytes">Required bytes (0 to use default estimate)</param>
        /// <returns>True if sufficient space available, false otherwise</returns>
        public static bool ValidateDiskSpace(long requiredBytes = 0)
        {
            try
            {
                // Use default if not specified
                if (requiredBytes <= 0)
                {
                    requiredBytes = DEFAULT_ESTIMATED_SIZE;
                }
                
                string path = Application.streamingAssetsPath;
                if (!Directory.Exists(path))
                {
                    path = Application.dataPath;
                }
                
                DriveInfo drive = new DriveInfo(Path.GetPathRoot(path));
                
                // Require 2x the model size for safety (download + extraction)
                long requiredWithBuffer = requiredBytes * 2;
                
                if (drive.AvailableFreeSpace < requiredWithBuffer)
                {
                    Debug.LogError($"Insufficient disk space. Required: {FormatBytes(requiredWithBuffer)}, Available: {FormatBytes(drive.AvailableFreeSpace)}");
                    return false;
                }
                
                Debug.Log($"Disk space check passed. Available: {FormatBytes(drive.AvailableFreeSpace)}, Required: {FormatBytes(requiredWithBuffer)}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not validate disk space: {e.Message}. Allowing download to proceed.");
                return true; // Allow download to proceed if we can't check
            }
        }

        /// <summary>
        /// Formats bytes into human-readable string (KB, MB, GB)
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Cleans up temporary download files
        /// </summary>
        public static void CleanupTempFiles(string zipPath)
        {
            try
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                    Debug.Log($"Cleaned up temporary file: {zipPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to clean up temporary file {zipPath}: {e.Message}");
            }
        }

        /// <summary>
        /// Gets the model storage path for a specific platform
        /// </summary>
        public static string GetModelStoragePath(string platform)
        {
            string basePath = Path.Combine(Application.streamingAssetsPath, "Models");
            string platformPath = Path.Combine(basePath, platform);
            
            if (!Directory.Exists(platformPath))
            {
                Directory.CreateDirectory(platformPath);
            }
            
            return platformPath;
        }
    }
}
