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
        // Dark copies by name: the build mode copies a renderer's materials ("<name> (Instance)") and puts the copies back later,
        // so a dark copy can come back as an instance of itself.
        private static readonly Dictionary<string, Material> darkByName = new Dictionary<string, Material>();
        private const string InstanceSuffix = " (Instance)";

        /// <summary>Idempotent: safe to call on every refresh.</summary>
        public static void Apply(LightController light, bool powered)
        {
            Transform root = Root(light);
            if (root == null || root.GetComponentInChildren<LightTurnOffOn>(true) != null)
                return; // switchable lamps are handled by LampVisuals
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (powered || !SpawnedBelow(renderer.transform, root))
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
                if (lit || !SpawnedBelow(renderer.transform, root))
                    ApplyRenderer(renderer, lit);
        }

        /// <summary>
        /// Something the game spawned into the lamp or device, e.g. mushrooms planted in a terrarium: it glows by itself and is never
        /// dimmed (putting the original back is always allowed).
        /// </summary>
        private static bool SpawnedBelow(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (t.name.Contains("(Clone)"))
                    return true;
            return false;
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
                if (powered && (originals.TryGetValue(m, out Material original) || InstanceOfDark(m, out original)))
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

        /// <summary>
        /// The mod unloads: every dark copy still on a renderer (unpowered lamps, meshes dimmed for a switched-off lamp) goes back to
        /// its original, then the copies are destroyed.
        /// </summary>
        public static void Unload()
        {
            if (originals.Count > 0)
                foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    ApplyRenderer(renderer, powered: true);
            foreach (Material dark in originals.Keys)
                if (dark != null)
                    Object.Destroy(dark);
            originals.Clear();
            darkCopies.Clear();
            darkByName.Clear();
        }

        private static Transform Root(LightController light)
        {
            // A bare lamp light's parent is the whole room: only its tube, or nothing if the tube isn't there.
            if (ElectricLights.IsBareLamp(light.transform))
                return ElectricLights.TubeOf(light.transform);
            // A device's light: only the lamp around it (e.g. the terrarium's roof), not the whole device.
            if (Utilities.DeviceOf(light) != null)
                return light.transform.parent != null ? light.transform.parent : light.transform;
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

        private static bool InstanceOfDark(Material m, out Material original)
        {
            original = null;
            return m.name.EndsWith(InstanceSuffix)
                && darkByName.TryGetValue(m.name.Substring(0, m.name.Length - InstanceSuffix.Length), out Material dark)
                && originals.TryGetValue(dark, out original);
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
            darkByName[dark.name] = dark;
            return dark;
        }
    }
}
