namespace StickyPeerSort
{
    [System.Flags]
    public enum TutorialLesson { None = 0, Peel = 1, Mystery = 2, Lock = 4 }

    // Read-only lesson selection. Tutorials never select a sticker or alter puzzle rules.
    public static class TutorialGuidePolicy
    {
        static readonly LockedRule Locks = new LockedRule();
        public static TutorialLesson Next(GameSession session, int completed)
        {
            if (session == null || session.State != GameplayState.Playing) return TutorialLesson.None;
            if (session.Level.levelNumber == 1 && (completed & (int)TutorialLesson.Peel) == 0) return TutorialLesson.Peel;
            for (int i = 0; i < session.Stickers.Count; i++)
                if ((completed & (int)TutorialLesson.Mystery) == 0 && session.Stickers[i].location == StickerLocation.Board &&
                    session.Stickers[i].placement.special.kind == SpecialKind.Mystery && !session.Stickers[i].revealed) return TutorialLesson.Mystery;
            for (int i = 0; i < session.Stickers.Count; i++)
                if ((completed & (int)TutorialLesson.Lock) == 0 && session.Stickers[i].location == StickerLocation.Board &&
                    session.Stickers[i].placement.special.kind == SpecialKind.Locked && Locks.Evaluate(session, i) == SpecialAction.Block) return TutorialLesson.Lock;
            return TutorialLesson.None;
        }
        public static int Subject(GameSession session, TutorialLesson lesson)
        {
            int chosen = -1;
            for (int i = 0; i < session.Stickers.Count; i++)
            {
                var s = session.Stickers[i];
                bool match = lesson == TutorialLesson.Peel ? session.Legal(i) && session.HasTarget(i)
                    : lesson == TutorialLesson.Mystery ? s.location == StickerLocation.Board && s.placement.special.kind == SpecialKind.Mystery && !s.revealed
                    : lesson == TutorialLesson.Lock && s.location == StickerLocation.Board && s.placement.special.kind == SpecialKind.Locked && Locks.Evaluate(session, i) == SpecialAction.Block;
                if (match && (chosen < 0 || s.order > session.Stickers[chosen].order)) chosen = i;
            }
            return chosen;
        }
        public static int CoverToRemove(GameSession session, int subject)
        {
            // Follow coverage upward until a genuinely clickable cover is reached.
            var pending = new System.Collections.Generic.Queue<int>();
            var visited = new System.Collections.Generic.HashSet<int>(); pending.Enqueue(subject);
            while (pending.Count > 0)
            {
                int lower = pending.Dequeue();
                if (!visited.Add(lower)) continue;
                if (lower != subject && session.Legal(lower)) return lower;
                for (int i = 0; i < session.Stickers.Count; i++)
                    if (session.Stickers[i].location == StickerLocation.Board && session.OverlapCache[lower, i] &&
                        (session.Stickers[i].order > session.Stickers[lower].order || session.Stickers[i].order == session.Stickers[lower].order && i > lower)) pending.Enqueue(i);
            }
            return -1;
        }
    }
}
