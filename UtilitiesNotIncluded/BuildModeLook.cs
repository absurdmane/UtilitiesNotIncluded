using HarmonyLib;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// The build mode highlights the furniture under the cursor: it saves the furniture's materials, paints it green/red and puts the
    /// saved materials back when the cursor leaves (<c>BuildingSystem.SetColorToNormal</c>). A lamp just placed in an unpowered room
    /// is still under the cursor, so its lit materials came back over the mod's dimming (Floor Lamp, 20.10). Right after the
    /// materials are put back, the furniture gets its unpowered look again.
    /// </summary>
    [HarmonyPatch]
    internal static class BuildModeLook
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BuildingSystem), nameof(BuildingSystem.SetColorToNormal))]
        private static void SetColorToNormalPrefix(BuildingSystem __instance, out FurniturePlaceable __state)
        {
            __state = __instance.lastLooked != null && !__instance.updateColorDisabled
                ? __instance.lastLooked.GetComponentInParent<FurniturePlaceable>()
                : null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildingSystem), nameof(BuildingSystem.SetColorToNormal))]
        private static void SetColorToNormalPostfix(FurniturePlaceable __state)
        {
            if (__state == null || !Plugin.On)
                return;
            try
            {
                foreach (LightTurnOffOn lamp in __state.GetComponentsInChildren<LightTurnOffOn>(true))
                    if (LampVisuals.Unpowered(lamp))
                        LampVisuals.Apply(lamp, powered: false);
                foreach (LightController light in __state.GetComponentsInChildren<LightController>(true))
                    if (Utilities.IsElectric(light) && Patches.LightUnpowered(light))
                        EmissionDimmer.Apply(light, powered: false);
                GlowingFurniture.Reapply(__state);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Build mode look failed: {e}");
            }
        }
    }
}
