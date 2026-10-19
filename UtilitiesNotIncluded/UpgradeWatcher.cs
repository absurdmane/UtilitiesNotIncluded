using HarmonyLib;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Power follows the upgrades at once: any building or apartment upgrade level set while the scene runs asks for a refresh.
    /// <c>Update(upgrade)</c> is how the game (and other tools) set a level; <c>Update...Prefabs</c> rebuilds the visuals after a level
    /// is set any other way. Without this, a change showed only after a reload or a manual refresh.
    /// </summary>
    [HarmonyPatch]
    internal static class UpgradeWatcher
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TenementGeneral), nameof(TenementGeneral.Update), typeof(TenementUpgrade))]
        private static void GeneralUpdated() => Changed();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TenementApartment), nameof(TenementApartment.Update), typeof(TenementUpgrade))]
        private static void ApartmentUpdated() => Changed();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TenementGeneralPrefabs), nameof(TenementGeneralPrefabs.UpdateGeneralPrefabs))]
        private static void GeneralPrefabsUpdated() => Changed();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TenementApartmentPrefabs), nameof(TenementApartmentPrefabs.UpdateApartmentPrefabs))]
        private static void ApartmentPrefabsUpdated() => Changed();

        // Several in a row (an upgrade with its requirements) collapse into one refresh.
        private static void Changed()
        {
            Utilities.ClearCache();
            GlowingFurniture.InvalidateBuiltIn();
            Refresher.Request();
        }
    }
}
