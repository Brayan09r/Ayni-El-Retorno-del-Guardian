using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;

namespace Ayni.UI
{
    /// <summary>
    /// Aviso de "mantén el botón" sobre un objeto del escenario (jaulas, la tranca de la casa en llamas): banda de
    /// piedra con el botón de Interactuar y una barra de lo que falta. Se llama desde OnGUI.
    /// </summary>
    public static class AyniHoldPrompt
    {
        // Rombos ya dibujados en este pase de OnGUI, para que dos avisos pegados al mismo borde no se monten
        private static readonly List<Vector2> placed = new List<Vector2>();
        private static int placedFrame = -1;
        private static EventType placedEvent;

        public static void Draw(Vector3 world, string action, float progress)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 screen = cam.WorldToScreenPoint(world);
            var p = new Vector2(screen.x, Screen.height - screen.y);
            // Si el objeto queda detrás o fuera de la vista (Yari de espaldas a él), el aviso va al centro, abajo,
            // en vez de pegarse a un borde lejos de todo
            if (screen.z <= 0f || p.x < 0f || p.x > Screen.width || p.y < 0f || p.y > Screen.height)
                p = new Vector2(Screen.width / 2f, Screen.height - 230f);
            // Ni encima del rival y los objetivos (arriba) ni sobre la vida de Yari (abajo)
            p.x = Mathf.Clamp(p.x, 150f, Screen.width - 150f);
            p.y = Mathf.Clamp(p.y, 150f, Screen.height - 170f);
            var band = new Rect(p.x - 140f, p.y - 40f, 280f, 44f);
            AyniInkaUI.Band(band, 0.9f);
            AyniInkaUI.Label(new Rect(band.x, band.y + 4f, band.width, 18f),
                $"<b>[{AyniInput.Label(AyniInput.Action.Interact)}]</b> Mantén: {action}",
                AyniInkaUI.Style(13, FontStyle.Normal, TextAnchor.MiddleCenter), AyniInkaUI.Bone);
            AyniInkaUI.Bar(new Rect(band.x + 30f, band.y + 28f, band.width - 60f, 5f), progress, progress, AyniInkaUI.Jade);
        }

        /// <summary>
        /// Rombo que marca algo en el mundo; si queda fuera de la vista se pega al borde (sin tapar el panel de objetivos
        /// de arriba ni la vida de Yari de abajo).
        /// </summary>
        public static void Marker(Vector3 world, Color color, string label)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 screen = cam.WorldToScreenPoint(world);
            bool behind = screen.z < 0f;
            if (behind) screen = -screen;
            var p = new Vector2(screen.x, Screen.height - screen.y);
            const float side = 40f, top = 110f, bottom = 130f;
            if (behind || p.x < side || p.x > Screen.width - side || p.y < top || p.y > Screen.height - bottom)
            {
                Vector2 c = new Vector2(Screen.width / 2f, (top + Screen.height - bottom) / 2f);
                Vector2 d = p - c;
                if (behind) d.y = Mathf.Abs(d.y);
                if (d.sqrMagnitude < 1f) d = Vector2.up;
                float sx = (Screen.width / 2f - side) / Mathf.Max(Mathf.Abs(d.x), 0.001f);
                float sy = ((Screen.height - bottom - top) / 2f) / Mathf.Max(Mathf.Abs(d.y), 0.001f);
                p = c + d * Mathf.Min(sx, sy);
            }
            p = AvoidOverlap(p);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            float size = Mathf.Lerp(12f, 16f, pulse);
            var rect = new Rect(p.x - size / 2f, p.y - size / 2f, size, size);
            Matrix4x4 prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, rect.center);
            AyniInkaUI.Fill(new Rect(rect.x - 2f, rect.y - 2f, size + 4f, size + 4f), new Color(0f, 0f, 0f, 0.6f));
            AyniInkaUI.Fill(rect, color);
            GUI.matrix = prev;
            if (!string.IsNullOrEmpty(label))
            {
                AyniInkaUI.Label(new Rect(p.x - 70f, p.y - size - 14f, 140f, 16f), AyniInkaUI.Spaced(label),
                                 AyniInkaUI.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter), color, 0.95f);
            }
        }

        /// <summary>
        /// Si ya hay un rombo con su nombre en ese sitio en este pase de dibujo, este se aparta hacia el centro de la
        /// pantalla (arriba si está en la mitad de abajo, abajo si está en la de arriba).
        /// </summary>
        private static Vector2 AvoidOverlap(Vector2 p)
        {
            Event e = Event.current;
            EventType type = e != null ? e.type : EventType.Repaint;
            if (Time.frameCount != placedFrame || type != placedEvent)
            {
                placed.Clear();
                placedFrame = Time.frameCount;
                placedEvent = type;
            }
            float step = p.y > Screen.height * 0.5f ? -38f : 38f;
            for (int guard = 0; guard < 6; guard++)
            {
                bool clash = false;
                foreach (Vector2 q in placed)
                {
                    if (Mathf.Abs(q.x - p.x) < 120f && Mathf.Abs(q.y - p.y) < 34f) { clash = true; break; }
                }
                if (!clash) break;
                p.y += step;
            }
            placed.Add(p);
            return p;
        }
    }
}
