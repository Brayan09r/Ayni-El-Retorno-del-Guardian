# 📦 CATÁLOGO DE RECURSOS MULTIMEDIA, AUDIO Y LICENCIAS
# 🌄 AYNI: EL RETORNO DEL GUARDIÁN (`v0.8.0`)

> **Inventario Técnico de Activos 3D, Animaciones, Audio Procedural, Shaders, Tipografías, Fauna y Auditoría de Licencias**  
> **Motor:** Unity 6 (`6000.3.23f1`) · **Trazabilidad Legal:** Origen y licencia verificados fila por fila (con registro explícito de activos en auditoría)

---

## 📌 1. RESUMEN DE ACTIVOS Y ESTADO DE LICENCIAMIENTO

El repositorio de **AYNI: El Retorno del Guardián** documenta el origen técnico y la licencia de cada categoría de activos integrados. Los sistemas centrales de audio, shaders, arquitectura incaica y animaciones tácticas son de producción propia o síntesis por código, mientras que las fuentes tipográficas y paquetes externos registran su licencia específica o su estado actual de revisión antes de distribución pública:

| Categoría de Recurso | Ruta Real en el Proyecto | Cantidad | Origen, Autoría y Licencia Verificada |
| :--- | :--- | :---: | :--- |
| **Modelos 3D de Personajes (`.fbx`)** | `Assets/Art/Characters/` | 7 | Láminas A-pose propias + **Tripo3D** + Auto-rig en **Adobe Mixamo** (Uso propio del proyecto) |
| **Prefabs de Rivales y Jefes (`.prefab`)** | `Assets/Prefabs/` | 5 | Generados en Editor con [`AyniRivalSetup.cs`](Assets/Editor/AyniRivalSetup.cs) y [`AyniAmaruSetup.cs`](Assets/Editor/AyniAmaruSetup.cs) |
| **Fauna en Escena (Llama y Puma)** | `Assets/Art/Animals/` (`Llama.obj`, `pumacotw.fbx`) | 2 mallas | Añadidos en `53bd376` y `828f750` — **⚠️ En auditoría de licencia** (`pumacotw.fbx` pendiente de reemplazo; véase nota en Sección 2.1) |
| **Paquete Auxiliar de Animales** | `Assets/ithappy/Animals_FREE/` | Paquete 3D | *Animals FREE* por **ithappy** (Unity Asset Store Standard EULA; presente en repo, no usado para pumas/llamas de la aldea) |
| **Animaciones de Combate/Locomoción (`.fbx`)** | `Assets/Art/Characters/Animations/` | 30 | **Adobe Mixamo** (Esqueleto Humanoid, 30 fps; licencia gratuita de uso en proyectos para titulares de cuenta Adobe) |
| **Animaciones Procedurales (`.anim`)** | `Assets/Art/Characters/Animations/Generadas/` | 11 | Producción propia sintetizada por código en [`AyniAnimationForge.cs`](Assets/Editor/AyniAnimationForge.cs) |
| **Efectos de Sonido y Música (`.wav`)** | `Assets/Resources/AyniAudio/` | 42 | Producción propia por síntesis matemática en Python ([`generar_sfx.py`](Herramientas/Audio/generar_sfx.py)) |
| **Shaders Gráficos URP (`.shader`)** | `Assets/Shaders/` | 6 | Producción propia escrita a medida en **HLSL / ShaderLab** |
| **Iluminación Ceremonial Andina** | Escena / [`AyniLightingSetup.cs`](Assets/Editor/AyniLightingSetup.cs) | 24 + 5 + 6 | Producción propia procedural (24 antorchas, 5 fogatas y 6 rayos solares volumétricos) |
| **Arquitectura Inca y Vegetación** | Procedural en Escena / Runtime | 4 Kanchas + Bosques | Producción propia ([`AyniVillageBuilder.cs`](Assets/Editor/AyniVillageBuilder.cs) y [`AyniReforestation.cs`](Assets/Scripts/Story/AyniReforestation.cs)) |
| **Infografías y Arte Conceptual (`.jpg`/`.png`)** | `Assets/Art/GDD_Art/`, `Controls_Art/`, `ConceptArt/` | 28 | Ilustraciones conceptuales propias asistidas por IA para GDD, controles, rivales y jefes |

---

## 🧍 2. CATÁLOGO DE MODELOS 3D, PREFABS DE PERSONAJES Y FAUNA

Todos los personajes humanos utilizan configuración **Humanoid Avatar** en Unity con texturas **PBR (Diffuse, Normal, MetallicRoughness)**:

| Personaje / Entidad | Ruta Real del Activo en el Repositorio | Rol en el Juego | Origen y Estado de Licencia |
| :--- | :--- | :--- | :--- |
| **Yari (El Guardián)** | `Assets/Art/Characters/Yari_Character.fbx` (`1.80 m`) | Protagonista jugable | Lámina propia + Tripo3D + Mixamo (Propio) |
| **Amaru el Cazador** | `Assets/Art/Characters/Amaru_Rigged.fbx` (`1.80–1.92 m`) | Jefe del Nivel 1 (2 Fases + Cerbatana) | Lámina propia + Tripo3D + Mixamo (Propio) |
| **Rastreador (`K1`)** | `Assets/Prefabs/Rival_1_Rastreador.prefab` (`1.78 m`) | Rival ágil del Puesto de Chasquis | `ConceptArt/Rivales/` + Tripo3D + Mixamo (Propio) |
| **Saqueador (`K2`)** | `Assets/Prefabs/Rival_2_Saqueador.prefab` (`1.76 m`) | Rival robusto de la Kancha de los Tejedores | `ConceptArt/Rivales/` + Tripo3D + Mixamo (Propio) |
| **Guardia del Tambo (`K3`)** | `Assets/Prefabs/Rival_3_GuardiaTambo.prefab` (`1.86 m`) | Rival defensivo del Tambo del Camino | `ConceptArt/Rivales/` + Tripo3D + Mixamo (Propio) |
| **Cazador de Élite (`K4`)** | `Assets/Prefabs/Rival_4_CazadorElite.prefab` (`1.88 m`) | Rival de élite de la Portada del Cazador | `ConceptArt/Rivales/` + Tripo3D + Mixamo (Propio) |
| **Llama Andina** | `Assets/Art/Animals/Llama.obj` + `textures/Llama_BaseColor.png` | Fauna en campos abiertos cercanos a la aldea (`53bd376`) | **⚠️ Origen externo por documentar:** pendiente confirmar autor, URL y licencia abierta con `@JuniorEspiritu` |
| **Puma en Bordes** | `Assets/Art/Animals/pumacotw.fbx` + `textures/puma_common1_dif.ddsc.DECA.REFERENCE.png` | Delimitador visual en bordes del camino (`828f750`) | **⚠️ No libre de regalías (pendiente de sustitución):** nomenclatura `.ddsc.DECA` corresponde a extracción de *theHunter: Call of the Wild* (Expansive Worlds); debe reemplazarse por malla propia en Tripo3D o activo CC0 (Issue #4 / #9) |

### 2.1. Nota de Auditoría sobre Activos de Fauna (`Assets/Art/Animals/`)
Tras la eliminación definitiva de la carpeta heredada `Assets/Tiger/` (commits `a9aaab6` y `c2c93b6`), los modelos de fauna ubicados actualmente en la escena provienen de `Assets/Art/Animals/` y **no** del paquete `Assets/ithappy/Animals_FREE/`. Mientras no se complete la sustitución de `pumacotw.fbx` por un modelo de autoría propia o con licencia CC0 comprobada y se acredite la fuente de `Llama.obj`, estos dos archivos se mantienen registrados bajo observación en el control de calidad legal del repositorio.

---

## 🥋 3. CATÁLOGO DE ANIMACIONES (40 CLIPS EN TOTAL)

### 3.1. Las 11 Animaciones Sintetizadas en la Fragua ([`AyniAnimationForge.cs`](Assets/Editor/AyniAnimationForge.cs))
Todas llevan la etiqueta `SinIK` en el Animator Controller para evitar que `FootIK` y `AndeanCombatStanceModifier` deformen las piernas durante maniobras aéreas, esquivas o parkour:

| Archivo `.anim` (`Animations/Generadas/`) | Estado en el Animator | Duración | Función en Jugabilidad |
| :--- | :--- | :---: | :--- |
| `Yari_Avoid_Duck.anim` | `Avoid_Duck` | `0.44 s` | Esquiva en el sitio agachando el torso ante ataques altos. |
| `Yari_Avoid_Jump.anim` | `Avoid_Jump` / `Hunter_Leap` | `0.45 s` | Saltito en el sitio recogiendo piernas ante barridos bajos. |
| `Yari_Avoid_SwayL.anim` | `Avoid_SwayL` | `0.46 s` | Balanceo lateral izquierdo (extraído de `Dodge_Left` con guardia). |
| `Yari_Avoid_SwayR.anim` | `Avoid_SwayR` | `0.46 s` | Balanceo lateral derecho (extraído de `Dodge_Right` con guardia). |
| `Yari_Back_Kick.anim` | `Atk_BackKick` | `0.52 s` | Patada trasera directa cuando el rival elegido está a la espalda (`>115°`). |
| `Yari_Fall_Loop.anim` | `Fall_Loop` | Bucle | Caída libre en el aire al descender más de `1.2 m`. |
| `Yari_Fall_Back.anim` | `Fall_Back` | `1.40 s` | Caída dramática de espaldas hacia el abismo en el prólogo y quebrada. |
| `Yari_Land_Hard.anim` | `Land_Hard` | `0.55 s` | Aterrizaje pesado al caer desde más de `3.5 m` de altura. |
| `Gen_Roar.anim` | `Phase_Roar` | `1.80 s` | Grito de guerra de Amaru al iniciar su Segunda Fase (*"Desatado"*). |
| `Yari_Vault.anim` (`v0.8.0`) | `Traversal_Vault` | `0.55 s` | Salto de valla estilo Sifu con mano de apoyo sobre obstáculos bajos (`0.3–1.3 m`). |
| `Yari_Climb.anim` (`v0.8.0`) | `Traversal_Climb` | `0.78 s` | Trepada ágil de cornisa con agarre de manos e impulso hacia arriba (`1.3–2.4 m`). |

---

## 🔊 4. CATÁLOGO DE LOS 42 ARCHIVOS DE AUDIO SINTETIZADOS (`Assets/Resources/AyniAudio/`)

Generados mediante [`Herramientas/Audio/generar_sfx.py`](Herramientas/Audio/generar_sfx.py) (frecuencia de muestreo `44,100 Hz`, PCM 16-bit), garantizando **cero problemas de copyright**:

| Categoría | Archivos `.wav` en `Assets/Resources/AyniAudio/` | Disparador en Código |
| :--- | :--- | :--- |
| **Impactos de Combate** | `golpe_ligero_1..3.wav`, `golpe_pesado_1..3.wav`, `remate.wav`, `golpe_recibido_1..2.wav` | `CombatFeedback.cs` / `YariCombatController.cs` |
| **Defensa y Postura** | `bloqueo_1..2.wav`, `parry_1..2.wav`, `esquiva_1..2.wav`, `postura_rota.wav` | `YariCombatController.cs` / `StructureSystem.cs` |
| **Zumbidos (Whooshes)** | `aire_ligero_1..3.wav`, `aire_pesado_1..2.wav` | `YariCombatController.cs` / `EnemyController.cs` |
| **Locomoción y Caídas** | `paso_piedra_1..4.wav`, `salto.wav`, `aterrizaje.wav`, `aterrizaje_pesado.wav`, `caida_viento.wav`, `caida_agua.wav` | `AyniFootsteps.cs` / `YariCombatController.cs` / `YariTraversal.cs` |
| **Jefe Amaru (Fase 2)** | `cerbatana_disparo.wav`, `dardo_impacto.wav`, `veneno_pulso.wav`, `jefe_rugido.wav` | `AmaruHunter.cs` / `AyniBossPhaseDirector.cs` |
| **Historia y Juicio Ayni** | `juicio_tambor.wav`, `ayni_perdon_campanas.wav`, `venganza_golpe.wav`, `illa_resurreccion.wav` | `AyniJudgment.cs` / `AyniLevelOutcome.cs` / `IllaTalismanSystem.cs` |
| **Ambiente y Música** | `viento_andino_loop.wav`, `fuego_loop.wav`, `lluvia_loop.wav`, `quena_prologo.wav`, `tambores_kancha.wav` | `AyniLevelAmbience.cs` / `AyniPrologue.cs` |
| **Interfaz (UI)** | `ui_mover.wav`, `ui_confirmar.wav`, `ui_pausa.wav` | `AyniPauseMenu.cs` / `AyniTutorial.cs` |

---

## 📋 5. ESPECIFICACIÓN DE RECURSOS PENDIENTES PARA PRÓXIMAS ITERACIONES

De acuerdo con la hoja de ruta técnica en [`NIVEL_1_PROLOGO_Y_JUGABILIDAD.md`](NIVEL_1_PROLOGO_Y_JUGABILIDAD.md) y [`ALDEA_INCA.md`](ALDEA_INCA.md), se documentan los recursos listos para ser producidos e integrados:

1. **Malla 3D Dedicada del General Sayri (`Sayri_Character.fbx`):**
   - **Estado actual en `v0.8.0`:** El General Sayri y sus guardias ya aparecen en el puente mediante [`AyniPrologueCast.cs`](Assets/Scripts/Story/AyniPrologueCast.cs). Además, los prompts de frente y espalda en A-pose están listos en [`ConceptArt/Jefes_y_Prologo/prompts.md`](ConceptArt/Jefes_y_Prologo/prompts.md) por si se desea reemplazar por una malla exclusiva generada en **Tripo3D Web** + **Mixamo Web**.
2. **Voces y Exclamaciones Marciales (`voz_yari_ataque_1..3.wav`, `voz_yari_dolor_1..2.wav`, `voz_amaru_fase2.wav`):**
   - **Formato requerido:** `.wav` mono a `44,100 Hz`, normalizados a `-3 dBFS`, colocados en `Assets/Resources/AyniAudio/` para ser invocados mediante `AyniAudio.Play()`.
   - **Fuentes CC0 recomendadas:** Freesound.org (licencia *CC0 1.0 Universal Public Domain*) o grabación propia con guion en Quechua de [`NARRATIVA_Y_CULTURA_ANDINA.md`](NARRATIVA_Y_CULTURA_ANDINA.md).
3. **Barreras Físicas de Cierre Temporal en Kanchas (`AyniEncounterSite`):**
   - **Diseño:** Trancas de troncos de queñual o portones de cuerda que se levantan al dispararse `OnPlayerEntered` y descienden al emitirse `OnCleared` en cada una de las 4 kanchas.

---
*Catálogo de Recursos Multimedia y Licencias — Proyecto Ayni: El Retorno del Guardián (`v0.8.0`).*
