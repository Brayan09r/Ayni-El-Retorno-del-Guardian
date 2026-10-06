#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ayni.Editor
{
    /// <summary>Malla en construcción. Las UV van en metros (el shader de muro inca las usa como medida de las piedras).</summary>
    internal sealed class MeshBuf
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();

        public bool IsEmpty => triangles.Count == 0;

        /// <summary>Polígono convexo y plano. Se orienta solo para que su cara visible mire hacia <paramref name="facing"/>.</summary>
        public void Poly(Vector3[] p, Vector2[] uv, Vector3 facing)
        {
            int count = p.Length;
            if (count < 3) return;

            Vector3 normal = Vector3.zero; // método de Newell: vale también si el polígono no es del todo plano
            for (int i = 0; i < count; i++) normal += Vector3.Cross(p[i], p[(i + 1) % count]);
            if (normal.sqrMagnitude < 1e-10f) return;
            normal.Normalize();
            bool flip = Vector3.Dot(normal, facing) < 0f;
            if (flip) normal = -normal;

            if (uv == null)
            {
                Vector3 axisU, axisV;
                if (Mathf.Abs(normal.y) > 0.9f) { axisU = Vector3.right; axisV = Vector3.forward; }
                else { axisU = Vector3.Cross(Vector3.up, normal).normalized; axisV = Vector3.Cross(normal, axisU); }
                uv = new Vector2[count];
                for (int i = 0; i < count; i++) uv[i] = new Vector2(Vector3.Dot(p[i], axisU), Vector3.Dot(p[i], axisV));
            }

            int first = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                int k = flip ? count - 1 - i : i;
                vertices.Add(p[k]);
                normals.Add(normal);
                uvs.Add(uv[k]);
            }
            for (int i = 1; i < count - 1; i++)
            {
                triangles.Add(first);
                triangles.Add(first + i);
                triangles.Add(first + i + 1);
            }
        }

        public void Poly(Vector3[] p, Vector3 facing) => Poly(p, null, facing);

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing) => Poly(new[] { a, b, c, d }, null, facing);

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 facing)
        {
            Poly(new[] { a, b, c, d }, new[] { ua, ub, uc, ud }, facing);
        }

        /// <summary>Cuadrilátero con una normal por vértice (superficies curvas). Se orienta según la media de sus normales.</summary>
        public void SmoothQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Vector3 geometric = Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a);
            bool flip = Vector3.Dot(geometric, na + nb + nc + nd) < 0f;
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            normals.Add(na.normalized); normals.Add(nb.normalized); normals.Add(nc.normalized); normals.Add(nd.normalized);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            if (!flip)
            {
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }
            else
            {
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
                triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    /// <summary>Sistema de coordenadas de un edificio dentro de su conjunto: origen en el suelo y dos ejes horizontales.</summary>
    internal struct Frame
    {
        public Vector3 o, x, z;

        public Frame(Vector3 origin, float yawDegrees)
        {
            Quaternion q = Quaternion.Euler(0f, yawDegrees, 0f);
            o = origin;
            x = q * Vector3.right;
            z = q * Vector3.forward;
        }

        public Vector3 P(float px, float py, float pz) => o + x * px + Vector3.up * py + z * pz;
        public Vector3 D(float dx, float dy, float dz) => x * dx + Vector3.up * dy + z * dz;
    }

    /// <summary>Vano trapezoidal inca: más ancho abajo que arriba. Puede atravesar el muro (puerta, ventana) o ser una hornacina.</summary>
    internal sealed class Opening
    {
        public float u;          // centro, en metros desde el inicio del muro
        public float sill;       // altura del umbral
        public float height;
        public float wBottom, wTop;
        public float depth;      // 0 = atraviesa el muro; > 0 hornacina por fuera; < 0 hornacina por dentro
        public bool lintel = true;
        public float lintelHeight = 0.22f;
        public float lintelOver = 0.2f;
    }

    internal sealed class WallSpec
    {
        public Vector3 a, b;          // línea de la base de la cara exterior
        public Vector3 outward;       // hacia dónde mira la cara exterior
        public float height = 2.3f;
        public float thick = 0.6f;
        public float batterOut = 0.06f; // lo que se inclina hacia dentro la cara exterior por cada metro de altura
        public float batterIn;
        public float endBatter;       // inclinación de los extremos (esquinas de un edificio de muros inclinados)
        public float innerInset;      // la cara interior empieza y acaba este tramo más adentro (grosor del muro vecino)
        public float peak;            // altura extra del hastial en el centro (0 = muro de coronación recta)
        public bool capStart, capEnd;
        public float uvShift;
        public readonly List<Opening> openings = new List<Opening>();
    }

    /// <summary>Piezas de arquitectura inca: muros con vanos trapezoidales, techos de paja, colcas redondas y enseres.</summary>
    internal static class VillageGeo
    {
        public static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // ───────────────────────── Muro ─────────────────────────

        public static void Wall(MeshBuf stone, MeshBuf fine, WallSpec w)
        {
            Vector3 span = w.b - w.a;
            span.y = 0f;
            float length = span.magnitude;
            if (length < 0.05f) return;
            Vector3 dir = span / length;
            Vector3 up = Vector3.up;
            Vector3 o = w.outward;
            o.y = 0f;
            o.Normalize();
            float h = w.height;
            float half = length * 0.5f;
            float riseFrom = w.endBatter * h;

            float Top(float u)
            {
                if (w.peak <= 0f) return h;
                return h + w.peak * Mathf.Clamp01(1f - Mathf.Abs(u - half) / Mathf.Max(0.01f, half - riseFrom));
            }
            Vector3 Outer(float u, float v) => w.a + dir * u + up * v - o * (w.batterOut * v);
            Vector3 Inner(float u, float v) => w.a + dir * u + up * v - o * (w.thick - w.batterIn * v);

            var sorted = new List<Opening>(w.openings);
            sorted.Sort((p, q) => p.u.CompareTo(q.u));

            // Caras exterior e interior, recortadas alrededor de los vanos
            for (int side = 0; side < 2; side++)
            {
                bool outerSide = side == 0;
                Vector3 facing = outerSide ? o : -o;
                float slant = outerSide ? w.endBatter : 0f;
                float inset = outerSide ? 0f : w.innerInset;

                void Emit(params Vector2[] pts)
                {
                    var p = new Vector3[pts.Length];
                    var uv = new Vector2[pts.Length];
                    for (int i = 0; i < pts.Length; i++)
                    {
                        p[i] = outerSide ? Outer(pts[i].x, pts[i].y) : Inner(pts[i].x, pts[i].y);
                        uv[i] = new Vector2(pts[i].x + w.uvShift + (outerSide ? 0f : 37.3f), pts[i].y);
                    }
                    stone.Poly(p, uv, facing);
                }
                // Paño de muro de suelo a coronación entre dos bordes (que pueden estar inclinados)
                void Panel(float b0, float t0, float b1, float t1, float v0)
                {
                    if (b1 - b0 < 0.005f && t1 - t0 < 0.005f) return;
                    var pts = new List<Vector2> { new Vector2(b0, v0), new Vector2(t0, Top(t0)) };
                    if (w.peak > 0f && t0 < half - 0.001f && t1 > half + 0.001f) pts.Add(new Vector2(half, Top(half)));
                    pts.Add(new Vector2(t1, Top(t1)));
                    pts.Add(new Vector2(b1, v0));
                    Emit(pts.ToArray());
                }

                float prevBottom = inset, prevTop = inset + slant * h;
                foreach (Opening op in sorted)
                {
                    bool cuts = op.depth == 0f || (op.depth > 0f) == outerSide;
                    if (!cuts) continue;
                    float uL = op.u - op.wBottom * 0.5f, uR = op.u + op.wBottom * 0.5f;
                    float tL = op.u - op.wTop * 0.5f, tR = op.u + op.wTop * 0.5f;
                    float v0 = op.sill, v1 = op.sill + op.height;

                    Panel(prevBottom, prevTop, uL, uL, 0f);
                    if (v0 > 0.001f) Emit(new Vector2(uL, 0f), new Vector2(uL, v0), new Vector2(uR, v0), new Vector2(uR, 0f));
                    Emit(new Vector2(uL, v0), new Vector2(uL, v1), new Vector2(tL, v1));
                    Emit(new Vector2(uR, v0), new Vector2(tR, v1), new Vector2(uR, v1));
                    Panel(uL, uL, uR, uR, v1);
                    prevBottom = prevTop = uR;
                }
                Panel(prevBottom, prevTop, length - inset, length - inset - slant * h, 0f);
            }

            // Interior de cada vano y su dintel
            foreach (Opening op in sorted)
            {
                float uL = op.u - op.wBottom * 0.5f, uR = op.u + op.wBottom * 0.5f;
                float tL = op.u - op.wTop * 0.5f, tR = op.u + op.wTop * 0.5f;
                float v0 = op.sill, v1 = op.sill + op.height;

                Vector3 Front(float u, float v) => op.depth >= 0f ? Outer(u, v) : Inner(u, v);
                Vector3 Back(float u, float v)
                {
                    if (op.depth == 0f) return Inner(u, v);
                    return op.depth > 0f ? Outer(u, v) - o * op.depth : Inner(u, v) + o * (-op.depth);
                }

                stone.Quad(Front(uL, v0), Front(tL, v1), Back(tL, v1), Back(uL, v0), dir);
                stone.Quad(Front(uR, v0), Front(tR, v1), Back(tR, v1), Back(uR, v0), -dir);
                if (v0 > 0.001f) stone.Quad(Front(uL, v0), Front(uR, v0), Back(uR, v0), Back(uL, v0), up);
                if (!op.lintel) stone.Quad(Front(tL, v1), Front(tR, v1), Back(tR, v1), Back(tL, v1), -up);
                if (op.depth != 0f)
                {
                    Vector3 facing = op.depth > 0f ? o : -o;
                    stone.Quad(Back(uL, v0), Back(tL, v1), Back(tR, v1), Back(uR, v0), facing);
                }

                if (op.lintel && fine != null)
                {
                    // Un solo bloque tallado que salva el vano y asoma un poco del paramento
                    float l0 = tL - op.lintelOver, l1 = tR + op.lintelOver, y0 = v1, y1 = v1 + op.lintelHeight;
                    Vector3 F(float u, float v)
                    {
                        if (op.depth >= 0f) return Outer(u, v) + o * 0.035f;
                        return Inner(u, v) - o * 0.03f;
                    }
                    Vector3 B(float u, float v)
                    {
                        if (op.depth == 0f) return Inner(u, v) - o * 0.03f;
                        return op.depth > 0f ? Outer(u, v) - o * (op.depth + 0.02f) : Inner(u, v) + o * (-op.depth + 0.02f);
                    }
                    Vector3 frontFacing = op.depth >= 0f ? o : -o;
                    fine.Quad(F(l0, y0), F(l0, y1), F(l1, y1), F(l1, y0), frontFacing);
                    fine.Quad(B(l0, y0), B(l0, y1), B(l1, y1), B(l1, y0), -frontFacing);
                    fine.Quad(F(l0, y0), F(l1, y0), B(l1, y0), B(l0, y0), -up);
                    fine.Quad(F(l0, y1), F(l1, y1), B(l1, y1), B(l0, y1), up);
                    fine.Quad(F(l0, y0), F(l0, y1), B(l0, y1), B(l0, y0), -dir);
                    fine.Quad(F(l1, y0), F(l1, y1), B(l1, y1), B(l1, y0), dir);
                }
            }

            // Coronación
            var breaks = new List<float> { riseFrom };
            if (w.peak > 0f) breaks.Add(half);
            breaks.Add(length - riseFrom);
            for (int i = 0; i < breaks.Count - 1; i++)
            {
                float uA = breaks[i], uB = breaks[i + 1];
                float iA = Mathf.Clamp(uA, w.innerInset, length - w.innerInset), iB = Mathf.Clamp(uB, w.innerInset, length - w.innerInset);
                stone.Quad(Outer(uA, Top(uA)), Outer(uB, Top(uB)), Inner(iB, Top(iB)), Inner(iA, Top(iA)), up);
            }

            // Testeros (solo en muros sueltos)
            if (w.capStart) stone.Quad(Outer(0f, 0f), Outer(0f, h), Inner(0f, h), Inner(0f, 0f), -dir);
            if (w.capEnd) stone.Quad(Outer(length, 0f), Outer(length, h), Inner(length, h), Inner(length, 0f), dir);
        }

        // ───────────────────────── Bloques ─────────────────────────

        /// <summary>Bloque de base y coronación rectangulares distintas (pilón, plataforma, banco). Sin cara de abajo.</summary>
        public static void Frustum(MeshBuf buf, Frame f, float cx, float cz, float y0, float y1, float hx0, float hz0, float hx1, float hz1)
        {
            Vector3 P(float sx, float sz, bool top)
            {
                return top ? f.P(cx + sx * hx1, y1, cz + sz * hz1) : f.P(cx + sx * hx0, y0, cz + sz * hz0);
            }
            buf.Quad(P(-1, 1, false), P(-1, 1, true), P(1, 1, true), P(1, 1, false), f.z);
            buf.Quad(P(-1, -1, false), P(-1, -1, true), P(1, -1, true), P(1, -1, false), -f.z);
            buf.Quad(P(1, -1, false), P(1, -1, true), P(1, 1, true), P(1, 1, false), f.x);
            buf.Quad(P(-1, -1, false), P(-1, -1, true), P(-1, 1, true), P(-1, 1, false), -f.x);
            buf.Quad(P(-1, -1, true), P(-1, 1, true), P(1, 1, true), P(1, -1, true), Vector3.up);
        }

        /// <summary>Palo o rollizo entre dos puntos, con normales suaves.</summary>
        public static void Tube(MeshBuf buf, Vector3 a, Vector3 b, float r0, float r1, int sides, bool caps, float uvPerMeter = 1f)
        {
            Vector3 axis = b - a;
            float length = axis.magnitude;
            if (length < 0.001f) return;
            axis /= length;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 other = Vector3.Cross(axis, side);
            float around = Mathf.PI * (r0 + r1);

            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 n0 = side * Mathf.Cos(a0) + other * Mathf.Sin(a0);
                Vector3 n1 = side * Mathf.Cos(a1) + other * Mathf.Sin(a1);
                float v0 = around * i / sides * uvPerMeter, v1 = around * (i + 1) / sides * uvPerMeter;
                buf.SmoothQuad(a + n0 * r0, a + n1 * r0, b + n1 * r1, b + n0 * r1, n0, n1, n1, n0,
                    new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(length * uvPerMeter, v1), new Vector2(length * uvPerMeter, v0));
            }
            if (caps)
            {
                var start = new Vector3[sides];
                var end = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float ang = i * Mathf.PI * 2f / sides;
                    Vector3 n = side * Mathf.Cos(ang) + other * Mathf.Sin(ang);
                    start[i] = a + n * r0;
                    end[i] = b + n * r1;
                }
                buf.Poly(start, -axis);
                buf.Poly(end, axis);
            }
        }

        /// <summary>Pieza de torno (cántaros): perfil de radios y alturas girado alrededor de un eje vertical.</summary>
        public static void Lathe(MeshBuf buf, Vector3 basePoint, Vector2[] profile, int sides, float scale, float uShift)
        {
            float total = 0f;
            for (int i = 1; i < profile.Length; i++) total += (profile[i] - profile[i - 1]).magnitude;
            float run = 0f;
            for (int i = 0; i < profile.Length - 1; i++)
            {
                Vector2 p0 = profile[i] * scale, p1 = profile[i + 1] * scale;
                float seg = (profile[i + 1] - profile[i]).magnitude;
                Vector2 tan = (p1 - p0).normalized;
                Vector2 nrm = new Vector2(tan.y, -tan.x); // hacia fuera cuando el perfil sube
                float v0 = run / total, v1 = (run + seg) / total;
                run += seg;
                for (int s = 0; s < sides; s++)
                {
                    float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                    Vector3 r0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)), r1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                    Vector3 n0 = r0 * nrm.x + Vector3.up * nrm.y, n1 = r1 * nrm.x + Vector3.up * nrm.y;
                    float u0 = uShift + (float)s / sides, u1 = uShift + (float)(s + 1) / sides;
                    buf.SmoothQuad(basePoint + r0 * p0.x + Vector3.up * p0.y, basePoint + r1 * p0.x + Vector3.up * p0.y,
                        basePoint + r1 * p1.x + Vector3.up * p1.y, basePoint + r0 * p1.x + Vector3.up * p1.y,
                        n0, n1, n1, n0, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1));
                }
            }
        }

        /// <summary>Tela colgada: rejilla con pliegues verticales suaves.</summary>
        public static void Cloth(MeshBuf buf, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight, Vector3 bottomLeft, Rect uv,
            Vector3 facing, float wave, System.Random rng)
        {
            const int cols = 8, rows = 4;
            float phase = Range(rng, 0f, 6.28f);
            float folds = Range(rng, 2.2f, 3.4f);
            Vector3 n = facing.normalized;
            var grid = new Vector3[cols + 1, rows + 1];
            for (int c = 0; c <= cols; c++)
            {
                float s = (float)c / cols;
                for (int r = 0; r <= rows; r++)
                {
                    float t = (float)r / rows;
                    Vector3 p = Vector3.Lerp(Vector3.Lerp(topLeft, topRight, s), Vector3.Lerp(bottomLeft, bottomRight, s), t);
                    // Arriba va tensa en su palo; hacia abajo se ondula más
                    grid[c, r] = p + n * (Mathf.Sin(s * Mathf.PI * 2f * folds + phase) * wave * (0.25f + 0.75f * t));
                }
            }
            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    Vector2 U(int cc, int rr) => new Vector2(uv.xMin + uv.width * cc / cols, uv.yMax - uv.height * rr / rows);
                    buf.Quad(grid[c, r], grid[c + 1, r], grid[c + 1, r + 1], grid[c, r + 1], U(c, r), U(c + 1, r), U(c + 1, r + 1), U(c, r + 1), n);
                }
            }
        }

        /// <summary>Peñasco: una esfera achatada y deformada, de caras planas. <paramref name="radii"/> son sus tres semiejes.</summary>
        public static void Rock(MeshBuf buf, Vector3 center, Vector3 radii, float yawDegrees, System.Random rng)
        {
            const int rings = 5, segments = 9;
            Quaternion q = Quaternion.Euler(Range(rng, -12f, 12f), yawDegrees, Range(rng, -12f, 12f));
            var grid = new Vector3[rings + 1, segments];
            for (int r = 0; r <= rings; r++)
            {
                float phi = Mathf.PI * r / rings;
                for (int s = 0; s < segments; s++)
                {
                    float theta = (s + (r % 2) * 0.5f) * Mathf.PI * 2f / segments;
                    var dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    float bump = (r == 0 || r == rings) ? 1f : Range(rng, 0.8f, 1.1f);
                    grid[r, s] = center + q * Vector3.Scale(dir * bump, radii);
                }
            }
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    Vector3 a = grid[r, s], b = grid[r, s1], c = grid[r + 1, s1], d = grid[r + 1, s];
                    Vector3 facing = (a + b + c + d) * 0.25f - center;
                    if (r == 0) buf.Poly(new[] { a, c, d }, facing);
                    else if (r == rings - 1) buf.Poly(new[] { a, b, c }, facing);
                    else
                    {
                        buf.Poly(new[] { a, b, c }, facing);
                        buf.Poly(new[] { a, c, d }, facing);
                    }
                }
            }
        }

        // ───────────────────────── Techo de paja a dos aguas ─────────────────────────

        private const float ThatchThickness = 0.26f;
        private const float TierLift = 0.075f;
        private const float ThatchTile = 1.3f;

        /// <param name="yWall">Altura de la coronación de los muros largos.</param>
        /// <param name="peak">Lo que sube el hastial por encima de ella.</param>
        /// <param name="runIn">Cuánto se han metido las esquinas por la inclinación de los muros.</param>
        public static void GableRoof(MeshBuf thatch, MeshBuf fringe, MeshBuf wood, Frame f, float length, float depth, float yWall,
            float peak, float runIn, float overhang, float rake, System.Random rng)
        {
            float halfSpan = depth * 0.5f - runIn;
            float angle = Mathf.Atan2(peak, halfSpan);
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle), tan = Mathf.Tan(angle);
            float yRidge = yWall + peak + 0.04f;
            float slope = (depth * 0.5f + overhang) / cos;
            float T = ThatchThickness;
            float x0 = -length * 0.5f - rake, x1 = length * 0.5f + rake;
            int nx = Mathf.Max(4, Mathf.CeilToInt((x1 - x0) / 0.55f));
            const int tiers = 4;
            float topStart = -T * tan;

            for (int sgn = 1; sgn >= -1; sgn -= 2)
            {
                Vector3 down = f.D(0f, -sin, sgn * cos);
                Vector3 nrm = f.D(0f, cos, sgn * sin);
                Vector3 Pt(float x, float s, float lift) => f.P(x, yRidge, 0f) + down * s + nrm * lift;

                // Borde inferior de cada tanda de paja: nunca es una línea recta
                var edgeS = new float[tiers + 1, nx + 1];
                var edgeLift = new float[tiers + 1, nx + 1];
                for (int k = 1; k <= tiers; k++)
                {
                    for (int j = 0; j <= nx; j++)
                    {
                        edgeS[k, j] = slope * k / tiers + Range(rng, -0.06f, 0.06f);
                        edgeLift[k, j] = T + TierLift + Range(rng, -0.018f, 0.022f);
                    }
                }

                for (int k = 0; k < tiers; k++)
                {
                    float upper = k == 0 ? topStart : slope * k / tiers - 0.14f;
                    bool eave = k == tiers - 1;
                    for (int j = 0; j < nx; j++)
                    {
                        float xa = Mathf.Lerp(x0, x1, (float)j / nx), xb = Mathf.Lerp(x0, x1, (float)(j + 1) / nx);
                        float sa = edgeS[k + 1, j], sb = edgeS[k + 1, j + 1];
                        float la = edgeLift[k + 1, j], lb = edgeLift[k + 1, j + 1];

                        thatch.Quad(Pt(xa, upper, T), Pt(xb, upper, T), Pt(xb, sb, lb), Pt(xa, sa, la),
                            new Vector2(xa / ThatchTile, upper / ThatchTile), new Vector2(xb / ThatchTile, upper / ThatchTile),
                            new Vector2(xb / ThatchTile, sb / ThatchTile), new Vector2(xa / ThatchTile, sa / ThatchTile), nrm);

                        // Canto de la tanda (en el alero baja hasta la cara de abajo)
                        float bottom = eave ? 0f : T - 0.03f;
                        thatch.Quad(Pt(xa, sa, la), Pt(xb, sb, lb), Pt(xb, sb, bottom), Pt(xa, sa, bottom),
                            new Vector2(xa / ThatchTile, 0f), new Vector2(xb / ThatchTile, 0f),
                            new Vector2(xb / ThatchTile, (lb - bottom) / ThatchTile), new Vector2(xa / ThatchTile, (la - bottom) / ThatchTile), down);

                        // Flecos: las puntas de la paja que cuelgan sobre la tanda de abajo (o al aire en el alero)
                        float hang = eave ? 0.24f : 0.26f;
                        float endLift = eave ? T * 0.3f : T + 0.035f + TierLift * (hang + 0.14f) / (slope / tiers);
                        float ua = xa / 0.8f, ub = xb / 0.8f;
                        fringe.Quad(Pt(xa, sa - 0.04f, la + 0.006f), Pt(xb, sb - 0.04f, lb + 0.006f), Pt(xb, sb + hang, endLift), Pt(xa, sa + hang, endLift),
                            new Vector2(ua, 1f), new Vector2(ub, 1f), new Vector2(ub, 0f), new Vector2(ua, 0f), nrm);
                    }

                    // Cantos de los hastiales
                    for (int e = 0; e < 2; e++)
                    {
                        int j = e == 0 ? 0 : nx;
                        float xe = e == 0 ? x0 : x1;
                        float s = edgeS[k + 1, j], l = edgeLift[k + 1, j];
                        float under = Mathf.Max(0f, upper);
                        thatch.Quad(Pt(xe, under, 0f), Pt(xe, upper, T), Pt(xe, s, l), Pt(xe, s, 0f),
                            new Vector2(under / ThatchTile, 0f), new Vector2(upper / ThatchTile, T / ThatchTile),
                            new Vector2(s / ThatchTile, l / ThatchTile), new Vector2(s / ThatchTile, 0f), e == 0 ? -f.x : f.x);
                    }
                }

                // Cara de abajo
                float eaveS = slope + 0.06f;
                thatch.Quad(Pt(x0, 0f, 0f), Pt(x1, 0f, 0f), Pt(x1, eaveS, 0f), Pt(x0, eaveS, 0f),
                    new Vector2(x0 / ThatchTile, 0f), new Vector2(x1 / ThatchTile, 0f),
                    new Vector2(x1 / ThatchTile, eaveS / ThatchTile), new Vector2(x0 / ThatchTile, eaveS / ThatchTile), -nrm);

                // Armazón de palos: correas a lo largo y un par en cada hastial
                foreach (float at in new[] { 0.34f, 0.74f })
                {
                    Tube(wood, Pt(x0 - 0.2f, slope * at, -0.05f), Pt(x1 + 0.2f, slope * at, -0.05f), 0.055f, 0.05f, 6, true);
                }
                foreach (float xe in new[] { x0 + 0.1f, x1 - 0.1f })
                {
                    Tube(wood, Pt(xe, 0.02f, -0.06f), Pt(xe, slope - 0.04f, -0.06f), 0.05f, 0.045f, 6, true);
                    // Tijeras: los pares se cruzan y asoman por encima del caballete
                    Tube(wood, Pt(xe + (xe < 0f ? 0.16f : -0.16f), 0.55f, T + 0.03f), Pt(xe + (xe < 0f ? 0.16f : -0.16f), topStart - 0.42f, T + 0.03f), 0.04f, 0.03f, 5, true);
                }
            }

            // Caballete: un rollo de paja atado sobre la cumbrera, y la cumbrera de madera asomando por los hastiales
            float yTop = yRidge + T / cos;
            Tube(thatch, f.P(x0 - 0.05f, yTop + 0.02f, 0f), f.P(x1 + 0.05f, yTop + 0.02f, 0f), 0.15f, 0.15f, 8, true, 1f / ThatchTile);
            Tube(wood, f.P(x0 - 0.28f, yRidge - 0.07f, 0f), f.P(x1 + 0.28f, yRidge - 0.07f, 0f), 0.07f, 0.065f, 6, true);
        }

        // ───────────────────────── Construcciones redondas ─────────────────────────

        /// <summary>Muro circular de piedra con una abertura. El ángulo 0 mira hacia +z del conjunto.</summary>
        public static void RoundWall(MeshBuf stone, MeshBuf fine, Vector3 center, float radius, float height, float thick, float batter,
            int segments, float doorAngleDegrees, int doorSegments, float doorSill, float doorHeight, float uvShift)
        {
            float step = Mathf.PI * 2f / segments;
            float start = doorAngleDegrees * Mathf.Deg2Rad - doorSegments * step * 0.5f; // la abertura ocupa los primeros segmentos
            Vector3 up = Vector3.up;
            Vector3 Rad(float a) => new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 Outer(float a, float v) => center + Rad(a) * (radius - batter * v) + up * v;
            Vector3 Inner(float a, float v) => center + Rad(a) * (radius - thick) + up * v;
            Vector3 OuterN(float a) => (Rad(a) + up * batter).normalized;

            void Band(float a0, float a1, float v0, float v1)
            {
                if (v1 - v0 < 0.01f) return;
                float u0 = uvShift + (a0 - start) * radius, u1 = uvShift + (a1 - start) * radius;
                stone.SmoothQuad(Outer(a0, v0), Outer(a0, v1), Outer(a1, v1), Outer(a1, v0), OuterN(a0), OuterN(a0), OuterN(a1), OuterN(a1),
                    new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0));
                stone.SmoothQuad(Inner(a0, v0), Inner(a0, v1), Inner(a1, v1), Inner(a1, v0), -Rad(a0), -Rad(a0), -Rad(a1), -Rad(a1),
                    new Vector2(u0 + 50f, v0), new Vector2(u0 + 50f, v1), new Vector2(u1 + 50f, v1), new Vector2(u1 + 50f, v0));
            }

            float v1Door = doorSill + doorHeight;
            for (int i = 0; i < segments; i++)
            {
                float a0 = start + i * step, a1 = start + (i + 1) * step;
                if (i < doorSegments)
                {
                    Band(a0, a1, 0f, doorSill);
                    Band(a0, a1, v1Door, height);
                    if (doorSill > 0.01f) stone.Quad(Outer(a0, doorSill), Outer(a1, doorSill), Inner(a1, doorSill), Inner(a0, doorSill), up);
                }
                else
                {
                    Band(a0, a1, 0f, height);
                }
                stone.Quad(Outer(a0, height), Outer(a1, height), Inner(a1, height), Inner(a0, height), up);
            }

            if (doorSegments <= 0) return;
            float aStart = start, aEnd = start + doorSegments * step;
            Vector3 tangentStart = new Vector3(Mathf.Cos(aStart), 0f, -Mathf.Sin(aStart));
            Vector3 tangentEnd = new Vector3(Mathf.Cos(aEnd), 0f, -Mathf.Sin(aEnd));
            stone.Quad(Outer(aStart, doorSill), Outer(aStart, v1Door), Inner(aStart, v1Door), Inner(aStart, doorSill), tangentStart);
            stone.Quad(Outer(aEnd, doorSill), Outer(aEnd, v1Door), Inner(aEnd, v1Door), Inner(aEnd, doorSill), -tangentEnd);

            // Dintel tallado, quebrado para seguir la curva del muro
            var angles = new List<float> { aStart - step * 0.45f };
            for (int i = 0; i <= doorSegments; i++) angles.Add(aStart + i * step);
            angles.Add(aEnd + step * 0.45f);
            float y0 = v1Door, y1 = v1Door + 0.2f;
            Vector3 F(float a, float v) => center + Rad(a) * (radius - batter * v + 0.04f) + up * v;
            Vector3 B(float a, float v) => center + Rad(a) * (radius - thick - 0.03f) + up * v;
            for (int i = 0; i < angles.Count - 1; i++)
            {
                float a0 = angles[i], a1 = angles[i + 1];
                Vector3 mid = Rad((a0 + a1) * 0.5f);
                fine.Quad(F(a0, y0), F(a0, y1), F(a1, y1), F(a1, y0), mid);
                fine.Quad(B(a0, y0), B(a0, y1), B(a1, y1), B(a1, y0), -mid);
                fine.Quad(F(a0, y0), F(a1, y0), B(a1, y0), B(a0, y0), -up);
                fine.Quad(F(a0, y1), F(a1, y1), B(a1, y1), B(a0, y1), up);
            }
            Vector3 endA = new Vector3(Mathf.Cos(angles[0]), 0f, -Mathf.Sin(angles[0]));
            Vector3 endB = new Vector3(Mathf.Cos(angles[angles.Count - 1]), 0f, -Mathf.Sin(angles[angles.Count - 1]));
            fine.Quad(F(angles[0], y0), F(angles[0], y1), B(angles[0], y1), B(angles[0], y0), -endA);
            fine.Quad(F(angles[angles.Count - 1], y0), F(angles[angles.Count - 1], y1), B(angles[angles.Count - 1], y1), B(angles[angles.Count - 1], y0), endB);
        }

        /// <summary>Techo cónico de paja para colcas y torreones. <paramref name="center"/> está a la altura de la coronación del muro.</summary>
        public static void ConeRoof(MeshBuf thatch, MeshBuf fringe, MeshBuf wood, Vector3 center, float wallRadius, float pitchDegrees,
            float overhang, System.Random rng)
        {
            float angle = pitchDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle), tan = Mathf.Tan(angle);
            float T = ThatchThickness * 0.85f;
            Vector3 apex = center + Vector3.up * (wallRadius * tan + 0.04f);
            float slope = (wallRadius + overhang) / cos;
            const int tiers = 3;
            int sides = Mathf.Max(14, Mathf.RoundToInt(wallRadius * 9f));
            float topStart = -T * tan;
            Vector3 Rad(float a) => new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 Pt(float a, float s, float lift) => apex + Rad(a) * (cos * s + sin * lift) + Vector3.up * (-sin * s + cos * lift);

            var edgeS = new float[tiers + 1, sides + 1];
            var edgeLift = new float[tiers + 1, sides + 1];
            for (int k = 1; k <= tiers; k++)
            {
                for (int j = 0; j <= sides; j++)
                {
                    edgeS[k, j] = slope * k / tiers + Range(rng, -0.05f, 0.05f);
                    edgeLift[k, j] = T + TierLift + Range(rng, -0.015f, 0.02f);
                }
                edgeS[k, sides] = edgeS[k, 0];
                edgeLift[k, sides] = edgeLift[k, 0];
            }

            for (int j = 0; j < sides; j++)
            {
                float a0 = j * Mathf.PI * 2f / sides, a1 = (j + 1) * Mathf.PI * 2f / sides;
                Vector3 nrm = (Rad((a0 + a1) * 0.5f) * sin + Vector3.up * cos).normalized;
                Vector3 down = Rad((a0 + a1) * 0.5f) * cos - Vector3.up * sin;
                for (int k = 0; k < tiers; k++)
                {
                    float upper = k == 0 ? topStart : slope * k / tiers - 0.14f;
                    bool eave = k == tiers - 1;
                    float sa = edgeS[k + 1, j], sb = edgeS[k + 1, j + 1], la = edgeLift[k + 1, j], lb = edgeLift[k + 1, j + 1];
                    // Las UV siguen el arco a media altura, para que la paja no se estire hacia la punta más de la cuenta
                    float arc = (wallRadius + overhang) * 0.6f;
                    float ua = a0 * arc / ThatchTile, ub = a1 * arc / ThatchTile;
                    thatch.Quad(Pt(a0, upper, T), Pt(a1, upper, T), Pt(a1, sb, lb), Pt(a0, sa, la),
                        new Vector2(ua, upper / ThatchTile), new Vector2(ub, upper / ThatchTile), new Vector2(ub, sb / ThatchTile), new Vector2(ua, sa / ThatchTile), nrm);
                    float bottom = eave ? 0f : T - 0.03f;
                    thatch.Quad(Pt(a0, sa, la), Pt(a1, sb, lb), Pt(a1, sb, bottom), Pt(a0, sa, bottom),
                        new Vector2(ua, 0f), new Vector2(ub, 0f), new Vector2(ub, 0.2f), new Vector2(ua, 0.2f), down);
                    float hang = 0.24f;
                    float endLift = eave ? T * 0.3f : T + 0.035f + TierLift * (hang + 0.14f) / (slope / tiers);
                    float fa = a0 * arc / 0.8f, fb = a1 * arc / 0.8f;
                    fringe.Quad(Pt(a0, sa - 0.04f, la + 0.006f), Pt(a1, sb - 0.04f, lb + 0.006f), Pt(a1, sb + hang, endLift), Pt(a0, sa + hang, endLift),
                        new Vector2(fa, 1f), new Vector2(fb, 1f), new Vector2(fb, 0f), new Vector2(fa, 0f), nrm);
                }
                thatch.Quad(Pt(a0, 0f, 0f), Pt(a1, 0f, 0f), Pt(a1, slope + 0.05f, 0f), Pt(a0, slope + 0.05f, 0f), -nrm);
            }

            // Moño de paja atado en la punta y el palo central
            Vector3 tip = apex + Vector3.up * (T / cos);
            Tube(thatch, tip - Vector3.up * 0.12f, tip + Vector3.up * 0.3f, 0.17f, 0.06f, 8, true, 1f / ThatchTile);
            Tube(wood, tip + Vector3.up * 0.25f, tip + Vector3.up * 0.62f, 0.035f, 0.02f, 5, true);
        }
    }
}
#endif
