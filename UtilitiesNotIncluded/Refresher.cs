using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Re-evaluates power for devices, fridges and lamps when something that affects it happens. No polling.
    /// Triggers: a level finished loading (also covers finished upgrades — paying for one means leaving the Tenement),
    /// furniture placed or taken, a generator starting or stopping, a config change.
    /// The refresh runs a moment after the trigger, so freshly spawned devices have run their own Start first.
    /// After a load: two frames later, while the loading screen still covers the scene, plus a safety pass a second later
    /// for objects that finish initializing in coroutines (it changes nothing if all is already applied).
    /// </summary>
    internal sealed class Refresher : MonoBehaviour
    {
        private const int AfterLoadFrames = 2;
        private const float SafetyPassDelay = 1f;
        private const float AfterChangeDelay = 0.1f;

        private static Refresher instance;

        private float refreshAt = -1f;
        private int refreshFrame = -1;
        private readonly HashSet<FurnitureManager> subscribed = new HashSet<FurnitureManager>();

        // Last power state we applied. Missing = vanilla (powered), which is what the game assumes at Start.
        private readonly Dictionary<CraftingBase, bool> deviceState = new Dictionary<CraftingBase, bool>();
        private readonly Dictionary<Storage, bool> fridgeState = new Dictionary<Storage, bool>();
        private readonly Dictionary<LightController, bool> lightState = new Dictionary<LightController, bool>();
        private readonly Dictionary<LightTurnOffOn, bool> lampState = new Dictionary<LightTurnOffOn, bool>();

        /// <summary>For the dev menu: what the last refresh found.</summary>
        public static string Summary => instance == null
            ? "not running"
            : $"last refresh {(instance.lastRefresh < 0f ? "never" : $"{Time.unscaledTime - instance.lastRefresh:0}s ago")}, " +
              $"devices unpowered {instance.deviceState.Count(kv => !kv.Value)}/{instance.deviceState.Count}, " +
              $"fridges off {instance.fridgeState.Count(kv => !kv.Value)}, lamps off {instance.lightState.Count(kv => !kv.Value)}, glowing furniture off {GlowingFurniture.Off}, flickering {Flicker.Count}";

        private float lastRefresh = -1f;

        /// <summary>Ask for a refresh. Several requests in a row collapse into one.</summary>
        public static void Request(float delay = AfterChangeDelay)
        {
            if (instance == null)
                return;
            float at = Time.unscaledTime + delay;
            if (instance.refreshAt < 0f || at > instance.refreshAt)
                instance.refreshAt = at;
        }

        private void Awake()
        {
            instance = this;
            SaveController.LoadingDone += OnLoadingDone;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TimeOfDayAzure.SecondsPassed += Outages.OnSecondsPassed;
        }

        private void OnDestroy()
        {
            SaveController.LoadingDone -= OnLoadingDone;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            TimeOfDayAzure.SecondsPassed -= Outages.OnSecondsPassed;
        }

        // Rooms cached for the old scene are useless; devices of the new one start before LoadingDone.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Utilities.ClearCache();

        private void OnLoadingDone()
        {
            Utilities.ClearCache();
            Outages.Load();
            deviceState.Clear();
            fridgeState.Clear();
            lightState.Clear();
            lampState.Clear();
            GlowingFurniture.Reset();
            subscribed.Clear(); // the old scene's managers are gone
            refreshFrame = Time.frameCount + AfterLoadFrames;
            refreshAt = Time.unscaledTime + SafetyPassDelay;
        }

        private static void OnFurnitureChanged(GameObject _)
        {
            Utilities.ClearCache();
            Request();
        }

        private void Update()
        {
            bool frameDue = refreshFrame >= 0 && Time.frameCount >= refreshFrame;
            bool timeDue = refreshAt >= 0f && Time.unscaledTime >= refreshAt;
            if (!frameDue && !timeDue)
                return;
            if (frameDue)
                refreshFrame = -1;
            if (timeDue)
                refreshAt = -1f;
            try
            {
                Refresh();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Refresh failed: {e}");
            }
        }

        private void Refresh()
        {
            lastRefresh = Time.unscaledTime;
            Utilities.ClearCache();
            if (TenementController.instance == null || TenementController.instance.general == null)
                return;

            SubscribeToManagers();
            Outages.OnRefresh();
            CraftingBase[] devices = FindObjectsByType<CraftingBase>(FindObjectsSortMode.None);
            Utilities.SetRunningGenerators(devices
                .OfType<Generator>()
                .Where(g => g.PoweredAndActive && Utilities.IsMiniGenerator(g)));

            RefreshDevices(devices);
            RefreshFridges();
            RefreshLights();
        }

        private void SubscribeToManagers()
        {
            foreach (FurnitureManager m in FindObjectsByType<FurnitureManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!subscribed.Add(m))
                    continue;
                m.FurniturePlaced += OnFurnitureChanged;
                m.FurnitureTaken += OnFurnitureChanged;
            }
        }

        private void RefreshDevices(CraftingBase[] devices)
        {
            foreach (CraftingBase device in devices)
            {
                if (!Utilities.Handles(device))
                    continue;
                bool has = Utilities.DeviceHasPower(device, out _, out _);
                bool had = !deviceState.TryGetValue(device, out bool last) || last;
                deviceState[device] = has;
                if (has == had || !device.On || !device._Power.PowerInput)
                    continue;
                // The same path as the panel's power button: relays, sounds, the button's look, pausing/resuming the process.
                if (has)
                    device.OnSetPowerOn();
                else
                    device.OnSetPowerOff();
            }
        }

        private void RefreshFridges()
        {
            if (!Plugin.FridgesNeedPower.Value && fridgeState.Count == 0)
                return;
            foreach (Storage fridge in FindObjectsByType<Storage>(FindObjectsSortMode.None))
            {
                if (fridge.refigerationFactor <= 0f)
                    continue;
                bool has = !Patches.FridgeUnpowered(fridge);
                bool had = !fridgeState.TryGetValue(fridge, out bool last) || last;
                fridgeState[fridge] = has;
                // Spoilage reads the factor cached in the slots, so push the new value there.
                if (has != had)
                    fridge.UpdateRefigeration(fridge.RefigerationFactor);
            }
        }

        private void RefreshLights()
        {
            GlowingFurniture.Refresh();
            if (!Plugin.LightsNeedPower.Value && lightState.Count == 0 && lampState.Count == 0)
                return;
            // Switchable lamps first: restoring one shows its light objects, which the pass below then keeps off if unpowered.
            foreach (LightTurnOffOn lamp in FindObjectsByType<LightTurnOffOn>(FindObjectsSortMode.None))
            {
                bool has = !LampVisuals.Unpowered(lamp);
                bool had = !lampState.TryGetValue(lamp, out bool last) || last;
                lampState[lamp] = has;
                if (has != had)
                    LampVisuals.Apply(lamp, has);
            }
            foreach (LightController light in FindObjectsByType<LightController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!Utilities.IsElectric(light))
                    continue;
                bool has = light.disabled || light.moving || !Patches.LightUnpowered(light);
                Flicker.Set(light, has && !light.disabled && Plugin.LightsNeedPower.Value && Utilities.RoomOf(light.gameObject).InBuilding
                    && Utilities.Power(light.gameObject, isLight: true).Tier == PowerTier.Shit);
                // The glow is set on every refresh (idempotent), so a lamp can't stay dark by a missed change.
                EmissionDimmer.Apply(light, has);
                if (light.disabled || light.moving)
                    continue;
                bool had = !lightState.TryGetValue(light, out bool last) || last;
                lightState[light] = has;
                if (has == had)
                    continue;
                // Re-apply the game's own zone status; our Override prefix forces it off while unpowered.
                LightsController.controllerstatus.TryGetValue(light, out bool hidden);
                light.Override(hidden);
            }
        }
    }
}
