using System.Collections.Generic;
using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Electric lights on Shit power flicker: now and then a short burst where the light jumps between dim and full.
    /// The fault is in the supply, not the wiring, so all of them flicker together on one schedule.
    /// <c>LightController.Update</c> sets the light's intensity every frame, so the dimming is applied on top of it in LateUpdate
    /// and never saved. Which lights flicker is decided by the refresh (<see cref="Refresher"/>), not polled.
    /// </summary>
    internal sealed class Flicker : MonoBehaviour
    {
        private sealed class State
        {
            public float Base, Applied = -1f;
        }

        // The shared schedule.
        private static float nextBurst = -1f, burstEnd, nextStep;
        private static float factor = 1f;

        private static readonly Dictionary<LightController, State> lights = new Dictionary<LightController, State>();
        private static readonly List<LightController> dead = new List<LightController>();

        /// <summary>Called by the refresh for every electric light: whether it should flicker now.</summary>
        public static void Set(LightController light, bool flicker)
        {
            if (flicker && Plugin.On && Plugin.ShitLightsFlicker.Value)
            {
                if (!lights.ContainsKey(light))
                    lights[light] = new State();
                return;
            }
            if (lights.TryGetValue(light, out State state))
            {
                Restore(light, state);
                lights.Remove(light);
            }
        }

        public static int Count => lights.Count;

        /// <summary>After a setting change: the next burst comes on the new schedule, not one planned with the old frequency.</summary>
        public static void Reschedule()
        {
            nextBurst = Mathf.Min(nextBurst, Time.time + NextGap());
        }

        private static float NextGap() => Random.Range(0.2f, 1.8f) * 60f / Plugin.FlickersPerMinute.Value;

        private void LateUpdate()
        {
            if (lights.Count == 0)
                return;
            Step(Time.time);
            foreach (KeyValuePair<LightController, State> kv in lights)
            {
                LightController controller = kv.Key;
                if (controller == null)
                {
                    dead.Add(controller);
                    continue;
                }
                Light light = controller.cullLight;
                if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                    continue;
                State s = kv.Value;

                // The game resets the intensity each frame it differs from its target; if it didn't, the value is still ours.
                float current = light.intensity;
                s.Base = s.Applied >= 0f && Mathf.Approximately(current, s.Applied) ? s.Base : current;
                s.Applied = s.Base * factor;
                light.intensity = s.Applied;
            }
            foreach (LightController c in dead)
                lights.Remove(c);
            dead.Clear();
        }

        private static void Step(float now)
        {
            if (nextBurst < 0f)
                nextBurst = now + NextGap();
            if (now < burstEnd)
            {
                if (now >= nextStep)
                {
                    nextStep = now + Random.Range(0.5f, 1.5f) / Plugin.FlickerSpeed.Value;
                    factor = Random.value < 0.5f ? 1f - Plugin.FlickerDepth.Value * Random.Range(0.6f, 1f) : 1f;
                }
                return;
            }
            factor = 1f;
            if (now >= nextBurst)
            {
                burstEnd = now + Plugin.FlickerBurstSeconds.Value * Random.Range(1f / 6f, 1f);
                nextStep = now;
                nextBurst = burstEnd + NextGap();
            }
        }

        private static void Restore(LightController controller, State s)
        {
            Light light = controller != null ? controller.cullLight : null;
            if (light != null && s.Applied >= 0f && Mathf.Approximately(light.intensity, s.Applied))
                light.intensity = s.Base;
        }
    }
}
