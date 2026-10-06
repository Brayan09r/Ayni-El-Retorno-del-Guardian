#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ayni.Editor
{
    /// <summary>Texturas de la aldea inca, pintadas por código y guardadas como PNG: paja de ichu, madera, tejidos y cerámica.</summary>
    internal static class VillageTextures
    {
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Ruido suave que se repite cada (px, py) celdas: la textura enlosa sin costuras.</summary>
        private static float Noise(float x, float y, int px, int py, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = ((x0 % px) + px) % px, xb = (xa + 1) % px, ya = ((y0 % py) + py) % py, yb = (ya + 1) % py;
            float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static Color Ramp(Color dark, Color mid, Color light, float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? Color.Lerp(dark, mid, t * 2f) : Color.Lerp(mid, light, t * 2f - 1f);
        }

        private static float ThatchHeight(float u, float v)
        {
            // Hebras de ichu que bajan por el faldón (largas en v, finas en u)
            float strands = 0.42f * Noise(u * 96f, v * 5f, 96, 5, 1) + 0.24f * Noise(u * 41f, v * 3f, 41, 3, 2) + 0.34f * Noise(u * 190f, v * 12f, 190, 12, 3);
            return strands;
        }

        public static Texture2D Thatch(string albedoPath, string normalPath, out Texture2D normalMap)
        {
            const int size = 512;
            var albedo = new Texture2D(size, size, TextureFormat.RGB24, false);
            var normal = new Texture2D(size, size, TextureFormat.RGB24, false);
            var heights = new float[size, size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) heights[x, y] = ThatchHeight((x + 0.5f) / size, (y + 0.5f) / size);
            }

            var dark = new Color(0.20f, 0.155f, 0.095f);
            var mid = new Color(0.47f, 0.39f, 0.25f);
            var light = new Color(0.68f, 0.59f, 0.40f);
            var grey = new Color(0.40f, 0.38f, 0.33f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float h = heights[x, y];
                    float clump = Noise(u * 7f, v * 4f, 7, 4, 4);
                    float weather = Noise(u * 3f, v * 3f, 3, 3, 5);
                    Color c = Ramp(dark, mid, light, (h - 0.22f) * 1.75f * (0.8f + 0.4f * clump));
                    c = Color.Lerp(c, grey * (0.6f + 0.8f * h), 0.22f + 0.3f * weather);
                    albedo.SetPixel(x, y, c);

                    float dx = heights[(x + 1) % size, y] - heights[(x + size - 1) % size, y];
                    float dy = heights[x, (y + 1) % size] - heights[x, (y + size - 1) % size];
                    Vector3 n = new Vector3(-dx * 7f, -dy * 2.5f, 1f).normalized;
                    normal.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f));
                }
            }
            normalMap = Save(normal, normalPath, true, false, false);
            return Save(albedo, albedoPath, false, false, false);
        }

        /// <summary>Puntas de paja que cuelgan del borde de cada tanda: opaco arriba, hebras sueltas abajo.</summary>
        public static Texture2D Fringe(string path)
        {
            const int w = 256, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var rng = new System.Random(77);
            int x = 0;
            var dark = new Color(0.24f, 0.19f, 0.12f);
            var light = new Color(0.66f, 0.57f, 0.38f);
            while (x < w)
            {
                int width = 2 + rng.Next(3);
                float length = 0.3f + 0.7f * (float)rng.NextDouble();
                if (rng.NextDouble() < 0.18) length = 0.12f + 0.2f * (float)rng.NextDouble(); // huecos entre mechones
                Color strand = Color.Lerp(dark, light, 0.25f + 0.75f * (float)rng.NextDouble());
                for (int i = 0; i < width && x < w; i++, x++)
                {
                    float edge = (i == 0 || i == width - 1) ? 0.82f : 1f;
                    for (int y = 0; y < h; y++)
                    {
                        float v = (y + 0.5f) / h;
                        bool solid = v > 1f - length;
                        // La raíz queda a la sombra del canto de la tanda; la punta, más clara y seca
                        float shade = Mathf.Lerp(1.08f, 0.62f, Mathf.InverseLerp(0.35f, 1f, v));
                        Color c = strand * (shade * edge);
                        c.a = solid ? 1f : 0f;
                        tex.SetPixel(x, y, c);
                    }
                }
            }
            return Save(tex, path, false, true, true);
        }

        public static Texture2D Wood(string path)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var dark = new Color(0.19f, 0.135f, 0.09f);
            var mid = new Color(0.34f, 0.25f, 0.17f);
            var light = new Color(0.47f, 0.37f, 0.27f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    // La veta corre a lo largo del palo (u)
                    float grain = 0.5f * Noise(u * 5f, v * 46f, 5, 46, 11) + 0.3f * Noise(u * 9f, v * 110f, 9, 110, 12) + 0.2f * Noise(u * 3f, v * 14f, 3, 14, 13);
                    float knots = Noise(u * 4f, v * 4f, 4, 4, 14);
                    Color c = Ramp(dark, mid, light, grain * 1.25f - 0.1f);
                    c *= 0.85f + 0.3f * knots;
                    tex.SetPixel(x, y, c);
                }
            }
            return Save(tex, path, false, false, false);
        }

        /// <summary>Cuatro tejidos andinos en una sola textura (2 x 2): rombos escalonados, listas con zigzag, chakanas y bayeta lisa.</summary>
        public static Texture2D Textiles(string path)
        {
            const int size = 512, quad = 256, stitch = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var red = new Color(0.56f, 0.10f, 0.09f);
            var black = new Color(0.085f, 0.07f, 0.07f);
            var ochre = new Color(0.76f, 0.52f, 0.14f);
            var cream = new Color(0.82f, 0.76f, 0.62f);
            var teal = new Color(0.09f, 0.31f, 0.33f);
            var brown = new Color(0.33f, 0.21f, 0.13f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int qx = x / quad, qy = y / quad;
                    int ix = (x % quad) / stitch, iy = (y % quad) / stitch; // 32 x 32 puntos por tejido
                    Color c;
                    if (qx == 0 && qy == 0)
                    {
                        c = red;
                        if (iy == 2 || iy == 3 || iy == 28 || iy == 29) c = ochre;
                        if (iy == 4 || iy == 27 || iy == 1 || iy == 30) c = black;
                        float d = Mathf.Abs((ix % 16) - 7.5f) + Mathf.Abs(iy - 15.5f);
                        if (d < 2f) c = cream;
                        else if (d < 4f) c = black;
                        else if (d < 6f) c = ochre;
                        else if (d < 7f) c = black;
                    }
                    else if (qx == 1 && qy == 0)
                    {
                        int s = ix % 8;
                        c = s < 2 ? teal : s == 2 ? cream : s < 5 ? red : s == 5 ? black : ochre;
                        if (iy >= 13 && iy <= 18)
                        {
                            int k = iy - 13;
                            int zig = k < 3 ? k : 5 - k;
                            c = (ix + zig) % 4 == 0 ? cream : black;
                        }
                        if (iy == 12 || iy == 19) c = ochre;
                    }
                    else if (qx == 0 && qy == 1)
                    {
                        float cx = Mathf.Abs((ix % 16) - 7.5f), cy = Mathf.Abs((iy % 16) - 7.5f);
                        c = teal;
                        if ((cx < 2f && cy < 6f) || (cx < 6f && cy < 2f) || (cx < 4f && cy < 4f)) c = ochre;
                        if (cx < 1f && cy < 1f) c = black;
                        if (iy % 16 == 0 || iy % 16 == 15) c = cream;
                        if (ix % 16 == 0 || ix % 16 == 15) c = red;
                    }
                    else
                    {
                        c = brown * (0.8f + 0.4f * Noise(x / 37f, y / 9f, 14, 57, 21));
                        if (iy == 6 || iy == 7 || iy == 24 || iy == 25) c = black;
                        if (iy == 8 || iy == 23) c = cream * 0.8f;
                    }

                    // Trama: cada punto del tejido se abomba un poco y la lana no es de un tono uniforme
                    float thread = (x % 4 < 2 ? 1f : 0.9f) * (y % 4 < 2 ? 1f : 0.93f);
                    float fuzz = 0.9f + 0.2f * Hash(x, y, 31);
                    float wear = 0.82f + 0.28f * Noise(x / 64f, y / 64f, 8, 8, 32);
                    c *= thread * fuzz * wear;
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }
            return Save(tex, path, false, false, false);
        }

        /// <summary>Cerámica: barro cocido con una banda pintada en el hombro del cántaro.</summary>
        public static Texture2D Ceramic(string path)
        {
            const int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var clay = new Color(0.55f, 0.29f, 0.17f);
            var soot = new Color(0.17f, 0.12f, 0.10f);
            var cream = new Color(0.80f, 0.71f, 0.54f);
            var black = new Color(0.10f, 0.08f, 0.07f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float n = Noise(u * 12f, v * 12f, 12, 12, 41);
                    Color c = clay * (0.85f + 0.3f * n);
                    c = Color.Lerp(c, soot, Mathf.Clamp01((0.22f - v) * 4f) * (0.5f + 0.5f * n)); // tizne de fogón abajo
                    if (v > 0.50f && v < 0.68f)
                    {
                        c = cream * (0.88f + 0.2f * n);
                        int ix = x / 8, iy = (int)((v - 0.50f) / 0.18f * 6f);
                        int zig = iy < 3 ? iy : 5 - iy;
                        if ((ix + zig) % 4 == 0) c = black;
                        if ((ix + zig) % 4 == 2 && (iy == 2 || iy == 3)) c = clay * 0.8f;
                    }
                    if ((v > 0.485f && v <= 0.50f) || (v >= 0.68f && v < 0.695f)) c = black;
                    tex.SetPixel(x, y, c);
                }
            }
            return Save(tex, path, false, false, false);
        }

        private static Texture2D Save(Texture2D tex, string path, bool normalMap, bool alpha, bool clampV)
        {
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (!normalMap)
                {
                    importer.sRGBTexture = true;
                    importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                    importer.alphaIsTransparency = alpha;
                }
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = clampV ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 6;
                importer.maxTextureSize = 1024;
                if (alpha)
                {
                    importer.mipMapsPreserveCoverage = true;
                    importer.alphaTestReferenceValue = 0.38f;
                }
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
#endif
