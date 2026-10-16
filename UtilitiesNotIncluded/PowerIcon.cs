using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Adds a lightning icon to the station panel's trait row (next to Efficiency, Industrial, ...), using the game's own
    /// <c>Electricity</c> sprite: normal = powered, red = no power, plus a separate orange one for unstable (Shit) voltage.
    /// </summary>
    [HarmonyPatch]
    internal static class PowerIcon
    {
        private static readonly Color NoPower = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color Unstable = new Color(1f, 0.6f, 0.1f);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ModifiersUI), nameof(ModifiersUI.UpdateModifiers))]
        private static void UpdateModifiersPostfix(ModifiersUI __instance, PowerSource powerSource, bool generator)
        {
            try
            {
                if (generator || powerSource == null || powerSource.Parent == null)
                    return;
                CraftingBase device = powerSource.Parent.GetComponent<CraftingBase>();
                if (device == null || device._Power != powerSource || !Utilities.Handles(device))
                    return;

                bool has = Utilities.DeviceHasPower(device, out PowerInfo power, out string reason);
                if (!has)
                {
                    Add(__instance, "No power", reason, NoPower);
                    return;
                }
                string source = power.FromGenerator ? "Mini Generator" : "building grid";
                Add(__instance, $"Power: {source} ({power.Tier})", "", Color.white);
                if (power.Tier == PowerTier.Shit && Plugin.ShitElectricityQuality.Value < 1f)
                    Add(__instance, "Unstable voltage",
                        $"Equipment quality x{Plugin.ShitElectricityQuality.Value:0.##}", Unstable);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Power icon failed: {e}");
            }
        }

        private static void Add(ModifiersUI ui, string title, string details, Color color)
        {
            ModifierSprites sprites = ModifierSprites.instance;
            if (sprites == null || sprites.Electricity == null || ui.modifierPrefab == null)
                return;
            ModifierUI icon = Object.Instantiate(ui.modifierPrefab, ui.modifierList.transform).GetComponent<ModifierUI>();
            if (icon == null)
                return;
            icon.modifierImage.sprite = sprites.Electricity;
            icon.modifierImage.color = color;
            icon.toolTipInfo.UpdateToolTipInfo(title, details, update: false);
        }
    }
}
