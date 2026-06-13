using HarmonyLib;
using UnityEngine.EventSystems;

namespace Demention.Patches
{
    [HarmonyPatch(typeof(EventSystem), "Update")]
    public static class DementionUIUpdatePatch
    {
        static void Postfix()
        {
            // Automatically initialize the dementia loop if it doesn't exist yet
            if (DementionUI.Instance == null)
            {
                DementionUI.Initialize();
            }

            // Optional debug keybind to manually test the screen-flash and teleport sequence
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F5))
            {
                DementionUI.Instance?.ForceTriggerEpisode();
            }
        }
    }
}