using System.Collections.Generic;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Electric furniture that glows without a light source, so the light pass never sees it: decoration lights (garlands).
    /// Furniture on the electric lights list (<see cref="ElectricLights"/>) without a light of its own. Without power its glowing meshes are dimmed (<see cref="EmissionDimmer"/>), and
    /// an animated garland (<see cref="DecorationLights"/>, which swaps on/off materials itself) is paused; with power it is restored.
    /// Also lamp meshes built into upgrade prefabs whose name is on the list but which have no light of their own, e.g. the
    /// greenhouse's and shop's fluorescent tubes (their light hangs next to them, and not every tube has one).
    /// </summary>
    internal static class GlowingFurniture
    {
        // Last state applied. Missing = vanilla (lit).
        private static readonly Dictionary<FurniturePlaceable, bool> state = new Dictionary<FurniturePlaceable, bool>();

        public static int Off => Count(false);

        public static int Tracked => state.Count + meshState.Count;

        // Built-in lamp meshes: last state applied. Missing = vanilla (lit).
        private static readonly Dictionary<Renderer, bool> meshState = new Dictionary<Renderer, bool>();

        /// <summary>After a load: the old scene's furniture is gone.</summary>
        public static void Reset()
        {
            state.Clear();
            matched.Clear();
            meshState.Clear();
            builtInLamps = null;
        }

        private static int Count(bool value)
        {
            int n = 0;
            foreach (bool v in state.Values)
                if (v == value)
                    n++;
            return n;
        }

        public static void Refresh()
        {
            foreach (FurniturePlaceable furniture in Object.FindObjectsByType<FurniturePlaceable>(FindObjectsSortMode.None))
            {
                if (furniture == null)
                    continue;
                bool listed = Listed(furniture);
                if (!listed && !state.ContainsKey(furniture))
                    continue;
                bool lit = !listed || !Unpowered(furniture.gameObject);
                bool was = !state.TryGetValue(furniture, out bool last) || last;
                state[furniture] = lit;
                if (lit != was)
                    Apply(furniture, lit);
            }
            RefreshBuiltIn();
        }

        // The built-in lamp meshes, found once: scanning every renderer of the upgrades is costly, and placing furniture doesn't
        // change them. Rebuilt after a load, an upgrade change or a change of the lists.
        private static List<Renderer> builtInLamps;
        private static string builtInLists;

        public static void InvalidateBuiltIn() => builtInLamps = null;

        private static void RefreshBuiltIn()
        {
            string lists = Plugin.AddLights.Value + "|" + Plugin.RemoveLights.Value;
            if (builtInLamps == null || lists != builtInLists)
            {
                builtInLamps = new List<Renderer>();
                builtInLists = lists;
                var seen = new HashSet<Renderer>();
                foreach (Transform root in UpgradeRoots())
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                        if (seen.Add(r) && BuiltInLamp(r))
                            builtInLamps.Add(r);
                // Taken off the list or gone: put back what is still there.
                foreach (Renderer r in new List<Renderer>(meshState.Keys))
                    if (!seen.Contains(r) || !builtInLamps.Contains(r))
                    {
                        if (r != null && !meshState[r])
                            EmissionDimmer.ApplyAll(r.transform, true);
                        meshState.Remove(r);
                    }
            }
            foreach (Renderer r in builtInLamps)
            {
                if (r == null)
                    continue;
                bool lit = !Unpowered(r.gameObject);
                bool was = !meshState.TryGetValue(r, out bool last) || last;
                meshState[r] = lit;
                if (lit != was)
                    EmissionDimmer.ApplyAll(r.transform, lit);
            }
        }

        private static IEnumerable<Transform> UpgradeRoots()
        {
            foreach (TenementGeneralPrefabs p in Object.FindObjectsByType<TenementGeneralPrefabs>(FindObjectsSortMode.None))
                yield return p.transform;
            foreach (TenementApartmentPrefabs p in Object.FindObjectsByType<TenementApartmentPrefabs>(FindObjectsSortMode.None))
                yield return p.transform;
        }

        // A listed lamp mesh with no light or switch of its own (those are dimmed by the light pass and LampVisuals), not furniture.
        private static bool BuiltInLamp(Renderer r) =>
            r != null && ElectricLights.IsListed(ElectricLights.Normalize(r.name))
            && r.GetComponentInChildren<LightController>(true) == null && r.GetComponentInChildren<LightTurnOffOn>(true) == null
            && r.GetComponentInParent<LightTurnOffOn>() == null && r.GetComponentInParent<FurniturePlaceable>() == null;

        // Per furniture, for the lists it was decided with: the component scan below is the costly part of a refresh.
        private static readonly Dictionary<FurniturePlaceable, bool> matched = new Dictionary<FurniturePlaceable, bool>();
        private static string matchedLists;

        // On the electric lights list and glowing without a light source: lamps are handled by the light pass and LampVisuals.
        private static bool Listed(FurniturePlaceable furniture)
        {
            string lists = Plugin.AddLights.Value + "|" + Plugin.RemoveLights.Value;
            if (lists != matchedLists)
            {
                matched.Clear();
                matchedLists = lists;
            }
            if (matched.TryGetValue(furniture, out bool cached))
                return cached;
            bool result = ElectricLights.IsElectricFurniture(furniture)
                && furniture.GetComponentInChildren<LightController>(true) == null
                && furniture.GetComponentInChildren<LightTurnOffOn>(true) == null;
            matched[furniture] = result;
            return result;
        }

        private static bool Unpowered(GameObject go) =>
            Plugin.On && Plugin.LightsNeedPower.Value && TenementController.instance != null && Utilities.RoomOf(go).InBuilding
            && Utilities.Power(go, isLight: true).Tier == PowerTier.None;

        private static void Apply(FurniturePlaceable furniture, bool lit)
        {
            DecorationLights[] garlands = furniture.GetComponentsInChildren<DecorationLights>(true);
            if (!lit)
                foreach (DecorationLights g in garlands)
                    g.enabled = false; // stops the pattern from putting the "on" material back
            EmissionDimmer.ApplyAll(furniture.transform, lit);
            if (lit)
                foreach (DecorationLights g in garlands)
                {
                    g.enabled = true;
                    g.StartPattern();
                }
        }
    }
}
