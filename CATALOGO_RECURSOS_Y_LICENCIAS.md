# 📦 CATÁLOGO DE RECURSOS MULTIMEDIA, AUDIO Y LICENCIAS
# 🌄 AYNI: EL RETORNO DEL GUARDIÁN (`v0.7.0`)

> **Inventario Técnico de Activos 3D, Animaciones, Audio Procedural, Shaders y Especificación de Recursos Pendientes**  
> **Motor:** Unity 6 (`6000.3.23f1`) · **Política de Propiedad Intelectual:** 100% Libre de Infracciones de Terceros

---

## 📌 1. RESUMEN DE ACTIVOS DEL REPOSITORIO

El repositorio de **AYNI: El Retorno del Guardián** gestiona un total de **702 archivos versionados**, organizados para garantizar que todos los recursos gráficos y sonoros sean de producción propia, generados proceduralmente por código o derivados de herramientas con licencia de uso comercial/académico libre de regalías.

| Categoría de Recurso | Ruta en el Proyecto | Cantidad | Origen / Herramienta de Producción |
| :--- | :--- | :---: | :--- |
| **Modelos 3D de Personajes (`.fbx`)** | `Assets/Art/Characters/` | 6 | Láminas A-pose propias + **Tripo3D** + Auto-rig en **Mixamo** |
| **Prefabs de Rivales (`.prefab`)** | `Assets/Prefabs/` | 4 | Generados con [`AyniRivalSetup.cs`](Assets/Editor/AyniRivalSetup.cs) |
| **Animaciones de Combate/Locomoción (`.fbx`)** | `Assets/Art/Characters/Animations/` | 29 | **Adobe Mixamo** (Esqueleto Humanoid, 30 fps) |
| **Animaciones Procedurales (`.anim`)** | `Assets/Art/Characters/Animations/Generadas/` | 9 | Sintetizadas por código en [`AyniAnimationForge.cs`](Assets/Editor/AyniAnimationForge.cs) |
| **Efectos de Sonido y Música (`.wav`)** | `Assets/Resources/AyniAudio/` | 42 | Síntesis matemática en Python ([`generar_sfx.py`](Herramientas/Audio/generar_sfx.py)) |
| **Shaders Gráficos URP (`.shader`)** | `Assets/Shaders/` | 5 | Escritos a medida en **HLSL / ShaderLab** |
| **Arquitectura Inca y Vegetación** | Procedural en Escena / Runtime | 4 Kanchas + Bosques | [`AyniVillageBuilder.cs`](Assets/Editor/AyniVillageBuilder.cs) y [`AyniReforestation.cs`](Assets/Scripts/Story/AyniReforestation.cs) |
| **Infografías y Arte Conceptual (`.jpg`/`.png`)** | `Assets/Art/GDD_Art/`, `Controls_Art/`, `ConceptArt/` | 26 | Ilustraciones conceptuales del GDD, controles y rivales |

---

## 🧍 2. CATÁLOGO DE MODELOS 3D Y PREFABS DE PERSONAJES

Todos los personajes utilizan configuración **Humanoid Avatar** en Unity con texturas **PBR (Diffuse, Normal, MetallicRoughness)**:

| Personaje / Arquetipo | Archivo FBX / Prefab | Rol en el Juego | Altura Normalizada |
| :--- | :--- | :--- | :---: |
| **Yari (El Guardián)** | `Assets/Art/Characters/Yari_Character.fbx` | Protagonista jugable | `1.80 m` |
| **Amaru el Cazador** | `Assets/Art/Characters/Amaru_Character.fbx` | Jefe del Nivel 1 (2 Fases + Cerbatana) | `1.92 m` |
| **Rastreador (`K1`)** | `Assets/Prefabs/Rival_1_Rastreador.prefab` | Rival ágil del Puesto de Chasquis | `1.78 m` |
| **Saqueador (`K2`)** | `Assets/Prefabs/Rival_2_Saqueador.prefab` | Rival robusto de la Kancha de los Tejedores | `1.76 m` |
| **Guardia del Tambo (`K3`)** | `Assets/Prefabs/Rival_3_GuardiaTambo.prefab` | Rival defensivo del Tambo del Camino | `1.86 m` |
| **Cazador de Élite (`K4`)** | `Assets/Prefabs/Rival_4_CazadorElite.prefab` | Rival de élite de la Portada del Cazador | `1.88 m` |

---

## 🥋 3. CATÁLOGO DE ANIMACIONES (38 CLIPS)

### 3.1. Animaciones Syntetizadas en la Fragua ([`AyniAnimationForge.cs`](Assets/Editor/AyniAnimationForge.cs))
Todas llevan la etiqueta `SinIK` en el Animator Controller para evitar que `FootIK` y `AndeanCombatStanceModifier` deformen las piernas:

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

---

## 🔊 4. CATÁLOGO DE LOS 42 ARCHIVOS DE AUDIO SINTETIZADOS (`Assets/Resources/AyniAudio/`)

Generados mediante [`Herramientas/Audio/generar_sfx.py`](Herramientas/Audio/generar_sfx.py) (frecuencia de muestreo `44,100 Hz`, PCM 16-bit), garantizando **cero problemas de copyright**:

| Categoría | Archivos `.wav` en `Assets/Resources/AyniAudio/` | Disparador en Código |
| :--- | :--- | :--- |
| **Impactos de Combate** | `golpe_ligero_1..3.wav`, `golpe_pesado_1..3.wav`, `remate.wav`, `golpe_recibido_1..2.wav` | `CombatFeedback.cs` / `YariCombatController.cs` |
| **Defensa y Postura** | `bloqueo_1..2.wav`, `parry_1..2.wav`, `esquiva_1..2.wav`, `postura_rota.wav` | `YariCombatController.cs` / `StructureSystem.cs` |
| **Zumbidos (Whooshes)** | `aire_ligero_1..3.wav`, `aire_pesado_1..2.wav` | `YariCombatController.cs` / `EnemyController.cs` |
| **Locomoción y Caídas** | `paso_piedra_1..4.wav`, `salto.wav`, `aterrizaje.wav`, `aterrizaje_pesado.wav`, `caida_viento.wav`, `caida_agua.wav` | `AyniFootsteps.cs` / `YariCombatController.cs` |
| **Jefe Amaru (Fase 2)** | `cerbatana_disparo.wav`, `dardo_impacto.wav`, `veneno_pulso.wav`, `jefe_rugido.wav` | `AmaruHunter.cs` / `AyniBossPhaseDirector.cs` |
| **Historia y Juicio Ayni** | `juicio_tambor.wav`, `ayni_perdon_campanas.wav`, `venganza_golpe.wav`, `illa_resurreccion.wav` | `AyniJudgment.cs` / `AyniLevelOutcome.cs` / `IllaTalismanSystem.cs` |
| **Ambiente y Música** | `viento_andino_loop.wav`, `fuego_loop.wav`, `lluvia_loop.wav`, `quena_prologo.wav`, `tambores_kancha.wav` | `AyniLevelAmbience.cs` / `AyniPrologue.cs` |
| **Interfaz (UI)** | `ui_mover.wav`, `ui_confirmar.wav`, `ui_pausa.wav` | `AyniPauseMenu.cs` / `AyniTutorial.cs` |

---

## 📋 5. ESPECIFICACIÓN DE RECURSOS PENDIENTES PARA PRÓXIMAS ITERACIONES

De acuerdo con la hoja de ruta técnica en [`NIVEL_1_PROLOGO_Y_JUGABILIDAD.md`](NIVEL_1_PROLOGO_Y_JUGABILIDAD.md) y [`ALDEA_INCA.md`](ALDEA_INCA.md), se documentan los recursos listos para ser producidos e integrados:

1. **Modelo 3D Riggeado del General Sayri (`Sayri_Character.fbx`):**
   - **Estado:** Prompts de frente y espalda en A-pose ya redactados en [`ConceptArt/Jefes_y_Prologo/prompts.md`](ConceptArt/Jefes_y_Prologo/prompts.md).
   - **Proceso sin carga local:** Generar imagen frontal/trasera ➔ Convertir a FBX en **Tripo3D Web** ➔ Auto-rig en **Mixamo Web** ➔ Colocar en `Assets/Art/Characters/` para el prólogo en el puente colgante.
2. **Voces y Exclamaciones Marciales (`voz_yari_ataque_1..3.wav`, `voz_yari_dolor_1..2.wav`, `voz_amaru_fase2.wav`):**
   - **Formato requerido:** `.wav` mono a `44,100 Hz`, normalizados a `-3 dBFS`, colocados en `Assets/Resources/AyniAudio/` para ser invocados mediante `AyniAudio.Play()`.
   - **Fuentes CC0 recomendadas:** Freesound.org (licencia *CC0 1.0 Universal Public Domain*) o grabación propia con guion en Quechua de [`NARRATIVA_Y_CULTURA_ANDINA.md`](NARRATIVA_Y_CULTURA_ANDINA.md).
3. **Barreras Físicas de Cierre Temporal en Kanchas (`AyniEncounterSite`):**
   - **Diseño:** Trancas de troncos de queñual o portones de cuerda que se levantan al dispararse `OnPlayerEntered` y descienden al emitirse `OnCleared` en cada una de las 4 kanchas.

---
*Catálogo de Recursos Multimedia y Licencias — Proyecto Ayni: El Retorno del Guardián.*
