using System.Collections.Generic;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Stand-in water sources, one per liquid. Each is an infinite <see cref="LiquidStorage"/> on an inactive GameObject in the scene:
    /// inactive objects are skipped by the save system, and filling is driven by the liquid panel UI, not by the source itself.
    /// </summary>
    internal static class WaterSources
    {
        private const int DirtyWater = 12980;
        private const int CleanWater = 10840;
        private const int PureWater = 15970;
        private const int SpringWater = 16200;

        private static readonly Dictionary<int, LiquidStorage> sources = new Dictionary<int, LiquidStorage>();
        private static readonly HashSet<LiquidStorage> ours = new HashSet<LiquidStorage>();

        public static bool IsOurs(LiquidStorage storage) => ours.Contains(storage);

        public static LiquidStorage For(WaterLevel level, LiquidStorage global)
        {
            int liquid = LiquidFor(level);
            if (liquid > 0 && global.Liquid != null && global.Liquid.ID == liquid)
                return global; // the scene already gives this water: keep its exact meta
            if (sources.TryGetValue(liquid, out LiquidStorage source) && source != null)
                return source;
            source = Create(liquid, global);
            sources[liquid] = source;
            return source;
        }

        /// <summary>The mod unloads: taps pour from the game's sources again (the patch is gone), so ours go.</summary>
        public static void Unload()
        {
            foreach (LiquidStorage source in ours)
                if (source != null)
                    Object.Destroy(source.gameObject);
            ours.Clear();
            sources.Clear();
        }

        private static int LiquidFor(WaterLevel level)
        {
            switch (level)
            {
                case WaterLevel.Dirty:
                    return DirtyWater;
                case WaterLevel.Good:
                    return CleanWater;
                case WaterLevel.Best:
                    switch (Plugin.BestWater.Value)
                    {
                        case BestWaterKind.Clean:
                            return CleanWater;
                        case BestWaterKind.Spring:
                            return SpringWater;
                        default:
                            return PureWater;
                    }
                default:
                    return 0; // no water
            }
        }

        private static LiquidStorage Create(int liquid, LiquidStorage global)
        {
            var go = new GameObject($"UtilitiesNotIncluded water {liquid}");
            go.SetActive(false);
            LiquidStorage source = go.AddComponent<LiquidStorage>();
            source.title = global.title;
            source.NotPartOfMainSource = true;
            source.StorageOwner = global.StorageOwner;
            source.PermissionToTake = global.PermissionToTake;
            if (liquid > 0)
            {
                source.Capacity = -1f; // infinite
                source.SetLiquid(new ItemReference(liquid));
            }
            else
            {
                source.Capacity = 0f; // empty: filling reports Result.Empty
            }
            ours.Add(source);
            return source;
        }
    }
}
