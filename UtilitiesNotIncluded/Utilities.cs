using System.Collections.Generic;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>Electricity tier. Matches <c>UpgradeType.TenementElectricity</c> (None, Shit, Good, Best).</summary>
    public enum PowerTier
    {
        None,
        Shit,
        Good,
        Best
    }

    /// <summary>Water level. Matches <c>UpgradeType.TenementWater</c> / <c>ApartmentWater</c> (None/Off, Dirty, Good, Best).</summary>
    internal enum WaterLevel
    {
        None,
        Dirty,
        Good,
        Best
    }

    internal enum RoomKind
    {
        /// <summary>Not in the building, or the room couldn't be resolved: vanilla behaviour.</summary>
        Outside,
        /// <summary>The player's apartment, or the shop when it has its own upgrade lines.</summary>
        Apartment,
        /// <summary>Basement, workshop, sauna, greenhouse, ...: building-wide upgrades only.</summary>
        Building,
        /// <summary>Lights on building power, no outlets.</summary>
        Stairwell
    }

    internal readonly struct Room
    {
        public readonly RoomKind Kind;
        public readonly TenementApartment Apartment;
        /// <summary>The game's furniture area (player apartment, shop, basement, mine, ...): a generator powers its whole area.</summary>
        public readonly FurnitureManager Manager;

        public Room(RoomKind kind, TenementApartment apartment = null, FurnitureManager manager = null)
        {
            Kind = kind;
            Apartment = apartment;
            Manager = manager;
        }

        public bool InBuilding => Kind != RoomKind.Outside;
    }

    /// <summary>What power a device or lamp gets, and why.</summary>
    internal readonly struct PowerInfo
    {
        public readonly PowerTier Tier;
        public readonly bool FromGenerator;
        public readonly string Reason;

        public PowerInfo(PowerTier tier, bool fromGenerator, string reason)
        {
            Tier = tier;
            FromGenerator = fromGenerator;
            Reason = reason;
        }
    }

    /// <summary>
    /// Room and utility levels of any object. Rooms and the devices' power answers are cached; <see cref="Refresher"/> drops the cache
    /// on every refresh (and everything that changes power triggers one). Upgrade levels are read live.
    /// </summary>
    internal static class Utilities
    {
        private static readonly Dictionary<int, Room> rooms = new Dictionary<int, Room>();
        private static readonly HashSet<FurnitureManager> poweredAreas = new HashSet<FurnitureManager>();
        private static Dictionary<GameObject, FurnitureManager> furnitureOwner;

        /// <summary>Power source -> unpowered by us, for the <c>Powered</c> getter. Cleared with the rest.</summary>
        public static readonly Dictionary<PowerSource, bool> PoweredCache = new Dictionary<PowerSource, bool>();

        public static void ClearCache()
        {
            rooms.Clear();
            PoweredCache.Clear();
            furnitureOwner = null;
        }

        public static int RunningGeneratorCount { get; private set; }

        /// <summary>
        /// The game's <c>electricLight</c> flag, or a name from <c>ExtraElectricLights</c> on the light or its 3 parents
        /// (the stairwell's lamp_001 lights are not flagged).
        /// </summary>
        public static bool IsElectric(LightController light)
        {
            if (Listed(light.transform, Plugin.NonElectricLights.Value))
                return false;
            return light.electricLight || Listed(light.transform, Plugin.ExtraElectricLights.Value);
        }

        /// <summary>Whether the object or one of its 3 parents has a name containing one of the comma-separated parts (any case).</summary>
        internal static bool Listed(Transform t, string list)
        {
            string[] names = list.Split(',');
            for (int i = 0; i <= 3 && t != null; i++, t = t.parent)
                foreach (string raw in names)
                {
                    string name = raw.Trim();
                    if (name.Length > 0 && t.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            return false;
        }

        /// <summary>
        /// Only the placeable Mini Generator powers things. Other <see cref="Generator"/> components exist in the scene too, e.g. the
        /// basement biogas compressor (General/BuildingArea Basement/Biogas/compressor), which is not furniture.
        /// </summary>
        public static bool IsMiniGenerator(Generator g)
        {
            FurniturePlaceable placed = g.GetComponentInParent<FurniturePlaceable>();
            if (placed == null)
                return false;
            string asset = placed.furniture != null ? placed.furniture.name : "";
            return asset.StartsWith("Mini Generator") || placed.name.StartsWith("Mini Generator");
        }

        /// <summary>The areas of the running Mini Generators: each powers its whole area.</summary>
        public static void SetRunningGenerators(IEnumerable<Generator> generators)
        {
            poweredAreas.Clear();
            RunningGeneratorCount = 0;
            foreach (Generator g in generators)
            {
                RunningGeneratorCount++;
                FurnitureManager area = RoomOf(g.gameObject).Manager;
                if (area != null)
                    poweredAreas.Add(area);
            }
        }

        // ---- rooms ----

        public static Room RoomOf(GameObject go)
        {
            if (go == null || TenementController.instance == null)
                return new Room(RoomKind.Outside);
            int id = go.GetInstanceID();
            if (!rooms.TryGetValue(id, out Room room))
            {
                room = Classify(ManagerOf(go));
                if (!room.InBuilding)
                    room = BuiltIn(go) ?? room;
                rooms[id] = room;
            }
            return room;
        }

        /// <summary>
        /// Things built into an upgrade prefab (e.g. the lamps of the walls upgrade: UPGRADES_Apartment_PLAYER/Upgrade_PLAYER_Walls_.../
        /// Clutter/Lights) have no furniture manager among their parents: the apartment's manager sits on a sibling branch.
        /// Inside an apartment's prefabs they belong to that apartment; inside the building's prefabs, to the building.
        /// </summary>
        private static Room? BuiltIn(GameObject go)
        {
            TenementApartmentPrefabs apartment = go.GetComponentInParent<TenementApartmentPrefabs>(true);
            if (apartment != null)
                return apartment.furnitureManager != null ? Classify(apartment.furnitureManager) : (Room?)null;
            TenementGeneralPrefabs general = go.GetComponentInParent<TenementGeneralPrefabs>(true);
            if (general == null)
                return null;
            // The outdoor parts (courtyard, facade) stay vanilla, e.g. their lamps.
            TenementGeneralPrefabsContent c = general.content;
            if (c != null && (Under(go, c.currentCourtyard) || Under(go, c.currentFacade) || Under(go, c.currentFacadeBlock)))
                return null;
            // Each building upgrade's prefab belongs to its area, so a generator there powers it too.
            if (c != null)
            {
                (GameObject root, FurnitureManager area)[] parts =
                {
                    (c.currentBasement, general.basementManager), (c.currentBasementBlock, general.basementManager),
                    (c.currentWorkshop, general.workshopManager), (c.currentSauna, general.saunaManager),
                    (c.currentSaunaAndPier, general.saunaAndPierManager), (c.currentShrooms, general.shroomsManager),
                    (c.currentMine, general.mineManager), (c.currentStairwell, general.stairwellManager),
                    (c.currentElevator, general.elevatorManager), (c.currentElectricity, general.electricityManager),
                    (c.currentWater, general.waterManager), (c.currentHeating, general.heatingManager),
                    (c.currentRoof, general.roofManager) // Fine = the greenhouse
                };
                foreach ((GameObject root, FurnitureManager area) in parts)
                    if (area != null && Under(go, root))
                        return Classify(area);
            }
            return new Room(RoomKind.Building);
        }

        private static bool Under(GameObject go, GameObject root) => root != null && go.transform.IsChildOf(root.transform);

        /// <summary>
        /// The building area an object belongs to: a parent <see cref="FurnitureManager"/>, otherwise the manager whose furniture lists
        /// hold one of its ancestors (placed furniture may not be parented to its manager).
        /// </summary>
        private static FurnitureManager ManagerOf(GameObject go)
        {
            FurnitureManager parent = go.GetComponentInParent<FurnitureManager>(true);
            if (parent != null)
                return parent;
            if (furnitureOwner == null)
                furnitureOwner = BuildFurnitureOwners();
            for (Transform t = go.transform; t != null; t = t.parent)
                if (furnitureOwner.TryGetValue(t.gameObject, out FurnitureManager m) && m != null)
                    return m;
            return null;
        }

        private static Dictionary<GameObject, FurnitureManager> BuildFurnitureOwners()
        {
            var owners = new Dictionary<GameObject, FurnitureManager>();
            foreach (FurnitureManager m in Object.FindObjectsByType<FurnitureManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (BuildingSystem.FurnitureInfo info in m.AllFurnitures)
                    if (info != null && info.gameObject != null)
                        owners[info.gameObject] = m;
            return owners;
        }

        private static Room Classify(FurnitureManager m)
        {
            if (m == null)
                return new Room(RoomKind.Outside);
            TenementController tc = TenementController.instance;

            if (m.tenementApartmentPrefabs != null)
            {
                TenementApartment apartment = tc.GetApartmentFromNumber(m.tenementApartmentPrefabs.apartmentNumber);
                if (apartment == null)
                    return new Room(RoomKind.Outside);
                if (apartment.apartmentNumber == 1)
                    return new Room(RoomKind.Apartment, apartment, m);
                if (apartment.apartmentNumber == 2) // the shop: own upgrades if it has upgrade lines, otherwise like the basement
                    return HasLine(apartment.type?.electricity) ? new Room(RoomKind.Apartment, apartment, m) : new Room(RoomKind.Building, null, m);
                // Tenants' apartments: like the player's, on their own upgrade lines (decided 2026-10-16).
                return HasLine(apartment.type?.electricity) ? new Room(RoomKind.Apartment, apartment, m) : new Room(RoomKind.Building, null, m);
            }

            TenementGeneralPrefabs general = tc.general?.prefabs;
            if (general != null)
            {
                if (m == general.stairwellManager)
                    return new Room(RoomKind.Stairwell, null, m);
                // Courtyard, facade: nothing can be placed there; left vanilla (Outside).
                if (m == general.courtyardManager || m == general.facadeManager)
                    return new Room(RoomKind.Outside);
                // The roof's top upgrade (Fine) is the greenhouse: a room on building power.
                if (m == general.roofManager)
                    return new Room(RoomKind.Building, null, m);
                if (m == general.basementManager || m == general.workshopManager || m == general.saunaManager
                    || m == general.saunaAndPierManager || m == general.electricityManager || m == general.waterManager
                    || m == general.heatingManager || m == general.elevatorManager || m == general.shroomsManager || m == general.mineManager)
                    return new Room(RoomKind.Building, null, m);
            }

            switch (m.area)
            {
                case Furniture.BuildingArea.Stairs:
                    return new Room(RoomKind.Stairwell, null, m);
                case Furniture.BuildingArea.Workshop:
                case Furniture.BuildingArea.Greenhouse:
                    return new Room(RoomKind.Building, null, m);
                default:
                    return new Room(RoomKind.Outside); // other scenes, the street, unknown areas: leave vanilla
            }
        }

        private static bool HasLine(TenementUpgradeGroup group) => group != null && group.updateGroup != null && group.updateGroup.Length > 0;

        // ---- power ----

        public static PowerTier BuildingTier => (PowerTier)(int)TenementController.instance.general.currentElectricity;

        /// <summary>Power from the building grid only, with the reason when there is none.</summary>
        public static PowerInfo GridPower(Room room, bool isLight)
        {
            if (Outages.Active && room.InBuilding)
                return new PowerInfo(PowerTier.None, false, "Power outage: reset the breaker at the electrical panel in the basement");
            switch (room.Kind)
            {
                case RoomKind.Apartment:
                    if (BuildingTier == PowerTier.None)
                        return new PowerInfo(PowerTier.None, false, "The building has no electricity");
                    if (room.Apartment.currentElectricity != UpgradeType.ApartmentElectricity.On)
                        return new PowerInfo(PowerTier.None, false,
                            room.Apartment.apartmentNumber == 2 ? "The shop has no electricity" : "The apartment has no electricity");
                    return new PowerInfo(BuildingTier, false, null);
                case RoomKind.Building:
                    return BuildingTier == PowerTier.None
                        ? new PowerInfo(PowerTier.None, false, "The building has no electricity")
                        : new PowerInfo(BuildingTier, false, null);
                case RoomKind.Stairwell:
                    if (BuildingTier == PowerTier.None)
                        return new PowerInfo(PowerTier.None, false, "The building has no electricity");
                    if (!isLight && !Plugin.StairwellOutlets.Value)
                        return new PowerInfo(PowerTier.None, false, "No outlets in the stairwell");
                    return new PowerInfo(BuildingTier, false, null);
                default:
                    return new PowerInfo(PowerTier.Best, false, null);
            }
        }

        /// <summary>The best power available: grid (room rules) or a running generator in range.</summary>
        public static PowerInfo Power(GameObject go, bool isLight)
        {
            PowerInfo grid = GridPower(RoomOf(go), isLight);
            if (!Plugin.GeneratorPowersDevices.Value || poweredAreas.Count == 0)
                return grid;
            PowerTier generatorTier = Plugin.GeneratorPowerLevel.Value;
            if (generatorTier <= grid.Tier)
                return grid;
            FurnitureManager area = RoomOf(go).Manager;
            if (area != null && poweredAreas.Contains(area))
                return new PowerInfo(generatorTier, true, null);
            return grid;
        }

        public static bool IsIndustrial(CraftingBase device) => device.ModifiersWithUpgrades.Contains(RecipeCondition.Industrial);

        /// <summary>Electric device (not fuel-burning) inside the building: the only kind this mod touches.</summary>
        public static bool Handles(CraftingBase device) =>
            device != null && !device.AlwaysOn && device._Power != null && device._Power.Type == PowerSource.PowerSourceType.Electricity
            && !device._Power.RequiresFuel && RoomOf(device.gameObject).InBuilding;

        /// <summary>Whether the device may run, and why not. Industrial devices need <see cref="Plugin.IndustrialRequiredPowerLevel"/>.</summary>
        public static bool DeviceHasPower(CraftingBase device, out PowerInfo power, out string reason)
        {
            power = Power(device.gameObject, isLight: false);
            reason = power.Reason;
            if (power.Tier == PowerTier.None)
            {
                reason = reason ?? "No power";
                return false;
            }
            PowerTier required = Plugin.IndustrialRequiredPowerLevel.Value;
            if (power.Tier < required && IsIndustrial(device))
            {
                reason = $"Industrial equipment needs {required} electricity";
                return false;
            }
            return true;
        }

        // ---- water ----

        public static WaterLevel Water(Room room)
        {
            var building = (WaterLevel)(int)TenementController.instance.general.currentWater;
            switch (room.Kind)
            {
                case RoomKind.Apartment:
                    var apartment = (WaterLevel)(int)room.Apartment.currentWater;
                    return apartment < building ? apartment : building;
                case RoomKind.Building:
                case RoomKind.Stairwell:
                    // No pipes in the mine (checked in game at its high upgrade levels).
                    if (room.Manager != null && room.Manager == TenementController.instance.general.prefabs?.mineManager)
                        return WaterLevel.None;
                    return building;
                default:
                    return WaterLevel.None;
            }
        }
    }
}
