# 🌄 AYNI: EL RETORNO DEL GUARDIÁN

[![Unity 6](https://img.shields.io/badge/Unity-6000.3.23f1-blue.svg)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/URP-Universal%20Render%20Pipeline-green.svg)](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest)
[![Genre](https://img.shields.io/badge/Genre-3D%20Beat%20'em%20up%20%7C%20Martial%20Arts-orange.svg)](#)
[![Version](https://img.shields.io/badge/Version-v0.8.0-brightgreen.svg)](CHANGELOG.md)

**AYNI: El Retorno del Guardián** es un videojuego de acción y combate táctico en tercera persona (estilo *Sifu* / *Beat 'em up*) ambientado en un mundo andino mítico. El jugador encarna a **Yari**, el último guardián que lucha para restaurar el orden cósmico del Tawantinsuyu enfrentando a los usurpadores corruptos de los recursos sagrados.

---

## 🎮 Mecánicas Principales (Inspiradas en Sifu)

1. **Sistema de Estructura / Postura (Posture / Structure Bar):**
   - El combate no depende únicamente de la barra de vida: golpear y parar ataques llena la barra de Estructura del oponente.
   - Romper la estructura del enemigo permite ejecutar un remate contundente (*Takedown*) o aplicar el perdón del **Ayni**.
2. **Defensa Táctica, Cancelación de Golpes y Evasiones (Avoid System):**
   - **Guardia (L1 / Clic Derecho):** Bloquea impactos a costa de aumentar la propia estructura, corta al instante la recuperación de un golpe propio y permite el **Desvío Perfecto (*Parry*)** en el último instante (`0.22 s`).
   - **Esquivas (Avoid / Slip):** Agacharse, dar un saltito o balancearse en el momento preciso para esquivar ataques sin moverse del sitio y contraatacar de inmediato (`+60%` de daño a la postura).
3. **Parkour Contextual Estilo Sifu (`YariTraversal` — `v0.8.0`):**
   - Sin salto libre: `Espacio` / `A` salta troncos y muros bajos (`0.3–1.3 m`) apoyando una mano sin perder carrera, se sube a plataformas anchas y trepa cornisas altas (`1.3–2.4 m`).
4. **Mecánica del Talismán Sagrado "Illa" (Envejecimiento):**
   - Al caer derrotado, el talismán sagrado resucita a Yari en el combate, pero a costa de consumir años de vida.
   - Con cada resurrección, Yari envejece: aumenta su maestría y poder de ataque espiritual, pero disminuye su salud física máxima.
5. **Arte Marcial Ancestral "Rumi Maki":**
   - Golpes directos, codazos, patadas circulares, patada hacia atrás y ganchos inspirados en la tradición marcial incaica del puño de piedra (*Rumi Maki* / *Takanakuy*).

---

## 🕹️ Controles del Jugador (Yari)

| Acción | Teclado & Ratón | Descripción |
| :--- | :--- | :--- |
| **Moverse (Trotar)** | `W` `A` `S` `D` | Movimiento en 3ª persona con inercia física relativo a la cámara |
| **Correr / Sprint** | `Shift Izquierdo` (mantener) | Aumenta la velocidad y pasa a animación de carrera rápida |
| **Agacharse / Cuclillas** | `C` (alternar) o `Ctrl Izq` (mantener) | Reduce el perfil de colisión y permite avanzar sigilosamente |
| **Saltar obstáculo / Trepar** | `Espacio` (mando: `A`) | Como en Sifu, sin salto libre: salta troncos y muros bajos apoyando la mano y trepa cornisas de hasta 2.4 m |
| **Postura Relajada / Combate** | Automático | Brazos abajo en descanso; al combatir o defender levanta la guardia |
| **Guardia / Bloqueo** | `Clic Derecho` o `G` (mando: `LB`) | Yari se planta en el sitio; desvío perfecto (parry) si es en el último instante |
| **Esquivas Estilo Sifu** | En Guardia + `S` / `W` o `Espacio` / `A`·`D` | Agacharse (golpes altos), saltito (barridos) o balanceo (cualquiera), sin moverse del sitio |
| **Ataque Ligero Rumi Maki** | `Clic Izquierdo` | Combinación fluida de puñetazos rápidos |
| **Ataque Pesado Rumi Maki** | `Q` o `E` | Patada circular contundente con alto daño a la postura |
| **Juicio Ayni (Venganza / Perdón)** | `F` (Ejecutar) / `X` (Perdonar) — mando: `B` / `A` | Acciones morales cuando la estructura del rival está rota |
| **Mostrar / Ocultar Controles** | `H` (mando: `Cruceta Arriba`) | Muestra u oculta el panel de controles del HUD |
| **Menú de Pausa** | `Escape` (mando: `Menu`) | Abre el menú de pausa con ajuste de volúmenes, tutorial y reinicio |

**Mando de Xbox:** totalmente compatible con **vibración háptica XInput** (`AyniRumble`) en golpes, bloqueos, desvíos y aterrizajes. Tabla completa en [CONTROLES.md](CONTROLES.md).

---

## 🏔️ Requisitos del Proyecto

- **Motor:** Unity 6 (`6000.3.23f1` o superior)
- **Pipeline de Renderizado:** Universal Render Pipeline (URP)
- **Plataformas:** PC (Windows / Linux / macOS) y Navegador Web (WebGL)

---

## 🚀 Instalación y Ejecución

1. Clona el repositorio:
   ```bash
   git clone https://github.com/Brayan09r/Ayni-El-Retorno-del-Guardian.git
   ```
2. Abre **Unity Hub** y añade el proyecto seleccionando la carpeta raíz.
3. Abre el proyecto en Unity 6.
4. En el menú superior de la barra de herramientas, ejecuta:
   - `Ayni` ➔ `2. Integrar Combate en el Mapa de Caminos`
5. Abre la escena en `Assets/network of paths/Scenes/SampleScene.unity` y pulsa **Play (▶)**.

---

## 🌐 Jugar en el Navegador (Unity Play — WebGL)

El Nivel 1 completo (**Valle de las Sombras / El Camino del Antisuyo**) se encuentra publicado y disponible para jugar directamente en el navegador sin instalación:
- 🎮 **[Jugar AYNI: El Retorno del Guardián en Unity Play](https://play.unity.com/en/games/66b8abca-71ca-4703-8580-d51948d2be29/ayninivel1)**

---

## 📚 Índice Completo de Documentación del Proyecto

| Documento | Descripción y Alcance |
| :--- | :--- |
| 📘 **[DOCUMENTACION_INTEGRAL_AYNI.md](DOCUMENTACION_INTEGRAL_AYNI.md)** | **Documento Maestro Consolidado (`v0.8.0`):** Ficha técnica, narrativa, GDD, parkour, arquitectura de software, iluminación, fauna, diseño de niveles, ODS, QA y conclusiones. |
| 📜 **[GDD_AYNI.md](GDD_AYNI.md)** | **Game Design Document (GDD):** Historia ilustrada, mecánicas centrales y estructura de misiones. |
| 🏗️ **[ARQUITECTURA_TECNICA.md](ARQUITECTURA_TECNICA.md)** | **Arquitectura de Software (`v0.8.0`):** Arquitectura en 4 capas, los 33 scripts C# de runtime, máquina de estados de IA, 6 shaders URP y 29 herramientas de editor. |
| 🧪 **[PLAN_DE_PRUEBAS_QA.md](PLAN_DE_PRUEBAS_QA.md)** | **Aseguramiento de Calidad y Balanceo:** Curva matemática de envejecimiento de la Illa, métricas anti-patinaje y matriz de casos de prueba funcionales. |
| 🏔️ **[NARRATIVA_Y_CULTURA_ANDINA.md](NARRATIVA_Y_CULTURA_ANDINA.md)** | **Fundamentación Cultural y ODS:** Cosmovisión del *Ayni*, *Illa*, *Rumi Maki / Takanakuy*, glosario Quechua–Español, fauna andina y alineación con los **ODS 15, 6, 12 y 16**. |
| 🔥 **[ILUMINACION_ANDINA.md](ILUMINACION_ANDINA.md)** | **Iluminación Dinámica y Shaders:** Braseros ceremoniales incas, fogones con ruido Perlin, rayos solares volumétricos y 5 estados climáticos. |
| 🗺️ **[DISENO_NIVELES_2_AL_5.md](DISENO_NIVELES_2_AL_5.md)** | **Diseño de Niveles 2 al 5:** Expansión hacia las Canteras de Bronce (*Túpac*), Terrazas de Agua (*Apo Rumi*), Palacio Alquímico (*Mama Churi*) e Intihuatana (*General Sayri*). |
| 📦 **[CATALOGO_RECURSOS_Y_LICENCIAS.md](CATALOGO_RECURSOS_Y_LICENCIAS.md)** | **Catálogo de Recursos y Audio:** Inventario de modelos 3D, animaciones de combate y parkour, 42 efectos de sonido sintetizados en Python y fauna. |
| 🏘️ **[ALDEA_INCA.md](ALDEA_INCA.md)** · **[ENTORNO_ACANTILADOS.md](ENTORNO_ACANTILADOS.md)** | Diseño técnico y arquitectónico de las 4 *Kanchas* incas, 8 obstáculos, árboles renacientes y relieve del cañón. |
| 🕹️ **[CONTROLES.md](CONTROLES.md)** · **[NIVEL_1_PROLOGO_Y_JUGABILIDAD.md](NIVEL_1_PROLOGO_Y_JUGABILIDAD.md)** | Guía ilustrada de controles (Teclado/Ratón y Xbox) y notas técnicas de jugabilidad del Nivel 1. |
| 🎨 **[ConceptArt/Rivales/prompts.md](ConceptArt/Rivales/prompts.md)** · **[ConceptArt/Jefes_y_Prologo/prompts.md](ConceptArt/Jefes_y_Prologo/prompts.md)** | Prompts técnicos en A-pose simétrica (frente y espalda) para generación 3D de rivales, el General Sayri y jefes. |
| 📋 **[CHANGELOG.md](CHANGELOG.md)** · **[COLLABORATORS.md](COLLABORATORS.md)** | Historial detallado de versiones (`v0.3.0` a `v0.8.0`) y normas de contribución en Git. |

---

## 👥 Equipo de Desarrollo y Colaboradores

- **Brayan (`@Brayan09r`):** Dirección técnica y artística, arquitectura de entorno andino, modelos 3D/animaciones, aldea inca procedural, reforestación, tráiler y compilación WebGL.
- **Benn / Ragnar (`@BennJulius` / `@RagnarLoth21`):** Programación de sistemas de juego (prólogo escénico con Sayri, mando Xbox con vibración, esquivas y parkour estilo Sifu, síntesis de audio en Python, jefes en dos fases y menú de pausa).
- **Jhosue (`@Jhosue2330`):** Sistema de iluminación andina (`AyniDynamicLighting`, `AyniFireLight`, braseros ceremoniales incas y shader de rayos solares volumétricos).
- **Junior (`@JuniorEspiritu`):** Ambientación de fauna andina (llamas y pumas), estructuras de piedra del mapa y optimización de activos del proyecto.
- **Gilmer Antonny (`@Antonnyy`):** Ingeniería de documentación técnica, arquitectura de software, aseguramiento de calidad (QA), fundamentación cultural/ODS y gestión de recursos de diseño.

Desarrollado con pasión como proyecto de combate 3D andino y beat 'em up para Unity 6.
