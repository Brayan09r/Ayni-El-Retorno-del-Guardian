using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;
using Ayni.Player;
using Ayni.Story;
using Ayni.World;

namespace Ayni.UI
{
    /// <summary>
    /// Fichas pequeñas encima del nombre de Yari con lo que lleva ganado para el duelo contra Amaru (quipu, chumpi,
    /// cóndor, puma madre, los quipus de Amaru) y, en rojo, lo que Amaru sabe (trampas en su plaza). Cada ficha nueva brilla un momento.
    /// Así el jugador ve que lo que hace en el camino cuenta. SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniRewardsHUD : MonoBehaviour
    {
        private struct Chip
        {
            public string key, text;
            public bool good;
        }

        private YariCombatController yari;
        private IllaTalismanSystem talisman;
        private readonly Dictionary<string, float> firstSeen = new Dictionary<string, float>();
        private readonly List<Chip> chips = new List<Chip>();

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                yari = player.GetComponent<YariCombatController>();
                talisman = player.GetComponent<IllaTalismanSystem>();
            }
        }

        private void Collect()
        {
            chips.Clear();
            if (AyniPathRewards.QuipuRecovered) chips.Add(new Chip { key = "quipu", text = "Quipu · lees sus dardos", good = true });
            if (AyniPathRewards.ChumpiReceived) chips.Add(new Chip { key = "chumpi", text = "Chumpi · veneno a la mitad", good = true });
            if (AyniCaptiveCages.CondorFreed) chips.Add(new Chip { key = "kuntur", text = "Kuntur · avisa los dardos", good = true });
            if (AyniCaptiveCages.PumaFreed && !AyniFreedAllies.PumaHelped) chips.Add(new Chip { key = "puma", text = "Puma · su madre acudirá", good = true });
            if (AyniPathRewards.CaptainSpared && AyniTamboCaptain.DeserterName != null) chips.Add(new Chip { key = "capitan", text = "Capitán · un Cazador menos", good = true });
            if (AyniPathRewards.CaptainSlain) chips.Add(new Chip { key = "champi", text = "Champi · +20 % de daño a Amaru", good = true });
            if (AyniAmaruQuipus.KnowsStory) chips.Add(new Chip { key = "killa", text = "Killa · Amaru dudará", good = true });
            else if (AyniAmaruQuipus.Read > 0) chips.Add(new Chip { key = "quipus" + AyniAmaruQuipus.Read, text = $"Quipus de Amaru · {AyniAmaruQuipus.Read} / {AyniAmaruQuipus.Total}", good = true });
            if (AyniOfferings.Collected > 0) chips.Add(new Chip { key = "ofrendas" + AyniOfferings.Collected, text = $"Ofrendas · {AyniOfferings.Collected} / {AyniOfferings.Total}", good = true });
            if (talisman != null && talisman.ProtectedFalls > 0) chips.Add(new Chip { key = "apu", text = "Apu · tu próxima caída no cuesta años", good = true });
            if (AyniPathRewards.AmaruWarned) chips.Add(new Chip { key = "avisado", text = "Amaru avisado · trampas", good = false });
        }

        private void OnGUI()
        {
            if (yari == null || yari.IsBeingRescued || yari.IsGameOver) return;
            if (AyniGameState.CinematicPlaying || AyniGameState.InputLocked || AyniJudgment.Active || AyniBossIntro.Running || AyniDeathAging.Active) return;
            Collect();
            if (chips.Count == 0) return;

            // Encima del nombre de Yari (ver SifuCombatHUD.DrawPlayerPanel: medallón de 80 px a 26 px del borde)
            float x = 128f;
            float y = Screen.height - 26f - 80f - 24f;
            GUIStyle style = AyniInkaUI.Style(11, FontStyle.Bold, TextAnchor.MiddleCenter);
            foreach (Chip chip in chips)
            {
                if (!firstSeen.ContainsKey(chip.key)) firstSeen[chip.key] = Time.unscaledTime;
                float glow = Mathf.Clamp01(1f - (Time.unscaledTime - firstSeen[chip.key]) / 2.5f);
                float w = style.CalcSize(new GUIContent(chip.text)).x + 16f;
                var r = new Rect(x, y, w, 17f);
                Color tone = chip.good ? AyniInkaUI.Jade : new Color(1f, 0.42f, 0.32f);
                if (glow > 0.01f) AyniInkaUI.Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(tone.r, tone.g, tone.b, 0.35f * glow));
                AyniInkaUI.Fill(r, new Color(0.06f, 0.05f, 0.04f, 0.78f));
                AyniInkaUI.Fill(new Rect(r.x, r.yMax - 1.5f, r.width, 1.5f), tone);
                AyniInkaUI.Label(r, chip.text, style, Color.Lerp(tone, Color.white, 0.25f + 0.5f * glow));
                x += w + 6f;
            }
        }
    }
}
