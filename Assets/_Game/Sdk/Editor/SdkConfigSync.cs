using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StickyPeerSort.Sdk.Editor
{
    // Copies SdkConfig into the SDKs' own settings: the MAX key into AppLovinSettings and the Facebook IDs into
    // FacebookSettings (then regenerates the Facebook AndroidManifest). Runs before every build, when the config
    // asset is saved, and from the menu. The SDK editor types are reached by reflection, so this assembly does not
    // depend on how each vendor packages its editor code.
    public sealed class SdkConfigSync : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) => Apply(true);

        [MenuItem("Sticky Peer Sort/SDK/Open SDK Config")]
        public static void Open()
        {
            var config = FindOrCreate();
            Selection.activeObject = config; EditorGUIUtility.PingObject(config);
        }

        [MenuItem("Sticky Peer Sort/SDK/Apply SDK Config")]
        public static void ApplyFromMenu() => Apply(true);

        public static SdkConfig FindOrCreate()
        {
            var config = SdkConfig.Load();
            if (config) return config;
            const string folder = "Assets/_Game/Sdk/Resources";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Game/Sdk", "Resources");
            config = ScriptableObject.CreateInstance<SdkConfig>();
            AssetDatabase.CreateAsset(config, folder + "/" + SdkConfig.ResourcePath + ".asset");
            AssetDatabase.SaveAssets();
            return config;
        }

        public static void Apply(bool log)
        {
            var config = SdkConfig.Load();
            if (!config) { Debug.LogWarning("[SDK] No SdkConfig asset; nothing to apply."); return; }
            var notes = new List<string>();
            ApplyMax(config, notes);
            ApplyFacebook(config, notes);
            if (!config.HasAdjust) notes.Add("Adjust app token is empty (Adjust will be skipped)");
            if (config.adjustEnvironment == SdkEnvironment.Sandbox && !EditorUserBuildSettings.development)
                notes.Add("Adjust is in Sandbox: switch to Production for the store build");
            AssetDatabase.SaveAssets();
            if (log) Debug.Log("[SDK] Config applied." + (notes.Count > 0 ? " Notes: " + string.Join("; ", notes) : ""));
        }

        private static void ApplyMax(SdkConfig config, List<string> notes)
        {
            var type = FindType("AppLovinSettings");
            var settings = type?.GetProperty("Instance")?.GetValue(null) as ScriptableObject;
            if (!settings) { notes.Add("AppLovinSettings not found"); return; }
            EnsureAsset(settings, "Assets/MaxSdk/Resources", "AppLovinSettings");
            var key = type.GetProperty("SdkKey");
            var wanted = (config.maxSdkKey ?? "").Trim();
            if ((string)key.GetValue(settings) != wanted) { key.SetValue(settings, wanted); EditorUtility.SetDirty(settings); }
            if (!config.HasMax) notes.Add("MAX SDK key is empty (MAX will be skipped)");
        }

        private static void ApplyFacebook(SdkConfig config, List<string> notes)
        {
            var type = FindType("Facebook.Unity.Settings.FacebookSettings");
            var settings = type?.GetProperty("Instance")?.GetValue(null) as ScriptableObject;
            if (!settings) { notes.Add("FacebookSettings not found"); return; }
            EnsureAsset(settings, "Assets/FacebookSDK/SDK/Resources", "FacebookSettings");
            var appId = (config.facebookAppId ?? "").Trim();
            var token = (config.facebookClientToken ?? "").Trim();
            var idsProperty = type.GetProperty("AppIds"); var tokensProperty = type.GetProperty("ClientTokens");
            object Owner(System.Reflection.PropertyInfo p) => p.GetGetMethod().IsStatic ? null : settings;
            var ids = (List<string>)idsProperty.GetValue(Owner(idsProperty)) ?? new List<string>();
            var tokens = (List<string>)tokensProperty.GetValue(Owner(tokensProperty)) ?? new List<string>();
            bool changed = ids.Count != 1 || ids[0] != appId || tokens.Count != 1 || tokens[0] != token;
            if (changed)
            {
                idsProperty.SetValue(Owner(idsProperty), new List<string> { appId });
                tokensProperty.SetValue(Owner(tokensProperty), new List<string> { token });
                EditorUtility.SetDirty(settings);
            }
            if (!config.HasFacebook) { notes.Add("Facebook App ID / Client Token is empty (Facebook will be skipped)"); return; }
            // The Android manifest carries the app ID; regenerate it whenever the IDs are set.
            FindType("Facebook.Unity.Editor.ManifestMod")?.GetMethod("GenerateManifest")?.Invoke(null, null);
        }

        // A vendor settings object can exist only in memory until its own window saves it; give it its asset file.
        private static void EnsureAsset(ScriptableObject settings, string folder, string name)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(settings))) return;
            var parts = folder.Split('/'); var path = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = path + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
            AssetDatabase.CreateAsset(settings, folder + "/" + name + ".asset");
        }

        private static Type FindType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name);
                if (type != null) return type;
            }
            return null;
        }
    }

    // Re-applies whenever the config asset is saved, so the SDK settings never drift from it.
    public sealed class SdkConfigSaveWatcher : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var path in paths)
                if (path.EndsWith("/" + SdkConfig.ResourcePath + ".asset", StringComparison.Ordinal))
                { EditorApplication.delayCall += () => SdkConfigSync.Apply(false); break; }
            return paths;
        }
    }
}
