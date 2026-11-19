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
        // Spec: peeling starts from a sticker's edge. The edge follows the drawn outline (owner 17/11, partner feedback on
        // thin parts such as a flower stem): a point counts as edge when the artwork's outline is closer than
        // band x the smaller side of the drawn artwork (not of its box: the box's transparent margin made the band far
        // too wide, so presses near the middle peeled; owner 18/11). A point outside the artwork (a press just beside it) is always on the edge.
        // So a thin stem or handle is edge along its whole length, while the middle of a large round body is not.
        public static bool NearEdge(StickerRuntime s, Vector2 boardPoint, float band)
        {
            Vector2 size = s.placement.definition.FitSize(s.placement.size) * s.placement.scale;
            if (size.x <= 0 || size.y <= 0 || !Contains(s, boardPoint)) return true;
            float reach = EdgeReach(s, band);
            const int Rings = 3, Steps = 16;
            for (int ring = 1; ring <= Rings; ring++)
                for (int step = 0; step < Steps; step++)
                {
                    float a = (step + ring * .5f) * Mathf.PI * 2 / Steps;
                    if (!Contains(s, boardPoint + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (reach * ring / Rings))) return true;
                }
            return false;
        }
        // A visible, grabbable rim point of a sticker (board space), preferring the lower-right side; used by the guides.
        public static bool TryEdgePoint(GameSession session, int index, float band, out Vector2 point, out Vector2 outward)
        {
            var s = session.Stickers[index];
            Vector2 size = s.placement.definition.FitSize(s.placement.size) * s.placement.scale;
            float r = s.rotation * Mathf.Deg2Rad;
            foreach (float degrees in new[] { -40f, -20f, -60f, 0f, -90f, 20f, -120f, 45f, 180f, 90f, 135f, -150f })
                for (float radius = .44f; radius >= .5f - band - .02f; radius -= .03f)
                {
                    float a = degrees * Mathf.Deg2Rad;
                    Vector2 local = new Vector2(Mathf.Cos(a) * size.x * radius, Mathf.Sin(a) * size.y * radius);
                    Vector2 world = s.position + new Vector2(local.x * Mathf.Cos(r) - local.y * Mathf.Sin(r), local.x * Mathf.Sin(r) + local.y * Mathf.Cos(r));
                    if (Hit(session, world) != index || !NearEdge(s, world, band)) continue;
                    point = world; outward = (world - s.position).normalized; return true;
                }
            point = s.position; outward = new Vector2(.7f, -.7f); return false;
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
        // Size of the drawn artwork on the board: the sticker's box without its transparent margin.
        public static Vector2 ArtSize(StickerRuntime s)
        {
            Vector2 size = s.placement.definition.FitSize(s.placement.size) * s.placement.scale;
            Rect art = s.placement.definition.ArtUVRect;
            return new Vector2(size.x * art.width, size.y * art.height);
        }
        // Width of the edge band, in board units: band x the artwork's smaller side.
        public static float EdgeReach(StickerRuntime s, float band)
        {
            Vector2 art = ArtSize(s);
            return Mathf.Max(1, band * Mathf.Min(art.x, art.y));
        }
        // Fingers land imprecisely: a press on a playable sticker always takes it, but one that misses, or lands on a
        // covered sticker, goes to the closest playable sticker whose artwork is within reach: the touch slop, or, for a press
        // on empty paper, the edge band measured outside the outline when that is wider (owner 18/11: a press up to 20% of the art's size outside it
        // grabs the edge, as a press on the rim does).
        public static int ResolveTouch(GameSession session, Vector2 p, float slop) { return ResolveTouch(session, p, slop, 0); }
        public static int ResolveTouch(GameSession session, Vector2 p, float slop, float band)
        {
            int exact = Hit(session, p);
            if (slop <= 0 && band <= 0 || exact >= 0 && session.Legal(exact)) return exact;
            int best = -1; float bestDistance = float.MaxValue;
            for (int i = 0; i < session.Stickers.Count; i++)
            {
                var s = session.Stickers[i];
                if (s.location != StickerLocation.Board || !session.Legal(i)) continue;
                // The band only widens presses on empty paper; a press on a covered sticker keeps the plain slop, so it still
                // shakes that sticker instead of jumping to a playable neighbour.
                float reach = Mathf.Max(slop, band > 0 && exact < 0 ? EdgeReach(s, band) : 0);
                Vector2 h = HalfBounds(s), d = p - s.position;
                if (Mathf.Abs(d.x) > h.x + reach || Mathf.Abs(d.y) > h.y + reach) continue;
                float distance = DistanceToArt(s, p, reach);
                if (distance < bestDistance) { best = i; bestDistance = distance; }
            }
            return best >= 0 ? best : exact;
        }
        // Distance from p to the sticker's artwork, searched in rings up to reach; MaxValue when it is further away.
        static float DistanceToArt(StickerRuntime s, Vector2 p, float reach)
        {
            int rings = Mathf.Clamp(Mathf.CeilToInt(reach / 4), 1, 24); const int Steps = 16;
            for (int ring = 1; ring <= rings; ring++)
            {
                float r = reach * ring / rings;
                for (int step = 0; step < Steps; step++)
                {
                    float angle = (step + (ring & 1) * .5f) * Mathf.PI * 2 / Steps;
                    if (Contains(s, p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r)) return r;
                }
            }
            return float.MaxValue;
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
