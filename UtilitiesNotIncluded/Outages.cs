using System;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Power outages on the cheapest grid. While the building has Shit electricity the breaker trips now and then: the building grid
    /// goes dead (devices, lamps, fridges), generators keep running. Power comes back only by hand, at the electrical panel in the
    /// basement. The state lives in our own file in the save folder, so it survives save/load and goes away with the save.
    /// </summary>
    [HarmonyPatch]
    internal static class Outages
    {
        private const string FileName = "UtilitiesNotIncluded.txt";
        private const ulong QuietAfterLoadMinutes = 10;

        /// <summary>The breaker is down: no grid power anywhere in the building.</summary>
        public static bool Active
        {
            get => active;
            private set
            {
                if (active != value)
                    Utilities.PoweredCache.Clear(); // stations see it at once, before the refresh it triggers
                active = value;
            }
        }

        private static bool active;

        private static ulong lastChange; // in-game minutes of the last trip / reset
        private static ulong loadedAt;

        private static ulong Now => TimeOfDayAzure.instance != null ? TimeOfDayAzure.instance.currentTimeAndDay.TotalMinutes : 0;

        private static bool OnShitGrid =>
            TenementController.instance != null && TenementController.instance.general != null && Utilities.BuildingTier == PowerTier.Shit;

        public static string Status()
        {
            if (!Plugin.OutagesEnabled.Value)
                return "Outages: off in the config";
            if (Active)
                return "Outages: the breaker is DOWN — reset it at the panel in the basement";
            if (!OnShitGrid)
                return "Outages: none (only on Shit electricity)";
            ulong since = Now >= lastChange ? (Now - lastChange) / 60 : 0;
            return $"Outages: power on, last trip/reset {since} h ago (next possible after {Plugin.OutageMinHoursBetween.Value} h)";
        }

        // ---- tripping ----

        /// <summary>Called every in-game second batch (<c>TimeOfDayAzure.SecondsPassed</c>).</summary>
        public static void OnSecondsPassed(int seconds)
        {
            if (!Plugin.On || !Plugin.OutagesEnabled.Value || Active || seconds <= 0 || !OnShitGrid || SaveController.Loading)
                return;
            ulong now = Now;
            if (now < loadedAt + QuietAfterLoadMinutes || now < lastChange + (ulong)(Plugin.OutageMinHoursBetween.Value * 60f))
                return;
            float chance = seconds * Plugin.OutagesPerDay.Value / 86400f;
            if (UnityEngine.Random.value < chance)
                Trip();
        }

        public static void Trip()
        {
            if (Active || !OnShitGrid)
                return;
            Active = true;
            lastChange = Now;
            Plugin.Log.LogInfo("Outage: the breaker tripped");
            Notifications.instance?.CreateNotification("Power outage", "The breaker tripped. Reset it at the electrical panel in the basement.");
            Refresher.Request(0f);
        }

        public static void Reset()
        {
            if (!Active)
                return;
            Active = false;
            lastChange = Now;
            Plugin.Log.LogInfo("Outage: the breaker was reset");
            Notifications.instance?.CreateNotification("Power restored", "The breaker is back on.");
            Refresher.Request(0f);
        }

        /// <summary>On every refresh: a better grid has its own panel, so an outage ends with the upgrade; the panel gets its switch.</summary>
        public static void OnRefresh()
        {
            if (Active && !OnShitGrid)
            {
                Active = false;
                lastChange = Now;
            }
            BreakerSwitch.Attach();
        }

        // ---- save / load: our own file in the save folder ----

        public static void Load()
        {
            Active = false;
            lastChange = Now;
            loadedAt = Now;
            string folder = SaveController.LastSaveFolderPath;
            if (string.IsNullOrEmpty(folder))
                return;
            string path = Path.Combine(folder, FileName);
            if (!File.Exists(path))
                return;
            try
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] kv = line.Split(new[] { '=' }, 2);
                    if (kv.Length != 2)
                        continue;
                    if (kv[0] == "outage")
                        Active = kv[1].Trim() == "true";
                    else if (kv[0] == "lastChange" && ulong.TryParse(kv[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong t))
                        lastChange = t;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Reading {path}: {e}");
            }
        }

        /// <summary>The game writes a save into Saves/&lt;character&gt;/&lt;save&gt;; ours goes next to its files.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveController), nameof(SaveController.SaveGame))]
        private static void SaveGamePostfix()
        {
            try
            {
                if (string.IsNullOrEmpty(SaveController.CharacterName) || string.IsNullOrEmpty(SaveController.SaveName))
                    return;
                string folder = Path.Combine(Application.persistentDataPath, "Saves", SaveController.CharacterName, SaveController.SaveName);
                if (!Directory.Exists(folder))
                    return;
                File.WriteAllLines(Path.Combine(folder, FileName), new[]
                {
                    "# Utilities Not Included: per-save state",
                    $"outage={(Active ? "true" : "false")}",
                    $"lastChange={lastChange.ToString(CultureInfo.InvariantCulture)}"
                });
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Writing the outage state: {e}");
            }
        }
    }

    /// <summary>
    /// The breaker on the basement's electrical panel (the Shit-level panel, <c>smallelectricalbox_*</c> in the spawned electricity prefab).
    /// A plain component with the game's interact interface, not an <see cref="Interactable"/>: that one is a SavableScript and would be saved.
    /// Usable only while the breaker is down.
    /// </summary>
    internal sealed class BreakerSwitch : MonoBehaviour, IInteractable
    {
        public static void Attach()
        {
            GameObject prefab = TenementController.instance?.general?.prefabs?.content?.currentElectricity;
            if (prefab == null)
                return;
            Transform panel = prefab.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.IndexOf("electricalbox", StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponent<Collider>() != null);
            if (panel == null || panel.GetComponent<BreakerSwitch>() != null)
                return;
            int usable = LayerMask.NameToLayer("Usable");
            if (usable >= 0)
                panel.gameObject.layer = usable; // the interact raycast only looks at usable layers
            panel.gameObject.AddComponent<BreakerSwitch>();
            Plugin.Log.LogInfo($"Breaker switch on {panel.name}");
        }

        public void Interact()
        {
            Outages.Reset();
            UseText.instance?.ChangeUseText("");
        }

        public IInteractable StopInteract(bool stopHold) => null;

        public void UpdateMoney()
        {
        }

        public void UpdateUseMessage() => UseText.instance?.ChangeUseText("Electrical panel", "(Reset the breaker)");

        public bool IsInteractDisabled() => !Outages.Active;

        public void SetInteractDisabled(bool value)
        {
        }
    }
}
