using UnityEngine;
namespace StickyPeerSort
{
    // Shared by input, linked exposure checks, hints and the level simulator.
    public static class BoardGeometry
    {
        public static bool[,] BuildOverlapCache(GameSession session)
        {
            int count = session.Stickers.Count; var cache = new bool[count, count];
            for (int a = 0; a < count; a++) for (int b = a + 1; b < count; b++)
                cache[a, b] = cache[b, a] = Overlaps(session.Stickers[a], session.Stickers[b]);
            return cache;
        }
        static Vector2 HalfBounds(StickerRuntime s)
        {
            Vector2 h = s.placement.definition.FitSize(s.placement.size) * (s.placement.scale * .5f); float r = s.rotation * Mathf.Deg2Rad;
            float c = Mathf.Abs(Mathf.Cos(r)), sn = Mathf.Abs(Mathf.Sin(r));
            return new Vector2(c * h.x + sn * h.y, sn * h.x + c * h.y);
        }
        public static bool Overlaps(StickerRuntime a, StickerRuntime b)
        {
            Vector2 ah = HalfBounds(a), bh = HalfBounds(b);
            Vector2 min = Vector2.Max(a.position - ah, b.position - bh), max = Vector2.Min(a.position + ah, b.position + bh);
            if (min.x >= max.x || min.y >= max.y) return false;
            // Test the intersection, so even a narrow strip is sampled instead of being skipped.
            int nx = Mathf.Max(2, Mathf.CeilToInt((max.x - min.x) / 4));
            int ny = Mathf.Max(2, Mathf.CeilToInt((max.y - min.y) / 4));
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                var p = new Vector2(Mathf.Lerp(min.x, max.x, (x + .5f) / nx), Mathf.Lerp(min.y, max.y, (y + .5f) / ny));
                if (Contains(a, p) && Contains(b, p)) return true;
            }
            return false;
        }
        public static bool Covered(GameSession session, int index)
        {
            var s = session.Stickers[index];
            for (int i = 0; i < session.Stickers.Count; i++)
            {
                var upper = session.Stickers[i];
                if (i != index && upper.location == StickerLocation.Board && (upper.order > s.order || upper.order == s.order && i > index) && session.OverlapCache[index, i]) return true;
            }
            return false;
        }
        public static bool Contains(StickerRuntime s, Vector2 boardPoint)
        {
            Vector2 d = boardPoint - s.position;
            float r = -s.rotation * Mathf.Deg2Rad;
            Vector2 p = new Vector2(d.x * Mathf.Cos(r) - d.y * Mathf.Sin(r), d.x * Mathf.Sin(r) + d.y * Mathf.Cos(r));
            Vector2 size = s.placement.definition.FitSize(s.placement.size) * s.placement.scale;
            if (size.x <= 0 || size.y <= 0) return false;
            Vector2 uv = new Vector2(p.x / size.x + .5f, p.y / size.y + .5f);
            if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) return false;
            return s.placement.definition.ContainsUV(uv);
        }
        public static int Hit(GameSession session, Vector2 p)
        {
            int hit = -1;
            for (int i = 0; i < session.Stickers.Count; i++)
            {
                var s = session.Stickers[i];
                if (s.location != StickerLocation.Board || !Contains(s, p)) continue;
                if (hit < 0 || s.order >= session.Stickers[hit].order) hit = i;
            }
            return hit;
        }
        public static bool Exposed(GameSession session, int index)
        {
            var s = session.Stickers[index];
            if (s.location != StickerLocation.Board) return false;
            float r = s.rotation * Mathf.Deg2Rad;
            // A deterministic grid covers center and exposed edges, including overlapping piles.
            for (int y = -4; y <= 4; y++) for (int x = -4; x <= 4; x++)
            {
                Vector2 p = new Vector2(x * s.placement.size.x * s.placement.scale / 9,
                    y * s.placement.size.y * s.placement.scale / 9);
                p = s.position + new Vector2(p.x * Mathf.Cos(r) - p.y * Mathf.Sin(r), p.x * Mathf.Sin(r) + p.y * Mathf.Cos(r));
                if (Hit(session, p) == index) return true;
            }
            return false;
        }
    }
}
