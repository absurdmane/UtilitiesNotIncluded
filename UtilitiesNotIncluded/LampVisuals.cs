using HarmonyLib;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Switchable lamps (<see cref="LightTurnOffOn"/>; not the Ceiling Light, which has no switch) have a glowing "on" material on the shade. Without power
    /// the light itself goes off (<c>LightController.Override</c>), but the shade kept glowing. Here an unpowered lamp gets the "off"
    /// material and its light objects hidden, the way <c>turnLightOff</c> does it, without calling it: that would clear the saved
    /// switch state <c>on</c>. When power is back and the switch is on, the "on" look returns.
    /// </summary>
    [HarmonyPatch]
    internal static class LampVisuals
    {
        public static bool Unpowered(LightTurnOffOn lamp)
        {
            if (!Plugin.On || !Plugin.LightsNeedPower.Value || TenementController.instance == null)
                return false;
            // Candles and fire may use the same switch component; only electric lights need power.
            LightController controller = lamp.GetComponentInChildren<LightController>(true);
            if (controller != null && !Utilities.IsElectric(controller))
                return false;
            if (!Utilities.RoomOf(lamp.gameObject).InBuilding)
                return false;
            return Utilities.Power(lamp.gameObject, isLight: true).Tier == PowerTier.None;
        }

        /// <summary>Shows the lamp as off (unpowered) or as its switch says (powered).</summary>
        public static void Apply(LightTurnOffOn lamp, bool powered)
        {
            bool lit = powered && lamp.on;
            if (lit)
                EmissionDimmer.ApplyAll(Root(lamp), lit: true);
            Material material = lit ? (lamp.LocalOnMaterial != null ? lamp.LocalOnMaterial : lamp.lightOnMaterial) : lamp.lightOffMaterial;
            if (material != null && lamp.lightMeshList != null)
                foreach (MeshRenderer renderer in lamp.lightMeshList)
                {
                    if (renderer == null)
                        continue;
                    Material[] materials = renderer.sharedMaterials;
                    if (materials == null || materials.Length <= lamp.meshIndex)
                        continue;
                    materials[lamp.meshIndex] = material;
                    renderer.sharedMaterials = materials;
                }
            if (lamp.lights != null)
                foreach (GameObject light in lamp.lights)
                    if (light != null)
                        light.SetActive(lit);
            if (!lit)
                EmissionDimmer.ApplyAll(Root(lamp), lit: false);
        }

        // The lamp's own meshes: its furniture object, or for a built-in lamp the switch's object.
        private static Transform Root(LightTurnOffOn lamp)
        {
            FurniturePlaceable placed = lamp.GetComponentInParent<FurniturePlaceable>();
            return placed != null ? placed.transform : lamp.transform;
        }

        /// <summary>
        /// The game's switch only swaps the materials of its own mesh list; some lamps (the Wall Light) have a second glowing
        /// mesh that isn't in it and stays lit. Dim it too.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LightTurnOffOn), nameof(LightTurnOffOn.turnLightOff))]
        private static void TurnLightOffPostfix(LightTurnOffOn __instance)
        {
            try
            {
                if (Plugin.FixLampGlow.Value)
                    EmissionDimmer.ApplyAll(Root(__instance), lit: false);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Lamp look failed: {e}");
            }
        }

        /// <summary>Flipping the switch on without power: the game puts the glowing look back, so take it off again.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LightTurnOffOn), nameof(LightTurnOffOn.turnLightOn))]
        private static void TurnLightOnPostfix(LightTurnOffOn __instance)
        {
            try
            {
                if (Unpowered(__instance))
                    Apply(__instance, powered: false);
                else if (Plugin.FixLampGlow.Value)
                    EmissionDimmer.ApplyAll(Root(__instance), lit: true);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Lamp look failed: {e}");
            }
        }
    }
}
