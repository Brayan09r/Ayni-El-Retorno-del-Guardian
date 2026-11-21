using System;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.Story;
using Ayni.World;

namespace Ayni.UI
{
    /// <summary>
    /// Objetivos del Nivel 1, como un diario de misión: arriba a la derecha dice qué hay que hacer ahora (llegar a la
    /// siguiente kancha, liberarla, enfrentarse a Amaru), cuántos rivales quedan en pie y a cuánto está el destino; un
    /// rombo de oro en pantalla marca hacia dónde ir. Al entrar en una kancha sale su nombre y el motivo de la pelea, y
    /// al despejarla, el aviso de que está liberada.
    /// Lee el progreso de <see cref="AyniEncounterSite"/> y del jefe: no guarda estado propio que pueda desincronizarse.
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniLevelObjectives : MonoBehaviour
    {
        /// <summary>Lo que se cuenta de cada kancha, por su orden en el camino (1 a 4).</summary>
        private struct SiteStory
        {
            public string name, motive, rivals, liberated;
            public SiteStory(string name, string motive, string rivals, string liberated)
            {
                this.name = name;
                this.motive = motive;
                this.rivals = rivals;
                this.liberated = liberated;
            }
        }

        private static readonly SiteStory[] Stories =
        {
            new SiteStory("Puesto de Chasquis", "Los Rastreadores de Amaru tomaron el puesto de los mensajeros del Inca.",
                          "Rastreadores", "Los chasquis vuelven a correr por el Qhapaq Ñan."),
            new SiteStory("Kancha de los Tejedores", "Los Saqueadores queman los telares del ayllu.",
                          "Saqueadores", "Los telares del ayllu dejan de arder."),
            new SiteStory("Tambo del Camino", "Los Guardias de Bronce custodian los tributos robados al pueblo.",
                          "Guardias del Tambo", "El tambo vuelve a ser del pueblo."),
            new SiteStory("Portada del Cazador", "Los Cazadores de élite guardan los trofeos vivos de Amaru: abre sus jaulas.",
                          "Cazadores de élite", "El paso hacia Amaru está libre."),
        };

        /// <summary>Objetivo que se muestra ahora (también para las sondas de prueba).</summary>
        public static string CurrentObjective { get; private set; } = "";
        /// <summary>Detalle del objetivo (rivales en pie, distancia...).</summary>
        public static string CurrentDetail { get; private set; } = "";
        /// <summary>Kanchas liberadas hasta ahora.</summary>
        public static int SitesLiberated { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CurrentObjective = "";
            CurrentDetail = "";
            SitesLiberated = 0;
        }

        private YariCombatController yari;
        private EnemyController boss;
        private Vector3? waypoint;
        private string lastObjective = "";
        private float objectiveChangedAt = -10f;
        private bool levelDone;

        private void OnEnable()
        {
            AyniEncounterSite.OnPlayerEntered += HandleEntered;
            AyniEncounterSite.OnCleared += HandleCleared;
        }

        private void OnDisable()
        {
            AyniEncounterSite.OnPlayerEntered -= HandleEntered;
            AyniEncounterSite.OnCleared -= HandleCleared;
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
            foreach (EnemyController e in EnemyController.All)
            {
                if (e != null && e.IsBoss && (boss == null || e.CharacterName.Contains("Amaru"))) boss = e;
            }
        }

        /// <summary>Nombre corto de una kancha: el de la historia, o el de la escena sin el "K1 · " delante.</summary>
        public static string NameOf(AyniEncounterSite site)
        {
            if (site == null) return "";
            if (site.Order >= 1 && site.Order <= Stories.Length) return Stories[site.Order - 1].name;
            string name = site.SiteName ?? "";
            int dot = name.IndexOf('·');
            return dot >= 0 ? name.Substring(dot + 1).Trim() : name;
        }

        private static SiteStory? StoryOf(AyniEncounterSite site)
        {
            if (site == null || site.Order < 1 || site.Order > Stories.Length) return null;
            return Stories[site.Order - 1];
        }

        private void HandleEntered(AyniEncounterSite site)
        {
            SiteStory? story = StoryOf(site);
            AyniScreenFX.Title(NameOf(site).ToUpperInvariant(), story?.motive ?? "", 3.2f);
        }

        private void HandleCleared(AyniEncounterSite site)
        {
            SitesLiberated = CountLiberated();
            SiteStory? story = StoryOf(site);
            AyniAudio.Play2D("illa_brillo", 0.45f, 1.1f);
            AyniScreenFX.Title(NameOf(site).ToUpperInvariant() + " LIBERADO", story?.liberated ?? "", 3.2f);
            Debug.Log($"[Ayni Objetivos] {NameOf(site)} liberado ({SitesLiberated}/{AyniEncounterSite.All.Count})");
        }

        private static int CountLiberated()
        {
            int n = 0;
            foreach (AyniEncounterSite s in AyniEncounterSite.All) if (s != null && s.Cleared) n++;
            return n;
        }

        // ───────────────────────── Qué toca ahora ─────────────────────────

        private void Update()
        {
            if (yari == null) return;
            SitesLiberated = CountLiberated();

            // 1) Una kancha en la que Yari ya entró y aún tiene rivales en pie
            AyniEncounterSite fighting = null, next = null;
            foreach (AyniEncounterSite s in AyniEncounterSite.All)
            {
                if (s == null || s.Cleared) continue;
                if (s.PlayerEntered && (fighting == null || s.Order < fighting.Order)) fighting = s;
                if (!s.PlayerEntered && (next == null || s.Order < next.Order)) next = s;
            }

            string objective, detail;
            if (fighting != null)
            {
                int standing = Standing(fighting);
                SiteStory? story = StoryOf(fighting);
                objective = "Libera " + Article(fighting) + NameOf(fighting);
                detail = standing > 0
                    ? $"{story?.rivals ?? "Rivales"} en pie: <b>{standing}</b>"
                    : "Derrota a los que quedan";
                detail += CagesNote(fighting);
                waypoint = null;
            }
            else if (next != null)
            {
                Vector3 target = next.Entry != null ? next.Entry.position : next.transform.position;
                objective = "Ve " + ToArticle(next) + NameOf(next);
                detail = $"Sigue el camino · a {Mathf.RoundToInt(Flat(yari.transform.position, target))} m";
                waypoint = target;
            }
            else if (boss != null && !boss.IsDead)
            {
                if (boss.Dormant)
                {
                    objective = "Llega al puente de Amaru";
                    detail = $"El Cazador te espera · a {Mathf.RoundToInt(Flat(yari.transform.position, boss.transform.position))} m";
                    if (AyniCaptiveCages.Total > 0 && AyniCaptiveCages.Opened < AyniCaptiveCages.Total)
                    {
                        detail += $" · jaulas {AyniCaptiveCages.Opened}/{AyniCaptiveCages.Total}";
                    }
                    waypoint = boss.transform.position;
                }
                else
                {
                    objective = "Derrota a Amaru el Cazador";
                    detail = boss.InFinalPhase ? "Rompe su postura y decide su destino" : "Lee sus golpes y rompe su postura";
                    waypoint = null;
                }
            }
            else
            {
                objective = "";
                detail = "";
                waypoint = null;
                levelDone = boss != null;
            }

            if (objective != lastObjective)
            {
                if (!string.IsNullOrEmpty(lastObjective) && !string.IsNullOrEmpty(objective))
                {
                    AyniAudio.Play2D("ui_confirmar", 0.5f);
                }
                lastObjective = objective;
                objectiveChangedAt = Time.unscaledTime;
                if (!string.IsNullOrEmpty(objective)) Debug.Log($"[Ayni Objetivos] {objective}");
            }
            CurrentObjective = objective;
            CurrentDetail = StripTags(detail);
            currentDetailRich = detail;
        }

        private string currentDetailRich = "";

        /// <summary>En la Portada del Cazador, cuántas jaulas van abiertas (opcional, pero cada animal ayuda).</summary>
        private static string CagesNote(AyniEncounterSite site)
        {
            if (AyniCaptiveCages.Total == 0 || site == null || site.Order < AyniEncounterSite.All.Count) return "";
            return AyniCaptiveCages.Opened < AyniCaptiveCages.Total
                ? $" · <color=#5cc7a8>jaulas {AyniCaptiveCages.Opened}/{AyniCaptiveCages.Total}</color>"
                : " · <color=#5cc7a8>animales libres</color>";
        }

        /// <summary>Rivales de la kancha que siguen en pie (cuentan los refuerzos que ya llegaron).</summary>
        public static int Standing(AyniEncounterSite site)
        {
            int n = 0;
            IReadOnlyList<GameObject> rivals = site.Rivals;
            for (int i = 0; i < rivals.Count; i++)
            {
                GameObject rival = rivals[i];
                if (rival == null) continue; // los que esperan ocultos en las casas también cuentan
                var enemy = rival.GetComponent<EnemyController>();
                if (enemy != null && !enemy.IsDead) n++;
            }
            return n;
        }

        /// <summary>"el Puesto", "la Kancha", "el Tambo", "la Portada".</summary>
        private static string Article(AyniEncounterSite site)
        {
            string name = NameOf(site);
            return name.StartsWith("Kancha") || name.StartsWith("Portada") ? "la " : "el ";
        }

        /// <summary>"al Puesto", "a la Kancha"...</summary>
        private static string ToArticle(AyniEncounterSite site) => Article(site) == "el " ? "al " : "a la ";

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static string StripTags(string text) =>
            string.IsNullOrEmpty(text) ? "" : System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");

        // ───────────────────────── Dibujo ─────────────────────────

        private bool Hidden()
        {
            if (levelDone || string.IsNullOrEmpty(CurrentObjective)) return true;
            if (AyniGameState.CinematicPlaying || AyniGameState.InputLocked) return true;
            if (AyniJudgment.Active || AyniBossIntro.Running || AyniDeathAging.Active) return true;
            if (yari == null || yari.IsBeingRescued || yari.IsGameOver) return true;
            return false;
        }

        private void OnGUI()
        {
            if (Hidden()) return;

            float width = Mathf.Clamp(Screen.width * 0.26f, 270f, 340f);
            float x = Screen.width - width - 18f;
            float y = 16f;
            // Si el panel del rival (arriba, centrado) lo pisaría, baja
            float enemyHalf = Mathf.Clamp(Screen.width * 0.36f, 240f, 440f) / 2f;
            if (x < Screen.width / 2f + enemyHalf + 12f) y = 92f;

            float sinceChange = Time.unscaledTime - objectiveChangedAt;
            float glow = Mathf.Clamp01(1f - sinceChange / 2.2f);
            // Entra deslizándose cuando cambia el objetivo
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sinceChange / 0.45f));
            x += (1f - slide) * 40f;

            var area = new Rect(x, y, width, 70f);
            AyniInkaUI.Band(area, 0.55f + 0.3f * slide);
            if (glow > 0.01f)
            {
                AyniInkaUI.Fill(new Rect(area.x, area.y, 3f, area.height), new Color(AyniInkaUI.BrightGold.r, AyniInkaUI.BrightGold.g, AyniInkaUI.BrightGold.b, glow));
            }

            int total = Mathf.Max(AyniEncounterSite.All.Count, 1);
            var head = AyniInkaUI.Style(11, FontStyle.Bold, TextAnchor.MiddleLeft);
            AyniInkaUI.Label(new Rect(area.x + 12f, area.y + 8f, width - 24f, 14f),
                             $"{AyniInkaUI.Spaced("Qhapaq Ñan")}   <color=#bfae8e>{SitesLiberated} / {total} kanchas</color>",
                             head, AyniInkaUI.Gold, slide);

            var main = AyniInkaUI.Style(14, FontStyle.Bold, TextAnchor.MiddleLeft);
            AyniInkaUI.Label(new Rect(area.x + 12f, area.y + 25f, width - 24f, 20f), CurrentObjective, main,
                             Color.Lerp(AyniInkaUI.Bone, AyniInkaUI.BrightGold, glow), slide);

            var small = AyniInkaUI.Style(12, FontStyle.Italic, TextAnchor.MiddleLeft);
            AyniInkaUI.Label(new Rect(area.x + 12f, area.y + 47f, width - 24f, 16f), currentDetailRich, small,
                             new Color(0.75f, 0.68f, 0.56f), slide);

            DrawWaypoint();
        }

        /// <summary>Rombo de oro sobre el destino, con la distancia; si queda fuera de la vista, se pega al borde.</summary>
        private void DrawWaypoint()
        {
            if (waypoint == null || yari == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 world = waypoint.Value + Vector3.up * 2.5f;
            float dist = Flat(yari.transform.position, world);
            if (dist < 14f) return;

            Vector3 screen = cam.WorldToScreenPoint(world);
            bool behind = screen.z < 0f;
            if (behind) screen = -screen;
            var p = new Vector2(screen.x, Screen.height - screen.y);
            const float margin = 46f;
            bool clamped = behind || p.x < margin || p.x > Screen.width - margin || p.y < margin || p.y > Screen.height - margin;
            if (clamped)
            {
                // Desde el centro de la pantalla hacia el destino, hasta el borde
                Vector2 c = new Vector2(Screen.width / 2f, Screen.height / 2f);
                Vector2 d = p - c;
                if (behind) d.y = Mathf.Abs(d.y); // detrás: abajo
                if (d.sqrMagnitude < 1f) d = Vector2.up;
                float sx = (Screen.width / 2f - margin) / Mathf.Max(Mathf.Abs(d.x), 0.001f);
                float sy = (Screen.height / 2f - margin) / Mathf.Max(Mathf.Abs(d.y), 0.001f);
                p = c + d * Mathf.Min(sx, sy);
            }

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
            const float size = 12f;
            var rect = new Rect(p.x - size / 2f, p.y - size / 2f, size, size);
            Matrix4x4 prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, rect.center);
            AyniInkaUI.Fill(new Rect(rect.x - 2f, rect.y - 2f, size + 4f, size + 4f), new Color(0f, 0f, 0f, 0.55f));
            AyniInkaUI.Fill(rect, new Color(AyniInkaUI.Gold.r, AyniInkaUI.Gold.g, AyniInkaUI.Gold.b, pulse));
            AyniInkaUI.Fill(new Rect(rect.x + 3.5f, rect.y + 3.5f, size - 7f, size - 7f), new Color(0f, 0f, 0f, 0.75f));
            GUI.matrix = prev;

            AyniInkaUI.Label(new Rect(p.x - 40f, p.y + 10f, 80f, 16f), $"{Mathf.RoundToInt(dist)} m",
                             AyniInkaUI.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter), AyniInkaUI.Bone, 0.85f);
        }
    }
}
