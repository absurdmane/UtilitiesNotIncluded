using System.Collections.Generic;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Lamps without a switch component (<see cref="LightTurnOffOn"/>) have no "off" material in the game. Without power their glowing
    /// meshes get copies of their materials with the emission turned off; the original materials go back when power returns.
    /// Only the lamp's own meshes: its furniture object, or for a built-in lamp the light's direct parent.
    /// </summary>
    internal static class EmissionDimmer
    {
        private static readonly string[] EmissionColors = { "_EmissionColor", "_EmissiveColor" };

        // Dark copy → the material it was made from, and back. Restoring needs no memory of which renderer was dimmed:
        // any dark copy found on a lamp's renderer is swapped back for its original.
        private static readonly Dictionary<Material, Material> darkCopies = new Dictionary<Material, Material>();
        private static readonly Dictionary<Material, Material> originals = new Dictionary<Material, Material>();

        /// <summary>Idempotent: safe to call on every refresh.</summary>
        public static void Apply(LightController light, bool powered)
        {
            Transform root = Root(light);
            if (root == null || root.GetComponentInChildren<LightTurnOffOn>(true) != null)
                return; // switchable lamps are handled by LampVisuals
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                ApplyRenderer(renderer, powered);
        }

        /// <summary>
        /// Every renderer under <paramref name="root"/>, switchable lamps included: dims what still glows (lit = false) or puts the
        /// originals back (lit = true). For meshes a lamp's switch doesn't cover, e.g. the Wall Light's second bulb.
        /// </summary>
        public static void ApplyAll(Transform root, bool lit)
        {
            if (root == null)
                return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                ApplyRenderer(renderer, lit);
        }

        private static void ApplyRenderer(Renderer renderer, bool powered)
        {
            if (renderer == null)
                return;
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material m = materials[i];
                if (m == null)
                    continue;
                if (powered && originals.TryGetValue(m, out Material original))
                {
                    materials[i] = original;
                    changed = true;
                }
                else if (!powered && !originals.ContainsKey(m) && Glows(m))
                {
                    materials[i] = Dark(m);
                    changed = true;
                }
            }
            if (changed)
                renderer.sharedMaterials = materials;
        }

        private static Transform Root(LightController light)
        {
            FurniturePlaceable placed = light.GetComponentInParent<FurniturePlaceable>();
            if (placed != null)
                return placed.transform;
            return light.transform.parent != null ? light.transform.parent : light.transform;
        }

        private static bool Glows(Material m)
        {
            if (m == null)
                return false;
            foreach (string property in EmissionColors)
                if (m.HasProperty(property) && m.GetColor(property).maxColorComponent > 0.01f)
                    return m.IsKeywordEnabled("_EMISSION") || property == "_EmissiveColor";
            return false;
        }

        private static Material Dark(Material original)
        {
            if (darkCopies.TryGetValue(original, out Material dark) && dark != null)
                return dark;
            dark = new Material(original) { name = original.name + " (UNI off)" };
            foreach (string property in EmissionColors)
                if (dark.HasProperty(property))
                    dark.SetColor(property, Color.black);
            dark.DisableKeyword("_EMISSION");
            darkCopies[original] = dark;
            originals[dark] = original;
            return dark;
        }
    }
}
