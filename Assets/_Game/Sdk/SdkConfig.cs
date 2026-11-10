using UnityEngine;

namespace StickyPeerSort.Sdk
{
    public enum SdkEnvironment { Sandbox, Production }

    // Every third-party ID in one place. Fill it in the Inspector (Assets/_Game/Sdk/Resources/SdkConfig.asset).
    // On save and before every build the editor copies the MAX key and Facebook IDs into the SDKs' own settings
    // (AppLovinSettings, FacebookSettings + AndroidManifest), so this asset is the only place to edit.
    [CreateAssetMenu(fileName = "SdkConfig", menuName = "Sticky Peer Sort/SDK Config")]
    public sealed class SdkConfig : ScriptableObject
    {
        public const string ResourcePath = "SdkConfig";

        [Header("AppLovin MAX")]
        [Tooltip("SDK Key from the AppLovin dashboard (Account > Keys).")]
        public string maxSdkKey = "";
        [Tooltip("Extra MAX logs in Logcat/Xcode. Turn off for release builds.")]
        public bool maxVerboseLogging;

        [Header("AppLovin MAX ad units")]
        [Tooltip("Banner Ad Unit ID from the MAX dashboard (Monetize > Ad Units).")]
        public string maxBannerAdUnitId = "";
        [Tooltip("Interstitial Ad Unit ID from the MAX dashboard.")]
        public string maxInterstitialAdUnitId = "";
        [Tooltip("Rewarded Ad Unit ID from the MAX dashboard.")]
        public string maxRewardedAdUnitId = "";

        [Header("Facebook")]
        public string facebookAppId = "";
        public string facebookClientToken = "";

        [Header("Adjust")]
        public string adjustAppToken = "";
        [Tooltip("Sandbox while testing, Production for the store build.")]
        public SdkEnvironment adjustEnvironment = SdkEnvironment.Sandbox;
        [Tooltip("Verbose Adjust logs (only used in Sandbox).")]
        public bool adjustVerboseLogging = true;

        [Header("Startup")]
        [Tooltip("Initialise the SDKs when running in the Unity Editor too (they only log there).")]
        public bool initializeInEditor;

        public bool HasMax => !string.IsNullOrWhiteSpace(maxSdkKey);
        public bool HasFacebook => !string.IsNullOrWhiteSpace(facebookAppId) && !string.IsNullOrWhiteSpace(facebookClientToken);
        // Trimmed ad unit IDs, ready for MaxSdk.CreateBanner / LoadInterstitial / LoadRewardedAd.
        public string BannerAdUnitId => (maxBannerAdUnitId ?? "").Trim();
        public string InterstitialAdUnitId => (maxInterstitialAdUnitId ?? "").Trim();
        public string RewardedAdUnitId => (maxRewardedAdUnitId ?? "").Trim();
        public bool HasAdjust => !string.IsNullOrWhiteSpace(adjustAppToken);

        public static SdkConfig Load() => Resources.Load<SdkConfig>(ResourcePath);
    }
}
