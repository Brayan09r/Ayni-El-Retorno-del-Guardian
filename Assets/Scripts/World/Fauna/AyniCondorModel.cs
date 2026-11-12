using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ayni.World.Fauna
{
    /// <summary>
    /// Construye por código un cóndor andino estilizado (low-poly), sin depender de modelos externos ni licencias:
    ///  - Cuerpo y cola de plumaje negro.
    ///  - Collar de plumón blanco, rasgo más reconocible del Kuntur.
    ///  - Cabeza desnuda de tono rosado grisáceo con pico marfil.
    ///  - Alas largas (~3 m de envergadura) con banda blanca en las secundarias y plumas primarias
    ///    separadas como "dedos" en la punta, como se ve al cóndor planear sobre los cañones.
    /// Las alas cuelgan de pivotes propios para que AyniCondorFlight pueda aletearlas.
    /// No crea colisionadores.
    /// </summary>
    public static class AyniCondorModel
    {
        public struct Parts
        {
            public GameObject root;
            public Transform leftWing;
            public Transform rightWing;
        }

        private static readonly Color PlumajeNegro = new Color(0.06f, 0.06f, 0.07f, 1f);
        private static readonly Color PlumonBlanco = new Color(0.92f, 0.91f, 0.88f, 1f);
        private static readonly Color PielCabeza = new Color(0.62f, 0.42f, 0.40f, 1f);
        private static readonly Color PicoMarfil = new Color(0.82f, 0.78f, 0.66f, 1f);

        private static Material matNegro, matBlanco, matCabeza, matPico;
        private static Mesh wingMesh, wingPanelMesh;

        /// <summary>Crea un cóndor completo como hijo de <paramref name="parent"/>.</summary>
        public static Parts Build(Transform parent, string name, float scale = 1f)
        {
            EnsureAssets();

            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);

            var body = new GameObject("Cuerpo");
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * scale;

            // Torso alargado en el eje de vuelo (Z)
            Primitive(PrimitiveType.Capsule, "Torso", body.transform, matNegro,
                new Vector3(0f, 0f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.34f, 0.55f, 0.30f));
            // Collar blanco
            Primitive(PrimitiveType.Sphere, "Collar", body.transform, matBlanco,
                new Vector3(0f, 0.02f, 0.44f), Vector3.zero, new Vector3(0.30f, 0.26f, 0.22f));
            // Cuello y cabeza desnuda
            Primitive(PrimitiveType.Capsule, "Cuello", body.transform, matCabeza,
                new Vector3(0f, 0.03f, 0.56f), new Vector3(80f, 0f, 0f), new Vector3(0.09f, 0.09f, 0.09f));
            Primitive(PrimitiveType.Sphere, "Cabeza", body.transform, matCabeza,
                new Vector3(0f, 0.05f, 0.66f), Vector3.zero, new Vector3(0.13f, 0.12f, 0.17f));
            Primitive(PrimitiveType.Cube, "Pico", body.transform, matPico,
                new Vector3(0f, 0.03f, 0.76f), new Vector3(12f, 0f, 0f), new Vector3(0.04f, 0.045f, 0.08f));
            // Cola corta en abanico
            Primitive(PrimitiveType.Cube, "Cola", body.transform, matNegro,
                new Vector3(0f, -0.01f, -0.62f), Vector3.zero, new Vector3(0.36f, 0.025f, 0.34f));

            Transform left = BuildWing(body.transform, "Ala_Izquierda", -1f);
            Transform right = BuildWing(body.transform, "Ala_Derecha", 1f);

            return new Parts { root = root, leftWing = left, rightWing = right };
        }

        private static Transform BuildWing(Transform body, string name, float side)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(body, false);
            pivot.transform.localPosition = new Vector3(0.13f * side, 0.06f, 0.05f);
            // El ala derecha se refleja con escala negativa en X; la malla es de doble cara
            pivot.transform.localScale = new Vector3(side, 1f, 1f);

            AddMesh(pivot.transform, "Plumas", wingMesh, matNegro);
            AddMesh(pivot.transform, "Banda_Blanca", wingPanelMesh, matBlanco);
            return pivot.transform;
        }

        // ------------------------------------------------------------------------------------------------
        private static void EnsureAssets()
        {
            if (matNegro == null) matNegro = MakeMaterial("Ayni_Condor_Negro", PlumajeNegro, 0.18f);
            if (matBlanco == null) matBlanco = MakeMaterial("Ayni_Condor_Blanco", PlumonBlanco, 0.10f);
            if (matCabeza == null) matCabeza = MakeMaterial("Ayni_Condor_Cabeza", PielCabeza, 0.30f);
            if (matPico == null) matPico = MakeMaterial("Ayni_Condor_Pico", PicoMarfil, 0.40f);
            if (wingMesh == null) wingMesh = CreateWingMesh();
            if (wingPanelMesh == null) wingPanelMesh = CreateWingPanelMesh();
        }

        private static Material MakeMaterial(string name, Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name, color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat,
            Vector3 pos, Vector3 euler, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) DestroyCollider(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            ConfigureRenderer(go.GetComponent<MeshRenderer>(), mat);
            return go;
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            ConfigureRenderer(go.AddComponent<MeshRenderer>(), mat);
        }

        private static void ConfigureRenderer(MeshRenderer mr, Material mat)
        {
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;
        }

        private static void DestroyCollider(Collider col)
        {
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
        }

        // ------------------------------------------------------------------------------------------------
        // Mallas de ala (plano XZ, se extienden hacia +X desde el hombro)

        /// <summary>Ala negra: tramo interior ancho + 6 plumas primarias separadas en la punta.</summary>
        private static Mesh CreateWingMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // Tramo interior (brazo y antebrazo), algo más estrecho hacia la muñeca
            AddQuad(verts, tris,
                new Vector3(0.00f, 0f,  0.17f),
                new Vector3(1.05f, 0f,  0.13f),
                new Vector3(1.05f, 0f, -0.27f),
                new Vector3(0.00f, 0f, -0.33f));

            // Plumas primarias abiertas como dedos, cada una algo más larga y desviada hacia atrás
            const int fingers = 6;
            for (int i = 0; i < fingers; i++)
            {
                float t = i / (float)(fingers - 1);
                float zRoot = Mathf.Lerp(0.11f, -0.24f, t);
                float length = Mathf.Lerp(0.42f, 0.30f, Mathf.Abs(t - 0.35f) * 1.4f);
                float sweep = Mathf.Lerp(0.04f, -0.16f, t);
                const float halfWidth = 0.028f;

                Vector3 rootA = new Vector3(1.04f, 0f, zRoot + halfWidth);
                Vector3 rootB = new Vector3(1.04f, 0f, zRoot - halfWidth);
                Vector3 tipA = new Vector3(1.04f + length, 0.02f * i, zRoot + sweep + halfWidth * 0.6f);
                Vector3 tipB = new Vector3(1.04f + length, 0.02f * i, zRoot + sweep - halfWidth * 0.6f);
                AddQuad(verts, tris, rootA, tipA, tipB, rootB);
            }

            return BuildDoubleSided("Ayni_Condor_Ala", verts, tris);
        }

        /// <summary>Banda blanca de las plumas secundarias, sobre el borde de fuga del ala.</summary>
        private static Mesh CreateWingPanelMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            AddQuad(verts, tris,
                new Vector3(0.12f, 0.004f, -0.10f),
                new Vector3(0.98f, 0.004f, -0.08f),
                new Vector3(0.98f, 0.004f, -0.24f),
                new Vector3(0.12f, 0.004f, -0.29f));
            return BuildDoubleSided("Ayni_Condor_Banda", verts, tris);
        }

        private static void AddQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>Duplica la geometría con caras invertidas para que el ala se vea desde arriba y desde abajo.</summary>
        private static Mesh BuildDoubleSided(string name, List<Vector3> verts, List<int> tris)
        {
            int n = verts.Count;
            var allVerts = new List<Vector3>(verts);
            allVerts.AddRange(verts);

            var normals = new List<Vector3>(n * 2);
            for (int i = 0; i < n; i++) normals.Add(Vector3.up);
            for (int i = 0; i < n; i++) normals.Add(Vector3.down);

            var allTris = new List<int>(tris);
            for (int i = 0; i < tris.Count; i += 3)
            {
                allTris.Add(tris[i] + n);
                allTris.Add(tris[i + 2] + n);
                allTris.Add(tris[i + 1] + n);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(allVerts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(allTris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
