using HarmonyLib;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Every effect is a getter patch: nothing saved is changed, so removing the mod restores vanilla behaviour.
    /// </summary>
    [HarmonyPatch]
    internal static class Patches
    {
        // ---- devices ----

        /// <summary>Electric devices in the building run only when their room (or a generator nearby) has power.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PowerSource), nameof(PowerSource.Powered), MethodType.Getter)]
        private static void PoweredPostfix(PowerSource __instance, ref bool __result)
        {
            if (!__result || __instance.RequiresFuel || __instance.Type != PowerSource.PowerSourceType.Electricity || __instance.Parent == null)
                return;
            CraftingBase device = __instance.Parent.GetComponent<CraftingBase>();
            if (device == null || device._Power != __instance || !Utilities.Handles(device))
                return;
            if (!Utilities.DeviceHasPower(device, out _, out _))
                __result = false;
        }

        /// <summary>
        /// The panel's power button asks <c>TogglePower</c>, which reads the raw <c>PowerInput</c>, not <c>Powered</c>: an unpowered device
        /// "turned on" (blue button, power-on sounds and relays) although nothing runs. Answer no while it has no power. The switch
        /// position (<c>On</c>) is still remembered, so the device starts by itself when power comes (see <see cref="Refresher"/>).
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PowerSource), nameof(PowerSource.TogglePower))]
        private static void TogglePowerPostfix(PowerSource __instance, ref bool __result)
        {
            if (!__result || __instance.RequiresFuel || __instance.Type != PowerSource.PowerSourceType.Electricity || __instance.Parent == null)
                return;
            CraftingBase device = __instance.Parent.GetComponent<CraftingBase>();
            if (device == null || device._Power != __instance || !Utilities.Handles(device))
                return;
            if (!Utilities.DeviceHasPower(device, out _, out _))
                __result = false;
        }

        /// <summary>Unstable voltage: electric devices on Shit power work at lower equipment quality.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CraftingBase), nameof(CraftingBase.EquipmentQualityWithUpgrades), MethodType.Getter)]
        private static void EquipmentQualityPostfix(CraftingBase __instance, ref float __result)
        {
            if (Plugin.ShitElectricityQuality.Value >= 1f || !Utilities.Handles(__instance))
                return;
            if (Utilities.DeviceHasPower(__instance, out PowerInfo power, out _) && power.Tier == PowerTier.Shit)
                __result *= Plugin.ShitElectricityQuality.Value;
        }

        /// <summary>A generator starting or stopping (fuel in, fuel out, switched) changes power around it.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Generator), nameof(Generator.OnPowerOn))]
        private static void GeneratorOnPostfix() => Refresher.Request();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Generator), nameof(Generator.OnPowerOut))]
        private static void GeneratorOffPostfix() => Refresher.Request();

        /// <summary>
        /// A moved (or loaded) generator restores its burning fuel a few frames after it's placed (<c>CraftingBase.LoadDelay</c>),
        /// with no OnPowerOn. The refresh after "furniture placed" runs before that and sees it off, so refresh again here.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Fuel), nameof(Fuel.LoadedGame))]
        private static void FuelLoadedPostfix(Fuel __instance)
        {
            if (__instance.active)
                Refresher.Request();
        }

        // ---- fridges ----

        public static bool FridgeUnpowered(Storage fridge)
        {
            if (!Plugin.FridgesNeedPower.Value || !Utilities.RoomOf(fridge.gameObject).InBuilding)
                return false;
            return Utilities.Power(fridge.gameObject, isLight: false).Tier == PowerTier.None;
        }

        /// <summary>No power, no cooling. Patched on the getter so the saved <c>powerOff</c> flag is never touched.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Storage), nameof(Storage.RefigerationFactor), MethodType.Getter)]
        private static void RefigerationFactorPostfix(Storage __instance, ref float __result)
        {
            if (__result > 0f && FridgeUnpowered(__instance))
                __result = 0f;
        }

        // ---- lights ----

        public static bool LightUnpowered(LightController light)
        {
            if (!Plugin.LightsNeedPower.Value || !Utilities.IsElectric(light) || light.disabled || light.moving)
                return false;
            if (!Utilities.RoomOf(light.gameObject).InBuilding)
                return false;
            return Utilities.Power(light.gameObject, isLight: true).Tier == PowerTier.None;
        }

        /// <summary>The light-zone system re-applies its on/off status through <c>Override</c>; keep unpowered lamps off.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(LightController), nameof(LightController.Override))]
        private static void LightOverridePrefix(LightController __instance, ref bool state)
        {
            if (!state && __instance != null && LightUnpowered(__instance))
                state = true;
        }

        // ---- water ----

        /// <summary>
        /// Taps come in two kinds: some pour from the scene's global water source, others are infinite sources with their own liquid
        /// (e.g. the Commercial Kitchen Table With Sink: Clean Water, Capacity -1, no external source). In the building both pour from a
        /// source of their room's water level instead, or from an empty one when the room has no water. All filling goes through
        /// this getter when it isn't null.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LiquidStorage), nameof(LiquidStorage.ExternalOutputStorage), MethodType.Getter)]
        private static void ExternalOutputStoragePostfix(LiquidStorage __instance, ref LiquidStorage __result)
        {
            if (!Plugin.WaterEnabled.Value || WaterSources.IsOurs(__instance) || (__result != null && WaterSources.IsOurs(__result)))
                return;
            WaterSourceController controller = WaterSourceController.instance;
            bool global = __result != null && controller != null && __result == controller.globalStorage;
            bool ownInfinite = __result == null && __instance.Capacity < 0f;
            if (!global && !ownInfinite)
                return;
            Room room = Utilities.RoomOf(__instance.gameObject);
            if (!room.InBuilding)
                return;
            LiquidStorage source = WaterSources.For(Utilities.Water(room), global ? __result : __instance);
            // The tap's own water is the right one: leave it pouring by itself. Never return the tap itself, filling would loop.
            __result = source == __instance ? null : source;
        }
    }
}
