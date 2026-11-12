using UnityEngine;

namespace Ayni.World.Fauna
{
    /// <summary>
    /// Construye por código la fauna menor de la puna, low-poly y de autoría propia:
    ///  - Vizcacha de la sierra (Lagidium peruanum): pelaje gris, orejas largas, ancas grandes
    ///    y una cola larga enroscada hacia arriba con la punta oscura. Suele tomar sol sentada sobre las rocas.
    /// Orientación: el animal mira hacia +Z y se apoya en Y = 0. No crea colisionadores.
    /// </summary>
    public static class AyniCritterModel
    {
        public struct VizcachaParts
        {
            public GameObject root;
            public Transform body;      // Se encoge al agazaparse y se estira al saltar
            public Transform head;
            public Transform leftEar;
            public Transform rightEar;
            public Transform tail;
        }

        private static readonly Color VizcachaGray = new Color(0.56f, 0.54f, 0.50f);
        private static readonly Color VizcachaCream = new Color(0.84f, 0.79f, 0.68f);
        private static readonly Color VizcachaTail = new Color(0.32f, 0.30f, 0.27f);
        private static readonly Color DarkTip = new Color(0.08f, 0.07f, 0.07f);
        private static readonly Color EyeDark = new Color(0.04f, 0.03f, 0.03f);

        private static Material M(Color c) => AyniFaunaPrimitives.Mat(c, 0.1f);

        private static GameObject P(PrimitiveType type, string name, Transform parent, Color color,
            Vector3 pos, Vector3 euler, Vector3 scale)
            => AyniFaunaPrimitives.Primitive(type, name, parent, M(color), pos, euler, scale);

        /// <summary>Crea una vizcacha sentada como hijo de <paramref name="parent"/>.</summary>
        public static VizcachaParts BuildVizcacha(Transform parent, string name, float scale = 1f)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);

            Transform scaled = AyniFaunaPrimitives.Pivot("Escala", root.transform, Vector3.zero);
            scaled.localScale = Vector3.one * scale;
            Transform body = AyniFaunaPrimitives.Pivot("Cuerpo", scaled, Vector3.zero);

            // Tronco inclinado (sentada sobre las ancas) y vientre claro
            P(PrimitiveType.Sphere, "Tronco", body, VizcachaGray,
                new Vector3(0f, 0.17f, -0.02f), new Vector3(-25f, 0f, 0f), new Vector3(0.24f, 0.26f, 0.36f));
            P(PrimitiveType.Sphere, "Vientre", body, VizcachaCream,
                new Vector3(0f, 0.17f, 0.09f), Vector3.zero, new Vector3(0.16f, 0.2f, 0.12f));
            for (int side = -1; side <= 1; side += 2)
            {
                P(PrimitiveType.Sphere, side < 0 ? "Anca_Izq" : "Anca_Der", body, VizcachaGray,
                    new Vector3(side * 0.09f, 0.09f, -0.1f), Vector3.zero, new Vector3(0.13f, 0.16f, 0.2f));
                P(PrimitiveType.Capsule, side < 0 ? "Mano_Izq" : "Mano_Der", body, VizcachaCream,
                    new Vector3(side * 0.05f, 0.05f, 0.11f), Vector3.zero, new Vector3(0.04f, 0.05f, 0.04f));
            }

            // Cabeza redondeada con hocico y ojos grandes
            Transform head = AyniFaunaPrimitives.Pivot("Cabeza", body, new Vector3(0f, 0.31f, 0.11f));
            P(PrimitiveType.Sphere, "Cabeza_Malla", head, VizcachaGray,
                new Vector3(0f, 0f, 0.03f), Vector3.zero, new Vector3(0.15f, 0.14f, 0.18f));
            P(PrimitiveType.Sphere, "Hocico", head, VizcachaCream,
                new Vector3(0f, -0.025f, 0.11f), Vector3.zero, new Vector3(0.08f, 0.07f, 0.08f));
            P(PrimitiveType.Sphere, "Ojo_Izq", head, EyeDark,
                new Vector3(-0.06f, 0.02f, 0.07f), Vector3.zero, Vector3.one * 0.028f);
            P(PrimitiveType.Sphere, "Ojo_Der", head, EyeDark,
                new Vector3(0.06f, 0.02f, 0.07f), Vector3.zero, Vector3.one * 0.028f);

            // Orejas largas con pivote propio para que se muevan
            Transform leftEar = AyniFaunaPrimitives.Pivot("Oreja_Izq", head, new Vector3(-0.04f, 0.055f, -0.01f));
            Transform rightEar = AyniFaunaPrimitives.Pivot("Oreja_Der", head, new Vector3(0.04f, 0.055f, -0.01f));
            leftEar.localRotation = Quaternion.Euler(-8f, 0f, 14f);
            rightEar.localRotation = Quaternion.Euler(-8f, 0f, -14f);
            P(PrimitiveType.Capsule, "Oreja_Malla", leftEar, VizcachaGray,
                new Vector3(0f, 0.07f, 0f), Vector3.zero, new Vector3(0.045f, 0.075f, 0.022f));
            P(PrimitiveType.Capsule, "Oreja_Malla", rightEar, VizcachaGray,
                new Vector3(0f, 0.07f, 0f), Vector3.zero, new Vector3(0.045f, 0.075f, 0.022f));

            // Cola larga enroscada hacia arriba, cada vez más oscura hasta la punta
            Transform tail = AyniFaunaPrimitives.Pivot("Cola", body, new Vector3(0f, 0.1f, -0.2f));
            Vector3[] curl =
            {
                new Vector3(0f, 0.00f, -0.05f),
                new Vector3(0f, 0.05f, -0.11f),
                new Vector3(0f, 0.12f, -0.14f),
                new Vector3(0f, 0.19f, -0.12f),
                new Vector3(0f, 0.24f, -0.07f),
            };
            for (int i = 0; i < curl.Length; i++)
            {
                float t = i / (float)(curl.Length - 1);
                Color c = i == curl.Length - 1 ? DarkTip : Color.Lerp(VizcachaGray, VizcachaTail, t);
                float size = Mathf.Lerp(0.07f, 0.085f, t); // la punta es más tupida
                P(PrimitiveType.Sphere, "Cola_" + (i + 1), tail, c, curl[i], Vector3.zero, Vector3.one * size);
            }

            return new VizcachaParts
            {
                root = root, body = body, head = head, leftEar = leftEar, rightEar = rightEar, tail = tail
            };
        }
    }
}
