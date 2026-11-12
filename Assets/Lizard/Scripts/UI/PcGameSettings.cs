using UnityEngine;

namespace Lizards
{
    public static class PcGameSettings
    {
        const string Prefix = "Lizards.PC.";
        public static float FieldOfView => Get("Fov", 60f);
        public static float Sensitivity => Get("Sensitivity", 1f);
        public static bool InvertY => Get("Invert", 0) == 1;
        public static bool CameraShake => Get("Shake", 1) == 1;
        public static bool PlayerNames => Get("Names", 1) == 1;
        public static float EffectsVolume => Get("Effects", 1f);

        public static int Get(string key, int fallback) => PlayerPrefs.GetInt(Prefix + key, fallback);
        public static float Get(string key, float fallback) => PlayerPrefs.GetFloat(Prefix + key, fallback);

        public static void Set(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            PlayerPrefs.Save();
        }

        public static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            PlayerPrefs.Save();
        }

        public static void Apply()
        {
            AudioListener.volume = Get("Volume", 1f);
            QualitySettings.vSyncCount = Get("VSync", 1);
            Application.targetFrameRate = Get("Fps", 120);
            if (Application.isEditor) return;
            var mode = (FullScreenMode)Get("Window", (int)FullScreenMode.FullScreenWindow);
            Screen.SetResolution(Get("Width", Screen.width), Get("Height", Screen.height), mode);
        }

        public static void Reset()
        {
            foreach (string key in new[] { "Fov", "Sensitivity", "Invert", "Shake", "Names", "Effects", "Volume", "VSync", "Fps", "Window", "Width", "Height", "AutoCamera", "Occlusion" })
                PlayerPrefs.DeleteKey(Prefix + key);
            PlayerPrefs.Save();
            Apply();
        }
    }
}
