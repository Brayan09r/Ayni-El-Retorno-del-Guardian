using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ayni.Core
{
    /// <summary>
    /// Voces de los subtítulos. Cada frase que sale con <c>AyniScreenFX.Caption</c> busca su audio en
    /// <c>Resources/AyniVoces/v_XXXXXXXX</c>, donde XXXXXXXX es un hash (FNV-1a de 32 bits) del texto normalizado: sin el
    /// nombre de quien habla ("AMARU:"), sin etiquetas de color, sin signos y en minúsculas. Así no hay que tocar cada
    /// llamada: basta con que la frase esté en el guion (<c>Herramientas/Audio/voces_guion.txt</c>) y se genere su audio con
    /// <c>Herramientas/Audio/generar_voces.py</c>, que calcula el mismo hash.
    /// Mientras alguien habla, la música baja un poco. Si una frase no tiene voz, se muestra solo el subtítulo.
    /// </summary>
    public static class AyniVoice
    {
        public const string ResourceFolder = "AyniVoces";
        /// <summary>Cuánto baja la música mientras se oye una voz.</summary>
        private const float MusicDuck = 0.55f;

        /// <summary>Duración de la última voz que empezó a sonar (0 si la frase no tenía voz).</summary>
        public static float LastLength { get; private set; }
        /// <summary>Hay una voz sonando.</summary>
        public static bool Speaking => source != null && source.isPlaying;
        /// <summary>Voces que han sonado en esta partida (para las pruebas).</summary>
        public static int SpokenCount { get; private set; }
        /// <summary>Texto de la última frase que sonó (para las pruebas).</summary>
        public static string LastSpoken { get; private set; } = "";

        private static AudioSource source;
        private static readonly Dictionary<uint, AudioClip> cache = new Dictionary<uint, AudioClip>();
        private static readonly HashSet<uint> missingLogged = new HashSet<uint>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            source = null;
            cache.Clear();
            missingLogged.Clear();
            LastLength = 0f;
            SpokenCount = 0;
            LastSpoken = "";
        }

        /// <summary>
        /// Dice la frase de un subtítulo (corta la anterior). Devuelve cuánto dura su voz, o 0 si no tiene.
        /// </summary>
        public static float Speak(string caption)
        {
            LastLength = 0f;
            if (string.IsNullOrEmpty(caption)) return 0f;
            uint key = Key(caption);
            if (!cache.TryGetValue(key, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>($"{ResourceFolder}/v_{key:x8}");
                cache[key] = clip;
            }
            if (clip == null)
            {
                if (missingLogged.Add(key)) Debug.Log($"[Ayni Voz] Sin voz (v_{key:x8}): {caption}");
                return 0f;
            }

            AudioSource s = Source();
            s.Stop();
            s.clip = clip;
            s.volume = AyniAudio.Volume;
            s.Play();
            LastLength = clip.length;
            SpokenCount++;
            LastSpoken = caption;
            return clip.length;
        }

        /// <summary>Calla la voz que esté sonando (al saltar una escena).</summary>
        public static void Stop()
        {
            if (source != null) source.Stop();
        }

        /// <summary>
        /// Cuánto esperar tras mostrar una frase antes de la siguiente: lo que dure su voz (y un respiro) o, si no tiene,
        /// los segundos de siempre.
        /// </summary>
        public static float HoldFor(float seconds) => Mathf.Max(seconds, LastLength + 0.35f);

        /// <summary>
        /// Muestra (y dice) la respuesta <paramref name="caption"/> cuando termina la frase que acaba de sonar, o pasados
        /// <paramref name="minDelay"/> segundos si no tenía voz. Sirve para que Yari conteste.
        /// </summary>
        public static Coroutine Reply(MonoBehaviour host, string caption, float minDelay, float seconds = 3.2f)
        {
            if (host == null || !host.isActiveAndEnabled) return null;
            return host.StartCoroutine(ReplyRoutine(caption, HoldFor(minDelay), seconds));
        }

        private static IEnumerator ReplyRoutine(string caption, float delay, float seconds)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (AyniGameState.CinematicPlaying && !AyniGameState.InputLocked) yield break;
            Ayni.UI.AyniScreenFX.Caption(caption, seconds);
        }

        // ───────────────────────── Clave del texto ─────────────────────────

        /// <summary>
        /// Clave de una frase: FNV-1a (32 bits) de su texto normalizado en UTF-8. Debe coincidir con
        /// <c>clave()</c> de generar_voces.py.
        /// </summary>
        public static uint Key(string caption)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Normalize(caption));
            uint hash = 2166136261u;
            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= 16777619u;
            }
            return hash;
        }

        /// <summary>
        /// Texto normalizado: sin etiquetas &lt;...&gt; ni teclas [...] (cambian con el teclado o el mando), sin el "NOMBRE:" del principio (mayúsculas), solo letras, números y
        /// espacios, en minúsculas y con los espacios colapsados.
        /// </summary>
        public static string Normalize(string caption)
        {
            if (string.IsNullOrEmpty(caption)) return "";
            var noTags = new StringBuilder(caption.Length);
            bool inTag = false;
            foreach (char c in caption)
            {
                if (c == '<' || c == '[') { inTag = true; continue; }
                if ((c == '>' || c == ']') && inTag) { inTag = false; continue; }
                if (!inTag) noTags.Append(c);
            }
            string text = noTags.ToString().Trim();

            // "AMARU: «...»", "CAPITÁN DEL TAMBO: ..." → sin el nombre
            int colon = text.IndexOf(':');
            if (colon > 1 && colon < 32)
            {
                bool speaker = true;
                for (int i = 0; i < colon; i++)
                {
                    char c = text[i];
                    if (!(char.IsUpper(c) || c == ' ')) { speaker = false; break; }
                }
                if (speaker) text = text.Substring(colon + 1);
            }

            var sb = new StringBuilder(text.Length);
            bool space = false;
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (space && sb.Length > 0) sb.Append(' ');
                    space = false;
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (char.IsWhiteSpace(c))
                {
                    space = true;
                }
            }
            return sb.ToString();
        }

        // ───────────────────────── Fuente de sonido ─────────────────────────

        private static AudioSource Source()
        {
            if (source != null) return source;
            var go = new GameObject("Ayni_Voz");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 16;
            go.AddComponent<AyniVoiceDucker>();
            return source;
        }

        /// <summary>Baja la música mientras hay voz y la devuelve después, suavemente.</summary>
        private class AyniVoiceDucker : MonoBehaviour
        {
            private void Update()
            {
                float target = Speaking ? MusicDuck : 1f;
                AyniAudio.Duck = Mathf.MoveTowards(AyniAudio.Duck, target, Time.unscaledDeltaTime * (Speaking ? 3f : 0.8f));
            }

            private void OnDestroy()
            {
                AyniAudio.Duck = 1f;
            }
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>Dice una frase como si saliera en un subtítulo (por ejemplo, "AMARU: «Corre, guardián...»").</summary>
        public static void Test(string caption)
        {
            float length = Speak(caption);
            Debug.Log($"[Ayni Voz] v_{Key(caption):x8} «{Normalize(caption)}» · {(length > 0f ? $"{length:F2} s" : "sin voz")}");
        }

        /// <summary>Cuántas voces hay en Resources y cuántas han sonado.</summary>
        public static void Report()
        {
            AudioClip[] all = Resources.LoadAll<AudioClip>(ResourceFolder);
            Debug.Log($"[Ayni Voz] {all.Length} voces en Resources/{ResourceFolder} · sonaron {SpokenCount} · última: {LastSpoken}");
        }
    }
}
