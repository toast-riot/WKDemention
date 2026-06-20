using BepInEx;
using HarmonyLib;

namespace Demention {
    [BepInPlugin("com.nimius.demention", "Demention", "1.0.2")]
    public class DementionPlugin : BaseUnityPlugin {
        Harmony _harmony;

        void Awake() {
            // Apply all harmony patches automatically
            Settings.Init(Config);
            this._harmony = new Harmony("com.nimius.demention");
            this._harmony.PatchAll();
            Logger.LogInfo("Demention Harmony Patches applied successfully.");

            // Launch our core memory loop engine
            DementionUI.Initialize();
            Logger.LogInfo("Demention UI Engine Initialized.");
        }

        void OnDestroy() {
            Config.Save();
            _harmony?.UnpatchSelf();
        }
    }
}