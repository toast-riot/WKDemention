using BepInEx;
using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Demention
{
    [BepInPlugin("com.nimius.demention", "Demention", "1.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        void Awake()
        {
            // Apply all harmony patches automatically
            this._harmony = new Harmony("com.nimius.demention");
            this._harmony.PatchAll();
            Logger.LogInfo("Demention Harmony Patches applied successfully.");

            // Launch our core memory loop engine
            DementionUI.Initialize();
            Logger.LogInfo("Demention UI Engine Initialized.");
        }
    }
}
