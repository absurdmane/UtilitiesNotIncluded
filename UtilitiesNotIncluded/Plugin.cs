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
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<PowerTier> IndustrialRequiredPowerLevel;
        internal static ConfigEntry<float> ShitElectricityQuality;
        internal static ConfigEntry<bool> LightsNeedPower;
        internal static ConfigEntry<string> ExtraElectricLights;
        internal static ConfigEntry<string> NonElectricLights;
        internal static ConfigEntry<bool> FridgesNeedPower;
        internal static ConfigEntry<bool> StairwellOutlets;
        internal static ConfigEntry<bool> ShitLightsFlicker;
        internal static ConfigEntry<bool> FixLampGlow;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> GlowingFurniture;
        internal static ConfigEntry<float> FlickersPerMinute;
        internal static ConfigEntry<float> FlickerDepth;
        internal static ConfigEntry<float> FlickerBurstSeconds;
        internal static ConfigEntry<float> FlickerSpeed;

        internal static ConfigEntry<bool> GeneratorPowersDevices;
        internal static ConfigEntry<PowerTier> GeneratorPowerLevel;

        internal static ConfigEntry<bool> OutagesEnabled;
        internal static ConfigEntry<float> OutagesPerDay;
        internal static ConfigEntry<float> OutageMinHoursBetween;

        internal static ConfigEntry<bool> WaterEnabled;
        internal static ConfigEntry<BestWaterKind> BestWater;

        private void Awake()
        {
            Log = Logger;

            IndustrialRequiredPowerLevel = Config.Bind("Electricity", "IndustrialRequiredPowerLevel", PowerTier.Best,
                "Power tier industrial devices (stations with the Industrial condition) need to work at all. Shit = no extra requirement.");
            ShitElectricityQuality = Config.Bind("Electricity", "ShitElectricityQuality", 0.85f,
                new ConfigDescription("Equipment quality multiplier for electric devices on Shit power (unstable voltage). 1 = no debuff.",
                    new AcceptableValueRange<float>(0.1f, 1f)));
            LightsNeedPower = Config.Bind("Electricity", "LightsNeedPower", true,
                "Electric lamps go off without power. Candles and fire are never affected.");
            ExtraElectricLights = Config.Bind("Electricity", "ExtraElectricLights", "lamp_001, Fluorescent_light, elevator_001_light, lamp_mushroom, desk_lamp_, decoration_lights",
                "Lights the game doesn't mark as electric but are: comma-separated name parts, matched (any case) against the light and " +
                "its 3 parents. Default: the stairwell lamps, all fluorescent lights, the elevator lights, mushroom and desk lamps, decoration lights.");
            NonElectricLights = Config.Bind("Electricity", "NonElectricLights", "lantern, mushrooms, Barrelstove, candle, oil_lamp",
                "Lights the game marks as electric but aren't: same matching as ExtraElectricLights, and wins over it. " +
                "Default: kerosene lanterns (shroom lanterns too), glowing mushrooms (not the electric mushroom lamps), the barrel stove (fire), candles, oil lamps.");
            FridgesNeedPower = Config.Bind("Electricity", "FridgesNeedPower", true,
                "Fridges and freezers don't cool without power.");
            StairwellOutlets = Config.Bind("Electricity", "StairwellOutlets", false,
                "Devices in the stairwell get building power. Lights there always do.");
            GlowingFurniture = Config.Bind("Electricity", "GlowingFurniture", "Decoration Lights, decoration_lights",
                "Electric furniture that glows without a light source (comma-separated name parts, any case): goes dark without power. " +
                "Default: the decoration light garlands (all sizes).");
            FixLampGlow = Config.Bind("Electricity", "FixLampGlow", true,
                "A lamp switched off in the game stops glowing on every mesh, not only the ones its switch knows about " +
                "(the Wall Light's second bulb stays lit in vanilla). Visual only.");
            ShitLightsFlicker = Config.Bind("Electricity", "ShitLightsFlicker", true,
                "Electric lights on Shit power flicker now and then (unstable voltage). Visual only.");
            FlickersPerMinute = Config.Bind("Electricity", "FlickersPerMinute", 8f,
                new ConfigDescription("Average flicker bursts per minute (all lights flicker together).", new AcceptableValueRange<float>(0.5f, 60f)));
            FlickerDepth = Config.Bind("Electricity", "FlickerDepth", 0.15f,
                new ConfigDescription("How far a flicker dims the light: 1 = goes fully dark.", new AcceptableValueRange<float>(0.1f, 1f)));
            FlickerBurstSeconds = Config.Bind("Electricity", "FlickerBurstSeconds", 0.6f,
                new ConfigDescription("Longest flicker burst, seconds (each burst lasts from a sixth of this to all of it).",
                    new AcceptableValueRange<float>(0.1f, 5f)));
            FlickerSpeed = Config.Bind("Electricity", "FlickerSpeed", 30f,
                new ConfigDescription("Brightness changes per second during a burst.", new AcceptableValueRange<float>(2f, 60f)));

            GeneratorPowersDevices = Config.Bind("Generator", "PowersDevices", true,
                "A running Mini Generator powers all electric devices and lights in its room (the game's furniture area: apartment, shop, basement, mine, ...).");
            GeneratorPowerLevel = Config.Bind("Generator", "PowerLevel", PowerTier.Shit,
                "Power tier a generator provides.");

            OutagesEnabled = Config.Bind("Outages", "Enabled", true,
                "With Shit building electricity the breaker trips now and then: the building grid goes dead until you reset the breaker " +
                "at the electrical panel in the basement. Generators keep running.");
            OutagesPerDay = Config.Bind("Outages", "PerDay", 1f,
                new ConfigDescription("Average outages per in-game day (random times).", new AcceptableValueRange<float>(0.1f, 10f)));
            OutageMinHoursBetween = Config.Bind("Outages", "MinHoursBetween", 6f,
                new ConfigDescription("At least this many in-game hours between a reset and the next outage.", new AcceptableValueRange<float>(0f, 48f)));

            WaterEnabled = Config.Bind("Water", "Enabled", true,
                "Taps in the building give the water of their room's upgrade level; no upgrade = no water.");
            BestWater = Config.Bind("Water", "BestWater", BestWaterKind.Pure,
                "Water from the Best water upgrade.");

            Config.SettingChanged += (_, e) =>
            {
                if (e.ChangedSetting.Definition.Key.StartsWith("Flicker"))
                    Flicker.Reschedule();
                Refresher.Request();
            };
            Enabled = Config.Bind("Mod", "Enabled", true,
                "Off = the mod does nothing at all: no patches, everything as in vanilla (e.g. to compare performance). Needs a game restart.");
            if (!Enabled.Value)
            {
                Log.LogInfo($"{Name} {Version} is off ([Mod] Enabled = false)");
                return;
            }

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            gameObject.AddComponent<Refresher>();
            gameObject.AddComponent<Flicker>();
            Log.LogInfo($"{Name} {Version} loaded");
        }
    }
}
