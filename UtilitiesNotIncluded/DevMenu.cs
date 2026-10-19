using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Buttons for the dev-only DevMenu menu, which finds this class by name. Nothing here runs without it,
    /// and neither mod references the other.
    /// </summary>
    internal static class DevMenu
    {
        public static string Status()
        {
            TenementController tc = TenementController.instance;
            if (tc == null || tc.general == null)
                return "Not in the Tenement.";
            string levels = $"Building: electricity {tc.general.currentElectricity}, water {tc.general.currentWater}";
            foreach (int n in new[] { 1, 2 })
            {
                TenementApartment a = tc.GetApartmentFromNumber(n);
                if (a != null)
                    levels += $"\n{(n == 1 ? "Player apartment" : "Shop")}: electricity {a.currentElectricity}, water {a.currentWater}";
            }
            levels += $"\nTest hotkey: {Plugin.PowerCutKey.Value} power cut ({(DevKeys.PowerCut ? "CUT" : "off")})";
            return $"{levels}\nRunning generators: {Utilities.RunningGeneratorCount}\n{Outages.Status()}\nRefresh: {Refresher.Summary}";
        }

        [Description("Refresh power now (devices, fridges, lamps)")]
        public static void RefreshPower() => Refresher.Request(0f);

        [Description("Outage: trip the breaker now (Shit electricity only)")]
        public static void TripBreaker() => Outages.Trip();

        [Description("Outage: reset the breaker (as at the basement panel)")]
        public static void ResetBreaker() => Outages.Reset();

        private static string DumpPath => Path.Combine(Paths.BepInExRootPath, "UtilitiesNotIncluded", "electric-dump.txt");

        [Description("Dump lights and fridges (BepInEx/UtilitiesNotIncluded/electric-dump.txt)")]
        public static void DumpElectric()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Electric dump {System.DateTime.Now}, scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            sb.AppendLine("Lights are grouped by what identifies them: furniture by its asset name (title), a built-in light by its object and 3 parents.");
            sb.AppendLine("listed = on the electric list (goes off without power); flag = the game's electricLight.");

            // ---- lights: LightController and switchable lamps ----
            var groups = new SortedDictionary<string, (bool listed, string key, int count, HashSet<string> flags, HashSet<string> rooms, string path)>();
            void Add(UnityEngine.Component c, string flag)
            {
                bool listed = ElectricLights.Decide(c.transform, out string key);
                string id = Identity(c.transform);
                groups.TryGetValue(id, out var g);
                if (g.flags == null)
                    g = (listed, key, 0, new HashSet<string>(), new HashSet<string>(), FullPath(c.transform));
                g.count++;
                g.flags.Add(flag);
                g.rooms.Add(Utilities.RoomOf(c.gameObject).Kind.ToString());
                groups[id] = g;
            }
            foreach (LightController light in Object.FindObjectsByType<LightController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Add(light, light.electricLight ? "flag" : "no flag");
            foreach (LightTurnOffOn lamp in Object.FindObjectsByType<LightTurnOffOn>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Add(lamp, "switch");
            foreach (bool listed in new[] { true, false })
            {
                sb.AppendLine();
                sb.AppendLine(listed ? "===== Lights: listed =====" : "===== Lights: not listed (vanilla) =====");
                foreach (var kv in groups.Where(kv => kv.Value.listed == listed))
                    sb.AppendLine($"{kv.Value.count,3}  {kv.Key}{(kv.Value.key != null ? $"  [by {kv.Value.key}]" : "")}  " +
                                  $"{string.Join("/", kv.Value.flags)}  rooms={string.Join("/", kv.Value.rooms)}  e.g. {kv.Value.path}");
            }

            // ---- furniture on the list without a light (glows by its meshes) ----
            sb.AppendLine();
            sb.AppendLine("===== Listed furniture without a light source (dimmed meshes) =====");
            foreach (FurniturePlaceable f in Object.FindObjectsByType<FurniturePlaceable>(FindObjectsSortMode.None))
                if (ElectricLights.IsElectricFurniture(f) && f.GetComponentInChildren<LightController>(true) == null
                    && f.GetComponentInChildren<LightTurnOffOn>(true) == null)
                    sb.AppendLine($"{FurnitureName(f)}  room={Utilities.RoomOf(f.gameObject).Kind}  {FullPath(f.transform)}");

            // ---- fridges ----
            sb.AppendLine();
            sb.AppendLine("===== Storages with refrigeration =====");
            foreach (Storage s in Object.FindObjectsByType<Storage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s.refigerationFactor > 0f)
                {
                    FurniturePlaceable f = s.GetComponentInParent<FurniturePlaceable>();
                    sb.AppendLine($"{(Utilities.IsElectricFridge(s) ? "electric" : "no power needed")}  settings='{s.StorageSettings}'  " +
                                  $"factor={s.refigerationFactor}  furniture={(f != null ? FurnitureName(f) : "-")}  " +
                                  $"room={Utilities.RoomOf(s.gameObject).Kind}  {FullPath(s.transform)}");
                }

            // ---- the catalog: loaded furniture of category Lights ----
            sb.AppendLine();
            sb.AppendLine("===== Loaded furniture assets, category Lights =====");
            foreach (Furniture f in Resources.FindObjectsOfTypeAll<Furniture>().Where(f => f.category == Furniture.Category.Lights)
                         .GroupBy(f => f.name).Select(g => g.First()).OrderBy(f => f.name))
                sb.AppendLine($"{(ElectricLights.IsListed(f.name) || ElectricLights.IsListed(f.title) ? "listed    " : "not listed")}  {f.name} ({f.title})");

            Directory.CreateDirectory(Path.GetDirectoryName(DumpPath));
            File.WriteAllText(DumpPath, sb.ToString());
            Plugin.Log.LogInfo($"Electric dump written to {DumpPath}");
        }

        private static string FurnitureName(FurniturePlaceable f) =>
            f.furniture != null ? $"{f.furniture.name} ({f.furniture.title})" : ElectricLights.Normalize(f.name);

        // Furniture: its name. Built-in: the light's object and 3 parents, normalized, so copies of one lamp group together.
        private static string Identity(Transform t)
        {
            FurniturePlaceable f = t.GetComponentInParent<FurniturePlaceable>();
            if (f != null)
                return "furniture " + FurnitureName(f);
            var names = new List<string>();
            for (int i = 0; i <= 3 && t != null; i++, t = t.parent)
                names.Add(ElectricLights.Normalize(t.name));
            return "built-in " + string.Join(" < ", names);
        }

        private static string FullPath(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
