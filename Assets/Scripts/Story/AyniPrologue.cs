using System.Collections;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.UI;

namespace Ayni.Story
{
    /// <summary>
    /// Prólogo jugable "La Noche de las Cenizas" (Biblia del juego, cap. 3):
    /// Yari defiende el puente colgante sobre la garganta, el General Sayri le quiebra la guardia de un golpe
    /// de su Champi de bronce y lo arroja al abismo. En la caída la Illa de la Pachamama despierta y sella el pacto.
    /// Años después Yari despierta en el camino del Antisuyo, frente a Amaru el Cazador.
    ///
    /// Todo se monta por código con lo que ya hay en la escena (puente, Yari, Amaru, cámara): no hay que tocar la escena.
    /// SifuCombatHUD lo añade solo. Se ve una vez por sesión de juego y se salta con Tab / Esc / Menu del mando.
    /// Para desactivarlo mientras se prueba el combate: Ayni > Historia > Prólogo al dar Play.
    /// </summary>
    public class AyniPrologue : MonoBehaviour
    {
        public const string PrefKey = "AyniPrologueEnabled";

        /// <summary>True desde que se añade hasta que termina (el tutorial espera a que acabe).</summary>
        public static bool IsRunning { get; private set; }

        private static bool shownThisSession;

        private static readonly Color Ember = new Color(1f, 0.45f, 0.12f);
        private static readonly Color IllaGold = new Color(1f, 0.82f, 0.35f);

        private YariCombatController yari;
        private CharacterController yariMover;
        private Animator yariAnimator;
        private Transform yariTransform;
        private Camera cam;
        private ThirdPersonSifuCamera gameplayCamera;
        private EnemyController amaru;

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private Vector3 deck;          // centro del puente
        private Vector3 acrossBridge;  // horizontal, perpendicular al puente: hacia donde cae Yari
        private float waterY = -18f;

        private GameObject embers;
        // El General Sayri y su Guardia de Bronce (copias de modelos de la escena) y la noche de incendio
        private AyniPrologueCast cast;
        private Animator sayri, guardA, guardB;
        private Transform planks;
        private bool captionShown;
        private int fireLoop = -1;
        private int music = -1;
        private Light fireLight;
        private Light illaLight;
        private bool skipRequested;
        private bool finished;
        private Coroutine routine;

        private float shakeUntil;
        private float shakeAmount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsRunning = false;
            shownThisSession = false;
        }

        private void Awake()
        {
            if (shownThisSession || PlayerPrefs.GetInt(PrefKey, 1) == 0)
            {
                enabled = false;
                return;
            }
            shownThisSession = true;
            IsRunning = true;
        }

        private void Start()
        {
            if (!enabled) return;
            if (!Setup())
            {
                Debug.LogWarning("[Ayni Prólogo] Falta Yari, la cámara o el puente en la escena: se omite el prólogo.");
                IsRunning = false;
                enabled = false;
                return;
            }
            routine = StartCoroutine(Play());
        }

        private void OnDisable()
        {
            if (IsRunning && routine != null) Finish();
        }

        // ───────────────────────── Preparación ─────────────────────────

        private bool Setup()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            cam = Camera.main;
            if (player == null || cam == null) return false;

            yari = player.GetComponent<YariCombatController>();
            yariMover = player.GetComponent<CharacterController>();
            yariAnimator = player.GetComponentInChildren<Animator>();
            yariTransform = player.transform;
            gameplayCamera = cam.GetComponent<ThirdPersonSifuCamera>();
            if (yari == null || yariAnimator == null) return false;

            spawnPosition = yariTransform.position;
            spawnRotation = yariTransform.rotation;

            if (!FindBridge()) return false;

            // Agua de la garganta (si existe en el entorno)
            var entorno = GameObject.Find("Ayni_Entorno");
            if (entorno != null)
            {
                foreach (Transform t in entorno.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Contains("Agua"))
                    {
                        waterY = t.position.y;
                        break;
                    }
                }
            }

            for (int i = 0; i < EnemyController.All.Count; i++)
            {
                if (EnemyController.All[i] != null && EnemyController.All[i].IsBoss) amaru = EnemyController.All[i];
            }
            return true;
        }

        /// <summary>Centro del puente colgante y la dirección perpendicular a él (la de la caída).</summary>
        private bool FindBridge()
        {
            var bridge = GameObject.Find("Puente_Colgante");
            if (bridge == null) return false;

            planks = bridge.transform.Find("Tablones");
            if (planks == null || planks.childCount < 3) return false;

            Transform first = planks.GetChild(0);
            Transform last = planks.GetChild(planks.childCount - 1);
            Transform middle = planks.GetChild(planks.childCount / 2);

            deck = middle.position + Vector3.up * 0.04f;
            Vector3 along = last.position - first.position;
            along.y = 0f;
            if (along.sqrMagnitude < 1f) return false;
            acrossBridge = Vector3.Cross(Vector3.up, along.normalized).normalized;
            return true;
        }

        // ───────────────────────── La escena ─────────────────────────

        private IEnumerator Play()
        {
            // Los primeros fotogramas tras cargar la escena llegan a trompicones: se espera a que el ritmo sea estable
            // (si no, el título se "come" esos segundos de carga y no se llega a leer)
            AyniScreenFX.SetFade(Color.black, 1f);
            for (int i = 0; i < 60 && (i < 3 || Time.unscaledDeltaTime > 0.1f); i++) yield return null;

            startTime = Time.unscaledTime;
            Phase("comienzo");
            AyniGameState.CinematicPlaying = true;
            AyniGameState.LockInput();
            yari.CinematicControl = true;
            if (yariMover != null) yariMover.enabled = false;
            if (gameplayCamera != null) gameplayCamera.enabled = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            AyniScreenFX.SetFade(Color.black, 1f);
            AyniScreenFX.Letterbox(true);
            AyniScreenFX.Hint(AyniInput.UsingGamepad ? "[Menu] Saltar escena" : "[Tab / Esc] Saltar escena");

            // Yari en el puente, junto a la cuerda por la que caerá, de cara a la orilla por la que llega Sayri
            Vector3 along = Vector3.Cross(acrossBridge, Vector3.up).normalized;
            Vector3 yariSpot = deck + acrossBridge * 0.38f;
            PlaceYari(yariSpot, Quaternion.LookRotation(along));
            Play("Guard_Stance", 0f);

            // El General Sayri y dos soldados de su Guardia de Bronce cruzan el puente hacia él
            cast = new AyniPrologueCast(planks);
            cast.BeginNight(deck, acrossBridge);
            Vector3 sayriStop = yariSpot + along * 1.6f;
            Vector3 sayriStart = deck + along * 11f;
            Quaternion towardYari = Quaternion.LookRotation(-along);
            sayri = cast.Spawn("General_Sayri", "CazadorElite", sayriStart, towardYari, 1.1f, new Color(0.9f, 0.66f, 0.42f));
            cast.GiveChampi(sayri);
            Vector3 flank = acrossBridge * 0.5f;
            guardA = cast.Spawn("Guardia_de_Bronce_1", "Guardia", sayriStart + along * 1.8f + flank, towardYari, 1f, new Color(0.78f, 0.56f, 0.36f));
            guardB = cast.Spawn("Guardia_de_Bronce_2", "Guardia", sayriStart + along * 1.8f - flank, towardYari, 1f, new Color(0.78f, 0.56f, 0.36f));
            StartCoroutine(March(sayriStart, sayriStop, flank, along, 9.2f));

            BuildNightOfAshes();
            fireLoop = AyniAudio.PlayLoop("amb_fuego", 0.8f, 2.5f);
            music = AyniAudio.PlayMusic("prologo_quena", 0.75f, 2f);

            // El título aparece sobre el puente en llamas, no sobre negro
            AyniAudio.Play2D("titulo_golpe", 0.8f);
            AyniScreenFX.FadeTo(Color.black, 0f, 1.8f);
            AyniScreenFX.Title("AYNI", "Prólogo — La Noche de las Cenizas", 4.0f);

            Phase("plano 1: el puente sobre la garganta");
            // Plano 1: la garganta en llamas; por el puente avanza Sayri con su guardia
            yield return Shot(6.2f, u =>
            {
                if (u > 0.45f && !captionShown)
                {
                    captionShown = true;
                    AyniScreenFX.Caption("El General Sayri traicionó al Consejo de los Amautas y tomó el Cusco a sangre y fuego.", 4.2f);
                }
                Vector3 from = deck - acrossBridge * 11f + along * 2f + Vector3.up * 6f;
                Vector3 to = deck - acrossBridge * 8f + along * 1f + Vector3.up * 3f;
                Vector3 mid = Vector3.Lerp(yariSpot, SayriPosition(), 0.5f) + Vector3.up * 1.2f;
                return (Vector3.Lerp(from, to, Smooth(u)), mid);
            });

            Phase("plano 2: Sayri avanza");
            // Plano 2: por encima del hombro de Yari, Sayri se acerca con la Champi en la mano
            AyniScreenFX.Caption("Esa noche su Guardia de Bronce arrasó el Ayllu del Valle Verde.", 3.8f);
            yield return Shot(4.0f, u =>
            {
                Vector3 pos = yariSpot - along * Mathf.Lerp(2.3f, 1.9f, u) - acrossBridge * 0.55f + Vector3.up * 1.75f;
                return (pos, SayriPosition() + Vector3.up * 1.5f);
            });

            Phase("plano 3: el traidor");
            // Plano 3: contrapicado de Sayri, ya frente a Yari
            AyniScreenFX.Title("GENERAL SAYRI", "Señor de la Guardia de Bronce · Antiguo hermano de armas de Yari", 3.2f);
            yield return Shot(3.4f, u =>
            {
                Vector3 s = SayriPosition();
                Vector3 pos = s - along * Mathf.Lerp(2.0f, 1.75f, u) - acrossBridge * 0.55f + Vector3.up * 1.35f;
                return (pos, s + Vector3.up * 1.55f);
            });
            if (skipRequested) { Finish(); yield break; }

            Phase("el combate en el puente");
            // Plano de perfil: Sayri golpea, Yari bloquea y contraataca, y la Champi le rompe la guardia
            AyniScreenFX.Caption("En el puente sobre la garganta, Yari enfrentó a su antiguo hermano de armas...", 3.4f);
            Vector3 fightMid = Vector3.Lerp(yariSpot, sayriStop, 0.5f);
            System.Func<float, (Vector3, Vector3)> profile = u =>
                (fightMid - acrossBridge * Mathf.Lerp(3.6f, 3.2f, u) + along * 0.3f + Vector3.up * 2.15f, fightMid + Vector3.up * 1.3f);

            // Mazazo de arriba abajo (el clip llega al impacto a los 0.3 s)
            AyniPrologueCast.PlayState(sayri, "Atk_Overhand", 0.08f, 1f);
            yield return Shot(0.32f, u => profile(u * 0.15f));
            Play("Guard_BlockHit", 0.04f);
            AyniAudio.Play2D("bloqueo", 0.9f);
            Ayni.Combat.CombatFeedback.Flash(yariSpot + along * 0.45f + Vector3.up * 1.45f, new Color(0.8f, 0.9f, 1f), 0.9f, 0.14f);
            Shake(0.05f, 0.18f);
            yield return Shot(0.55f, u => profile(0.15f + u * 0.15f));

            // Contraataque: directo de derecha, entrando con la preparación ya avanzada (impacto a los 0.3 s)
            yariAnimator.SetFloat("AttackSpeed", 1.3f);
            if (HasState("Atk_Light2")) yariAnimator.CrossFadeInFixedTime("Atk_Light2", 0.06f, 0, 0.5f);
            AyniAudio.Play2D("swing_ligero", 0.6f);
            yield return Shot(0.3f, u => profile(0.3f + u * 0.1f));
            AyniPrologueCast.PlayState(sayri, "Hit_Head", 0.05f);
            AyniAudio.Play2D("golpe_ligero", 0.8f);
            Ayni.Combat.CombatFeedback.Flash(sayriStop + Vector3.up * 1.75f - along * 0.2f, Color.white, 0.7f, 0.1f);
            yield return Shot(0.6f, u => profile(0.4f + u * 0.15f));

            // La Champi: golpe de codo a la guardia, de lado, que lo lanza por encima de la cuerda
            // (clip de codo: impacto a los 0.68 s; a 1.2x y entrando a los 0.14 s, llega en 0.45 s)
            AyniPrologueCast.PlayState(sayri, "Atk_Elbow", 0.06f, 1.2f, 0.14f);
            AyniAudio.Play2D("swing_fuerte", 0.8f);
            Play("Guard_Stance", 0.1f);
            yield return Shot(0.45f, u => profile(0.55f + u * 0.15f));
            if (skipRequested) { Finish(); yield break; }

            // El golpe de la Champi: destello de fuego, sacudida y cámara lenta
            Phase("el golpe de Sayri");
            AyniScreenFX.ClearCaption();
            Play("Fall_Back", 0.04f);
            AyniAudio.Play2D("remate", 1f, 0.85f);
            AyniAudio.Play2D("caida_viento", 0.7f);
            AyniAudio.SetLoopVolume(music, 0.35f, 1f);
            Ayni.Combat.CombatFeedback.Flash(yariSpot + Vector3.up * 1.3f - acrossBridge * 0.3f, new Color(1f, 0.55f, 0.2f), 2.6f, 0.25f);
            AyniScreenFX.SetFade(new Color(1f, 0.5f, 0.15f), 0.7f);
            AyniScreenFX.FadeTo(new Color(1f, 0.5f, 0.15f), 0f, 0.7f);
            Shake(0.22f, 0.5f);
            Time.timeScale = 0.3f;
            AyniScreenFX.Caption("<color=#ffb070>Sayri quebró su guardia con la Champi de bronce</color> y lo arrojó al abismo.", 3.6f);

            // Vuelo de espaldas por encima de las sogas y caída a la garganta
            Vector3 velocity = acrossBridge * 4.2f + Vector3.up * 4.8f;
            Vector3 pos = yariSpot;
            const float fallGravity = -15f;
            float elapsed = 0f;
            Vector3 camAnchor = deck - acrossBridge * 0.5f + Vector3.up * 2.0f;
            // Corte seco al plano desde el puente (si la cámara viajara desde el perfil, cruzaría los tablones)
            Aim(camAnchor, yariSpot + Vector3.up * 1.1f, 1f);
            bool slowEnded = false;
            bool closeShot = false;

            while (pos.y > waterY + 2.5f && !skipRequested)
            {
                float dt = Time.deltaTime;
                elapsed += Time.unscaledDeltaTime;
                if (!slowEnded && elapsed > 1.3f)
                {
                    slowEnded = true;
                    Time.timeScale = 1f;
                }

                velocity.y += fallGravity * dt;
                pos += velocity * dt;
                velocity.x *= 1f - 0.6f * dt;
                velocity.z *= 1f - 0.6f * dt;
                yariTransform.position = pos;

                // Primero desde el puente mirando hacia abajo; al final, desde abajo viendo caer a Yari
                Vector3 camPos;
                if (!closeShot && pos.y < deck.y - 12f) closeShot = true;
                if (!closeShot) camPos = camAnchor;
                else camPos = new Vector3(pos.x, Mathf.Max(waterY + 1.5f, pos.y - 7f), pos.z) + acrossBridge * 4.5f + Vector3.Cross(acrossBridge, Vector3.up) * 2f;
                Aim(camPos, pos + Vector3.up * 0.9f, 0.18f);

                // El Illa empieza a brillar poco antes del agua
                float toWater = pos.y - waterY;
                if (toWater < 9f) LightIlla(Mathf.InverseLerp(9f, 3f, toWater));
                yield return null;
            }
            Time.timeScale = 1f;
            if (skipRequested) { Finish(); yield break; }

            Phase($"el pacto de la Illa (Yari a {yariTransform.position.y:F1} m, agua a {waterY:F1} m)");
            // El pacto: la Illa absorbe el golpe mortal
            AyniScreenFX.Caption("...pero la Illa de la Pachamama despertó.", 2.6f);
            AyniAudio.Play2D("illa_brillo", 0.9f);
            AyniAudio.Play2D("chapuzon", 0.45f);
            AyniAudio.StopLoop(fireLoop, 2f);
            Ayni.Combat.CombatFeedback.Flash(yariTransform.position + Vector3.up, IllaGold, 2.2f, 0.45f);
            AyniScreenFX.FadeTo(IllaGold, 1f, 0.6f);
            yield return Wait(1.4f);
            if (skipRequested) { Finish(); yield break; }

            // Bajo el destello dorado cambia todo: se acaba la noche y, años después, Yari yace en el camino
            ClearNightOfAshes();
            AyniScreenFX.Hint(null);
            PlaceYari(spawnPosition, spawnRotation);
            Play("GetUp", 0f);
            yariAnimator.speed = 0f;
            Vector3 right = spawnRotation * Vector3.right;
            Vector3 fwd = spawnRotation * Vector3.forward;
            yield return null; // un fotograma para que el cuerpo tome la pose tumbada antes de medir el antebrazo
            Transform forearm = yariAnimator.isHuman ? yariAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
            Vector3 illaPoint = forearm != null ? forearm.position : spawnPosition + Vector3.up * 0.3f;
            Vector3 illaView = right * 0.8f + Vector3.up * 0.45f + fwd * 0.2f;

            Phase("la Illa");
            // Primer plano de la Illa en el antebrazo de Yari: late dorada mientras se cuenta el pacto
            AyniScreenFX.Tint(IllaGold, 0.16f);
            AyniScreenFX.FadeTo(IllaGold, 0f, 1.8f);
            AyniScreenFX.Caption("En el fondo del cañón, la Illa absorbió el golpe mortal y selló un pacto con Yari.", 4.4f);
            bool secondCaption = false;
            yield return Shot(9.6f, u =>
            {
                LightIlla(0.45f + 0.25f * Mathf.Sin(u * 9.6f * 2.6f));
                if (u > 0.47f && !secondCaption)
                {
                    secondCaption = true;
                    AyniScreenFX.Caption("No morirá mientras su corazón anhele volver a casa... pero cada vez que caiga, la Illa cobrará años de su vida.", 5.0f);
                }
                return (illaPoint + illaView * Mathf.Lerp(1.15f, 0.75f, Smooth(u)), illaPoint);
            });
            if (skipRequested) { Finish(); yield break; }

            Phase("años después");
            // La cámara se eleva hasta ver el camino que tiene por delante, ya de día
            AyniAudio.Play2D("titulo_golpe", 0.7f);
            AyniScreenFX.Title("AÑOS DESPUÉS", "Nivel 1 · Antisuyo — El dominio de Amaru el Cazador", 3.8f);
            AyniScreenFX.Tint(IllaGold, 0f);
            Vector3 closeUp = illaPoint + illaView * 0.75f;
            Vector3 high = spawnPosition - fwd * 7f + right * 1.2f + Vector3.up * 5.5f;
            Vector3 roadAhead = spawnPosition + fwd * 28f + Vector3.up;
            // En lo alto se ve a Yari tumbado abajo y el camino que se abre delante
            Vector3 overview = Vector3.Lerp(spawnPosition, roadAhead, 0.4f);
            yield return Shot(4.2f, u =>
            {
                LightIlla(Mathf.Lerp(0.45f, 0f, u));
                float k = Smooth(u);
                return (Vector3.Lerp(closeUp, high, k), Vector3.Lerp(illaPoint, overview, k));
            });
            if (skipRequested) { Finish(); yield break; }

            Phase("despertar");
            // Yari se levanta mientras la cámara baja hasta quedar detrás de él, mirando el camino
            AyniScreenFX.Caption("Yari despierta. El camino a casa empieza aquí.", 3.4f);
            yariAnimator.speed = 0.8f;
            Vector3 behind = spawnPosition + spawnRotation * new Vector3(0.5f, 1.6f, -3.2f);
            yield return Shot(3.8f, u =>
            {
                float k = Smooth(u);
                // La cámara no pierde a Yari mientras se levanta; al final mira el camino por encima de su hombro
                Vector3 look = Vector3.Lerp(overview, spawnPosition + Vector3.up * 1.2f + fwd * 2.5f, k);
                return (Vector3.Lerp(high, behind, k), look);
            });
            yariAnimator.speed = 1f;
            if (skipRequested) { Finish(); yield break; }

            Phase("presentación de Amaru");
            // Presentación del primer teniente de Sayri
            if (amaru != null)
            {
                Vector3 a = amaru.transform.position;
                Vector3 toYari = spawnPosition - a;
                toYari.y = 0f;
                Vector3 dir = toYari.sqrMagnitude > 0.01f ? toYari.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                AyniAudio.Play2D("juicio", 0.6f);
                AyniScreenFX.Title("AMARU EL CAZADOR", "Primer teniente de Sayri · Señor de los bosques quemados del Antisuyo", 3.2f);
                yield return Shot(3.3f, u =>
                {
                    Vector3 p = a + dir * Mathf.Lerp(3.2f, 2.6f, u) + side * 1.1f + Vector3.up * 0.7f;
                    return (p, a + Vector3.up * 1.45f);
                });
            }

            Finish();
        }

        // ───────────────────────── Fin (normal o saltado) ─────────────────────────

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (routine != null) StopCoroutine(routine);

            Time.timeScale = 1f;
            ClearNightOfAshes();
            AyniScreenFX.ClearAll();
            AyniAudio.StopLoop(fireLoop, 1.5f);
            AyniAudio.StopLoop(music, 2.5f);

            bool skipped = skipRequested;
            Phase(skipped ? "saltado por el jugador" : "fin");
            PlaceYari(spawnPosition, spawnRotation);
            if (yariAnimator != null)
            {
                yariAnimator.speed = 1f;
                if (skipped || !yariAnimator.GetCurrentAnimatorStateInfo(0).IsName("Combat_Locomotion"))
                {
                    if (HasState("Combat_Locomotion")) yariAnimator.CrossFadeInFixedTime("Combat_Locomotion", 0.2f);
                }
            }

            if (yariMover != null) yariMover.enabled = true;
            if (yari != null)
            {
                yari.CinematicControl = false;
                yari.EnterCombatStance();
            }
            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = true;
                gameplayCamera.SnapBehindTarget();
            }

            AyniScreenFX.SetFade(Color.black, skipped ? 1f : 0f);
            if (skipped) AyniScreenFX.FadeTo(Color.black, 0f, 0.6f);

            AyniGameState.CinematicPlaying = false;
            AyniGameState.UnlockInput();
            IsRunning = false;
            enabled = false;
        }

        private void Update()
        {
            if (!IsRunning || finished) return;
            if (AyniInput.Down(AyniInput.Action.Skip)) skipRequested = true;

            if (fireLight != null)
            {
                fireLight.intensity = 3.2f + 1.4f * Mathf.PerlinNoise(Time.unscaledTime * 3.1f, 0.3f);
            }
        }

        // ───────────────────────── Piezas ─────────────────────────

        private Vector3 SayriPosition()
        {
            return sayri != null ? sayri.transform.parent.position : deck;
        }

        /// <summary>
        /// Sayri y su guardia cruzan el puente al paso, con los dos soldados detrás, y se paran frente a Yari.
        /// </summary>
        private IEnumerator March(Vector3 from, Vector3 to, Vector3 side, Vector3 along, float seconds)
        {
            float t = 0f;
            while (t < seconds && !finished && sayri != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / seconds);
                // Paso constante y frenada corta al final
                float k = u < 0.85f ? u / 0.85f * 0.94f : 0.94f + 0.06f * Smooth((u - 0.85f) / 0.15f);
                Vector3 p = Vector3.Lerp(from, to, k);
                cast.Place(sayri, p, -along);
                cast.Place(guardA, p + along * 1.8f + side, -along);
                cast.Place(guardB, p + along * 1.8f - side, -along);
                float pace = u < 0.85f ? 0.5f : Mathf.Lerp(0.5f, 0f, (u - 0.85f) / 0.15f);
                AyniPrologueCast.SetSpeed(sayri, pace);
                AyniPrologueCast.SetSpeed(guardA, pace);
                AyniPrologueCast.SetSpeed(guardB, pace);
                yield return null;
            }
            AyniPrologueCast.SetSpeed(sayri, 0f);
            AyniPrologueCast.SetSpeed(guardA, 0f);
            AyniPrologueCast.SetSpeed(guardB, 0f);
        }

        private void PlaceYari(Vector3 position, Quaternion rotation)
        {
            if (yariTransform == null) return;
            bool wasEnabled = yariMover != null && yariMover.enabled;
            if (yariMover != null) yariMover.enabled = false;
            yariTransform.SetPositionAndRotation(position, rotation);
            if (yariMover != null) yariMover.enabled = wasEnabled;
        }

        private void Play(string state, float fade)
        {
            if (!HasState(state)) return;
            if (fade <= 0f) yariAnimator.Play(state, 0, 0f);
            else yariAnimator.CrossFadeInFixedTime(state, fade, 0, 0f);
        }

        private bool HasState(string state)
        {
            return yariAnimator != null && yariAnimator.runtimeAnimatorController != null &&
                   yariAnimator.HasState(0, Animator.StringToHash(state));
        }

        private IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds && !skipRequested)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Plano de cámara: la función devuelve posición y punto de mira para cada instante (0..1).</summary>
        private IEnumerator Shot(float seconds, System.Func<float, (Vector3 pos, Vector3 look)> path)
        {
            float t = 0f;
            while (t < seconds && !skipRequested)
            {
                var (pos, look) = path(Mathf.Clamp01(t / seconds));
                Aim(pos, look, 1f);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Coloca la cámara (suavizada con <paramref name="follow"/>: 1 = directa) y aplica la sacudida.</summary>
        private void Aim(Vector3 position, Vector3 look, float follow)
        {
            if (cam == null) return;
            Vector3 p = follow >= 1f ? position : Vector3.Lerp(cam.transform.position, position, follow);
            if (Time.unscaledTime < shakeUntil)
            {
                float k = (shakeUntil - Time.unscaledTime) / 0.5f;
                p += Random.insideUnitSphere * shakeAmount * Mathf.Clamp01(k);
            }
            cam.transform.position = p;
            Vector3 dir = look - p;
            if (dir.sqrMagnitude > 0.0001f) cam.transform.rotation = Quaternion.LookRotation(dir);
        }

        private void Shake(float amount, float seconds)
        {
            shakeAmount = amount;
            shakeUntil = Time.unscaledTime + seconds;
        }

        /// <summary>La noche de las cenizas: resplandor rojizo, brasas que suben de la garganta y el fuego del valle.</summary>
        private void BuildNightOfAshes()
        {
            AyniScreenFX.Tint(new Color(0.2f, 0.035f, 0.015f), 0.22f);

            Vector3 along = Vector3.Cross(acrossBridge, Vector3.up);
            var fireGo = new GameObject("Prologo_FuegoDelValle");
            fireGo.transform.position = deck + along * 16f + Vector3.up * 4f;
            fireLight = fireGo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.42f, 0.12f);
            fireLight.range = 45f;
            fireLight.intensity = 3.5f;

            embers = new GameObject("Prologo_Brasas");
            embers.transform.position = deck + Vector3.down * 4f;
            var ps = embers.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(Ember, new Color(1f, 0.8f, 0.3f));
            main.gravityModifier = -0.06f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            var emission = ps.emission;
            emission.rateOverTime = 90f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 10f, 34f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.7f;
            noise.frequency = 0.35f;
            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = gradient;
            var renderer = embers.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = EmberMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Simulate(4f, true, true);
            ps.Play();
        }

        private void ClearNightOfAshes()
        {
            AyniScreenFX.Tint(Color.clear, 0f);
            if (cast != null) cast.Clear();
            cast = null;
            sayri = guardA = guardB = null;
            if (embers != null) Destroy(embers);
            if (fireLight != null) Destroy(fireLight.gameObject);
            if (illaLight != null) Destroy(illaLight.gameObject);
            embers = null;
            fireLight = null;
            illaLight = null;
        }

        /// <summary>La Illa del antebrazo izquierdo de Yari se enciende con luz dorada (0 = apagada, 1 = plena).</summary>
        private void LightIlla(float amount)
        {
            if (illaLight == null)
            {
                Transform forearm = yariAnimator.isHuman ? yariAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
                var go = new GameObject("Prologo_Illa");
                go.transform.SetParent(forearm != null ? forearm : yariTransform, false);
                go.transform.localPosition = forearm != null ? Vector3.zero : Vector3.up;
                illaLight = go.AddComponent<Light>();
                illaLight.type = LightType.Point;
                illaLight.color = IllaGold;
                illaLight.range = 9f;
            }
            illaLight.intensity = Mathf.Lerp(0f, 14f, amount);
            if (Random.value < amount * 0.12f)
            {
                Ayni.Combat.CombatFeedback.Flash(illaLight.transform.position, IllaGold, Mathf.Lerp(0.3f, 0.9f, amount), 0.18f);
            }
        }

        private static Material emberMaterial;

        /// <summary>Punto de luz suave para las brasas (generado por código).</summary>
        private static Material EmberMaterial()
        {
            if (emberMaterial != null) return emberMaterial;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            emberMaterial = new Material(Ayni.Combat.CombatFeedback.SpriteMaterial) { mainTexture = tex };
            return emberMaterial;
        }

        private float startTime;

        /// <summary>Deja constancia de cada fase en la consola (sirve para ajustar los tiempos de la escena).</summary>
        private void Phase(string name)
        {
            Debug.Log($"[Ayni Prólogo] {Time.unscaledTime - startTime:F1} s · {name}");
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
