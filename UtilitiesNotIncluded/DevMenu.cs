using System.ComponentModel;

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
            return $"{levels}\nRunning generators: {Utilities.RunningGeneratorCount}\n{Outages.Status()}\nRefresh: {Refresher.Summary}";
        }

        [Description("Refresh power now (devices, fridges, lamps)")]
        public static void RefreshPower() => Refresher.Request(0f);

        [Description("Outage: trip the breaker now (Shit electricity only)")]
        public static void TripBreaker() => Outages.Trip();

        [Description("Outage: reset the breaker (as at the basement panel)")]
        public static void ResetBreaker() => Outages.Reset();
    }
}
