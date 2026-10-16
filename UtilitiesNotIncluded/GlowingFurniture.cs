using System.Collections.Generic;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Electric furniture that glows without a light source, so the light pass never sees it: decoration lights (garlands).
    /// Matched by name (<c>GlowingFurniture</c>). Without power its glowing meshes are dimmed (<see cref="EmissionDimmer"/>), and
    /// an animated garland (<see cref="DecorationLights"/>, which swaps on/off materials itself) is paused; with power it is restored.
    /// </summary>
    internal static class GlowingFurniture
    {
        // Last state applied. Missing = vanilla (lit).
        private static readonly Dictionary<FurniturePlaceable, bool> state = new Dictionary<FurniturePlaceable, bool>();

        public static int Off => Count(false);

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
            string list = Plugin.GlowingFurniture.Value;
            if (string.IsNullOrWhiteSpace(list) && state.Count == 0)
                return;
            foreach (FurniturePlaceable furniture in Object.FindObjectsByType<FurniturePlaceable>(FindObjectsSortMode.None))
            {
                if (furniture == null)
                    continue;
                bool listed = !string.IsNullOrWhiteSpace(list) && Listed(furniture, list);
                if (!listed && !state.ContainsKey(furniture))
                    continue;
                bool lit = !listed || !Unpowered(furniture.gameObject);
                bool was = !state.TryGetValue(furniture, out bool last) || last;
                state[furniture] = lit;
                if (lit != was)
                    Apply(furniture, lit);
            }
        }

        // The furniture's own name, or the name of one of its meshes (variants share mesh names like decoration_lights_00N).
        private static bool Listed(FurniturePlaceable furniture, string list)
        {
            if (Utilities.Listed(furniture.transform, list))
                return true;
            foreach (Renderer r in furniture.GetComponentsInChildren<Renderer>(true))
                if (Utilities.Listed(r.transform, list))
                    return true;
            return false;
        }

        private static bool Unpowered(GameObject go) =>
            Plugin.LightsNeedPower.Value && TenementController.instance != null && Utilities.RoomOf(go).InBuilding
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
