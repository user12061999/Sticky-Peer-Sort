using UnityEngine;

namespace StickyPeerSort
{
    // Spec "Peeling and feedback": small, amplitude-controlled pulses (Android VibrationEffect, API 26+).
    // Handheld.Vibrate is a ~400 ms buzz and is only the fallback for devices without amplitude control.
    public static class GameHaptics
    {
        const float MinGap = .04f;
        static float lastPulse = -1;
        public static bool Enabled = true;
        public static int Pulses { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static bool ready, amplitude;
        static int sdk;
        static void Init()
        {
            if (ready) return; ready = true;
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                amplitude = sdk >= 26 && vibrator != null && vibrator.Call<bool>("hasAmplitudeControl");
            }
            catch (System.Exception e) { Debug.LogWarning("Haptics unavailable: " + e.Message); vibrator = null; }
        }
        static void OneShot(int ms, int amp)
        {
            Init(); if (vibrator == null) return;
            if (sdk >= 26)
                using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var shot = effect.CallStatic<AndroidJavaObject>("createOneShot", (long)ms, amplitude ? Mathf.Clamp(amp, 1, 255) : -1))
                    vibrator.Call("vibrate", shot);
            else vibrator.Call("vibrate", (long)ms);
        }
#else
        static void OneShot(int ms, int amp) { }
#endif

        // force: skip the 40 ms anti-spam gap (used for the single completion pulse).
        public static void Pulse(int ms, int amplitude, bool force = false)
        {
            if (!Enabled) return;
            if (!force && Time.unscaledTime - lastPulse < MinGap) return;
            lastPulse = Time.unscaledTime; Pulses++;
            OneShot(ms, amplitude);
        }
    }
}
