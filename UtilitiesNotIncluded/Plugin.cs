using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;

namespace UtilitiesNotIncluded
{
    /// <summary>Which water a Best water upgrade gives from the tap.</summary>
    public enum BestWaterKind
    {
        Clean,
        Pure,
        Spring
    }

    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "obenseuer.absurdmane.utilitiesnotincluded";
        public const string Name = "Utilities Not Included";
        public const string Version = "1.1.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<PowerTier> IndustrialRequiredPowerLevel;
        internal static ConfigEntry<float> ShitElectricityQuality;
        internal static ConfigEntry<bool> LightsNeedPower;
        internal static ConfigEntry<string> AddLights;
        internal static ConfigEntry<string> RemoveLights;
        internal static ConfigEntry<UnityEngine.KeyCode> PowerCutKey;
        internal static ConfigEntry<bool> FridgesNeedPower;
        internal static ConfigEntry<bool> StairwellOutlets;
        internal static ConfigEntry<bool> ShitLightsFlicker;
        internal static ConfigEntry<bool> Enabled;

        internal static ConfigEntry<bool> GeneratorPowersDevices;
        internal static ConfigEntry<PowerTier> GeneratorPowerLevel;

        internal static ConfigEntry<bool> OutagesEnabled;
        internal static ConfigEntry<float> OutagesPerDay;

        internal static ConfigEntry<bool> WaterEnabled;
        internal static ConfigEntry<BestWaterKind> BestWater;

        private void Awake()
        {
            Log = Logger;

            IndustrialRequiredPowerLevel = Config.Bind("Devices", "IndustrialRequiredPowerLevel", PowerTier.Best,
                "Power tier industrial devices (stations with the Industrial condition) need to work at all. Shit = no extra requirement.");
            ShitElectricityQuality = Config.Bind("Devices", "ShitElectricityQuality", 0.85f,
                new ConfigDescription("Equipment quality multiplier for electric devices on Shit power (unstable voltage). 1 = no debuff.",
                    new AcceptableValueRange<float>(0.1f, 1f)));
            LightsNeedPower = Config.Bind("Lights", "NeedPower", true,
                "Electric lights go off without power: lamps and decoration light garlands on the mod's list (see AddLights, RemoveLights). Candles, fire and lanterns aren't on it.");
            AddLights = Config.Bind("Lights", "AddLights", "",
                "Lights to treat as electric in addition to the mod's own list: comma-separated exact names (any case). " +
                "Furniture by its name; a lamp built into the building by its object's name.");
            RemoveLights = Config.Bind("Lights", "RemoveLights", "",
                "Lights from the mod's own list that should keep working without power: same names as AddLights.");
            FridgesNeedPower = Config.Bind("Fridges", "NeedPower", true,
                "Fridges and freezers don't cool without power.");
            StairwellOutlets = Config.Bind("Devices", "StairwellOutlets", false,
                "Devices in the stairwell get building power. Lights there always do.");
            ShitLightsFlicker = Config.Bind("Lights", "Flicker", true,
                "Electric lights on Shit power flicker now and then (unstable voltage). Visual only.");

            GeneratorPowersDevices = Config.Bind("Generator", "PowersDevices", true,
                "A running Mini Generator powers all electric devices and lights in its room (the game's furniture area: apartment, shop, basement, mine, ...).");
            GeneratorPowerLevel = Config.Bind("Generator", "PowerLevel", PowerTier.Shit,
                "Power tier a generator provides.");

            OutagesEnabled = Config.Bind("Outages", "Enabled", true,
                "With Shit building electricity the breaker trips now and then: the building grid goes dead until you reset the breaker " +
                "at the electrical panel in the basement. Generators keep running.");
            OutagesPerDay = Config.Bind("Outages", "PerDay", 1f,
                new ConfigDescription("Average outages per in-game day (random times).", new AcceptableValueRange<float>(0.1f, 10f)));

            WaterEnabled = Config.Bind("Water", "Enabled", true,
                "Taps in the building give the water of their room's upgrade level; no upgrade = no water.");
            BestWater = Config.Bind("Water", "BestWater", BestWaterKind.Pure,
                "Water from the Best water upgrade.");

            PowerCutKey = Config.Bind("Testing", "PowerCutKey", UnityEngine.KeyCode.None,
                "Key that cuts the building's grid power or brings it back, to see what works without electricity (generators keep " +
                "running). Not saved: power is back after loading a save. None = off.");

            Enabled = Config.Bind("Mod", "Enabled", true,
                "Off = the mod does nothing: everything goes back to vanilla and its patches are removed (e.g. to compare performance). " +
                "Works without a restart.");
            Config.SettingChanged += OnSettingChanged;

            harmony = new Harmony(Guid);
            refresher = gameObject.AddComponent<Refresher>();
            flicker = gameObject.AddComponent<Flicker>();
            if (Enabled.Value)
            {
                harmony.PatchAll(typeof(Plugin).Assembly);
                LampVisuals.AddToExisting();
            }
            // Loaded mid-game (DevMenu's script reload): the save's outage state, and power for what's already in the scene.
            if (TenementController.instance != null && !SaveController.Loading)
            {
                Outages.Load();
                Refresher.Request(0f);
            }
            Log.LogInfo($"{Name} {Version} loaded{(Enabled.Value ? "" : ", off ([Mod] Enabled = false)")}");
        }

        private static Harmony harmony;
        private Refresher refresher;
        private Flicker flicker;

        /// <summary>Set while the mod unloads: everything answers as if it were off.</summary>
        internal static bool Unloading { get; private set; }

        /// <summary>The master switch. Every "is there power" answer is "yes" while off, so a refresh puts everything back to vanilla.</summary>
        internal static bool On => !Unloading && (Enabled == null || Enabled.Value);

        private void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (e.ChangedSetting == Enabled)
            {
                SetOn(Enabled.Value);
                return;
            }
            if (e.ChangedSetting == ShitLightsFlicker)
                Flicker.Reschedule();
            Refresher.Request();
        }

        // Undoes everything the mod did, so a fresh copy can be loaded next to it (DevMenu's script reload).
        // The components on this object may already be destroyed (any order), so nothing below relies on them being alive.
        private void OnDestroy()
        {
            Config.SettingChanged -= OnSettingChanged;
            Unloading = true;
            // As when switched off: restore while our patches are still in, then drop them.
            Refresher.Unload();
            harmony?.UnpatchSelf();
            harmony = null;
            Flicker.Unload();
            EmissionDimmer.Unload();
            BreakerSwitch.Unload();
            WaterSources.Unload();
            Utilities.ClearCache();
            ElectricLights.ClearCache();
            if (refresher != null)
                Destroy(refresher);
            if (flicker != null)
                Destroy(flicker);
        }

        private static bool patched => harmony != null && harmony.GetPatchedMethods().Any();

        private static void SetOn(bool on)
        {
            if (on)
            {
                if (!patched)
                    harmony.PatchAll(typeof(Plugin).Assembly);
                Refresher.Request(0f);
                Log.LogInfo($"{Name} on");
                return;
            }
            // Restore while our patches are still in (the lights' Override prefix, the fridges' getter), then drop them.
            Refresher.RestoreVanilla();
            harmony.UnpatchSelf();
            Log.LogInfo($"{Name} off: back to vanilla, patches removed");
        }
    }
}
