using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// TVs (<see cref="TelevisionNoise"/>) aren't devices with a <c>PowerSource</c>: they have their own power flag, <c>powerOn</c>,
    /// which is saved, so it's never touched. Without power in its room a TV can't be turned on ("No Power", as the game shows for
    /// its own power flag) and one that is on goes dark; its on/off switch (<c>isOn</c>) is kept, so it comes back with power.
    /// </summary>
    [HarmonyPatch]
    internal static class Televisions
    {
        // Last state applied. Missing = vanilla (powered).
        private static readonly Dictionary<TelevisionNoise, bool> state = new Dictionary<TelevisionNoise, bool>();

        public static int Tracked => state.Count;

        public static void Reset() => state.Clear();

        public static bool Unpowered(TelevisionNoise tv) =>
            Plugin.On && TenementController.instance != null && Utilities.RoomOf(tv.gameObject).InBuilding
            && Utilities.Power(tv.gameObject, isLight: false).Tier == PowerTier.None;

        public static void Refresh()
        {
            foreach (TelevisionNoise tv in Object.FindObjectsByType<TelevisionNoise>(FindObjectsSortMode.None))
            {
                bool has = !Unpowered(tv);
                bool had = !state.TryGetValue(tv, out bool last) || last;
                state[tv] = has;
                if (has == had)
                    continue;
                if (!has)
                    tv.TurnOff(playAudio: false, powerOff: true, force: true); // keeps isOn
                else if (tv.isOn)
                    tv.TurnOn(playAudio: false);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TelevisionNoise), nameof(TelevisionNoise.TurnOn), typeof(bool), typeof(bool))]
        private static bool TurnOnPrefix(TelevisionNoise __instance) => !Unpowered(__instance);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TelevisionNoise), nameof(TelevisionNoise.Interact))]
        private static bool InteractPrefix(TelevisionNoise __instance)
        {
            if (__instance.locked || !Unpowered(__instance))
                return true;
            UseText.instance.WarningUseEffect();
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TelevisionNoise), nameof(TelevisionNoise.UpdateUseMessage))]
        private static bool UpdateUseMessagePrefix(TelevisionNoise __instance)
        {
            if (__instance.locked || !Unpowered(__instance))
                return true;
            UseText.instance.ChangeUseText(__instance.title, "No Power", showUseText: true, redColor: true);
            return false;
        }

        /// <summary>After loading, the game shows a TV that was on as on: keep it dark while unpowered.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TelevisionNoise), nameof(TelevisionNoise.Refresh))]
        private static void RefreshPostfix(TelevisionNoise __instance)
        {
            if (Unpowered(__instance))
                __instance.TurnOff(playAudio: false, powerOff: true, force: true);
        }
    }
}
