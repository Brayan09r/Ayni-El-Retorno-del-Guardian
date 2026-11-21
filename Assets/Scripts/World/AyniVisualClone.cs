using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ayni.World
{
    /// <summary>
    /// Copia solo lo que se ve de un personaje de la escena (malla, esqueleto y Animator), sin sus scripts, colisiones,
    /// físicas ni sonidos: para figuras de escena que no deben pelear ni contar como rivales (Amaru acechando en el
    /// camino, la puma madre que acude al duelo, la cría enjaulada).
    /// La copia se hace dentro de un contenedor inactivo, así que ningún componente del original llega a arrancar.
    /// </summary>
    public static class AyniVisualClone
    {
        /// <summary>
        /// Crea la copia visual de <paramref name="source"/>. Sale <b>inactiva</b>, en la raíz de la escena: actívala al
        /// colocarla. Devuelve null si no hay original.
        /// </summary>
        public static GameObject Create(GameObject source, string name)
        {
            if (source == null) return null;
            var holder = new GameObject(name + "_Montaje");
            holder.SetActive(false);
            GameObject copy = UnityEngine.Object.Instantiate(source, holder.transform);
            copy.name = name;
            StripToVisual(copy);

            Animator anim = copy.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            foreach (SkinnedMeshRenderer smr in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
            }

            // Ya sin scripts: se saca del contenedor (inactiva) y el contenedor sobra
            copy.SetActive(false);
            copy.transform.SetParent(null, true);
            UnityEngine.Object.Destroy(holder);
            return copy;
        }

        /// <summary>Animator de la copia (o null).</summary>
        public static Animator AnimatorOf(GameObject clone) => clone != null ? clone.GetComponentInChildren<Animator>(true) : null;

        /// <summary>Reproduce un estado si el Animator lo tiene. Devuelve si lo tenía.</summary>
        public static bool Play(Animator anim, string state, float fade)
        {
            if (anim == null || !anim.gameObject.activeInHierarchy || !anim.HasState(0, Animator.StringToHash(state))) return false;
            if (fade <= 0f) anim.Play(state, 0, 0f);
            else anim.CrossFadeInFixedTime(state, fade);
            return true;
        }

        /// <summary>Quita scripts, colisiones, físicas y sonidos; deja el esqueleto, las mallas y el Animator.</summary>
        public static void StripToVisual(GameObject root)
        {
            var behaviours = new List<MonoBehaviour>(root.GetComponentsInChildren<MonoBehaviour>(true));
            // Primero los que nadie necesita (RequireComponent): si no, Unity se niega a quitarlos
            for (int guard = 0; behaviours.Count > 0 && guard < 64; guard++)
            {
                bool removed = false;
                for (int i = behaviours.Count - 1; i >= 0; i--)
                {
                    MonoBehaviour b = behaviours[i];
                    if (b == null)
                    {
                        behaviours.RemoveAt(i);
                        continue;
                    }
                    if (RequiredByOthers(b, behaviours)) continue;
                    UnityEngine.Object.DestroyImmediate(b);
                    behaviours.RemoveAt(i);
                    removed = true;
                }
                if (!removed) break;
            }
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (Rigidbody r in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(r);
            foreach (AudioSource s in root.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(s);
        }

        private static bool RequiredByOthers(Component c, List<MonoBehaviour> others)
        {
            foreach (MonoBehaviour other in others)
            {
                if (other == null || other == c || other.gameObject != c.gameObject) continue;
                foreach (object attribute in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    var rc = (RequireComponent)attribute;
                    if (Requires(rc.m_Type0, c) || Requires(rc.m_Type1, c) || Requires(rc.m_Type2, c)) return true;
                }
            }
            return false;
        }

        private static bool Requires(Type type, Component c) => type != null && type.IsInstanceOfType(c);
    }
}
