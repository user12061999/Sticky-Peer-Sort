using System;
using UnityEngine;
namespace StickyPeerSort
{
    [Serializable] public sealed class SaveData
    {
        public int version = 1, unlockedLevel = 1, highestCompleted, coins;
        public int undo = 10, hint = 10, extraSlot = 10, shuffle = 10;
        public int completedTutorials;
    }
    public sealed class SaveService : IProgressStore
    {
        const string DefaultKey = "StickyPeerSort.Save.v1";
        readonly string Key;
        public SaveData Data { get; private set; }
        public SaveService(int startingBoosters = 10, string key = null)
        {
            Key = string.IsNullOrEmpty(key) ? DefaultKey : key;
            bool fresh = !PlayerPrefs.HasKey(Key) || string.IsNullOrEmpty(PlayerPrefs.GetString(Key));
            Data = Decode(PlayerPrefs.GetString(Key, ""));
            if (fresh) Data.undo = Data.hint = Data.extraSlot = Data.shuffle = Mathf.Max(0, startingBoosters);
        }
        public static SaveData Decode(string json)
        {
            try { var data = JsonUtility.FromJson<SaveData>(json); return data != null && data.version == 1 ? data : new SaveData(); }
            catch { return new SaveData(); }
        }
        public bool Complete(LevelDefinition level)
        {
            if (Data.highestCompleted >= level.levelNumber) return false;
            Data.highestCompleted = level.levelNumber; Data.unlockedLevel = level.levelNumber + 1; Data.coins += level.coinReward; Flush(); return true;
        }
        public void Flush() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data)); PlayerPrefs.Save(); }
        public void CompleteTutorial(TutorialLesson lesson)
        {
            int flag = (int)lesson;
            if ((Data.completedTutorials & flag) == flag) return;
            Data.completedTutorials |= flag; Flush();
        }
    }
    public interface IGameFeedback { void Play(PuzzleEventKind kind); void Haptic(); }
    public sealed class SilentFeedback : IGameFeedback { public void Play(PuzzleEventKind kind) { } public void Haptic() { } }
}
