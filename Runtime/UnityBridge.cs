/*
 * /unity-adapter/Assets/TournaHub/Runtime/UnityBridge.cs
 * 
 * INTERNAL-ONLY. This is the "dumb" bridge.
 * It uses P/Invoke to call the C-style functions in the compiled
 * Core (nne).[1, 2]
 * 
 * It handles all C# to C++ data marshalling.
 */

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TournaHub.Runtime
{
    internal static class UnityBridge
    {
        /*
         * This must match the name of the compiled C++ library in the
         * /Assets/Plugins/ folder.
         * Per the plan , this is our "Layer 1" black box.
         *
         * NOTE: Do NOT include "lib" prefix - Unity adds it automatically on macOS/Linux.
         * The actual file is: libtournahub_neural_engine.bundle (macOS)
         */
        private const string DllName = "tournahub_neural_engine";

        // We must specify Cdecl as the calling convention.
        private const CallingConvention ApiConvention = CallingConvention.Cdecl;

        // --- C-API Function Imports (from CoreAPI.h) ---

        [DllImport(DllName, CallingConvention = ApiConvention)]
        internal static extern void TournaHub_Initialize(
            string configPath);

        [DllImport(DllName, CallingConvention = ApiConvention)]
        internal static extern void TournaHub_LoadWorldContext(
            string worldLoreDBPath);

        [DllImport(DllName, CallingConvention = ApiConvention)]
        internal static extern int TournaHub_LoadNPC(
            string modelPath,
            string persistencePath);

        [DllImport(DllName, CallingConvention = ApiConvention)]
        internal static extern void TournaHub_UnloadNPC(int npcId);

        [DllImport(DllName, CallingConvention = ApiConvention)]
        internal static extern int TournaHub_ProcessDialogue(
            int npcId,
            string playerInput,
            [In, Out] StringBuilder outputBuffer, // Use StringBuilder for C-style 'char*' output buffers
            int bufferSize);

        [DllImport(DllName, CallingConvention = ApiConvention)]
        internal static extern int TournaHub_ObserveGameEvent(
            int npcId,
            string eventJson,
            [In, Out] StringBuilder outputBuffer, //
            int bufferSize);
    }
}