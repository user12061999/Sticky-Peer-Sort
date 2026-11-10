using System;
using AdjustSdk;
using Facebook.Unity;
using UnityEngine;

namespace StickyPeerSort.Sdk
{
    // Starts Adjust, Facebook and AppLovin MAX once, before the first scene loads. Start-up only:
    // no ads are loaded or shown here; the ad logic (banner, interstitial, rewarded) is added on top of this.
    // An SDK whose ID is empty in SdkConfig is skipped with a warning, so the game always runs.
    public sealed class SdkBootstrap : MonoBehaviour
    {
        public static bool MaxReady { get; private set; }
        public static bool FacebookReady { get; private set; }
        public static bool AdjustStarted { get; private set; }
        // Raised once MAX has finished initialising (ad units can be loaded from here on).
        public static event Action<MaxSdkBase.SdkConfiguration> MaxInitialized;

        private static SdkBootstrap instance;
        private SdkConfig config;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (instance) return;
            var config = SdkConfig.Load();
            if (!config) { Debug.LogWarning("[SDK] SdkConfig asset not found in Resources; SDKs are not started."); return; }
            if (Application.isEditor && !config.initializeInEditor) return;
            var go = new GameObject("SdkBootstrap");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SdkBootstrap>();
            instance.config = config;
            instance.StartAll();
        }

        private void StartAll()
        {
            StartAdjust();
            StartFacebook();
            StartMax();
        }

        private void StartAdjust()
        {
            if (!config.HasAdjust) { Debug.LogWarning("[SDK] Adjust app token is empty; Adjust skipped."); return; }
            var production = config.adjustEnvironment == SdkEnvironment.Production;
            // Production allows the Suppress level, so a store build writes no Adjust logs at all.
            var adjustConfig = new AdjustConfig(config.adjustAppToken.Trim(), production ? AdjustEnvironment.Production : AdjustEnvironment.Sandbox, production);
            adjustConfig.LogLevel = production ? AdjustLogLevel.Suppress : (config.adjustVerboseLogging ? AdjustLogLevel.Verbose : AdjustLogLevel.Info);
            Adjust.InitSdk(adjustConfig);
            AdjustStarted = true;
        }

        private void StartFacebook()
        {
            if (!config.HasFacebook) { Debug.LogWarning("[SDK] Facebook App ID / Client Token is empty; Facebook skipped."); return; }
            if (FB.IsInitialized) { FB.ActivateApp(); FacebookReady = true; return; }
            FB.Init(() =>
            {
                FacebookReady = FB.IsInitialized;
                if (FacebookReady) FB.ActivateApp();
                else Debug.LogWarning("[SDK] Facebook failed to initialise.");
            });
        }

        private void StartMax()
        {
            if (!config.HasMax) { Debug.LogWarning("[SDK] MAX SDK key is empty; AppLovin MAX skipped."); return; }
            MaxSdkCallbacks.OnSdkInitializedEvent += OnMaxInitialized;
            MaxSdk.SetVerboseLogging(config.maxVerboseLogging);
            MaxSdk.InitializeSdk();
        }

        private static void OnMaxInitialized(MaxSdkBase.SdkConfiguration sdkConfiguration)
        {
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnMaxInitialized;
            MaxReady = true;
            MaxInitialized?.Invoke(sdkConfiguration);
        }

        // Facebook asks to log app activation on every resume, not only at launch.
        private void OnApplicationPause(bool paused)
        {
            if (!paused && FacebookReady && FB.IsInitialized) FB.ActivateApp();
        }
    }
}
