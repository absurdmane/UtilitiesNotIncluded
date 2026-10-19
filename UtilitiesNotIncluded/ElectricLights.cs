using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Which lights are electric: a whitelist. The game's <c>electricLight</c> flag is set on fire, candles, lanterns, glowing mushrooms,
    /// windows and NPC glow too, so it isn't used. Anything not listed keeps working as in vanilla.
    /// A light in placed furniture is decided by the furniture alone (its asset name or title); a light built into the level or an upgrade
    /// prefab by the exact name of its object or one of its 3 parents, without "(Clone)" and " (n)".
    /// </summary>
    internal static class ElectricLights
    {
        /// <summary>Furniture (asset name or title) and built-in lamp objects. Built from the light dumps of 16.10.2026.</summary>
        private static readonly string[] Defaults =
        {
            // furniture, category Lights
            "Arc Floor Lamp", "Bankers Lamp", "Beer Lamp", "Ceiling Lamp Chain", "Ceiling Light", "Floor Lamp",
            "Gradient Paper Ceiling Light", "Mushroom Lamp Blue", "Mushroom Lamp Green", "Mushroom Lamp Orange", "Mushroom Lamp Red",
            "Orange Lava Lamp", "Plank Ceiling Light", "Red Desk Lamp", "Table Lamp", "Wall Light",
            // garlands: no light source, their meshes glow (GlowingFurniture)
            "Decoration Lights Small", "Decoration Lights Medium", "Decoration Lights Large",
            // built into the level and upgrade prefabs
            "lamp_001", "lamp_006_ON", "lamp_006_ON_red", "lamp_008_toggle", "lamp_009", "lamp_009_ON", "light_002",
            "ceiling_light_001_toggle", "ceiling_light_001_toggle_b", "Fluorescent_light_001", "Fluorescent_light_001_blue1_toggle",
            "elevator_001_light"
        };

        /// <summary>
        /// Lamp lights that hang bare in an upgrade prefab, next to their tube mesh instead of under it: the end of the light's path
        /// ("(Clone)" removed) → the tube's name somewhere under the path's first part (null: nothing to dim). Read from the upgrade prefabs in the game
        /// files (19.10.2026): a Fluorescent_light_001 mesh 5-12 cm from the light.
        /// </summary>
        private static readonly Dictionary<string, string> BareLamps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Upgrade_STORE_Walls_(lvl 4) - Fine/Point Light (4)"] = "Fluorescent_light_001",
            ["Upgrade_STORE_Walls_(lvl 4) - Fine/Point Light (5)"] = "Fluorescent_light_001 (1)",
            ["Upgrade_STORE_Walls_(lvl 4) - Fine/Point Light (6)"] = "Fluorescent_light_001 (2)",
            ["Upgrade_STORE_Walls_(lvl 4) - Fine/Point Light (7)"] = "Fluorescent_light_001 (3)",
            ["Upgrade_STORE_Walls_(lvl 4) - Fine/Point Light (8)"] = "Fluorescent_light_001 (5)",
            ["Upgrade_STORE_Walls_(lvl 4) - Fine/Point Light (9)"] = "Fluorescent_light_001 (6)",
            ["Upgrade_STORE_Walls_(lvl 4) - Fine/Point Light (10)"] = "Fluorescent_light_001 (4)",
            ["Upgrade_STORE_Walls_(lvl 4) - Fine/Point Light (11)"] = "Fluorescent_light_001 (7)",
            ["Upgrade_Greenhouse_(lvl 3) - Fine/Point Light (1)"] = "Fluorescent_light_001 (7)",
            ["Upgrade_Greenhouse_(lvl 3) - Fine/Point Light (2)"] = "Fluorescent_light_001 (9)",
            ["Upgrade_PLAYER_Kitchen_(lvl 3) - Decent/Point Light"] = "Fluorescent_light_001",
            ["Upgrade_PLAYER_Kitchen_(lvl 3) - Decent/Point Light (1)"] = "Fluorescent_light_001 (1)",
            ["Upgrade_PLAYER_Kitchen_(lvl 4) - Fine/Point Light"] = "Fluorescent_light_001",
            ["Upgrade_PLAYER_Kitchen_(lvl 4) - Fine/Point Light (1)"] = "Fluorescent_light_001 (1)",
            ["Upgrade_NPC_LARGE_Kitchen_(lvl 3) - Decent/Default/Point Light"] = "Fluorescent_light_001",
            ["Upgrade_NPC_LARGE_Kitchen_(lvl 3) - Decent/Default/Point Light (1)"] = "Fluorescent_light_001 (1)",
            ["Upgrade_NPC_LARGE_Kitchen_(lvl 4) - Fine/Default/Point Light"] = "Fluorescent_light_001",
            ["Upgrade_NPC_LARGE_Kitchen_(lvl 4) - Fine/Default/Point Light (1)"] = "Fluorescent_light_001 (1)",
            ["Upgrade_NPC_MEDIUM_Kitchen_(lvl 3) - Decent/Default/Point Light"] = "Fluorescent_light_001",
            ["Upgrade_NPC_MEDIUM_Kitchen_(lvl 3) - Decent/Default/Point Light (1)"] = "Fluorescent_light_001 (1)",
            ["Upgrade_NPC_MEDIUM_Kitchen_(lvl 4) - Fine/Default/Point Light (2)"] = "Fluorescent_light_001 (3)",
            ["Upgrade_NPC_MEDIUM_Kitchen_(lvl 4) - Fine/Default/Point Light (3)"] = "Fluorescent_light_001 (2)",
            ["Upgrade_NPC_SMALL_Kitchen_(lvl 3) - Decent/Default/Point Light (2)"] = "Fluorescent_light_001 (2)",
            ["Upgrade_NPC_SMALL_Kitchen_(lvl 4) - Fine/Default/Point Light (3)"] = "Fluorescent_light_001 (2)",
            ["Upgrade_NPC_SMALL_Kitchen_(lvl 4) - Fine/Default/Point Light (4)"] = "Fluorescent_light_001 (3)",
            // 0.8-1.5 m from an electric lamp, no other fixture near (19.10.2026; to be confirmed in game: goes dark without power?)
            ["Upgrade_STORE_Walls_(lvl 4) - Fine/Point Light (1)"] = "Ceiling_Shop",
            ["Upgrade_Basement_Walls_(lvl 4) - Fine/Main/Point Light (2)"] = "Fluorescent_light_001_blue1_toggle",
            ["Upgrade_Greenhouse_(lvl 3) - Fine/Point Light (17)"] = "Fluorescent_light_001 (9)",
            ["Upgrade_Greenhouse_(lvl 3) - Fine/Point Light (22)"] = "Fluorescent_light_001 (11)",
            ["Upgrade_PLAYER_WC_(lvl 3) - Decent/Point Light"] = "ceiling_light_001_s 2",
            ["Upgrade_PLAYER_WC_(lvl 3) - Decent/bathroom_cabinet_001_storage/Point Light"] = "ceiling_light_001_s 2",
            ["Upgrade_NPC_LARGE_WC_(lvl 3) - Decent/Default/bathroom_cabinet_001_storage/Point Light"] = "ceiling_light_001_s 2",
            ["Upgrade_NPC_MEDIUM_WC_(lvl 3) - Decent/Default/bathroom_cabinet_001_storage/Point Light"] = "ceiling_light_001_s",
            ["Upgrade_NPC_SMALL_WC_(lvl 3) - Decent/Default/bathroom_cabinet_001_storage/Point Light"] = "ceiling_light_001_s 2",
            ["Upgrade_NPC_LARGE_BalconyFront_(lvl 4) - Fine/Default/Cube (14)/Point Light (4)"] = "lamp_009 (5)",
            // Decorative terrarium of the tenants' furniture (Malone): not a device, its lamp is still electric. No tube to dim.
            ["Furniture/Terrarium/Volumetric Spot Light"] = null,
            ["Furniture/Terrarium/Volumetric Spot Light (1)"] = null,
            ["Furniture/Terrarium/Volumetric Spot Light (2)"] = null,
            ["rotate/aquarium_001_usable/Roof/Point Light"] = null
        };

        // Light → its tube (null: none found), per object.
        private static readonly Dictionary<int, Transform> tubes = new Dictionary<int, Transform>();

        /// <summary>The tube mesh of a bare lamp light, or null: the meshes to dim instead of the light's parent (the whole room).</summary>
        public static Transform TubeOf(Transform t)
        {
            int id = t.GetInstanceID();
            if (!tubes.TryGetValue(id, out Transform tube))
            {
                tube = BareLamp(t, out _, out Transform root, out string name) && name != null ? Find(root, name) : null;
                tubes[id] = tube;
            }
            return tube;
        }

        public static bool IsBareLamp(Transform t) => BareLamp(t, out _, out _, out _);

        // The entry that ends this light's path, and the transform its first part names.
        private static bool BareLamp(Transform t, out string path, out Transform root, out string tube)
        {
            path = t.name.Replace("(Clone)", "").Trim();
            root = t;
            for (int i = 0; i < 3 && root.parent != null; i++)
            {
                root = root.parent;
                path = root.name.Replace("(Clone)", "").Trim() + "/" + path;
                if (BareLamps.TryGetValue(path, out tube))
                    return true;
            }
            tube = null;
            return false;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                    return child;
            return null;
        }

        private static readonly Regex Suffix = new Regex(@"\s*\(\d+\)$");

        private static HashSet<string> listed, removed;
        private static string listedFrom, removedFrom;

        // Per object: names don't change, so the answer holds until the lists change or the scene is left.
        private static readonly Dictionary<int, bool> cache = new Dictionary<int, bool>();

        public static void ClearCache()
        {
            cache.Clear();
            tubes.Clear();
        }

        /// <summary>Whether the light (or switchable lamp) at <paramref name="t"/> is electric.</summary>
        public static bool IsElectric(Transform t)
        {
            if (t == null)
                return false;
            int id = t.GetInstanceID();
            if (!cache.TryGetValue(id, out bool result))
            {
                result = Decide(t, out _);
                cache[id] = result;
            }
            return result;
        }

        /// <summary>The answer and the name it was decided by (for the dump).</summary>
        public static bool Decide(Transform t, out string key)
        {
            UpdateLists();
            if (BareLamp(t, out string path, out _, out _))
            {
                key = path;
                return !removed.Contains(path);
            }
            FurniturePlaceable placed = t.GetComponentInParent<FurniturePlaceable>();
            if (placed != null)
            {
                Furniture furniture = placed.furniture;
                string asset = furniture != null ? furniture.name : Normalize(placed.name);
                string title = furniture != null ? furniture.title : null;
                key = asset;
                return Matches(asset) || Matches(title);
            }
            key = null;
            for (int i = 0; i <= 3 && t != null; i++, t = t.parent)
            {
                string name = Normalize(t.name);
                if (Matches(name))
                {
                    key = name;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether placed furniture is on the list (for furniture that glows without a light source).</summary>
        public static bool IsElectricFurniture(FurniturePlaceable placed)
        {
            UpdateLists();
            Furniture furniture = placed.furniture;
            return furniture != null ? Matches(furniture.name) || Matches(furniture.title) : Matches(Normalize(placed.name));
        }

        /// <summary>Whether a furniture or object name is on the list (exact, any case).</summary>
        public static bool IsListed(string name)
        {
            UpdateLists();
            return Matches(name);
        }

        public static string Normalize(string name) => Suffix.Replace(name.Replace("(Clone)", ""), "").Trim();

        private static bool Matches(string name) =>
            !string.IsNullOrEmpty(name) && listed.Contains(name) && !removed.Contains(name);

        private static void UpdateLists()
        {
            string add = Plugin.AddLights.Value, remove = Plugin.RemoveLights.Value;
            if (listed != null && add == listedFrom && remove == removedFrom)
                return;
            listed = new HashSet<string>(Defaults, StringComparer.OrdinalIgnoreCase);
            listed.UnionWith(Split(add));
            removed = new HashSet<string>(Split(remove), StringComparer.OrdinalIgnoreCase);
            listedFrom = add;
            removedFrom = remove;
            cache.Clear();
        }

        private static IEnumerable<string> Split(string list)
        {
            foreach (string raw in (list ?? "").Split(','))
            {
                string name = raw.Trim();
                if (name.Length > 0)
                    yield return name;
            }
        }
    }
}
