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
| **Fauna en Escena (Llama y Puma)** | `Assets/Art/Animals/Llama.obj`, `Assets/Art/Animals/PumaTripo/Puma_Tripo.fbx` | 2 mallas | Puma: GLB de Tripo proporcionado por Junior, convertido y animado en Blender. Llama: procedencia todavía pendiente. |
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
| **General Sayri (El Usurpador)** | `Assets/Art/Characters/GeneralSayri_Rigged.fbx` (`2.15 m`) + `Assets/Prefabs/General_Sayri.prefab` | Antagonista en el puente del prólogo (`083d2e0`) y Jefe Final | `ConceptArt/Jefes_y_Prologo/jefe_general_sayri_*.png` + **Tripo3D** (malla y mapas PBR `Sayri_Diffuse/Normal/MetallicRoughness`) + **Mixamo** (`Sayri_Walk.fbx`) |
| **Rastreador (`K1`)** | `Assets/Prefabs/Rival_1_Rastreador.prefab` (`1.78 m`) | Rival ágil del Puesto de Chasquis | `ConceptArt/Rivales/` + Tripo3D + Mixamo (Propio) |
| **Saqueador (`K2`)** | `Assets/Prefabs/Rival_2_Saqueador.prefab` (`1.76 m`) | Rival robusto de la Kancha de los Tejedores | `ConceptArt/Rivales/` + Tripo3D + Mixamo (Propio) |
| **Guardia del Tambo (`K3`)** | `Assets/Prefabs/Rival_3_GuardiaTambo.prefab` (`1.86 m`) | Rival defensivo del Tambo del Camino | `ConceptArt/Rivales/` + Tripo3D + Mixamo (Propio) |
| **Cazador de Élite (`K4`)** | `Assets/Prefabs/Rival_4_CazadorElite.prefab` (`1.88 m`) | Rival de élite de la Portada del Cazador | `ConceptArt/Rivales/` + Tripo3D + Mixamo (Propio) |
| **Llama Andina** | `Assets/Art/Animals/Llama.obj` + `textures/Llama_BaseColor.png` | Fauna en campos abiertos cercanos a la aldea (`53bd376`) | **⚠️ Origen externo por documentar:** pendiente confirmar autor, URL y licencia abierta con `@JuniorEspiritu` |
| **Puma Tripo** | `Assets/Art/Animals/PumaTripo/` | Ocho pumas territoriales: reposo, carrera, ataque y regreso al origen | Fuente: `tripo_pbr_model_44c7fb93-bc3d-468a-8a82-b60d275eccb3_meshopt.glb`, proporcionada por Junior desde Tripo. Optimizado a 18.000 triángulos, con dos texturas de 1024 px y tres clips propios. Las condiciones de la cuenta/exportación de Tripo no se adjuntaron; no se declara CC0. |

### 2.1. Nota de Auditoría sobre Activos de Fauna (`Assets/Art/Animals/`)
El puma anterior y sus archivos `.ddsc.DECA.REFERENCE` se sustituyeron por el GLB de Tripo proporcionado por Junior. El modelo experimental anterior también se retiró. Se conservan las ocho posiciones del mapa, usando un solo prefab compartido. Sigue pendiente documentar autor y licencia de `Llama.obj`, y conservar el comprobante de condiciones de la exportación de Tripo.

---

## 🥋 3. CATÁLOGO DE ANIMACIONES (41 CLIPS EN TOTAL: 30 MIXAMO + 11 FRAGUA)

Además de los 29 clips marciales y de locomoción de Yari, rivales y Amaru en `Assets/Art/Characters/Animations/`, el commit `083d2e0` incorporó el clip **`Sayri_Walk.fbx`** (caminata solemne en bucle con *Root Rotation/Position* bloqueados, vinculado a `GeneralSayri_AnimatorController.controller`).

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

## 🖼️ 5. ACTIVOS DEL MENÚ PRINCIPAL Y TIPOGRAFÍAS (`MainMenu` y `TextMesh Pro`)

| Activo / Recurso UI | Ruta Real en el Repositorio | Origen / Autoría | Licencia aplicable |
| :--- | :--- | :--- | :--- |
| **Fondo Panorámico del Menú** | `Assets/MianMenu/fondo.jpg` | Ilustración panorámica de ciudadela y cordillera andina al amanecer generada con IA (Google Gemini, integrada en `52fb1ce` por `@JuniorEspiritu`) | Generación asistida por IA del equipo (sin reclamo de terceros; pendiente confirmar si se mantiene o sustituye por captura en motor) |
| **Fuente UI `JosefinSans-SemiBold SDF`** | `Assets/TextMesh Pro/Resources/Fonts & Materials/JosefinSans-SemiBold SDF.asset` | Familia tipográfica **Josefin Sans**, diseñada por **Santiago Orozco** (*Typemade* / Google Fonts: `https://fonts.google.com/specimen/Josefin+Sans`) | **SIL Open Font License 1.1 (OFL)** — Uso comercial y académico libre |
| **Fuente Base `LiberationSans`** | `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` | Familia **Liberation Fonts** (*Red Hat, Inc.*, incluida en TMP Essential Resources) | **SIL Open Font License 1.1 (OFL)** — Texto íntegro en [`LiberationSans - OFL.txt`](Assets/TextMesh%20Pro/Fonts/LiberationSans%20-%20OFL.txt) |
| **Sprites de Ejemplo TMP (`EmojiOne`)** | `Assets/TextMesh Pro/Sprites/EmojiOne.png` | Paquete por defecto de TextMesh Pro (*EmojiOne Attribution.txt*, no mostrado en HUD de juego) | **CC-BY 4.0** (atribución incluida en `EmojiOne Attribution.txt`) |

---

## 📋 6. ESPECIFICACIÓN DE RECURSOS PENDIENTES PARA PRÓXIMAS ITERACIONES

Habiéndose completado en `083d2e0` la integración del modelo 3D riggeado y caminata del **General Sayri** (`GeneralSayri_Rigged.fbx`), quedan priorizados los siguientes recursos:

1. **Documentación de procedencia de la fauna:**
   - **Acción:** adjuntar las condiciones de exportación del puma generado en Tripo y confirmar autor, fuente y licencia de `Llama.obj`.
2. **Voces y Exclamaciones Marciales (`voz_yari_ataque_1..3.wav`, `voz_yari_dolor_1..2.wav`, `voz_amaru_fase2.wav`):**
   - **Formato requerido:** `.wav` mono a `44,100 Hz`, normalizados a `-3 dBFS`, colocados en `Assets/Resources/AyniAudio/` para ser invocados mediante `AyniAudio.Play()`.
   - **Fuentes CC0 recomendadas:** Grabación propia con el guion en Quechua de [`NARRATIVA_Y_CULTURA_ANDINA.md`](NARRATIVA_Y_CULTURA_ANDINA.md) o Freesound.org bajo *CC0 1.0 Universal*.
3. **Barreras Físicas de Cierre Temporal en Kanchas (`AyniEncounterSite`):**
   - **Diseño:** Trancas de troncos de queñual o portones de cuerda que se levantan al dispararse `OnPlayerEntered` y descienden al emitirse `OnCleared` en cada una de las 4 kanchas.

---
*Catálogo de Recursos Multimedia y Auditoría de Licencias — Proyecto Ayni: El Retorno del Guardián.*
