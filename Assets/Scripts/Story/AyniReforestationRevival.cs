using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ayni.World;

namespace Ayni.Story
{
    /// <summary>
    /// El bosque que renace no es solo el claro del perdón: la ola de vida recorre todo el camino y despierta cada árbol
    /// quemado que Yari fue encontrando (los que siguen en pie y los que cayeron a través del camino).
    ///   - La madera quemada recupera el color rojizo de la corteza de la queñua.
    ///   - En la punta de cada tronco en pie sale una copa nueva; en las ramas rotas y a lo largo del tronco, varas con hojas.
    ///   - Los troncos caídos rebrotan como lo hacen de verdad: echan varas hacia arriba desde los muñones y la corteza.
    ///     Siguen cruzados en el camino (hay que saltarlos igual), pero ya no están muertos.
    ///   - Al pie de cada árbol vuelve el ichu, y de vez en cuando una cantuta.
    /// Los árboles los señala <see cref="AyniBurntTree"/>; aquí no se busca nada por nombre.
    /// </summary>
    public partial class AyniReforestation
    {
        /// <summary>Más lejos de la cámara que esto, las plantas ya crecidas no se mecen.</summary>
        private const float SwayDistance = 90f;

        // La ola sale del lugar del perdón y llega al principio del camino antes de que termine la escena final
        private const float RouteWaveStart = 1.8f;
        private const float RouteWaveSeconds = 8.5f;
        private const float RouteWaveReach = 450f;
        private const float WoodFadeSeconds = 3.5f;

        /// <summary>Tinte que deja la madera de la aldea con el tono de la corteza viva de la queñua.</summary>
        private static readonly Color LivingBark = new Color(0.70f, 0.72f, 0.68f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private sealed class WoodFade
        {
            public Renderer renderer;
            public Material original, live;
            public Color from;
            public float delay;
            public bool done;
        }

        private readonly List<WoodFade> woodFades = new List<WoodFade>();

        /// <summary>Cuántos árboles quemados ha despertado esta reforestación (para pruebas).</summary>
        public int RevivedTrees { get; private set; }

        /// <summary>Segundos que tarda la ola en llegar a un punto del mapa.</summary>
        private float WaveArrival(Vector3 position)
        {
            Vector3 flat = position - center;
            flat.y = 0f;
            return RouteWaveStart + Mathf.Clamp01(flat.magnitude / RouteWaveReach) * RouteWaveSeconds;
        }

        private IEnumerator ReviveBurntTrees()
        {
            var trees = new List<AyniBurntTree>(AyniBurntTree.All);
            if (trees.Count == 0) yield break;

            var rng = new System.Random(151);
            var fades = new Dictionary<Renderer, WoodFade>();
            int made = 0;
            foreach (AyniBurntTree tree in trees)
            {
                if (tree == null) continue;
                float arrives = WaveArrival(tree.transform.position) + Range(rng, 0f, 0.8f);

                // La madera: una malla por obstáculo, así que cambia de color cuando llega la ola a su primer árbol
                foreach (Renderer renderer in tree.Wood)
                {
                    if (renderer == null || renderer.sharedMaterial == null) continue;
                    if (fades.TryGetValue(renderer, out WoodFade fade))
                    {
                        fade.delay = Mathf.Min(fade.delay, arrives);
                        continue;
                    }
                    Material original = renderer.sharedMaterial;
                    fade = new WoodFade
                    {
                        renderer = renderer,
                        original = original,
                        from = original.HasProperty(BaseColorId) ? original.GetColor(BaseColorId) : original.color,
                        delay = arrives
                    };
                    fades[renderer] = fade;
                    woodFades.Add(fade);
                }

                for (int i = 0; i < tree.ShootCount; i++)
                {
                    tree.GetShoot(i, out Vector3 point, out Vector3 direction, out float size, out bool crown);
                    Assets.TreeModel model = crown ? Assets.YoungTrees[rng.Next(Assets.YoungTrees.Length)]
                                                   : Assets.Saplings[rng.Next(Assets.Saplings.Length)];
                    // Primero cambia la corteza, luego abre la copa y por último salen las varas
                    float delay = arrives + (crown ? 0.9f : 1.5f) + Range(rng, 0f, 1.3f);
                    AddRegrowth(rng, crown ? Kind.YoungTree : Kind.Sapling, model, point, direction, size, crown ? 2.8f : 2.2f, delay);
                }

                if (!tree.Fallen) GroundLife(rng, tree.transform.position, arrives + 0.6f);

                RevivedTrees++;
                if (++made % 5 == 0) yield return null;
            }
        }

        /// <summary>Copa o vara que nace de la madera de un árbol quemado, orientada como la rama de la que sale.</summary>
        private void AddRegrowth(System.Random rng, Kind kind, Assets.TreeModel model, Vector3 position, Vector3 direction,
            float size, float duration, float delay)
        {
            var root = new GameObject(kind == Kind.Sapling ? "Rebrote_Quenua" : "Copa_Renacida");
            root.transform.SetParent(transform, true);
            root.transform.position = position;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
            root.transform.localScale = Vector3.zero;
            AddRenderer(root, model.bark, Assets.Bark, null, true);

            var crown = new GameObject("Copa");
            crown.transform.SetParent(root.transform, false);
            crown.transform.localPosition = model.crownPivot;
            crown.transform.localScale = Vector3.zero;
            AddRenderer(crown, model.leaves, Assets.Foliage, Assets.Leaves, true);

            Register(rng, root.transform, crown.transform, kind, position, size, duration,
                     kind == Kind.Sapling ? 2.6f : 1.5f, kind == Kind.Sapling ? 1.5f : 1.0f);
            Plant plant = plants[plants.Count - 1];
            plant.delay = delay;
            plant.fromWood = true;
        }

        /// <summary>Ichu (y a veces una cantuta) al pie de un árbol que renace.</summary>
        private void GroundLife(System.Random rng, Vector3 foot, float delay)
        {
            int tufts = 2 + rng.Next(3);
            bool flower = rng.Next(2) == 0;
            for (int i = 0; i < tufts + (flower ? 1 : 0); i++)
            {
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                Vector3 p = foot + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Range(rng, 0.45f, 1.3f);
                if (!Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 14f,
                                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (!(hit.collider is TerrainCollider)) continue; // ni sobre los peñascos ni sobre el propio tronco

                if (i < tufts)
                {
                    AddSimple(rng, Kind.Grass, Assets.Tufts[rng.Next(Assets.Tufts.Length)], Assets.Grass, null, hit.point, hit.normal,
                              Range(rng, 0.8f, 1.4f), 1.2f);
                }
                else
                {
                    int v = rng.Next(Assets.Flowers.Length);
                    AddSimple(rng, Kind.Flower, Assets.Flowers[v], Assets.Foliage, Assets.Petals[v % Assets.Petals.Length], hit.point, hit.normal,
                              Range(rng, 0.8f, 1.3f), 1.6f);
                }
                plants[plants.Count - 1].delay = delay + Range(rng, 0f, 1.6f);
            }
        }

        /// <summary>La madera quemada va tomando el color de la corteza viva.</summary>
        private void UpdateWood(float t)
        {
            for (int i = 0; i < woodFades.Count; i++)
            {
                WoodFade fade = woodFades[i];
                if (fade.done || fade.renderer == null) continue;
                float k = Mathf.Clamp01((t - fade.delay) / WoodFadeSeconds);
                if (k <= 0f) continue;

                if (fade.live == null)
                {
                    // Copia propia: el material original es un asset compartido y no se toca
                    fade.live = new Material(fade.original) { name = fade.original.name + " (renacido)" };
                    fade.renderer.sharedMaterial = fade.live;
                }
                Color color = Color.Lerp(fade.from, LivingBark, Smooth(0f, 1f, k));
                if (fade.live.HasProperty(BaseColorId)) fade.live.SetColor(BaseColorId, color);
                else fade.live.color = color;
                if (k >= 1f) fade.done = true;
            }
        }

        private void OnDestroy()
        {
            // Si se quita la reforestación (pruebas, volver a empezar), los árboles vuelven a estar quemados
            for (int i = 0; i < woodFades.Count; i++)
            {
                WoodFade fade = woodFades[i];
                if (fade.renderer != null && fade.live != null) fade.renderer.sharedMaterial = fade.original;
                if (fade.live != null) Destroy(fade.live);
            }
            woodFades.Clear();
        }
    }
}
