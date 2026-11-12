using UnityEngine;

namespace Ayni.World.Fauna
{
    /// <summary>Las tres especies de camélidos andinos que acompañan la aldea.</summary>
    public enum AyniCamelidSpecies
    {
        Llama = 0,   // Grande, orejas largas en forma de plátano, borlas de lana de colores (animal de carga del tambo)
        Alpaca = 1,  // Más baja y lanuda, orejas cortas y copete de lana en la cabeza (fibra para los tejedores)
        Vicuna = 2   // Silvestre, esbelta, color canela con pechera y vientre blancos
    }

    /// <summary>
    /// Construye por código camélidos andinos estilizados (low-poly), de autoría propia y sin licencias externas.
    /// Cada animal tiene pivotes articulados para que <see cref="AyniGrazingAnimal"/> pueda animarlo:
    ///  - Cuatro patas que cuelgan desde la cadera y el hombro.
    ///  - Cuello que baja hasta el pasto y cabeza que se orienta de forma independiente.
    /// Orientación: el animal mira hacia +Z y sus pezuñas tocan Y = 0. No crea colisionadores.
    /// </summary>
    public static class AyniCamelidModel
    {
        public struct Parts
        {
            public GameObject root;
            public Transform body;       // Se mece al caminar
            public Transform neck;       // Pivote en el hombro (gira en X para bajar a pastar)
            public Transform head;       // Pivote al final del cuello
            public Transform[] legs;     // 0 del-izq, 1 del-der, 2 tras-izq, 3 tras-der
        }

        private struct Proportions
        {
            public float backHeight, bodyLength, bodyThickness;
            public float legThickness, neckLength, neckThickness;
            public float headLength, headThickness, earLength;
        }

        // Paletas de color naturales de cada especie
        private static readonly Color[] LlamaCoats =
        {
            new Color(0.93f, 0.91f, 0.86f), // blanca
            new Color(0.45f, 0.30f, 0.19f), // café
            new Color(0.16f, 0.13f, 0.12f), // negra
            new Color(0.70f, 0.55f, 0.38f), // vicuñada clara
        };
        private static readonly Color[] AlpacaCoats =
        {
            new Color(0.96f, 0.94f, 0.88f), // blanca
            new Color(0.86f, 0.76f, 0.58f), // crema (fawn)
            new Color(0.55f, 0.40f, 0.27f), // café claro
            new Color(0.32f, 0.30f, 0.30f), // gris oscuro
        };
        private static readonly Color VicunaCoat = new Color(0.78f, 0.52f, 0.28f);   // canela
        private static readonly Color BellyWhite = new Color(0.95f, 0.93f, 0.88f);
        private static readonly Color HoofDark = new Color(0.12f, 0.10f, 0.09f);
        private static readonly Color EyeDark = new Color(0.05f, 0.04f, 0.04f);

        // Borlas de lana (t'ikas) que los pastores atan a las orejas de las llamas
        private static readonly Color[] TasselColors =
        {
            new Color(0.86f, 0.12f, 0.25f), // magenta
            new Color(0.95f, 0.55f, 0.08f), // naranja
            new Color(0.98f, 0.82f, 0.12f), // amarillo
            new Color(0.15f, 0.55f, 0.35f), // verde
        };

        /// <summary>Crea un camélido completo como hijo de <paramref name="parent"/>.</summary>
        /// <param name="seed">Semilla para el color del pelaje y pequeñas variaciones de tamaño.</param>
        public static Parts Build(Transform parent, string name, AyniCamelidSpecies species, int seed)
        {
            var rng = new System.Random(seed);
            Proportions p = GetProportions(species);
            float sizeJitter = 0.92f + (float)rng.NextDouble() * 0.16f;

            Color coat;
            switch (species)
            {
                case AyniCamelidSpecies.Llama: coat = LlamaCoats[rng.Next(LlamaCoats.Length)]; break;
                case AyniCamelidSpecies.Alpaca: coat = AlpacaCoats[rng.Next(AlpacaCoats.Length)]; break;
                default: coat = VicunaCoat; break;
            }

            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);

            // Contenedor escalado: todas las medidas de abajo están en metros de un animal "promedio"
            var scaled = new GameObject("Escala");
            scaled.transform.SetParent(root.transform, false);
            scaled.transform.localScale = Vector3.one * sizeJitter;

            var body = new GameObject("Cuerpo").transform;
            body.SetParent(scaled.transform, false);

            float legLength = p.backHeight - p.bodyThickness * 0.55f;
            float bodyY = legLength + p.bodyThickness * 0.05f;

            // Torso horizontal
            Primitive(PrimitiveType.Capsule, "Torso", body, Mat(coat),
                new Vector3(0f, bodyY, 0f), new Vector3(90f, 0f, 0f),
                new Vector3(p.bodyThickness * 0.9f, p.bodyLength * 0.5f, p.bodyThickness));

            if (species == AyniCamelidSpecies.Vicuna)
            {
                // Vientre y pechera blancos
                Primitive(PrimitiveType.Capsule, "Vientre", body, Mat(BellyWhite),
                    new Vector3(0f, bodyY - p.bodyThickness * 0.18f, 0f), new Vector3(90f, 0f, 0f),
                    new Vector3(p.bodyThickness * 0.82f, p.bodyLength * 0.42f, p.bodyThickness * 0.7f));
                Primitive(PrimitiveType.Sphere, "Pechera", body, Mat(BellyWhite),
                    new Vector3(0f, bodyY - p.bodyThickness * 0.05f, p.bodyLength * 0.42f), Vector3.zero,
                    new Vector3(p.bodyThickness * 0.55f, p.bodyThickness * 0.7f, p.bodyThickness * 0.4f));
            }
            else if (species == AyniCamelidSpecies.Llama && rng.NextDouble() < 0.35)
            {
                // Algunas llamas tienen manchas de otro color en el lomo
                Color patch = coat.grayscale > 0.5f ? LlamaCoats[1] : LlamaCoats[0];
                Primitive(PrimitiveType.Sphere, "Mancha", body, Mat(patch),
                    new Vector3(p.bodyThickness * 0.12f, bodyY + p.bodyThickness * 0.12f, -p.bodyLength * 0.1f), Vector3.zero,
                    new Vector3(p.bodyThickness * 0.75f, p.bodyThickness * 0.6f, p.bodyLength * 0.45f));
            }

            // Cola corta
            Primitive(PrimitiveType.Sphere, "Cola", body, Mat(coat),
                new Vector3(0f, bodyY + p.bodyThickness * 0.15f, -p.bodyLength * 0.55f), new Vector3(-30f, 0f, 0f),
                new Vector3(0.12f, 0.2f, 0.12f) * (species == AyniCamelidSpecies.Alpaca ? 1.3f : 1f));

            // Patas: pivote en el hombro/cadera, la pata cuelga hacia -Y
            var legs = new Transform[4];
            float legX = p.bodyThickness * 0.28f;
            float legZ = p.bodyLength * 0.38f;
            Vector3[] hips =
            {
                new Vector3(-legX, legLength, legZ),
                new Vector3( legX, legLength, legZ),
                new Vector3(-legX, legLength, -legZ),
                new Vector3( legX, legLength, -legZ),
            };
            string[] legNames = { "Pata_Del_Izq", "Pata_Del_Der", "Pata_Tras_Izq", "Pata_Tras_Der" };
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject(legNames[i]).transform;
                pivot.SetParent(body, false);
                pivot.localPosition = hips[i];

                // Muslo con lana (más grueso arriba) y caña delgada
                float upper = species == AyniCamelidSpecies.Alpaca ? 1.35f : 1.15f;
                Primitive(PrimitiveType.Capsule, "Muslo", pivot, Mat(species == AyniCamelidSpecies.Vicuna && i >= 2 ? BellyWhite : coat),
                    new Vector3(0f, -legLength * 0.25f, 0f), Vector3.zero,
                    new Vector3(p.legThickness * upper, legLength * 0.27f, p.legThickness * upper));
                Primitive(PrimitiveType.Capsule, "Cana", pivot, Mat(coat),
                    new Vector3(0f, -legLength * 0.68f, 0f), Vector3.zero,
                    new Vector3(p.legThickness * 0.7f, legLength * 0.3f, p.legThickness * 0.7f));
                Primitive(PrimitiveType.Cube, "Pezuna", pivot, Mat(HoofDark),
                    new Vector3(0f, -legLength + 0.03f, 0.03f), Vector3.zero,
                    new Vector3(p.legThickness * 0.8f, 0.06f, p.legThickness * 1.3f));
                legs[i] = pivot;
            }

            // Cuello: pivote en el hombro, se extiende hacia +Y
            var neck = new GameObject("Cuello").transform;
            neck.SetParent(body, false);
            neck.localPosition = new Vector3(0f, bodyY + p.bodyThickness * 0.2f, p.bodyLength * 0.45f);
            float neckThick = p.neckThickness * (species == AyniCamelidSpecies.Alpaca ? 1.35f : 1f);
            Primitive(PrimitiveType.Capsule, "Cuello_Malla", neck, Mat(species == AyniCamelidSpecies.Vicuna ? VicunaCoat : coat),
                new Vector3(0f, p.neckLength * 0.5f, 0f), Vector3.zero,
                new Vector3(neckThick, p.neckLength * 0.55f, neckThick));

            // Cabeza: pivote al final del cuello, el hocico apunta hacia +Z
            var head = new GameObject("Cabeza").transform;
            head.SetParent(neck, false);
            head.localPosition = new Vector3(0f, p.neckLength, 0f);
            Primitive(PrimitiveType.Capsule, "Cabeza_Malla", head, Mat(coat),
                new Vector3(0f, 0f, p.headLength * 0.3f), new Vector3(90f, 0f, 0f),
                new Vector3(p.headThickness, p.headLength * 0.5f, p.headThickness * 1.1f));
            Primitive(PrimitiveType.Sphere, "Ojo_Izq", head, Mat(EyeDark),
                new Vector3(-p.headThickness * 0.45f, p.headThickness * 0.2f, p.headLength * 0.25f), Vector3.zero, Vector3.one * 0.035f);
            Primitive(PrimitiveType.Sphere, "Ojo_Der", head, Mat(EyeDark),
                new Vector3(p.headThickness * 0.45f, p.headThickness * 0.2f, p.headLength * 0.25f), Vector3.zero, Vector3.one * 0.035f);

            for (int side = -1; side <= 1; side += 2)
            {
                // Orejas: largas y curvas en la llama, cortas en la alpaca, finas en la vicuña
                float tilt = species == AyniCamelidSpecies.Llama ? 8f : 18f;
                var ear = Primitive(PrimitiveType.Capsule, side < 0 ? "Oreja_Izq" : "Oreja_Der", head, Mat(coat),
                    new Vector3(side * p.headThickness * 0.3f, p.headThickness * 0.45f + p.earLength * 0.45f, 0f),
                    new Vector3(-10f, 0f, -side * tilt),
                    new Vector3(0.05f, p.earLength * 0.5f, 0.035f));

                if (species == AyniCamelidSpecies.Llama)
                {
                    Color tassel = TasselColors[rng.Next(TasselColors.Length)];
                    Primitive(PrimitiveType.Sphere, "Borla", ear.transform, Mat(tassel),
                        new Vector3(0f, 0.2f, 0.6f), Vector3.zero, new Vector3(1.6f, 0.6f, 2.2f));
                }
            }

            if (species == AyniCamelidSpecies.Alpaca)
            {
                // Copete de lana sobre la frente
                Primitive(PrimitiveType.Sphere, "Copete", head, Mat(coat),
                    new Vector3(0f, p.headThickness * 0.45f, 0.02f), Vector3.zero,
                    new Vector3(p.headThickness * 1.25f, p.headThickness * 0.9f, p.headThickness * 1.1f));
            }

            return new Parts { root = root, body = body, neck = neck, head = head, legs = legs };
        }

        private static Proportions GetProportions(AyniCamelidSpecies species)
        {
            switch (species)
            {
                case AyniCamelidSpecies.Alpaca:
                    return new Proportions
                    {
                        backHeight = 0.92f, bodyLength = 1.05f, bodyThickness = 0.62f,
                        legThickness = 0.13f, neckLength = 0.55f, neckThickness = 0.2f,
                        headLength = 0.28f, headThickness = 0.17f, earLength = 0.12f
                    };
                case AyniCamelidSpecies.Vicuna:
                    return new Proportions
                    {
                        backHeight = 0.9f, bodyLength = 1.0f, bodyThickness = 0.44f,
                        legThickness = 0.08f, neckLength = 0.62f, neckThickness = 0.13f,
                        headLength = 0.26f, headThickness = 0.13f, earLength = 0.14f
                    };
                default: // Llama
                    return new Proportions
                    {
                        backHeight = 1.15f, bodyLength = 1.3f, bodyThickness = 0.6f,
                        legThickness = 0.12f, neckLength = 0.78f, neckThickness = 0.18f,
                        headLength = 0.34f, headThickness = 0.18f, earLength = 0.26f
                    };
            }
        }

        // ------------------------------------------------------------------------------------------------
        private static Material Mat(Color color) => AyniFaunaPrimitives.Mat(color, 0.08f); // lana mate

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat,
            Vector3 pos, Vector3 euler, Vector3 scale)
            => AyniFaunaPrimitives.Primitive(type, name, parent, mat, pos, euler, scale);
    }
}
