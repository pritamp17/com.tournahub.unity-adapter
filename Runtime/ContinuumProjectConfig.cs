/*
 * /unity-adapter/Assets/TournaHub/Runtime/ContinuumProjectConfig.cs
 * 
 * This ScriptableObject holds the project-wide configuration.
 * It is created by the ContinuumWelcomeWindow and read by the
 * ModelBakerService and NPCCreatorWindow.
 */

using UnityEngine;

namespace TournaHub.Runtime
{
    // This allows developers to create this asset via the "Assets/Create" menu.
   
    public class ContinuumProjectConfig : ScriptableObject
    {
        // This enum is the core SDK logic flow [3]
        public enum TargetPlatform
        {
            Mobile,
            PC_CONSOLE,
            PC,
            CONSOLE
        }

        [Header("Platform Configuration")]
        public TargetPlatform platform = TargetPlatform.PC_CONSOLE;

        public string worldLore;
    }
}