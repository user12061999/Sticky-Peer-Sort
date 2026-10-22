namespace StickyPeerSort
{
    public enum SpecialAction { Collect, Block, DamageIce }
    public interface ISpecialRule { SpecialAction Evaluate(GameSession session, int index); }
    public sealed class OrdinaryRule : ISpecialRule
    { public SpecialAction Evaluate(GameSession s, int i) { return SpecialAction.Collect; } }
    public sealed class FrozenRule : ISpecialRule
    { public SpecialAction Evaluate(GameSession s, int i) { return s.Stickers[i].ice > 0 ? SpecialAction.DamageIce : SpecialAction.Collect; } }
    public sealed class LockedRule : ISpecialRule
    {
        public SpecialAction Evaluate(GameSession session, int index)
        {
            var config = session.Stickers[index].placement.special;
            if (config.unlockCondition == UnlockCondition.CategoryCompleted)
            {
                for (int c = 0; c < session.Categories.Count; c++)
                    if (session.Categories[c].objective.category.id == config.dependencyId && session.Categories[c].Complete)
                        return SpecialAction.Collect;
            }
            else foreach (var s in session.Stickers)
                if (s.placement.instanceId == config.dependencyId &&
                    (config.unlockCondition == UnlockCondition.KeyCollected ? s.location == StickerLocation.Collected : s.location != StickerLocation.Board))
                    return SpecialAction.Collect;
            return SpecialAction.Block;
        }
    }
    public sealed class LinkedRule : ISpecialRule
    {
        public SpecialAction Evaluate(GameSession session, int index)
        {
            string link = session.Stickers[index].placement.special.linkId;
            for (int i = 0; i < session.Stickers.Count; i++)
                if (i != index && session.Stickers[i].placement.special.kind == SpecialKind.Linked && session.Stickers[i].placement.special.linkId == link)
                    return session.Stickers[i].location != StickerLocation.Board || !BoardGeometry.Covered(session, i) ? SpecialAction.Collect : SpecialAction.Block;
            return SpecialAction.Block;
        }
    }
    public sealed class StickerRuleEngine
    {
        readonly ISpecialRule[] handlers = { new OrdinaryRule(), new LockedRule(), new FrozenRule(), new LinkedRule(), new OrdinaryRule() };
        public SpecialAction Evaluate(GameSession s, int i) { return handlers[(int)s.Stickers[i].placement.special.kind].Evaluate(s, i); }
    }
}
