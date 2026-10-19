using UnityEngine;

namespace UtilitiesNotIncluded
{
    /// <summary>
    /// Test hotkey, unbound by default (<c>[Testing] PowerCutKey</c>): cuts the building's grid power or brings it back
    /// (generators keep running). Nothing is saved: the power cut ends with a reload.
    /// </summary>
    internal static class DevKeys
    {
        /// <summary>The building grid is cut by the power cut hotkey.</summary>
        public static bool PowerCut { get; private set; }

        public static void Update()
        {
            if (!Plugin.On)
                return;
            if (Pressed(Plugin.PowerCutKey.Value))
            {
                PowerCut = !PowerCut;
                Notify(PowerCut ? "Power cut" : "Power back");
                Refresher.Request(0f);
            }
        }

        private static bool Pressed(KeyCode key) => key != KeyCode.None && Input.GetKeyDown(key);

        public static void Reset() => PowerCut = false;

        // A one-second label at the top centre of the screen, instead of the game's notifications.
        private const float PopupSeconds = 1f;
        private static string popup;
        private static float popupUntil;
        private static GUIStyle style;

        private static void Notify(string text)
        {
            Plugin.Log.LogInfo(text);
            popup = text;
            popupUntil = Time.unscaledTime + PopupSeconds;
        }

        /// <summary>Called from <see cref="Refresher"/>'s OnGUI.</summary>
        public static void OnGUI()
        {
            if (popup == null || Time.unscaledTime >= popupUntil)
                return;
            if (style == null)
                style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(16, 16, 8, 8) };
            Vector2 size = style.CalcSize(new GUIContent(popup));
            GUI.Box(new Rect((Screen.width - size.x) / 2f, Screen.height * 0.12f, size.x, size.y), popup, style);
        }
    }
}
