namespace StickyPeerSort
{
    public interface IProgressStore
    {
        SaveData Data { get; }
        bool Complete(LevelDefinition level);
        void Flush();
        void ResetTutorial(TutorialLesson lesson);
    }
}
