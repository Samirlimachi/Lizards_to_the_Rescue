using System;
using System.Collections.Generic;
using UnityEngine;
using K = Lizards.EditorTools.SewerKit;

namespace Lizards.EditorTools
{
    /// <summary>Piezas de atrezo del nivel 2 (alcantarillas de colores), todas como geometría acumulada en un SewerKit.</summary>
    public static partial class LevelTwoArtBuilder
    {
        // ---------------- Paleta (sacada de las láminas de referencia) ----------------
        static readonly Color Brick = new(0.70f, 0.36f, 0.64f), Brick2 = new(0.60f, 0.30f, 0.56f), Brick3 = new(0.78f, 0.44f, 0.72f), BrickDark = new(0.50f, 0.25f, 0.48f);
        static readonly Color Mortar = new(0.34f, 0.19f, 0.37f), Coping = new(0.72f, 0.42f, 0.70f);
        static readonly Color TileA = new(0.55f, 0.58f, 0.67f), TileB = new(0.64f, 0.66f, 0.73f), TileC = new(0.48f, 0.51f, 0.60f);
        static readonly Color PoolEdge = new(0.66f, 0.63f, 0.73f), StoneGray = new(0.50f, 0.50f, 0.62f), StoneGray2 = new(0.42f, 0.42f, 0.54f);
        static readonly Color Orange = new(1f, 0.46f, 0.12f), OrangeDark = new(0.82f, 0.32f, 0.08f), Yellow = new(1f, 0.80f, 0.16f), Black = new(0.13f, 0.13f, 0.16f);
        static readonly Color Mint = new(0.52f, 0.80f, 0.68f), MintDark = new(0.36f, 0.62f, 0.52f), Wood = new(0.88f, 0.55f, 0.27f), WoodDark = new(0.64f, 0.37f, 0.16f), WoodLight = new(0.95f, 0.66f, 0.36f);
        static readonly Color Steel = new(0.44f, 0.46f, 0.55f), SteelDark = new(0.26f, 0.27f, 0.33f), Lime = new(0.74f, 0.90f, 0.30f), LimeDark = new(0.52f, 0.68f, 0.18f);
        static readonly Color Glow = new(1f, 0.84f, 0.48f), GlowHot = new(1f, 0.95f, 0.75f), Red = new(0.88f, 0.22f, 0.16f), White = new(0.96f, 0.95f, 0.92f), Blue = new(0.24f, 0.36f, 0.88f), Beige = new(0.90f, 0.78f, 0.56f);
        static readonly Color LampLight = new(1f, 0.62f, 0.28f);

        static System.Random rnd;
        static float Rf(float a, float b) => a + (b - a) * (float)rnd.NextDouble();
        static Color V(Color c, float a = 0.06f) => K.Vary(c, rnd, a);
        static Quaternion Face(Vector3 n) => Quaternion.LookRotation(n, Vector3.up);

        // ---------------- Muros de ladrillo con arcos ----------------
        public struct Hole
        {
            public float u, w, y0, spring; public bool arch;
            public Hole(float u, float w = 6f, float spring = 3.4f, float y0 = 0f, bool arch = true) { this.u = u; this.w = w; this.spring = spring; this.y0 = y0; this.arch = arch; }
            public float Top => spring + (arch ? w * 0.5f : 0f);
        }

        /// <summary>Muro de a→b; el interior queda a la izquierda de la dirección (recorrido antihorario).</summary>
        static void Wall(K k, Vector3 a, Vector3 b, float h, Hole[] holes = null, bool pillars = true, Color? brickTint = null)
        {
            holes ??= new Hole[0];
            Vector3 d = b - a; d.y = 0; float L = d.magnitude; if (L < 0.05f) return; d /= L;
            Vector3 n = new Vector3(-d.z, 0, d.x); const float th = 1f; float y = a.y;
            var rot = Face(n);
            Vector3 P(float u, float yy, float depth) => a + d * u + Vector3.up * (y + yy) - n * depth;
            void Slab(float u0, float u1, float y0, float y1)
            {
                if (u1 - u0 < 0.01f || y1 - y0 < 0.01f) return;
                var c = P((u0 + u1) * 0.5f, (y0 + y1) * 0.5f, th * 0.5f); var s = new Vector3(u1 - u0, y1 - y0, th);
                k.Box(c, s, rot, Mortar);
                // La colisión cubre también los ladrillos que sobresalen hacia dentro.
                Col("Muro", P((u0 + u1) * 0.5f, (y0 + y1) * 0.5f, th * 0.5f - 0.1f), new Vector3(u1 - u0, y1 - y0, th + 0.2f), rot);
            }
            Array.Sort(holes, (p, q) => p.u.CompareTo(q.u));
            float cur = -th * 0.5f;
            foreach (var hl in holes)
            {
                float u0 = hl.u - hl.w * 0.5f, u1 = hl.u + hl.w * 0.5f;
                Slab(cur, u0, 0, h); cur = u1;
                if (hl.y0 > 0) Slab(u0, u1, 0, hl.y0);
                if (!hl.arch) { Slab(u0, u1, hl.spring, h); continue; }
                Slab(u0, u1, hl.Top, h);
                // Paños entre el arco y el rectángulo, por ambas caras, y el intradós.
                float r = hl.w * 0.5f; const int steps = 18;
                for (int i = 0; i < steps; i++)
                {
                    float x0 = -r + 2 * r * i / steps, x1 = -r + 2 * r * (i + 1) / steps;
                    float a0 = hl.spring + Mathf.Sqrt(Mathf.Max(0, r * r - x0 * x0)), a1 = hl.spring + Mathf.Sqrt(Mathf.Max(0, r * r - x1 * x1));
                    float top = hl.Top;
                    foreach (float dep in new[] { 0f, th })
                        k.Quad(P(hl.u + x0, a0, dep), P(hl.u + x0, top, dep), P(hl.u + x1, top, dep), P(hl.u + x1, a1, dep), Mortar, dep == 0 ? n : -n);
                    k.Quad(P(hl.u + x0, a0, 0), P(hl.u + x1, a1, 0), P(hl.u + x1, a1, th), P(hl.u + x0, a0, th), Mortar, Vector3.down);
                }
                // Dovelas del arco.
                for (int i = 0; i <= 10; i++)
                {
                    float t = Mathf.PI * i / 10f; var radial = (d * Mathf.Cos(t) + Vector3.up * Mathf.Sin(t)).normalized;
                    var c = P(hl.u + Mathf.Cos(t) * (r + 0.36f), hl.spring + Mathf.Sin(t) * (r + 0.36f), -0.08f);
                    k.Block(c, new Vector3(i == 5 ? 0.75f : 0.62f, i == 5 ? 0.95f : 0.78f, 0.42f), Quaternion.LookRotation(n, radial), V(i == 5 ? Brick3 : Coping, 0.05f));
                }
                // Piedras de las jambas.
                for (float yy = hl.y0; yy < hl.spring - 0.2f; yy += 0.64f)
                {
                    bool wide = ((int)((yy - hl.y0) / 0.64f)) % 2 == 0;
                    foreach (int s in new[] { -1, 1 })
                        k.Block(P(hl.u + s * (r + (wide ? 0.42f : 0.28f)), yy + 0.31f, -0.08f), new Vector3(wide ? 0.85f : 0.58f, 0.58f, 0.4f), rot, V(Coping, 0.05f));
                }
            }
            Slab(cur, L + th * 0.5f, 0, h);

            // Ladrillos de la cara interior.
            bool Blocked(float u0, float u1, float y0, float y1)
            {
                foreach (var hl in holes)
                {
                    float r = hl.w * 0.5f, m = hl.arch ? 1.0f : 0.15f;
                    if (u1 < hl.u - r - m || u0 > hl.u + r + m) continue;
                    if (y1 < hl.y0 - 0.05f && hl.y0 > 0) continue;
                    float top;
                    if (hl.arch) { float x = Mathf.Clamp(hl.u, u0, u1) - hl.u; top = hl.spring + Mathf.Sqrt(Mathf.Max(0, (r + 0.75f) * (r + 0.75f) - x * x)); }
                    else top = hl.spring + 0.05f;
                    if (y0 < top) return true;
                }
                return false;
            }
            var tint = brickTint ?? Color.white;
            int row = 0;
            for (float yy = 0.55f; yy < h - 0.4f; yy += 0.62f, row++)
            {
                float u = -0.3f + (row % 2) * 0.6f + Rf(0, 0.2f);
                while (u < L - 0.2f)
                {
                    float len = Rf(0.9f, 1.5f); float u1 = Mathf.Min(L - 0.05f, u + len);
                    if (u1 - u > 0.35f && !Blocked(u, u1, yy, yy + 0.52f))
                    {
                        float pick = (float)rnd.NextDouble();
                        var col = pick < 0.45f ? Brick : pick < 0.75f ? Brick2 : pick < 0.93f ? Brick3 : BrickDark;
                        col = V(col * tint, 0.05f); col.a = 1;
                        k.Box(P((u + u1) * 0.5f, yy + 0.26f, -0.04f), new Vector3(u1 - u - 0.08f, 0.52f, 0.2f + Rf(0, 0.08f)), rot, col);
                    }
                    u = u1 + 0.08f;
                }
            }
            // Zócalo.
            for (float u = 0; u < L - 0.3f; u += 1.7f)
            {
                float u1 = Mathf.Min(L, u + 1.62f);
                if (!Blocked(u, u1, 0, 0.5f)) k.Box(P((u + u1) * 0.5f, 0.25f, -0.06f), new Vector3(u1 - u, 0.5f, 0.32f), rot, V(BrickDark, 0.05f));
            }
            // Pilastras.
            if (pillars && L > 7)
            {
                int count = Mathf.FloorToInt(L / 7f);
                for (int i = 1; i <= count; i++)
                {
                    float u = L * i / (count + 1);
                    if (Blocked(u - 1.2f, u + 1.2f, 0.6f, h - 0.6f)) continue;
                    for (float yy = 0; yy < h - 0.3f; yy += 0.64f)
                        k.Box(P(u, yy + 0.31f, -0.2f), new Vector3(((int)(yy / 0.64f)) % 2 == 0 ? 1.25f : 1.05f, 0.6f, 0.5f), rot, V(((int)(yy / 0.64f)) % 2 == 0 ? Brick2 : Brick, 0.05f));
                    k.Block(P(u, h + 0.35f, 0.2f), new Vector3(1.6f, 0.7f, 1.8f), rot, V(Coping, 0.04f));
                    Col("Pilastra", P(u, h * 0.5f, -0.2f), new Vector3(1.25f, h, 0.5f), rot);
                    Col("Remate de pilastra", P(u, h + 0.35f, 0.2f), new Vector3(1.6f, 0.7f, 1.8f), rot);
                }
            }
            // Remate superior de bloques gruesos (con colisión por tramos continuos).
            float run = float.NaN, runEnd = 0;
            void EmitRun() { if (!float.IsNaN(run) && runEnd - run > 0.05f) Col("Remate", P((run + runEnd) * 0.5f, h + 0.27f, th * 0.5f), new Vector3(runEnd - run, 0.56f, th + 0.4f), rot); run = float.NaN; }
            for (float u = -0.4f; u < L + 0.3f;)
            {
                float len = Rf(1.2f, 1.7f); float u1 = Mathf.Min(L + 0.5f, u + len);
                bool open = false;
                foreach (var hl in holes)
                {
                    float hs = hl.u - hl.w * 0.5f, he = hl.u + hl.w * 0.5f;
                    if (hl.Top < h - 0.01f || u1 <= hs || u >= he) continue;
                    if (u < hs - 0.3f) u1 = hs; else { open = true; u1 = Mathf.Max(u1, he); }
                }
                if (open) { EmitRun(); u = u1; continue; }
                k.Block(P((u + u1) * 0.5f, h + 0.27f + Rf(-0.03f, 0.03f), th * 0.5f), new Vector3(u1 - u - 0.06f, 0.56f, th + 0.4f), rot, V(Coping, 0.05f));
                if (float.IsNaN(run)) run = u; runEnd = u1;
                u = u1;
            }
            EmitRun();
        }

        static void Polygon(K k, List<Vector3> pts, float h, Dictionary<int, Hole[]> holes = null, bool pillars = true)
        {
            for (int i = 0; i < pts.Count; i++)
                Wall(k, pts[i], pts[(i + 1) % pts.Count], h, holes != null && holes.TryGetValue(i, out var hh) ? hh : null, pillars);
        }

        static void Arc(List<Vector3> pts, Vector3 c, float r, float a0, float a1, int steps)
        {
            for (int i = 0; i <= steps; i++) { float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad; pts.Add(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r); }
        }

        // ---------------- Suelos ----------------
        /// <summary>Losas en una cuadrícula, sin cubrir el agua (sdf &lt; 0 = agua). También crea las colisiones del suelo.</summary>
        static void Floor(K k, Rect r, float y, Func<Vector2, float> water = null, float tile = 1.25f, bool collide = true)
        {
            int nx = Mathf.Max(1, Mathf.RoundToInt(r.width / tile)), nz = Mathf.Max(1, Mathf.RoundToInt(r.height / tile));
            float sx = r.width / nx, sz = r.height / nz;
            for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
            {
                var c = new Vector2(r.xMin + (x + 0.5f) * sx, r.yMin + (z + 0.5f) * sz);
                if (water != null && water(c) < 0.75f) continue;
                float pick = (float)rnd.NextDouble();
                var col = V(pick < 0.5f ? TileA : pick < 0.85f ? TileB : TileC, 0.04f);
                k.Box(new Vector3(c.x, y - 0.08f, c.y), new Vector3(sx - 0.07f, 0.2f, sz - 0.07f), Quaternion.identity, col * 0.9f);
                // Cara superior más clara con un bisel oscuro alrededor (aspecto de losa pintada).
                float ix = (sx - 0.07f) * 0.5f - 0.1f, iz = (sz - 0.07f) * 0.5f - 0.1f, ty = y + 0.026f; var lit = col * 1.06f; lit.a = 1;
                k.Quad(new Vector3(c.x - ix, ty, c.y - iz), new Vector3(c.x - ix, ty, c.y + iz), new Vector3(c.x + ix, ty, c.y + iz), new Vector3(c.x + ix, ty, c.y - iz), lit, Vector3.up);
                if (rnd.NextDouble() < 0.12) { float cx = Rf(-ix * 0.5f, ix * 0.5f), cz = Rf(-iz * 0.5f, iz * 0.5f); k.Box(new Vector3(c.x + cx, ty + 0.002f, c.y + cz), new Vector3(Rf(0.25f, 0.5f), 0.004f, 0.03f), Quaternion.Euler(0, Rf(0, 180), 0), col * 0.7f); }
            }
            // Fondo oscuro entre losas (lechada).
            if (water == null) k.Box(new Vector3(r.center.x, y - 0.2f, r.center.y), new Vector3(r.width, 0.1f, r.height), Quaternion.identity, Mortar * 0.8f);
            else
                for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
                {
                    var c = new Vector2(r.xMin + (x + 0.5f) * sx, r.yMin + (z + 0.5f) * sz);
                    if (water(c) >= 0.75f) k.Box(new Vector3(c.x, y - 0.2f, c.y), new Vector3(sx, 0.1f, sz), Quaternion.identity, new Color(0.25f, 0.24f, 0.3f));
                }
            if (collide) FloorColliders(r, y, water);
        }

        static void FloorColliders(Rect r, float y, Func<Vector2, float> water)
        {
            if (water == null) { Col("Suelo", new Vector3(r.center.x, y - 1.25f, r.center.y), new Vector3(r.width, 2.5f, r.height)); return; }
            const float cell = 0.5f; int nx = Mathf.CeilToInt(r.width / cell), nz = Mathf.CeilToInt(r.height / cell);
            string prev = null; int start = 0; List<(int, int)> prevRuns = null;
            void Emit(List<(int, int)> runs, int z0, int z1)
            {
                foreach (var (a, b) in runs)
                {
                    float x0 = r.xMin + a * cell, x1 = Mathf.Min(r.xMax, r.xMin + b * cell), zz0 = r.yMin + z0 * cell, zz1 = Mathf.Min(r.yMax, r.yMin + z1 * cell);
                    Col("Suelo", new Vector3((x0 + x1) / 2, y - 1.25f, (zz0 + zz1) / 2), new Vector3(x1 - x0, 2.5f, zz1 - zz0));
                }
            }
            for (int z = 0; z <= nz; z++)
            {
                var runs = new List<(int, int)>();
                if (z < nz)
                {
                    int s = -1;
                    for (int x = 0; x <= nx; x++)
                    {
                        bool solid = x < nx && water(new Vector2(r.xMin + (x + 0.5f) * cell, r.yMin + (z + 0.5f) * cell)) > 0;
                        if (solid && s < 0) s = x; if (!solid && s >= 0) { runs.Add((s, x)); s = -1; }
                    }
                }
                string key = z < nz ? string.Join(";", runs) : "#end";
                if (key != prev) { if (prevRuns != null) Emit(prevRuns, start, z); prev = key; prevRuns = runs; start = z; }
            }
        }

        // ---------------- Agua ----------------
        static float SdRoundBox(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - (half - Vector2.one * r);
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
        }
        static float SdPolyline(Vector2 p, List<Vector2> line, float hw)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                var a = line[i]; var b = line[i + 1]; var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
            }
            return best - hw;
        }
        static List<Vector2> RoundBoxOutline(Vector2 c, Vector2 half, float r)
        {
            var o = new List<Vector2>();
            var corners = new[] { new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1) };
            float[] start = { -90, 0, 90, 180 };
            for (int k = 0; k < 4; k++)
            {
                var cc = c + Vector2.Scale(corners[k], half - Vector2.one * r);
                for (int i = 0; i <= 6; i++) { float a = (start[k] + 90 * i / 6f) * Mathf.Deg2Rad; o.Add(cc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
            }
            return o;
        }
        static List<Vector2> Smooth(List<Vector2> ctrl, int per = 8)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < ctrl.Count - 1; i++)
            {
                Vector2 p0 = ctrl[Mathf.Max(0, i - 1)], p1 = ctrl[i], p2 = ctrl[i + 1], p3 = ctrl[Mathf.Min(ctrl.Count - 1, i + 2)];
                for (int j = 0; j < per; j++)
                {
                    float t = j / (float)per, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
                }
            }
            o.Add(ctrl[ctrl.Count - 1]); return o;
        }
        static List<Vector2> CanalOutline(List<Vector2> line, float hw)
        {
            var left = new List<Vector2>(); var right = new List<Vector2>();
            for (int i = 0; i < line.Count; i++)
            {
                var t = (line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(0, i - 1)]).normalized; var nn = new Vector2(-t.y, t.x);
                left.Add(line[i] + nn * hw); right.Add(line[i] - nn * hw);
            }
            var o = new List<Vector2>(right);
            var endT = (line[line.Count - 1] - line[line.Count - 2]).normalized;
            for (int i = 1; i < 6; i++) { float a = -Mathf.PI / 2 + Mathf.PI * i / 6; var nn = new Vector2(-endT.y, endT.x); o.Add(line[line.Count - 1] + (endT * Mathf.Cos(a) + nn * Mathf.Sin(a)) * hw); }
            for (int i = left.Count - 1; i >= 0; i--) o.Add(left[i]);
            var st = (line[1] - line[0]).normalized;
            for (int i = 1; i < 6; i++) { float a = -Mathf.PI / 2 + Mathf.PI * i / 6; var nn = new Vector2(st.y, -st.x); o.Add(line[0] + (-st * Mathf.Cos(a) + nn * Mathf.Sin(a)) * hw); }
            return o;
        }

        /// <summary>Bordillo a lo largo del contorno del agua (cerrado). striped = amarillo/negro.</summary>
        static void Curb(K k, List<Vector2> outline, float y, Color a, Color b, bool striped, float width = 0.8f)
        {
            // Asegura sentido: normal hacia fuera del agua.
            float area = 0; for (int i = 0; i < outline.Count; i++) { var p = outline[i]; var q = outline[(i + 1) % outline.Count]; area += p.x * q.y - q.x * p.y; }
            // Remuestreo uniforme del contorno (cerrado) para que las franjas tengan el mismo largo y no se solapen.
            float total = 0; var cum = new List<float> { 0 };
            for (int i = 0; i < outline.Count; i++) { total += (outline[(i + 1) % outline.Count] - outline[i]).magnitude; cum.Add(total); }
            float step = striped ? 0.6f : 1.1f; int n = Mathf.Max(8, Mathf.RoundToInt(total / step));
            var pts = new List<Vector2>(); int seg = 0;
            for (int s = 0; s < n; s++)
            {
                float d = total * s / n; while (seg < outline.Count - 1 && cum[seg + 1] < d) seg++;
                float t = (d - cum[seg]) / Mathf.Max(1e-4f, cum[seg + 1] - cum[seg]);
                pts.Add(Vector2.Lerp(outline[seg], outline[(seg + 1) % outline.Count], t));
            }
            var nrm = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var dir = (pts[(i + 1) % n] - pts[(i - 1 + n) % n]).normalized;
                nrm[i] = area > 0 ? new Vector2(dir.y, -dir.x) : new Vector2(-dir.y, dir.x);
            }
            Vector3 W(Vector2 p, Vector2 o, float off, float h) { var q = p + o * off; return new Vector3(q.x, y + h, q.y); }
            var band = new Color(0.5f, 0.52f, 0.6f);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n; Vector2 p0 = pts[i], p1 = pts[j], n0 = nrm[i], n1 = nrm[j];
                var mid = (n0 + n1).normalized; var outF = new Vector3(mid.x, 0, mid.y);
                var col = striped ? (i % 2 == 0 ? a : b) : V(i % 3 == 0 ? b : a, 0.03f);
                float wIn = -0.12f, wOut = width - 0.12f;
                // Cara interior (hacia el agua), chaflán, tapa, cara exterior y franja de suelo que tapa la junta con las losas.
                k.Quad(W(p0, n0, wIn, -0.7f), W(p1, n1, wIn, -0.7f), W(p1, n1, wIn, 0.07f), W(p0, n0, wIn, 0.07f), col * 0.9f, -outF);
                k.Quad(W(p0, n0, wIn, 0.07f), W(p1, n1, wIn, 0.07f), W(p1, n1, wIn + 0.07f, 0.14f), W(p0, n0, wIn + 0.07f, 0.14f), col * 1.05f, (Vector3.up - outF).normalized);
                k.Quad(W(p0, n0, wIn + 0.07f, 0.14f), W(p1, n1, wIn + 0.07f, 0.14f), W(p1, n1, wOut, 0.14f), W(p0, n0, wOut, 0.14f), col, Vector3.up);
                k.Quad(W(p0, n0, wOut, 0.14f), W(p1, n1, wOut, 0.14f), W(p1, n1, wOut, -0.02f), W(p0, n0, wOut, -0.02f), col * 0.85f, outF);
                k.Quad(W(p0, n0, wOut, 0.012f), W(p1, n1, wOut, 0.012f), W(p1, n1, wOut + 0.9f, 0.012f), W(p0, n0, wOut + 0.9f, 0.012f), band, Vector3.up);
                // Junta oscura fina entre piezas del bordillo (solo en los lisos).
                if (!striped) k.Quad(W(p0, n0, wIn + 0.07f, 0.145f), W(p0, n0, wOut, 0.145f), W(p0 + (p1 - p0).normalized * 0.04f, n0, wOut, 0.145f), W(p0 + (p1 - p0).normalized * 0.04f, n0, wIn + 0.07f, 0.145f), col * 0.6f, Vector3.up);
            }
        }

        static void WaterBody(Transform parent, string name, Rect bounds, float surface, Func<Vector2, float> sdf, float zoneStep = 2.6f)
        {
            var k = new K { bake = false };
            var a = new Vector3(bounds.xMin, surface, bounds.yMin); var b = new Vector3(bounds.xMax, surface, bounds.yMin);
            var c = new Vector3(bounds.xMax, surface, bounds.yMax); var d = new Vector3(bounds.xMin, surface, bounds.yMax);
            // Subdividido para que WaterFlow pueda ondular la lámina.
            int nx = Mathf.Max(2, Mathf.CeilToInt(bounds.width / 1.5f)), nz = Mathf.Max(2, Mathf.CeilToInt(bounds.height / 1.5f));
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            {
                float x0 = Mathf.Lerp(bounds.xMin, bounds.xMax, i / (float)nx), x1 = Mathf.Lerp(bounds.xMin, bounds.xMax, (i + 1) / (float)nx);
                float z0 = Mathf.Lerp(bounds.yMin, bounds.yMax, j / (float)nz), z1 = Mathf.Lerp(bounds.yMin, bounds.yMax, (j + 1) / (float)nz);
                if (sdf(new Vector2((x0 + x1) / 2, (z0 + z1) / 2)) > 1.6f) continue;
                k.Quad(new Vector3(x0, surface, z0), new Vector3(x1, surface, z0), new Vector3(x1, surface, z1), new Vector3(x0, surface, z1), Color.white, Vector3.up);
            }
            var go = k.Flush(name, parent, waterMat, Vector3.zero, false, false);
            go.AddComponent<WaterFlow>().amplitude = 0.02f;
            Col("Fondo del agua", new Vector3(bounds.center.x, surface - 2.5f, bounds.center.y), new Vector3(bounds.width, 0.4f, bounds.height));
            for (float x = bounds.xMin + zoneStep * 0.5f; x < bounds.xMax; x += zoneStep)
                for (float z = bounds.yMin + zoneStep * 0.5f; z < bounds.yMax; z += zoneStep)
                {
                    if (sdf(new Vector2(x, z)) > zoneStep * 0.3f) continue;
                    var zg = new GameObject("Zona de nado"); zg.transform.SetParent(parent, false); zg.transform.position = new Vector3(x, surface, z);
                    var sz = zg.AddComponent<SwimZone>(); sz.radius = zoneStep * 0.78f; sz.surfaceY = 0; sz.depth = 2.4f;
                }
        }

        // ---------------- Atrezo ----------------
        static void Lantern(K k, Vector3 p, Vector3 n)
        {
            int a = k.V.Count, b = glowKit.V.Count;
            LanternRaw(k, p, n);
            const float s = 1.55f;
            for (int i = a; i < k.V.Count; i++) k.V[i] = p + (k.V[i] - p) * s;
            for (int i = b; i < glowKit.V.Count; i++) glowKit.V[i] = p + (glowKit.V[i] - p) * s;
        }
        static void LanternRaw(K k, Vector3 p, Vector3 n)
        {
            var rot = Face(n);
            k.Box(p + n * 0.25f + Vector3.up * 0.55f, new Vector3(0.12f, 0.12f, 0.6f), rot, SteelDark);
            k.Cyl(p + n * 0.5f + Vector3.up * 0.55f, p + n * 0.5f + Vector3.up * 0.42f, 0.03f, SteelDark, 6);
            var c = p + n * 0.5f;
            k.Cyl(c + Vector3.up * 0.42f, c + Vector3.up * 0.3f, 0.28f, WoodDark * 0.8f, 8, true, 0.12f);
            k.Box(c + Vector3.up * 0.27f, new Vector3(0.46f, 0.06f, 0.46f), rot, SteelDark);
            k.Box(c - Vector3.up * 0.27f, new Vector3(0.46f, 0.08f, 0.46f), rot, SteelDark);
            foreach (var o in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) })
                k.Box(c + rot * (o * 0.2f), new Vector3(0.05f, 0.52f, 0.05f), rot, SteelDark);
            k.Box(c + rot * new Vector3(0, 0, 0.2f), new Vector3(0.36f, 0.04f, 0.02f), rot, SteelDark);
            glowKit.Box(c, new Vector3(0.36f, 0.48f, 0.36f), rot, Glow);
            glowKit.Box(c, new Vector3(0.14f, 0.2f, 0.37f), rot, GlowHot);
            K.Lamps.Add((c, LampLight, 6.5f));
        }

        static void Pipe(K k, Vector3 a, Vector3 b, float r, Color col, float flangeEvery = 3.2f)
        {
            k.Cyl(a, b, r, col, 16, true);
            var d = (b - a).normalized; float L = Vector3.Distance(a, b); int n = Mathf.Max(1, Mathf.RoundToInt(L / flangeEvery));
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n); if (i == 0) p += d * 0.12f; if (i == n) p -= d * 0.12f;
                Flange(k, p, d, r, col);
            }
        }
        static void Flange(K k, Vector3 p, Vector3 d, float r, Color col)
        {
            k.Cyl(p - d * 0.1f, p + d * 0.1f, r * 1.2f, col * 0.88f, 16, true);
            var u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var w = Vector3.Cross(d, u);
            for (int j = 0; j < 8; j++)
            {
                float t = j * Mathf.PI / 4; var q = p + (u * Mathf.Cos(t) + w * Mathf.Sin(t)) * r * 1.2f;
                k.Sphere(q, Vector3.one * 0.06f * Mathf.Max(1, r * 2), Orange, 6);
            }
        }
        /// <summary>Codo: arco de 90° de 'from' (dirección de llegada) a 'to'.</summary>
        static void Elbow(K k, Vector3 corner, Vector3 from, Vector3 to, float bend, float r, Color col)
        {
            var center = corner - from.normalized * bend + to.normalized * bend;
            var x = (corner - from.normalized * bend - center).normalized; var y = (corner + to.normalized * bend - center).normalized;
            var z = Vector3.Cross(x, y);
            var rot = Quaternion.LookRotation(z, y);
            // En el plano XY local, x → ángulo 0; queremos ir de 'x' a 'y' (90°).
            var localX = Quaternion.Inverse(rot) * x; float start = Mathf.Atan2(localX.y, localX.x) * Mathf.Rad2Deg;
            k.Torus(center, rot, bend, r, col, 12, 14, 90, start);
        }

        static void Valve(K k, Vector3 c, Vector3 n, float R, Color? col = null)
        {
            var cc = col ?? new Color(0.92f, 0.34f, 0.14f); var rot = Face(n);
            k.Torus(c, rot, R, R * 0.13f, cc, 24, 8);
            for (int i = 0; i < 3; i++) { var d = rot * new Vector3(Mathf.Cos(i * Mathf.PI / 3), Mathf.Sin(i * Mathf.PI / 3), 0); k.Cyl(c - d * R, c + d * R, R * 0.07f, cc, 6, false); }
            k.Cyl(c - n * 0.06f, c + n * 0.12f, R * 0.22f, cc * 0.85f, 10);
            k.Cyl(c - n * 0.4f, c, R * 0.12f, Steel, 8);
        }

        static void Crate(K k, Vector3 c, float s, float yaw = 0, bool marked = false)
        {
            var rot = Quaternion.Euler(0, yaw, 0); var body = V(Wood, 0.06f); var dark = V(WoodDark, 0.05f);
            k.Box(c, Vector3.one * s, rot, body);
            float b = s * 0.12f;
            foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up })
            {
                var fr = Quaternion.LookRotation(rot * n, Mathf.Abs(n.y) > 0.5f ? rot * Vector3.forward : Vector3.up);
                var fc = c + rot * n * (s * 0.5f + 0.012f);
                foreach (int sgn in new[] { -1, 1 })
                {
                    k.Box(fc + fr * new Vector3(0, sgn * (s * 0.5f - b * 0.5f), 0), new Vector3(s, b, 0.04f), fr, dark);
                    k.Box(fc + fr * new Vector3(sgn * (s * 0.5f - b * 0.5f), 0, 0), new Vector3(b, s - 2 * b, 0.04f), fr, dark);
                }
                float inner = s - 2 * b; float diag = Mathf.Sqrt(2) * inner;
                k.Box(fc, new Vector3(b * 0.9f, diag - b, 0.035f), fr * Quaternion.Euler(0, 0, 45), V(WoodLight, 0.04f));
                for (int i = 1; i < 3; i++) k.Box(fc + fr * new Vector3(0, -inner / 2 + inner * i / 3, -0.005f), new Vector3(inner, 0.02f, 0.03f), fr, dark * 0.9f);
            }
            if (marked) k.Box(c + rot * Vector3.back * (s * 0.5f + 0.03f), new Vector3(s * 0.45f, s * 0.3f, 0.02f), rot, Yellow);
        }

        static void Barrel(K k, Vector3 basePos, float r = 0.45f, float h = 1.15f)
        {
            var prof = new[] { new Vector2(0, 0), new Vector2(r * 0.86f, 0), new Vector2(r, h * 0.5f), new Vector2(r * 0.86f, h), new Vector2(0, h) };
            k.Lathe(basePos, prof, V(Wood, 0.05f), 16);
            foreach (float t in new[] { 0.12f, 0.5f, 0.88f }) { float rr = Mathf.Lerp(r * 0.86f, r, 1 - Mathf.Abs(t - 0.5f) * 2) + 0.02f; k.Torus(basePos + Vector3.up * h * t, Quaternion.Euler(90, 0, 0), rr, 0.035f, SteelDark, 20, 6); }
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); k.Box(basePos + Vector3.up * h * 0.5f + d * (r + 0.005f), new Vector3(0.02f, h * 0.9f, 0.02f), Quaternion.LookRotation(d), WoodDark); }
        }

        static void Can(K k, Vector3 basePos, float r, float h, Color label)
        {
            k.Cyl(basePos, basePos + Vector3.up * h, r, new Color(0.78f, 0.8f, 0.84f), 10, true);
            k.Cyl(basePos + Vector3.up * h * 0.2f, basePos + Vector3.up * h * 0.8f, r + 0.008f, label, 10, false);
            k.Cyl(basePos + Vector3.up * h * 0.42f, basePos + Vector3.up * h * 0.58f, r + 0.012f, White, 10, false);
        }
        static readonly Color[] LabelCols = { new(0.9f, 0.3f, 0.2f), new(0.25f, 0.45f, 0.9f), new(0.3f, 0.7f, 0.35f), new(1f, 0.6f, 0.15f), new(0.95f, 0.85f, 0.3f) };

        static void Shelf(K k, Vector3 basePos, float yaw, float w = 2.4f, float h = 2.4f, float d = 0.7f, int shelves = 4, bool cans = true, bool boxes = false)
        {
            var rot = Quaternion.Euler(0, yaw, 0); var right = rot * Vector3.right; var fwd = rot * Vector3.forward;
            foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
                k.Box(basePos + right * sx * (w / 2 - 0.06f) + fwd * sz * (d / 2 - 0.06f) + Vector3.up * h / 2, new Vector3(0.12f, h, 0.12f), rot, WoodDark);
            for (int i = 0; i < shelves; i++)
            {
                float y = 0.15f + i * (h - 0.25f) / (shelves - 1);
                k.Box(basePos + Vector3.up * y, new Vector3(w, 0.07f, d), rot, V(Wood, 0.05f));
                k.Box(basePos + Vector3.up * (y - 0.06f) + fwd * (d / 2 - 0.03f), new Vector3(w, 0.1f, 0.05f), rot, WoodDark);
                if (i == shelves - 1) break;
                if (cans)
                    for (float x = -w / 2 + 0.22f; x < w / 2 - 0.15f; x += Rf(0.28f, 0.36f))
                    {
                        if (rnd.NextDouble() < 0.12) continue;
                        float ch = Rf(0.24f, 0.36f), cr = Rf(0.09f, 0.12f);
                        Can(k, basePos + Vector3.up * (y + 0.035f) + right * x + fwd * Rf(-0.12f, 0.12f), cr, ch, LabelCols[rnd.Next(LabelCols.Length)]);
                        if (rnd.NextDouble() < 0.3) Can(k, basePos + Vector3.up * (y + 0.035f + ch) + right * x + fwd * Rf(-0.05f, 0.05f), cr, ch * 0.8f, LabelCols[rnd.Next(LabelCols.Length)]);
                    }
                if (boxes && rnd.NextDouble() < 0.7) Crate(k, basePos + Vector3.up * (y + 0.24f) + right * Rf(-w / 4, w / 4), 0.42f, yaw + Rf(-8, 8));
            }
            Col("Estante", basePos + Vector3.up * h / 2, new Vector3(w, h, d), rot);
        }

        static void LifeRing(K k, Vector3 c, Vector3 n)
        {
            var rot = Face(n);
            for (int i = 0; i < 4; i++) k.Torus(c, rot, 0.42f, 0.13f, i % 2 == 0 ? White : Orange, 8, 10, 90, i * 90 + 45);
            k.Cyl(c - n * 0.1f + Vector3.up * 0.5f, c + n * 0.15f + Vector3.up * 0.5f, 0.04f, SteelDark, 6);
        }

        static void Bollard(K k, Vector3 p)
        {
            k.Cyl(p, p + Vector3.up * 0.14f, 0.5f, Yellow, 14, true);
            k.Cyl(p + Vector3.up * 0.14f, p + Vector3.up * 0.75f, 0.3f, new Color(0.22f, 0.22f, 0.27f), 14, false);
            k.Cyl(p + Vector3.up * 0.7f, p + Vector3.up * 0.88f, 0.38f, new Color(0.3f, 0.3f, 0.36f), 14, true);
            k.Cyl(p + Vector3.up * 0.88f, p + Vector3.up * 0.95f, 0.3f, Yellow, 14, true);
            Col("Bolardo", p + Vector3.up * 0.47f, new Vector3(0.7f, 0.95f, 0.7f));
        }

        static void Rope(K k, Vector3 p, float r = 0.45f)
        {
            for (int i = 0; i < 4; i++) k.Torus(p + Vector3.up * (0.06f + i * 0.1f), Quaternion.Euler(90, 0, 0), r - i * 0.03f, 0.06f, V(Beige, 0.05f), 20, 6);
        }

        static void Grate(K k, Vector3 c, float w, float l, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            k.Box(c + Vector3.up * 0.03f, new Vector3(w, 0.06f, l), rot, StoneGray2);
            k.Box(c + Vector3.up * 0.035f, new Vector3(w - 0.24f, 0.06f, l - 0.24f), rot, new Color(0.08f, 0.07f, 0.1f));
            int bars = Mathf.RoundToInt((w - 0.3f) / 0.17f);
            for (int i = 0; i <= bars; i++) k.Box(c + rot * new Vector3(-w / 2 + 0.15f + (w - 0.3f) * i / bars, 0.05f, 0), new Vector3(0.07f, 0.06f, l - 0.24f), rot, new Color(0.32f, 0.31f, 0.38f));
        }

        static void Railing(K k, Vector3 a, Vector3 b, float h = 1.05f, bool collide = true)
        {
            float L = Vector3.Distance(a, b); int n = Mathf.Max(1, Mathf.RoundToInt(L / 1.2f));
            for (int i = 0; i <= n; i++) { var p = Vector3.Lerp(a, b, i / (float)n); k.Cyl(p, p + Vector3.up * h, 0.055f, Yellow, 8); }
            k.Cyl(a + Vector3.up * h, b + Vector3.up * h, 0.065f, Yellow, 8);
            k.Cyl(a + Vector3.up * h * 0.5f, b + Vector3.up * h * 0.5f, 0.045f, Yellow, 8);
            if (collide) { var d = b - a; Col("Barandilla", (a + b) / 2 + Vector3.up * (h * 0.6f), new Vector3(0.12f, h * 1.2f, L), Quaternion.LookRotation(d.normalized)); }
        }

        /// <summary>Pasarela de rejilla metálica (rect en XZ, cara superior en y).</summary>
        static void Deck(K k, Rect r, float y, float supportsTo = float.NaN)
        {
            k.Box(new Vector3(r.center.x, y - 0.12f, r.center.y), new Vector3(r.width, 0.24f, r.height), Quaternion.identity, SteelDark);
            float s = 0.18f;
            foreach (int sz in new[] { -1, 1 }) k.Box(new Vector3(r.center.x, y + 0.02f, r.center.y + sz * (r.height / 2 - s / 2)), new Vector3(r.width, 0.06f, s), Quaternion.identity, Steel);
            foreach (int sx in new[] { -1, 1 }) k.Box(new Vector3(r.center.x + sx * (r.width / 2 - s / 2), y + 0.02f, r.center.y), new Vector3(s, 0.06f, r.height), Quaternion.identity, Steel);
            var g = new Color(0.36f, 0.37f, 0.45f);
            for (float x = r.xMin + 0.4f; x < r.xMax - 0.2f; x += 0.32f) k.Box(new Vector3(x, y + 0.005f, r.center.y), new Vector3(0.05f, 0.04f, r.height - 2 * s), Quaternion.identity, g);
            for (float z = r.yMin + 0.4f; z < r.yMax - 0.2f; z += 0.32f) k.Box(new Vector3(r.center.x, y + 0.01f, z), new Vector3(r.width - 2 * s, 0.04f, 0.05f), Quaternion.identity, g);
            for (float x = r.xMin + 1.2f; x < r.xMax - 0.5f; x += 2.4f) k.Box(new Vector3(x, y - 0.32f, r.center.y), new Vector3(0.2f, 0.3f, r.height), Quaternion.identity, SteelDark * 0.9f);
            if (!float.IsNaN(supportsTo))
                for (float x = r.xMin + 0.3f; x < r.xMax; x += Mathf.Max(2.5f, (r.width - 0.6f) / Mathf.Max(1, Mathf.Round((r.width - 0.6f) / 3.5f))))
                    foreach (float z in new[] { r.yMin + 0.3f, r.yMax - 0.3f }) k.Box(new Vector3(x, (y + supportsTo) / 2, z), new Vector3(0.28f, y - supportsTo, 0.28f), Quaternion.identity, Steel);
            Col("Pasarela", new Vector3(r.center.x, y - 0.15f, r.center.y), new Vector3(r.width, 0.3f, r.height));
        }

        /// <summary>Escalera de piedra de 'from' (abajo) a 'to' (arriba).</summary>
        static void Stairs(K k, Vector3 from, Vector3 to, float width, Color step, Color side)
        {
            var d = to - from; var flat = new Vector3(d.x, 0, d.z); float run = flat.magnitude, rise = d.y; var f = flat / run;
            int n = Mathf.Max(2, Mathf.CeilToInt(rise / 0.24f)); var rot = Quaternion.LookRotation(f);
            for (int i = 0; i < n; i++)
            {
                float h = rise * (i + 1) / n; float z0 = run * i / n, z1 = run * (i + 1) / n;
                k.Block(from + f * ((z0 + z1) / 2 + 0.02f) + Vector3.up * (h - 0.13f), new Vector3(width, 0.26f, run / n + 0.08f), rot, V(step, 0.04f));
                k.Box(from + f * ((z0 + z1) / 2) + Vector3.up * ((h - 0.2f) / 2), new Vector3(width - 0.1f, Mathf.Max(0.05f, h - 0.2f), run / n), rot, side);
                // Cuerpo macizo de la escalera (por debajo de la rampa) para no atravesarla por los costados.
                float body = h - rise / n - 0.06f;
                if (body > 0.1f) Col("Cuerpo de escalera", from + f * ((z0 + z1) / 2) + Vector3.up * (body / 2), new Vector3(width, body, run / n + 0.02f), rot);
                // Canto de seguridad en el primer y el último peldaño.
                if (i == 0 || i == n - 1)
                {
                    var o = from + f * (z0 + 0.02f) + Vector3.up * (h + 0.005f) - Vector3.Cross(Vector3.up, f) * (width * 0.5f - 0.05f);
                    k.Stripes(o, Vector3.Cross(Vector3.up, f) * (width - 0.1f), f * 0.22f, 0.25f, Yellow, Black, Vector3.up);
                }
            }
            var up = Vector3.Cross(d.normalized, Vector3.Cross(Vector3.up, d.normalized)).normalized; if (up.y < 0) up = -up;
            Col("Rampa de escalera", (from + to) / 2 - up * 0.2f, new Vector3(width, 0.4f, d.magnitude + 0.4f), Quaternion.LookRotation(d.normalized, up));
        }

        static void HazardStrip(K k, Vector3 a, Vector3 b, float width, float y, float stripe = 0.45f)
        {
            var d = b - a; d.y = 0; var side = Vector3.Cross(Vector3.up, d.normalized) * width;
            var o = new Vector3(a.x, y, a.z) - side * 0.5f;
            k.Box((a + b) / 2 + Vector3.up * (y - 0.02f - a.y), new Vector3(width, 0.04f, d.magnitude), Quaternion.LookRotation(d.normalized), Black);
            k.Stripes(o + Vector3.up * 0.001f, d, side, stripe, Yellow, Black, Vector3.up, 0.01f);
        }

        static void Window(K k, Vector3 c, Vector3 n, float w, float h)
        {
            var rot = Face(n); var right = rot * Vector3.right; float r = w / 2; float rect = h - r;
            Vector3 P(float x, float y, float dep = 0) => c + right * x + Vector3.up * y + n * dep;
            // Cristal (lámina cálida) y fondo.
            var pane = new List<Vector3> { P(-r, 0, 0.02f), P(r, 0, 0.02f), P(r, rect, 0.02f), P(-r, rect, 0.02f) };
            glowKit.Poly(pane, Glow, n);
            var arc = new List<Vector3> { P(0, rect, 0.02f) };
            for (int i = 0; i <= 12; i++) { float a = Mathf.PI * i / 12; arc.Add(P(Mathf.Cos(a) * r, rect + Mathf.Sin(a) * r, 0.02f)); }
            glowKit.Poly(arc, Glow, n);
            // Parteluces.
            var mull = new Color(0.45f, 0.24f, 0.18f);
            k.Box(P(0, h / 2, 0.06f), new Vector3(0.09f, h, 0.06f), rot, mull);
            foreach (float x in new[] { -r / 2, r / 2 }) k.Box(P(x, (h - 0.25f) / 2, 0.06f), new Vector3(0.07f, h - 0.3f, 0.05f), rot, mull);
            foreach (float y in new[] { rect * 0.33f, rect * 0.66f, rect }) k.Box(P(0, y, 0.06f), new Vector3(w, 0.07f, 0.05f), rot, mull);
            // Marco de piedra.
            foreach (int s in new[] { -1, 1 }) k.Block(P(s * (r + 0.2f), rect / 2, 0.05f), new Vector3(0.4f, rect + 0.1f, 0.3f), rot, V(Coping, 0.04f));
            k.Block(P(0, -0.12f, 0.1f), new Vector3(w + 0.7f, 0.25f, 0.4f), rot, V(Coping, 0.04f));
            for (int i = 0; i <= 8; i++) { float a = Mathf.PI * i / 8; var rad = (right * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)).normalized; k.Block(P(Mathf.Cos(a) * (r + 0.2f), rect + Mathf.Sin(a) * (r + 0.2f), 0.05f), new Vector3(0.45f, 0.42f, 0.3f), Quaternion.LookRotation(n, rad), V(Coping, 0.04f)); }
            K.Lamps.Add((P(0, h / 2, 0.6f), LampLight * 0.8f, 4.5f));
        }

        static void RoundWindow(K k, Vector3 c, Vector3 n, float R)
        {
            var rot = Face(n);
            k.Torus(c + n * 0.1f, rot, R + 0.25f, 0.32f, V(Coping, 0.03f), 28, 8);
            k.Cyl(c, c + n * 0.06f, R, new Color(0.12f, 0.08f, 0.16f), 28);
            for (int i = -3; i <= 3; i++) { float x = i * R / 3.6f; float hh = Mathf.Sqrt(Mathf.Max(0, R * R - x * x)); k.Cyl(c + rot * new Vector3(x, -hh, 0.12f), c + rot * new Vector3(x, hh, 0.12f), 0.07f, new Color(0.42f, 0.36f, 0.5f), 8); }
            k.Cyl(c + rot * new Vector3(-R, 0, 0.12f), c + rot * new Vector3(R, 0, 0.12f), 0.06f, new Color(0.42f, 0.36f, 0.5f), 8);
        }

        /// <summary>Cascada: lámina de agua que cae con espuma abajo.</summary>
        static void Waterfall(Transform parent, Vector3 top, Vector3 outDir, float width, float bottomY, float push = 0.6f)
        {
            var k = new K { bake = false }; var side = Vector3.Cross(Vector3.up, outDir).normalized;
            const int rows = 10, cols = 4; var pts = new Vector3[rows + 1];
            for (int i = 0; i <= rows; i++) { float t = i / (float)rows; pts[i] = top + outDir * (push * Mathf.Sqrt(t) + 0.05f) + Vector3.up * ((bottomY - top.y) * t * t * 0.4f + (bottomY - top.y) * t * 0.6f); }
            for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++)
            {
                float u0 = j / (float)cols, u1 = (j + 1) / (float)cols, v0 = i / (float)rows, v1 = (i + 1) / (float)rows;
                k.Quad(pts[i] + side * width * (u0 - 0.5f), pts[i] + side * width * (u1 - 0.5f), pts[i + 1] + side * width * (u1 - 0.5f), pts[i + 1] + side * width * (u0 - 0.5f),
                    Color.white, outDir, new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) });
            }
            var go = k.Flush("Cascada", parent, fallMat, Vector3.zero, false, false);
            var wf = go.AddComponent<WaterFlow>(); wf.kind = WaterFlow.Kind.Curtain; wf.amplitude = 0.04f;
            var foam = new K { bake = false }; var bottom = pts[rows];
            var foamCol = new Color(0.9f, 1f, 0.98f);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2 / 16, rr = Rf(0.75f, 1.05f);
                foam.Sphere(bottom + side * Mathf.Cos(a) * (width * 0.5f + 0.15f) * rr + outDir * Mathf.Sin(a) * 0.55f * rr + Vector3.up * 0.01f, new Vector3(Rf(0.14f, 0.24f), Rf(0.05f, 0.1f), Rf(0.14f, 0.24f)), foamCol, 8);
            }
            foam.Sphere(bottom + Vector3.up * 0.01f, new Vector3(width * 0.5f, 0.06f, 0.45f), foamCol, 12);
            foam.Torus(bottom + Vector3.up * 0.005f, Quaternion.Euler(90, 0, 0), Mathf.Max(width * 0.6f, 0.7f), 0.045f, new Color(0.7f, 0.98f, 0.95f), 32, 4);
            var fg = foam.Flush("Espuma de cascada", parent, unlitMat, bottom, false, false); // pivote en la espuma: así gira sobre sí misma
            var sp = new GameObject("Salpicadura"); sp.transform.SetParent(parent, false); sp.transform.position = bottom;
            var ws = sp.AddComponent<WaterfallSplash>(); ws.radius = width * 0.4f;
            fg.transform.SetParent(sp.transform, true); ws.foam = fg.transform;
        }

        static void Raft(K k, Vector3 c, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); float hw = 2.4f, hl = 1.5f, tr = 0.42f;
            Vector3 P(float x, float y, float z) => c + rot * new Vector3(x, y, z);
            var o = V(Orange, 0.02f);
            k.Cyl(P(-hw + hl, tr, -hl), P(hw - hl, tr, -hl), tr, o, 14, false);
            k.Cyl(P(-hw + hl, tr, hl), P(hw - hl, tr, hl), tr, o, 14, false);
            k.Torus(P(hw - hl, tr, 0), rot * Quaternion.Euler(90, 0, 0), hl, tr, o, 16, 14, 180, -90);
            k.Torus(P(-hw + hl, tr, 0), rot * Quaternion.Euler(90, 0, 0), hl, tr, o, 16, 14, 180, 90);
            // Fondo y asientos de madera.
            k.Sphere(P(0, 0.12f, 0), new Vector3(hw - 0.2f, 0.12f, hl - 0.1f), OrangeDark, 18);
            k.Box(P(0, 0.2f, 0), new Vector3((hw - 0.3f) * 2, 0.08f, (hl - 0.3f) * 2), rot, OrangeDark * 0.9f);
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                for (int i = -1; i <= 1; i++) k.Box(P(x + i * 0.26f, 0.55f, 0), new Vector3(0.24f, 0.08f, hl * 2 - 0.2f), rot, V(Wood, 0.05f));
                k.Box(P(x, 0.45f, 0), new Vector3(0.7f, 0.1f, 0.1f), rot, WoodDark);
            }
            // Cuerda en los costados.
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 5; i++)
                {
                    float x = -1.6f + i * 0.8f;
                    k.Torus(P(x, tr + 0.08f, s * (hl + tr * 0.9f)), rot * Quaternion.Euler(0, 0, 0), 0.32f, 0.04f, Beige, 10, 6, 180, 180);
                    k.Sphere(P(x - 0.32f, tr + 0.1f, s * (hl + tr * 0.85f)), Vector3.one * 0.09f, OrangeDark, 6);
                }
            foreach (int s in new[] { -1, 1 }) k.Torus(P(s * (hw + tr * 0.8f), tr + 0.15f, 0), rot * Quaternion.Euler(0, 90, 0), 0.3f, 0.05f, Black, 10, 6, 180, 0);
            Col("Cubierta de la balsa", P(0, 0.35f, 0), new Vector3(hw * 2 + 0.6f, 0.5f, hl * 2 + 0.6f), rot);
        }

        static void Pump(K k, Vector3 basePos, Vector3 facing, float wallZ)
        {
            var rot = Face(facing);
            k.Block(basePos + Vector3.up * 0.3f, new Vector3(4.4f, 0.6f, 4.4f), rot, V(StoneGray, 0.03f));
            var c = basePos + Vector3.up * 0.6f;
            var prof = new[] { new Vector2(0, 0), new Vector2(1.9f, 0.02f), new Vector2(2.15f, 0.6f), new Vector2(2.25f, 1.6f), new Vector2(2.1f, 2.6f), new Vector2(1.6f, 3.3f), new Vector2(0.9f, 3.6f), new Vector2(0, 3.65f) };
            k.Lathe(c, prof, Mint, 28);
            k.Torus(c + Vector3.up * 1.6f, Quaternion.Euler(90, 0, 0), 2.27f, 0.12f, MintDark, 32, 8);
            k.Torus(c + Vector3.up * 0.25f, Quaternion.Euler(90, 0, 0), 2.0f, 0.12f, MintDark, 32, 8);
            for (int i = 0; i < 24; i++) { float a = i * Mathf.PI / 12; k.Sphere(c + Vector3.up * 1.6f + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.38f, Vector3.one * 0.09f, Orange, 6); }
            // Rueda grande frontal.
            var wc = c + Vector3.up * 1.55f + facing * 2.35f;
            k.Cyl(wc - facing * 0.3f, wc + facing * 0.12f, 0.55f, MintDark, 16);
            k.Torus(wc + facing * 0.15f, rot, 1.45f, 0.2f, new Color(0.9f, 0.36f, 0.14f), 32, 10);
            k.Torus(wc + facing * 0.15f, rot, 1.2f, 0.06f, new Color(0.7f, 0.26f, 0.1f), 32, 6);
            for (int i = 0; i < 8; i++) { var d = rot * new Vector3(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4), 0); k.Cyl(wc + facing * 0.15f + d * 0.45f, wc + facing * 0.15f + d * 1.3f, 0.09f, new Color(0.88f, 0.34f, 0.13f), 6, false); }
            k.Cyl(wc + facing * 0.1f, wc + facing * 0.4f, 0.45f, Mint, 14);
            for (int i = 0; i < 8; i++) { var d = rot * new Vector3(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4), 0); k.Sphere(wc + facing * 0.4f + d * 0.3f, Vector3.one * 0.06f, Orange, 6); }
            // Tubo superior en codo hacia el muro del fondo.
            var top = c + Vector3.up * 3.4f; float pr = 0.75f; var back = new Vector3(0, 0, Mathf.Sign(wallZ - basePos.z));
            var corner = new Vector3(top.x, top.y + 2.6f, top.z);
            Pipe(k, top, corner - Vector3.up * 1.1f, pr, Mint, 2.0f);
            Elbow(k, corner, Vector3.up, back, 1.1f, pr, Mint);
            Pipe(k, corner + back * 1.1f, new Vector3(corner.x, corner.y, wallZ + back.z * 1.2f), pr, Mint, 2.2f);
            // Tubo lateral pequeño y manómetro.
            var side = Vector3.Cross(Vector3.up, facing);
            Pipe(k, c + Vector3.up * 1.0f + side * 2.0f, c + Vector3.up * 1.0f + side * 2.9f, 0.32f, MintDark, 1f);
            k.Cyl(c + Vector3.up * 1.0f + side * 2.9f + Vector3.down * 1.0f, c + Vector3.up * 1.0f + side * 2.9f, 0.32f, MintDark, 12);
            Valve(k, c + Vector3.up * 1.1f + side * 2.9f + facing * 0.45f, facing, 0.32f);
            k.Cyl(c + Vector3.up * 3.1f + facing * 1.2f, c + Vector3.up * 3.1f + facing * 1.32f, 0.28f, White, 14, true);
            Col("Bomba", c + Vector3.up * 1.8f, new Vector3(4.4f, 3.6f, 4.4f), rot);
        }

        static void Tank(K k, Vector3 basePos, float r, float h)
        {
            k.Cyl(basePos, basePos + Vector3.up * h, r, Mint, 22, false);
            var prof = new[] { new Vector2(r, 0), new Vector2(r * 0.9f, r * 0.35f), new Vector2(r * 0.55f, r * 0.62f), new Vector2(0, r * 0.7f) };
            k.Lathe(basePos + Vector3.up * h, prof, Mint, 22);
            foreach (float t in new[] { 0.08f, 0.55f, 1f }) k.Torus(basePos + Vector3.up * h * t, Quaternion.Euler(90, 0, 0), r + 0.04f, 0.09f, MintDark, 28, 6);
            for (int i = 0; i < 16; i++) { float a = i * Mathf.PI / 8; k.Sphere(basePos + Vector3.up * h * 0.55f + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (r + 0.1f), Vector3.one * 0.07f, Orange, 6); }
            k.Cyl(basePos + Vector3.up * (h + r * 0.7f - 0.05f), basePos + Vector3.up * (h + r * 0.7f + 0.2f), 0.3f, MintDark, 12);
            Col("Depósito", basePos + Vector3.up * (h / 2 + 0.2f), new Vector3(r * 2, h + 0.4f, r * 2));
        }

        static void Sack(K k, Vector3 p, float s = 0.5f)
        {
            k.Sphere(p + Vector3.up * s * 0.55f, new Vector3(s * 0.6f, s * 0.55f, s * 0.5f), V(Beige, 0.05f), 12);
            k.Cyl(p + Vector3.up * s * 1.0f, p + Vector3.up * s * 1.25f, s * 0.2f, V(Beige * 0.9f, 0.03f), 8, true, s * 0.28f);
            k.Torus(p + Vector3.up * s * 1.02f, Quaternion.Euler(90, 0, 0), s * 0.2f, 0.03f, new Color(0.55f, 0.3f, 0.15f), 10, 5);
            k.Box(p + Vector3.up * s * 0.55f + Vector3.back * s * 0.49f, new Vector3(s * 0.4f, s * 0.3f, 0.02f), Quaternion.identity, new Color(0.75f, 0.35f, 0.25f));
        }

        static void Table(K k, Vector3 p, float w, float d, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); float h = 0.95f;
            foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) k.Box(p + rot * new Vector3(sx * (w / 2 - 0.1f), h / 2, sz * (d / 2 - 0.1f)), new Vector3(0.1f, h, 0.1f), rot, WoodDark);
            k.Box(p + Vector3.up * h, new Vector3(w, 0.08f, d), rot, Wood);
            int nx = 8, nz = 4;
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
                k.Box(p + rot * new Vector3(-w / 2 + (i + 0.5f) * w / nx, h + 0.045f, -d / 2 + (j + 0.5f) * d / nz), new Vector3(w / nx, 0.01f, d / nz), rot, (i + j) % 2 == 0 ? Red : White);
            foreach (int s in new[] { -1, 1 }) k.Box(p + rot * new Vector3(0, h - 0.18f, s * (d / 2 + 0.01f)), new Vector3(w, 0.36f, 0.015f), rot, Red);
            Col("Mesa", p + Vector3.up * h / 2, new Vector3(w, h, d), rot);
        }

        static void HangingLamp(K k, Vector3 top, float drop)
        {
            var c = top + Vector3.down * drop;
            k.Cyl(top, c + Vector3.up * 0.2f, 0.025f, Black, 5);
            k.Lathe(c, new[] { new Vector2(0.08f, 0.22f), new Vector2(0.14f, 0.18f), new Vector2(0.45f, -0.08f), new Vector2(0.48f, -0.12f) }, new Color(0.9f, 0.55f, 0.15f), 16);
            glowKit.Sphere(c + Vector3.down * 0.08f, new Vector3(0.18f, 0.12f, 0.18f), GlowHot, 10);
            K.Lamps.Add((c + Vector3.down * 0.5f, LampLight, 5.5f));
        }

        static void ToolBoard(K k, Vector3 c, Vector3 n)
        {
            var rot = Face(n); var right = rot * Vector3.right;
            k.Box(c, new Vector3(2.2f, 1.2f, 0.08f), rot, new Color(0.5f, 0.52f, 0.62f));
            k.Box(c + n * 0.01f, new Vector3(2.3f, 1.3f, 0.06f), rot, SteelDark);
            k.Box(c + n * 0.05f, new Vector3(2.1f, 1.1f, 0.02f), rot, new Color(0.55f, 0.58f, 0.68f));
            for (int i = 0; i < 6; i++)
            {
                var p = c + n * 0.08f + right * (-0.8f + i * 0.32f) + Vector3.down * 0.05f;
                float len = 0.45f + (i % 3) * 0.08f;
                k.Box(p, new Vector3(0.06f, len, 0.03f), rot, new Color(0.82f, 0.84f, 0.88f));
                k.Torus(p + Vector3.up * (len / 2 + 0.06f), rot, 0.07f, 0.025f, new Color(0.82f, 0.84f, 0.88f), 10, 5, 260, -40);
            }
            k.Box(c + n * 0.09f + right * 0.95f + Vector3.up * 0.1f, new Vector3(0.12f, 0.5f, 0.05f), rot, new Color(0.9f, 0.7f, 0.2f));
        }

        static void Workbench(K k, Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); float w = 2.4f, d = 0.9f, h = 1.0f;
            foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) k.Box(p + rot * new Vector3(sx * (w / 2 - 0.1f), h / 2, sz * (d / 2 - 0.1f)), new Vector3(0.12f, h, 0.12f), rot, WoodDark);
            k.Box(p + Vector3.up * h, new Vector3(w, 0.12f, d), rot, Wood);
            k.Box(p + Vector3.up * 0.25f, new Vector3(w - 0.2f, 0.06f, d - 0.15f), rot, V(Wood, 0.05f));
            k.Box(p + rot * new Vector3(0.7f, h + 0.18f, 0), new Vector3(0.35f, 0.25f, 0.3f), rot, Blue);
            k.Box(p + rot * new Vector3(0.7f, h + 0.18f, -0.22f), new Vector3(0.2f, 0.12f, 0.2f), rot, Blue * 0.8f);
            for (int i = 0; i < 3; i++) Can(k, p + rot * new Vector3(-0.8f + i * 0.28f, h + 0.06f, 0.1f), 0.1f, 0.24f, LabelCols[i + 1]);
            for (int i = 0; i < 3; i++) Can(k, p + rot * new Vector3(-0.6f + i * 0.4f, 0.28f, 0f), 0.13f, 0.3f, LabelCols[i]);
            Col("Banco de trabajo", p + Vector3.up * h / 2, new Vector3(w, h, d), rot);
        }

        static void LimePad(K k, Vector3 c, float r, float waterY)
        {
            k.Cyl(new Vector3(c.x, waterY - 2.2f, c.z), new Vector3(c.x, c.y - 0.25f, c.z), r * 0.92f, StoneGray2, 20, false);
            k.Cyl(new Vector3(c.x, c.y - 0.28f, c.z), new Vector3(c.x, c.y, c.z), r, LimeDark, 24, false);
            k.Cyl(new Vector3(c.x, c.y - 0.02f, c.z), new Vector3(c.x, c.y + 0.05f, c.z), r - 0.06f, Lime, 24, true);
            k.Torus(new Vector3(c.x, c.y - 0.3f, c.z), Quaternion.Euler(90, 0, 0), r, 0.06f, SteelDark, 24, 6);
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3 + 0.3f; k.Sphere(c + new Vector3(Mathf.Cos(a), 0.06f, Mathf.Sin(a)) * (r * 0.72f), new Vector3(0.07f, 0.04f, 0.07f), LimeDark, 6); }
            k.Torus(new Vector3(c.x, c.y + 0.07f, c.z), Quaternion.Euler(90, 0, 0), r * 0.55f, 0.03f, LimeDark, 24, 5);
            var foam = new K { bake = false };
            for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6; foam.Sphere(new Vector3(c.x, waterY + 0.02f, c.z) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (r + 0.12f), new Vector3(0.28f, 0.06f, 0.28f), White, 6); }
            foam.Flush("Espuma", currentRoom, unlitMat, Vector3.zero, false, false);
            var go = new GameObject("Plataforma lima"); go.transform.SetParent(currentRoom, false); go.transform.position = new Vector3(c.x, c.y - 1.2f, c.z);
            var cc = go.AddComponent<CapsuleCollider>(); cc.radius = r; cc.height = 2.5f; cc.center = Vector3.zero;
            var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(r * 1.5f, 2.4f, r * 1.5f);
            UnityEngine.Object.DestroyImmediate(cc);
        }
    }
}
