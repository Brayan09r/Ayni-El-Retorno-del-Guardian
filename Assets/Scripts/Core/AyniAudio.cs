using System.Collections.Generic;
using UnityEngine;

namespace Ayni.Core
{
    /// <summary>
    /// Sonido del juego. Carga los .wav de Assets/Resources/AyniAudio (los genera Herramientas/Audio/generar_sfx.py)
    /// y los reproduce por nombre: <c>AyniAudio.Play("golpe_ligero", punto)</c> elige al azar entre las variantes
    /// golpe_ligero_1, _2, _3... con un poco de variación de tono, para que dos golpes nunca suenen idénticos.
    ///
    ///   Play       efecto en un punto del mundo (sonido 3D)
    ///   Play2D     efecto sin posición (interfaz, música, escenas)
    ///   PlayLoop   bucle con fundido de entrada (ambiente: viento, fuego, lluvia); StopLoop lo apaga con fundido
    ///
    /// Se crea solo al cargar la escena; no hay que añadir nada. Para cambiar un sonido basta con reemplazar el .wav.
    /// </summary>
    public static class AyniAudio
    {
        public const string ResourceFolder = "AyniAudio";

        /// <summary>Volumen general de los efectos (0-1).</summary>
        public static float Volume = 1f;
        /// <summary>Volumen de la música y del ambiente (0-1).</summary>
        public static float AmbienceVolume = 1f;

        private static Dictionary<string, List<AudioClip>> clips;
        private static readonly Queue<string> recent = new Queue<string>();

        /// <summary>Los últimos sonidos reproducidos (para las pruebas automáticas, que se hacen con el audio silenciado).</summary>
        public static string Recent => string.Join(", ", recent);

        /// <summary>Cuántos sonidos distintos hay cargados.</summary>
        public static int LoadedCount
        {
            get
            {
                if (clips == null) LoadClips();
                return clips.Count;
            }
        }

        private static void Remember(string name)
        {
            recent.Enqueue(name);
            while (recent.Count > 16) recent.Dequeue();
        }
        private static AyniAudioPlayer player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            clips = null;
            player = null;
            recent.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePlayer()
        {
            Get();
        }

        private static AyniAudioPlayer Get()
        {
            if (player == null)
            {
                var go = new GameObject("Ayni_Audio");
                Object.DontDestroyOnLoad(go);
                player = go.AddComponent<AyniAudioPlayer>();
            }
            return player;
        }

        private static void LoadClips()
        {
            clips = new Dictionary<string, List<AudioClip>>();
            foreach (AudioClip clip in Resources.LoadAll<AudioClip>(ResourceFolder))
            {
                string key = BaseName(clip.name);
                if (!clips.TryGetValue(key, out List<AudioClip> list)) clips[key] = list = new List<AudioClip>();
                list.Add(clip);
            }
        }

        /// <summary>"golpe_ligero_2" → "golpe_ligero"; "remate" → "remate".</summary>
        private static string BaseName(string name)
        {
            int underscore = name.LastIndexOf('_');
            if (underscore > 0 && int.TryParse(name.Substring(underscore + 1), out _)) return name.Substring(0, underscore);
            return name;
        }

        /// <summary>Un clip del sonido (una variante al azar), o null si no existe.</summary>
        public static AudioClip Clip(string name)
        {
            if (clips == null) LoadClips();
            if (!clips.TryGetValue(name, out List<AudioClip> list) || list.Count == 0) return null;
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>Efecto en un punto del mundo.</summary>
        public static void Play(string name, Vector3 position, float volume = 1f, float pitchJitter = 0.06f, float pitch = 1f)
        {
            AudioClip clip = Clip(name);
            if (clip == null) return;
            Remember(name);
            Get().PlayOneShot(clip, position, volume * Volume, pitch * (1f + Random.Range(-pitchJitter, pitchJitter)), true);
        }

        /// <summary>Efecto sin posición (interfaz, escenas).</summary>
        public static void Play2D(string name, float volume = 1f, float pitch = 1f)
        {
            AudioClip clip = Clip(name);
            if (clip == null) return;
            Remember(name);
            Get().PlayOneShot(clip, Vector3.zero, volume * Volume, pitch, false);
        }

        /// <summary>Arranca un bucle con fundido de entrada. Devuelve un número para apagarlo luego (−1 si no existe el sonido).</summary>
        public static int PlayLoop(string name, float volume = 1f, float fadeSeconds = 1.5f)
        {
            AudioClip clip = Clip(name);
            if (clip == null) return -1;
            return Get().StartLoop(clip, volume, fadeSeconds, true);
        }

        /// <summary>Música o pista larga sin repetir, que se puede apagar con fundido (StopLoop).</summary>
        public static int PlayMusic(string name, float volume = 1f, float fadeSeconds = 0.5f)
        {
            AudioClip clip = Clip(name);
            if (clip == null) return -1;
            return Get().StartLoop(clip, volume, fadeSeconds, false);
        }

        public static void StopLoop(int handle, float fadeSeconds = 1.5f)
        {
            if (handle < 0 || player == null) return;
            player.StopLoop(handle, fadeSeconds);
        }

        public static void SetLoopVolume(int handle, float volume, float fadeSeconds = 0.5f)
        {
            if (handle < 0 || player == null) return;
            player.FadeLoop(handle, volume, fadeSeconds);
        }
    }

    /// <summary>Altavoces del juego: un grupo de fuentes para los efectos y una por cada bucle de ambiente.</summary>
    internal class AyniAudioPlayer : MonoBehaviour
    {
        private const int PoolSize = 28;

        private readonly List<AudioSource> pool = new List<AudioSource>();
        private int next;

        private class Loop
        {
            public AudioSource source;
            public float target;
            public float speed;
            public bool stopping;
        }

        private readonly Dictionary<int, Loop> loops = new Dictionary<int, Loop>();
        private int nextHandle;

        private void Awake()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Fuente_" + i);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 4f;
                source.maxDistance = 55f;
                pool.Add(source);
            }
        }

        public void PlayOneShot(AudioClip clip, Vector3 position, float volume, float pitch, bool spatial)
        {
            // La fuente más antigua se reutiliza si todas están ocupadas
            AudioSource source = null;
            for (int i = 0; i < pool.Count; i++)
            {
                AudioSource candidate = pool[(next + i) % pool.Count];
                if (!candidate.isPlaying)
                {
                    source = candidate;
                    next = (next + i + 1) % pool.Count;
                    break;
                }
            }
            if (source == null)
            {
                source = pool[next];
                next = (next + 1) % pool.Count;
            }

            source.transform.position = position;
            // Algo de mezcla 2D: los golpes se oyen claros aunque la cámara esté a unos metros
            source.spatialBlend = spatial ? 0.75f : 0f;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.loop = false;
            source.Play();
        }

        public int StartLoop(AudioClip clip, float volume, float fadeSeconds, bool repeat)
        {
            var go = new GameObject("Bucle_" + clip.name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.loop = repeat;
            source.spatialBlend = 0f;
            source.volume = 0f;
            // Los bucles de ambiente empiezan en un punto al azar para que no suenen siempre igual al entrar
            if (repeat) source.time = Random.Range(0f, clip.length * 0.9f);
            source.Play();

            int handle = nextHandle++;
            loops[handle] = new Loop
            {
                source = source,
                target = Mathf.Clamp01(volume),
                speed = fadeSeconds > 0.01f ? 1f / fadeSeconds : 100f
            };
            return handle;
        }

        public void StopLoop(int handle, float fadeSeconds)
        {
            if (!loops.TryGetValue(handle, out Loop loop)) return;
            loop.target = 0f;
            loop.stopping = true;
            loop.speed = fadeSeconds > 0.01f ? 1f / fadeSeconds : 100f;
        }

        public void FadeLoop(int handle, float volume, float fadeSeconds)
        {
            if (!loops.TryGetValue(handle, out Loop loop)) return;
            loop.target = Mathf.Clamp01(volume);
            loop.speed = fadeSeconds > 0.01f ? 1f / fadeSeconds : 100f;
        }

        private readonly List<int> finished = new List<int>();

        private void Update()
        {
            finished.Clear();
            foreach (var pair in loops)
            {
                Loop loop = pair.Value;
                if (loop.source == null)
                {
                    finished.Add(pair.Key);
                    continue;
                }
                float wanted = loop.target * AyniAudio.AmbienceVolume;
                loop.source.volume = Mathf.MoveTowards(loop.source.volume, wanted, loop.speed * Time.unscaledDeltaTime);
                bool ended = !loop.source.loop && !loop.source.isPlaying;
                if (ended || (loop.stopping && loop.source.volume <= 0.001f))
                {
                    Destroy(loop.source.gameObject);
                    finished.Add(pair.Key);
                }
            }
            foreach (int handle in finished) loops.Remove(handle);
        }
    }
}
