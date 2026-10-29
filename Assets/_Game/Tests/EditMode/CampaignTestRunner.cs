using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace StickyPeerSort.Tests
{
    [InitializeOnLoad]
    public static class CampaignTestRunner
    {
        static TestRunnerApi api;
        const string PlayPending = "StickyPeerSort.CampaignPresentationTests";
        static CampaignTestRunner()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(PlayPending, false))
                { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results("Temp/CampaignPresentationTests.txt", true)); }
                const string playRequest = "Temp/TestCampaignPresentation.request";
                if (File.Exists(playRequest) && !EditorApplication.isPlayingOrWillChangePlaymode)
                { File.Delete(playRequest); RunPresentation(); return; }
                const string tutorialRequest = "Temp/TestTutorialGuides.request";
                if (File.Exists(tutorialRequest) && !EditorApplication.isPlayingOrWillChangePlaymode)
                { File.Delete(tutorialRequest); RunTutorials(); return; }
                const string request = "Temp/TestStickerCampaign.request";
                if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(request); Run();
            };
        }
        [MenuItem("Tools/Sticker Category Game/Test Campaign Balance")]
        public static void Run()
        {
            api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results("Temp/CampaignBalanceTests.txt"));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] {
                "StickyPeerSort.Tests.CampaignBalanceTests", "StickyPeerSort.Tests.LevelGenerationTests", "StickyPeerSort.Tests.LevelAnalysisTests" } }));
        }
        [MenuItem("Tools/Sticker Category Game/Test Campaign Presentation (Play Mode)")]
        public static void RunPresentation()
        {
            SessionState.SetBool(PlayPending, true);
            api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results("Temp/CampaignPresentationTests.txt", true));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { "StickyPeerSort.Tests.ReferenceStylePlayTests", "StickyPeerSort.Tests.TutorialGuidePlayTests" } }));
        }
        [MenuItem("Tools/Sticker Category Game/Test Tutorial Guides (Play Mode)")]
        public static void RunTutorials()
        {
            SessionState.SetBool(PlayPending, true);
            api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results("Temp/CampaignPresentationTests.txt", true));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { "StickyPeerSort.Tests.TutorialGuidePlayTests" } }));
        }
        sealed class Results : ICallbacks
        {
            readonly StringBuilder failures = new StringBuilder();
            readonly string path;
            readonly bool presentation;
            public Results(string path, bool presentation = false) { this.path = path; this.presentation = presentation; }
            public void RunStarted(ITestAdaptor test) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            { if (!result.HasChildren && result.FailCount > 0) failures.AppendLine(result.FullName + ": " + result.Message + "\n" + result.StackTrace); }
            public void RunFinished(ITestResultAdaptor result)
            {
                // A callback restored around a domain reload must never label another run's result.
                if (Matches(result))
                {
                    File.WriteAllText(path, "Passed=" + result.PassCount + " Failed=" + result.FailCount + " Skipped=" + result.SkipCount + "\n" + failures);
                    if (presentation) SessionState.SetBool(PlayPending, false);
                }
                TestRunnerApi.UnregisterTestCallback(this);
                if (api != null) Object.DestroyImmediate(api);
            }
            bool Matches(ITestResultAdaptor result)
            {
                if (result.FullName.Contains(presentation ? "ReferenceStylePlayTests" : "CampaignBalanceTests") || presentation && result.FullName.Contains("TutorialGuidePlayTests")) return true;
                if (result.HasChildren) foreach (var child in result.Children) if (Matches(child)) return true;
                return false;
            }
        }
    }
}
