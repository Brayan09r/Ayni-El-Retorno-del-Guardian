using System.Collections.Generic;
using UnityEngine;

namespace Ayni.Story
{
    /// <summary>
    /// Elenco y ambiente del prólogo "La Noche de las Cenizas", montados en tiempo de juego (no hay que tocar la escena):
    ///  - El General Sayri y su Guardia de Bronce en el puente: copias de los modelos de los rivales que ya hay en la escena
    ///    (solo el cuerpo y el Animator, sin IA ni colisiones), teñidas, y Sayri con su Champi de bronce en la mano.
    ///  - La noche de verdad: cielo, sol y luz ambiente de incendio, niebla rojiza, ceniza que cae y fuegos con humo en el valle.
    /// Todo se deshace al terminar (Clear): el cielo vuelve a su material y la luz a sus valores.
    /// </summary>
    public class AyniPrologueCast
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Material> createdMaterials = new List<Material>();
        private Transform planks;

        // Luz de la escena antes de la noche (para restaurarla)
        private bool nightOn;
        private Material skyOriginal;
        private Light sun;
        private Color sunColor;
        private float sunIntensity;
        private Quaternion sunRotation;
        private UnityEngine.Rendering.AmbientMode ambientMode;
        private Color ambientSky, ambientEquator, ambientGround;
        private bool fog;
        private Color fogColor;
        private float fogDensity;
        private FogMode fogMode;

        public AyniPrologueCast(Transform bridgePlanks)
        {
            planks = bridgePlanks;
        }

        // ───────────────────────── Actores ─────────────────────────

        /// <summary>
        /// Copia el cuerpo de un rival de la escena cuyo Animator Controller contenga <paramref name="controllerHint"/>
        /// ("Guardia", "CazadorElite"...), sin su IA ni sus colisiones, y lo coloca en el puente.
        /// </summary>
        public Animator Spawn(string name, string controllerHint, Vector3 position, Quaternion rotation, float scale, Color tint)
        {
            Animator source = null;
            foreach (Animator a in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (a == null || a.runtimeAnimatorController == null || !a.isHuman) continue;
                if (a.GetComponentInParent<Ayni.Player.YariCombatController>() != null) continue;
                if (a.runtimeAnimatorController.name.Contains(controllerHint))
                {
                    source = a;
                    break;
                }
                if (source == null && a.GetComponentInParent<Ayni.Enemy.EnemyController>() != null) source = a;
            }
            if (source == null) return null;

            // Se copia dentro de un contenedor apagado: así no llega a despertar ningún script de la copia
            var root = new GameObject("Prologo_" + name);
            root.SetActive(false);
            GameObject body = Object.Instantiate(source.gameObject, root.transform);
            body.name = name;
            foreach (MonoBehaviour mb in body.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            foreach (Collider c in body.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            root.transform.SetPositionAndRotation(OnDeck(position), rotation);
            root.transform.localScale = Vector3.one * scale;
            Tint(body, tint);
            root.SetActive(true);
            spawned.Add(root);

            var anim = body.GetComponent<Animator>();
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (anim.HasState(0, Animator.StringToHash("Idle"))) anim.Play("Idle", 0, Random.value);
            return anim;
        }

        /// <summary>Mueve un actor por el puente (a la altura de los tablones) mirando hacia donde va.</summary>
        public void Place(Animator actor, Vector3 position, Vector3 facing)
        {
            if (actor == null) return;
            Transform root = actor.transform.parent;
            facing.y = 0f;
            Quaternion rot = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : root.rotation;
            root.SetPositionAndRotation(OnDeck(position), rot);
        }

        /// <summary>Altura del tablón más cercano (el puente cuelga: no es plano). Sin tablones, la altura pedida.</summary>
        public Vector3 OnDeck(Vector3 p)
        {
            if (planks == null || planks.childCount == 0) return p;
            Transform best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < planks.childCount; i++)
            {
                Vector3 d = planks.GetChild(i).position - p;
                d.y = 0f;
                float dist = d.sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = planks.GetChild(i);
                }
            }
            if (best != null && bestDist < 9f) p.y = best.position.y + best.lossyScale.y * 0.5f;
            return p;
        }

        private void Tint(GameObject body, Color tint)
        {
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.materials; // copias: el material del rival no cambia
                foreach (Material m in mats)
                {
                    createdMaterials.Add(m);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
                    else if (m.HasProperty("_Color")) m.SetColor("_Color", m.GetColor("_Color") * tint);
                }
                r.materials = mats;
            }
        }

        /// <summary>
        /// La Champi: porra de guerra inca con cabeza de bronce en estrella, en la mano derecha. Se orienta siguiendo el
        /// antebrazo, así acompaña los golpes del Animator.
        /// </summary>
        public void GiveChampi(Animator actor)
        {
            if (actor == null || !actor.isHuman) return;
            Transform hand = actor.GetBoneTransform(HumanBodyBones.RightHand);
            Transform forearm = actor.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (hand == null || forearm == null) return;

            Material wood = NewLit(new Color(0.32f, 0.2f, 0.11f), 0f, 0.25f);
            Material bronze = NewLit(new Color(0.78f, 0.5f, 0.22f), 0.85f, 0.6f);

            var champi = new GameObject("Champi");
            float s = 1f / Mathf.Max(0.01f, actor.transform.parent.localScale.x);
            Vector3 axis = (hand.position - forearm.position).normalized;
            champi.transform.position = hand.position;
            champi.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);

            GameObject handle = Primitive(PrimitiveType.Cylinder, champi.transform, wood);
            handle.transform.localScale = new Vector3(0.045f, 0.36f, 0.045f);
            handle.transform.localPosition = Vector3.up * 0.22f;

            GameObject head = Primitive(PrimitiveType.Sphere, champi.transform, bronze);
            head.transform.localScale = Vector3.one * 0.13f;
            head.transform.localPosition = Vector3.up * 0.58f;
            // Seis puntas en estrella alrededor de la cabeza
            for (int i = 0; i < 6; i++)
            {
                GameObject spike = Primitive(PrimitiveType.Cube, head.transform, bronze);
                spike.transform.localRotation = Quaternion.Euler(0f, i * 60f, 45f);
                spike.transform.localScale = new Vector3(1.25f, 0.42f, 0.42f);
            }

            champi.transform.SetParent(hand, true);
            champi.transform.localScale *= s;
        }

        private GameObject Primitive(PrimitiveType type, Transform parent, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go;
        }

        private Material NewLit(Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            createdMaterials.Add(m);
            return m;
        }

        /// <summary>Lanza un estado del Animator del actor (si existe).</summary>
        public static void PlayState(Animator actor, string state, float fade, float speed = 1f, float startSeconds = 0f)
        {
            if (actor == null || !actor.HasState(0, Animator.StringToHash(state))) return;
            foreach (AnimatorControllerParameter p in actor.parameters)
            {
                if (p.name == "AttackSpeed") actor.SetFloat("AttackSpeed", speed);
            }
            actor.CrossFadeInFixedTime(state, fade, 0, startSeconds);
        }

        /// <summary>Paso de marcha (Speed del Animator: 0 quieto, 0.5 andando, 1 corriendo).</summary>
        public static void SetSpeed(Animator actor, float speed)
        {
            if (actor == null) return;
            foreach (AnimatorControllerParameter p in actor.parameters)
            {
                if (p.name == "Speed") actor.SetFloat("Speed", speed);
            }
        }

        // ───────────────────────── La noche ─────────────────────────

        /// <summary>
        /// Noche de incendio: sol bajo y rojizo, cielo oscuro (sobre una copia del material del cielo, nunca el asset),
        /// luz ambiente y niebla de brasas, ceniza que cae alrededor del puente y fuegos con humo en el valle.
        /// </summary>
        public void BeginNight(Vector3 deck, Vector3 acrossBridge)
        {
            if (nightOn) return;
            nightOn = true;

            skyOriginal = RenderSettings.skybox;
            if (skyOriginal != null)
            {
                var sky = new Material(skyOriginal) { name = skyOriginal.name + " (noche del prólogo)" };
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 0.32f);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.42f, 0.16f, 0.12f));
                if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 1.6f);
                createdMaterials.Add(sky);
                RenderSettings.skybox = sky;
            }

            sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type == LightType.Directional) { sun = l; break; }
                }
            }
            if (sun != null)
            {
                sunColor = sun.color;
                sunIntensity = sun.intensity;
                sunRotation = sun.transform.rotation;
                sun.color = new Color(1f, 0.42f, 0.26f);
                sun.intensity = 0.75f;
                sun.transform.rotation = Quaternion.Euler(6f, sun.transform.eulerAngles.y, 0f);
            }

            ambientMode = RenderSettings.ambientMode;
            ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor;
            fog = RenderSettings.fog;
            fogColor = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity;
            fogMode = RenderSettings.fogMode;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.2f, 0.12f, 0.14f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.18f, 0.09f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.06f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.22f, 0.08f, 0.05f);
            RenderSettings.fogDensity = 0.009f;

            Vector3 along = Vector3.Cross(acrossBridge, Vector3.up).normalized;
            spawned.Add(Ash(deck));
            // Resplandor del incendio sobre el puente: sin él, la pelea no se lee en la noche
            var glow = new GameObject("Prologo_Resplandor");
            glow.transform.position = deck + Vector3.up * 4f + acrossBridge * 2.5f;
            var glowLight = glow.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(1f, 0.55f, 0.28f);
            glowLight.range = 14f;
            glowLight.intensity = 3.2f;
            glow.AddComponent<PrologueFlicker>().Init(glowLight, 3.2f, 11);
            spawned.Add(glow);
            // El valle arde: fuegos con humo al otro lado de la garganta, donde mira el plano general
            Vector3[] fires =
            {
                deck + acrossBridge * 30f + along * 14f,
                deck + acrossBridge * 38f - along * 8f,
                deck + acrossBridge * 26f - along * 26f,
                deck + along * 34f + acrossBridge * 8f,
            };
            for (int i = 0; i < fires.Length; i++) spawned.Add(Fire(Ground(fires[i]), i));
        }

        private static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 200f, Physics.DefaultRaycastLayers,
                                QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return p;
        }

        /// <summary>Copos de ceniza que caen despacio y a la deriva alrededor del puente.</summary>
        private GameObject Ash(Vector3 deck)
        {
            var go = new GameObject("Prologo_Ceniza");
            go.transform.position = deck + Vector3.up * 12f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 13f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.52f, 0.5f, 0.9f), new Color(0.3f, 0.28f, 0.27f, 0.8f));
            main.gravityModifier = 0.025f;
            main.maxParticles = 900;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            var emission = ps.emission;
            emission.rateOverTime = 70f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 1f, 40f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.25f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SoftMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Simulate(10f, true, true);
            ps.Play();
            return go;
        }

        /// <summary>Un incendio lejano: luz naranja que parpadea y una columna de humo oscuro.</summary>
        private GameObject Fire(Vector3 ground, int seed)
        {
            var go = new GameObject("Prologo_Incendio_" + seed);
            go.transform.position = ground + Vector3.up * 3f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.45f, 0.15f);
            light.range = 28f;
            light.intensity = 5f;
            go.AddComponent<PrologueFlicker>().Init(light, 5f, seed);

            var smoke = new GameObject("Humo");
            smoke.transform.SetParent(go.transform, false);
            var ps = smoke.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.12f, 0.09f, 0.08f, 0.55f), new Color(0.25f, 0.13f, 0.08f, 0.45f));
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            var emission = ps.emission;
            emission.rateOverTime = 9f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 2f;
            smoke.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0f), new GradientColorKey(Color.white, 0.25f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SoftMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Simulate(8f, true, true);
            ps.Play();
            return go;
        }

        private Material soft;

        /// <summary>Material de partícula con un punto suave (sin él, cada copo o bocanada de humo sería un cuadrado).</summary>
        private Material SoftMaterial()
        {
            if (soft != null) return soft;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            }
            tex.Apply();
            soft = new Material(Ayni.Combat.CombatFeedback.SpriteMaterial) { mainTexture = tex, name = "Prologo_Particula" };
            createdMaterials.Add(soft);
            return soft;
        }

        /// <summary>Devuelve el cielo, el sol, la luz ambiente y la niebla a como estaban.</summary>
        public void EndNight()
        {
            if (!nightOn) return;
            nightOn = false;
            if (skyOriginal != null) RenderSettings.skybox = skyOriginal;
            if (sun != null)
            {
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.transform.rotation = sunRotation;
            }
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogMode = fogMode;
        }

        /// <summary>Quita el elenco y los efectos y restaura la luz.</summary>
        public void Clear()
        {
            EndNight();
            foreach (GameObject go in spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            spawned.Clear();
            foreach (Material m in createdMaterials)
            {
                if (m != null) Object.Destroy(m);
            }
            createdMaterials.Clear();
        }

        /// <summary>Parpadeo de un fuego lejano.</summary>
        private class PrologueFlicker : MonoBehaviour
        {
            private Light target;
            private float baseIntensity;
            private float seed;

            public void Init(Light light, float intensity, int s)
            {
                target = light;
                baseIntensity = intensity;
                seed = s * 7.3f;
            }

            private void Update()
            {
                if (target == null) return;
                target.intensity = baseIntensity * (0.75f + 0.5f * Mathf.PerlinNoise(Time.unscaledTime * 2.7f, seed));
            }
        }
    }
}
