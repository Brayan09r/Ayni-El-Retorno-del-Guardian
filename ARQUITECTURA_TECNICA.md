# 🏗️ ARQUITECTURA TÉCNICA Y DE SOFTWARE
# 🌄 AYNI: EL RETORNO DEL GUARDIÁN (`v0.8.0`)

> **Documento de Ingeniería de Software y Arquitectura de Sistemas**  
> **Motor:** Unity 6 (`6000.3.23f1`) · **Pipeline Gráfico:** Universal Render Pipeline (URP `17.3.0`) · **Lenguajes:** C# 9.0, HLSL (ShaderLab), Python 3

---

## 📌 1. VISIÓN GENERAL DE LA ARQUITECTURA

**AYNI: El Retorno del Guardián (`v0.8.0`)** implementa una arquitectura modular desacoplada basada en **Componentes orientados a Eventos (Event-Driven Component Architecture)** y **Máquinas de Estados Finitos (FSM)** bajo el espacio de nombres raíz `Ayni.*`.

### Principios de Diseño Aplicados
1. **Auto-Ensamblaje en Tiempo de Ejecución (*Runtime Bootstrap Pattern*):** Para evitar conflictos de fusión (*merge conflicts*) sobre el archivo de la escena principal (`Assets/network of paths/Scenes/SampleScene.unity`), el componente [`SifuCombatHUD`](Assets/Scripts/UI/SifuCombatHUD.cs) actúa como inyector de dependencias en su método `Start()`, instanciando dinámicamente los directores de historia (`AyniPrologue`, `AyniBossPhaseDirector`, `AyniJudgment`, `AyniLevelOutcome`), audio ambiental (`AyniLevelAmbience`), menú de pausa (`AyniPauseMenu`), tutorial (`AyniTutorial`) y comportamientos de segunda fase de jefes (`AmaruHunter`).
2. **Desacoplamiento por Eventos (`System.Action`):** Los sistemas troncales ([`StructureSystem`](Assets/Scripts/Combat/StructureSystem.cs), [`IllaTalismanSystem`](Assets/Scripts/Core/IllaTalismanSystem.cs), [`AyniPurificationManager`](Assets/Scripts/Core/AyniPurificationManager.cs) y [`AyniEncounterSite`](Assets/Scripts/World/AyniEncounterSite.cs)) publican eventos C# puros sin depender directamente de la capa de interfaz ni de la narrativa.
3. **Generación Procedural de Activos (*Code-Driven Content*):** Tanto las animaciones de esquiva, caída y parkour ([`AyniAnimationForge`](Assets/Editor/AyniAnimationForge.cs)), como la aldea inca ([`AyniVillageBuilder`](Assets/Editor/AyniVillageBuilder.cs)), la iluminación de braseros y rayos solares ([`AyniLightingSetup`](Assets/Editor/AyniLightingSetup.cs)), el bosque de queñuales ([`AyniReforestation`](Assets/Scripts/Story/AyniReforestation.cs)) y los 42 efectos de sonido ([`generar_sfx.py`](Herramientas/Audio/generar_sfx.py)) se construyen por código reproducible.

---

## 🧩 2. ESTRUCTURA DE LA ARQUITECTURA EN CUATRO CAPAS

El sistema se organiza en cuatro niveles jerárquicos claramente diferenciados:

1. **Capa 1 — Entrada, Vibración Háptica y Estado Global (`Ayni.Core`):**
   - Captura las acciones del jugador desde Teclado/Ratón o Mando de Xbox a través de `AyniInput`, exponiendo acciones semánticas en lugar de teclas físicas.
   - Gestiona la respuesta háptica del mando mediante `AyniRumble` (XInput) y coordina el bloqueo de controles durante cinemáticas o pausas mediante `AyniGameState`.
2. **Capa 2 — Control del Jugador, Parkour y Física de Combate (`Ayni.Player` y `Ayni.Combat`):**
   - `YariCombatController` procesa la locomoción con inercia (`20 m/s²` de aceleración y `26 m/s²` de frenado), los combos de **Rumi Maki** con cancelación de recuperación, la guardia plantada y las esquivas en el sitio.
   - `YariTraversal` evalúa la geometría frontal para ejecutar el parkour contextual al estilo *Sifu* (saltar obstáculos de `0.3 a 1.3 m` apoyando la mano o trepar cornisas de `1.3 a 2.4 m`).
   - `StructureSystem` administra la barra de postura, `IllaTalismanSystem` controla el envejecimiento de 20 a 75 años, `AndeanCombatStanceModifier` aplica la guardia ritual *Takanakuy* sobre el esqueleto y `CombatFeedback` junto a `ThirdPersonSifuCamera` generan el empujón direccional de cámara con ruido Perlin y el *Hitstop*.
3. **Capa 3 — Inteligencia Artificial Enemiga, Iluminación y Mundo (`Ayni.Enemy` y `Ayni.World`):**
   - `EnemyController` ejecuta la máquina de estados de los rivales y el sistema de jefes en dos fases, complementado por `AmaruHunter` para el salto atrás y disparo de dardos envenenados.
   - `AyniEncounterSite` controla la activación de rivales en las 4 *kanchas* incas, `AyniAbyssRescue` detecta caídas mortales a la quebrada (`y < -3 m`), y los sistemas `AyniFireLight` y `AyniDynamicLighting` regulan el parpadeo de los 24 braseros ceremoniales y las 5 atmósferas lumínicas del nivel.
4. **Capa 4 — Dirección Narrativa, Audio y Presentación (`Ayni.Story` y `Ayni.UI`):**
   - `AyniPrologue` y `AyniPrologueCast` dirigen la escena inicial *"La Noche de las Cenizas"* con el General Sayri, su guardia y el incendio en el puente colgante.
   - `AyniBossPhaseDirector` dirige la cinemática entre fases del jefe, `AyniJudgment` y `AyniPurificationManager` resuelven el dilema moral a cámara lenta (`0.15×`), y `AyniLevelOutcome` junto con `AyniReforestation` y `AyniReforestationRevival` ejecutan la lluvia y la regeneración de los queñuales y los 61 árboles quemados (**ODS 15**).

---

## 📦 3. ORGANIZACIÓN DE MÓDULOS Y SCRIPTS DE RUNTIME (`Assets/Scripts/`)

El código de tiempo de ejecución consta de **33 scripts C#** distribuidos en **7 espacios de nombres**:

### 3.1. Módulo `Ayni.Core` (Sistemas Nucleares)
| Script | Responsabilidad Principal |
| :--- | :--- |
| [`AyniInput.cs`](Assets/Scripts/Core/AyniInput.cs) | Capa de abstracción de entrada que unifica Teclado/Ratón y Mando de Xbox (ejes `Ayni_*`), detecta en tiempo real el dispositivo activo (`UsingGamepad`) y permite inyección de entradas simuladas (`Simulate`, `SimulateMove`, `SimulateKeys`). |
| [`AyniRumble.cs`](Assets/Scripts/Core/AyniRumble.cs) | Sistema de vibración del mando (XInput) con perfiles diferenciados de motor grave y agudo para golpes ligeros, pesados, remates, bloqueos, desvíos, esquivas y aterrizajes pesados (`v0.8.0`). |
| [`IllaTalismanSystem.cs`](Assets/Scripts/Core/IllaTalismanSystem.cs) | Gestiona la edad de Yari (`startingAge = 20`, `maxAge = 75`), el contador de muertes (`deathCounter`) y las tres etapas vitales (`Youth`, `Prime`, `Elder`), calculando los multiplicadores de daño, velocidad y vida máxima. |
| [`AyniPurificationManager.cs`](Assets/Scripts/Core/AyniPurificationManager.cs) | Singleton que evalúa el dilema moral sobre enemigos con estructura rota (`TriggerExecutionAction`): **Venganza** (`killed: true`) o **Perdón Ayni** (`killed: false`, que invoca `DecreaseDeathCounter()` en el talismán). |
| [`AyniAudio.cs`](Assets/Scripts/Core/AyniAudio.cs) | Motor de sonido sin dependencias externas; carga bajo demanda los 42 clips `.wav` desde `Resources/AyniAudio/`, aplica variación aleatoria de tono/clip, espacialización 3D y mezcla de bucles ambientales. |
| [`AyniFootsteps.cs`](Assets/Scripts/Core/AyniFootsteps.cs) | Emite sonidos de pasos sobre piedra sincronizados con la distancia física de cada zancada recorrida en el suelo. |
| [`AyniGameState.cs`](Assets/Scripts/Core/AyniGameState.cs) | Estado global de bloqueo de entrada durante cinemáticas (`CinematicPlaying`, `InputLocked`). |
| [`AyniVersion.cs`](Assets/Scripts/Core/AyniVersion.cs) | Constante central de versionado semántico (`v0.8.0`) mostrada en el HUD y herramientas del editor. |

### 3.2. Módulo `Ayni.Player` (Control del Jugador, Parkour y Cámara)
| Script | Responsabilidad Principal |
| :--- | :--- |
| [`YariCombatController.cs`](Assets/Scripts/Player/YariCombatController.cs) | Controlador principal de Yari. Implementa locomoción de 3 marchas con inercia física (`20 m/s²` aceleración, `26 m/s²` frenado) y corrección de cadencia anti-patinaje, guardia plantada, 3 esquivas en el sitio (`Avoid_Duck`, `Avoid_Jump`, `Avoid_SwayL/R`), ventana de *Parry* (`0.22s`), contraataque (`+60%` daño de postura), combos con cancelación inmediata de recuperación al cubrirse o moverse (`v0.8.0`), selección direccional con stick y patada hacia atrás (`Atk_BackKick`). |
| [`YariTraversal.cs`](Assets/Scripts/Player/YariTraversal.cs) | Sistema de parkour contextual estilo *Sifu* (`v0.8.0`): reemplaza el salto libre con `Espacio`/`A` para saltar obstáculos bajos (`0.3–1.3 m`, estado `Traverse_Vault`), subirse a bloques anchos, trepar cornisas altas (`1.3–2.4 m`, estado `Traverse_Climb`) y realizar saltos largos sobre huecos corriendo. |
| [`ThirdPersonSifuCamera.cs`](Assets/Scripts/Player/ThirdPersonSifuCamera.cs) | Cámara cinemática en tercera persona sobre el hombro con prevención de colisiones mediante `SphereCast`, encuadre compartido en modo *Lock-On*, apertura dinámica de FOV por velocidad y empujón direccional con rebote en cada golpe (`v0.8.0`). |

### 3.3. Módulo `Ayni.Combat` (Física Marcial y Postura)
| Script | Responsabilidad Principal |
| :--- | :--- |
| [`StructureSystem.cs`](Assets/Scripts/Combat/StructureSystem.cs) | Gestiona la barra de Estructura/Postura (`100` en rivales comunes, `150` en jefes). Tras `1.2s` sin recibir golpes se recupera a `18 pts/s`. Al saturarse activa `IsBroken` durante `4.0s` (o indefinidamente si `HoldBroken = true` durante el Juicio Ayni). |
| [`AndeanCombatStanceModifier.cs`](Assets/Scripts/Combat/AndeanCombatStanceModifier.cs) | Modificador procedural en `LateUpdate` sobre el rig Humanoid que aplica la postura tradicional **Takanakuy / Rumi Maki** (descenso de cadera `-12 cm`, inclinación de torso `8°`, apertura de codos `14°`) y micro-pausas de impacto (*Hitstop* de `0.065s`). |
| [`FootIK.cs`](Assets/Scripts/Combat/FootIK.cs) | Cinemática inversa de pies para adaptar el apoyo de Yari a pendientes y peldaños de los andenes. Se inhibe automáticamente cuando el estado del Animator contiene la etiqueta `SinIK`. |
| [`AttackTimingTable.cs`](Assets/Scripts/Combat/AttackTimingTable.cs) | `ScriptableObject` (`YariAttackTimings.asset`) que almacena el segundo exacto de impacto de cada animación de combate y la velocidad base en `m/s` de cada clip de locomoción. |
| [`CombatFeedback.cs`](Assets/Scripts/Combat/CombatFeedback.cs) | Coordina el *Game Feel*: temblor suave de cámara mediante ruido Perlin (`v0.8.0`), cámara lenta breve al esquivar/romper postura y disparo de sonidos y vibración de golpe. |
| [`CombatTypes.cs`](Assets/Scripts/Combat/CombatTypes.cs) | Enumeraciones compartidas (`AttackHeight.High`, `AttackHeight.Low`) para lectura de esquivas. |
| [`RootMotionRelay.cs`](Assets/Scripts/Combat/RootMotionRelay.cs) | Reenvía el desplazamiento `OnAnimatorMove` del sub-objeto del modelo 3D hacia el controlador raíz. |

### 3.4. Módulo `Ayni.Enemy` (Inteligencia Artificial y Jefes)
| Script | Responsabilidad Principal |
| :--- | :--- |
| [`EnemyController.cs`](Assets/Scripts/Enemy/EnemyController.cs) | Máquina de estados de IA (`Idle`, `Chase`, `Windup`, `Recover`, `Stagger`, `Stunned`, `Downed`, `Dead`), telégrafos cromáticos (rojo = golpe alto, amarillo = barrido bajo), sistema de jefes multifase (`bossPhaseHealth = { 380f, 460f }`) con rutina de transición invulnerable (`PhaseTransitionRoutine`) y apertura del Juicio Ayni en la fase final (`≤ 40%` HP). |
| [`AmaruHunter.cs`](Assets/Scripts/Enemy/AmaruHunter.cs) | Comportamiento especializado de la Fase 2 de **Amaru el Cazador**: salto acrobático de `5.5 m` hacia atrás y disparo de ráfagas de 3 dardos envenenados con aviso verde (`0.5s`), esquivables, bloqueables o desviables con *Parry*. |

### 3.5. Módulo `Ayni.Story` (Dirección Cinemática y Reforestación)
| Script | Responsabilidad Principal |
| :--- | :--- |
| [`AyniPrologue.cs`](Assets/Scripts/Story/AyniPrologue.cs) | Secuencia cinemática-jugable *"La Noche de las Cenizas"* en el puente colgante (~43 s, omitible con `Tab`/`Esc`/`Menu`), con paneles visuales para el pacto de la Illa y el despertar al inicio del camino (`v0.8.0`). |
| [`AyniPrologueCast.cs`](Assets/Scripts/Story/AyniPrologueCast.cs) | Puesta en escena del prólogo en el puente colgante: instancia y dirige al General Sayri, su guardia y los efectos de incendio durante la emboscada inicial (`v0.8.0`). |
| [`AyniBossPhaseDirector.cs`](Assets/Scripts/Story/AyniBossPhaseDirector.cs) | Dirige la cinemática de transición entre la Fase 1 y Fase 2 de Amaru: caída en cámara lenta, órbita de cámara de 360°, rugido `Gen_Roar`, título *"SEGUNDA FASE · Desatado"* y generación del aura de brasas (`AyniEnrageAura`). |
| [`AyniJudgment.cs`](Assets/Scripts/Story/AyniJudgment.cs) | Ralentiza el tiempo a `0.15x` cuando la postura del jefe se rompe en su última fase y presenta la interfaz de decisión moral (**Venganza** vs. **Ayni**). |
| [`AyniLevelOutcome.cs`](Assets/Scripts/Story/AyniLevelOutcome.cs) | Ejecuta la cinemática de cierre del Nivel 1 según la decisión tomada, presenta el mensaje del **ODS 15** y despliega la tarjeta de estadísticas finales. |
| [`AyniReforestation.cs`](Assets/Scripts/Story/AyniReforestation.cs) | Genera proceduralmente en tiempo de ejecución mallas y texturas de un bosque de **queñuales (`Polylepis`)**, matas de **ichu** y flores de **cantuta** que emergen en ola concéntrica al perdonar a Amaru. |
| [`AyniReforestationRevival.cs`](Assets/Scripts/Story/AyniReforestationRevival.cs) | Extiende la ola de vida a lo largo de los 630 m del Qhapaq Ñan para hacer reverdecer los 56 árboles quemados en pie y 5 caídos. |
| [`AyniLevelAmbience.cs`](Assets/Scripts/Story/AyniLevelAmbience.cs) | Controla el sonido ambiental de viento andino y activa tambores rituales al entrar en las kanchas con rivales. |

### 3.6. Módulos `Ayni.World` y `Ayni.UI`
| Script | Responsabilidad Principal |
| :--- | :--- |
| [`AyniEncounterSite.cs`](Assets/Scripts/World/AyniEncounterSite.cs) | Gestiona cada una de las 4 kanchas incas (`K1` a `K4`), manteniendo ocultos a sus rivales hasta que Yari cruza el umbral (`triggerRadius`) y emitiendo `OnCleared` al derrotarlos. |
| [`AyniDynamicLighting.cs`](Assets/Scripts/World/AyniDynamicLighting.cs) | Controlador de iluminación dinámica reactiva que interpola sol, cielo, niebla e intensidad de braseros a través de 5 estados: *Hora Dorada*, *Combate en Kancha*, *Segunda Fase del Jefe*, *Amanecer Sagrado (Ayni)* y *Ocaso de Cenizas (Venganza)*. |
| [`AyniFireLight.cs`](Assets/Scripts/World/AyniFireLight.cs) | Simula el parpadeo orgánico de fuego y brasas mediante doble octava de ruido Perlin y *Distance Culling* para respetar el límite de luces por objeto en URP. |
| [`AyniAbyssRescue.cs`](Assets/Scripts/World/AyniAbyssRescue.cs) | Registra la última posición segura de Yari en el camino (`y >= 0.5m`, pendiente `< 30°`). Si cae a la garganta (`y < -3m`), dispara `FallToDeath` y lo resucita mediante el Talismán Illa. |
| [`AyniBurntTree.cs`](Assets/Scripts/World/AyniBurntTree.cs) | Almacena la geometría y puntos de rebrote de cada árbol quemado del camino para su restauración en el final Ayni. |
| [`SifuCombatHUD.cs`](Assets/Scripts/UI/SifuCombatHUD.cs) | Dibuja el HUD táctico (barras de vida/postura, edad, avisos contextuales de parkour `[A] Saltar / Trepar` y panel de controles ocultable con `H`) e inicializa los sistemas del nivel. |
| [`AyniPauseMenu.cs`](Assets/Scripts/UI/AyniPauseMenu.cs) | Menú de pausa (`Esc` / `Menu`) con control persistente de volúmenes (`PlayerPrefs`), consulta del tutorial y reinicio del nivel. |
| [`AyniTutorial.cs`](Assets/Scripts/UI/AyniTutorial.cs) | Tutorial paginado adaptable a Teclado/Ratón o Mando de Xbox que se despliega al iniciar el primer combate del nivel (`v0.8.0`). |
| [`AyniScreenFX.cs`](Assets/Scripts/UI/AyniScreenFX.cs) | Fundidos, franjas cinematográficas (*letterbox*), subtítulos, carteles de título y viñeta verde de envenenamiento. |
| [`AyniGamepadTester.cs`](Assets/Scripts/UI/AyniGamepadTester.cs) | Depurador superpuesto en vivo (`F9`) para inspeccionar ejes y botones del mando conectado. |

---

## 🔗 4. RELACIÓN ENTRE CLASES, ESTADOS DE IA Y FLUJO DE COMBATE

### 4.1. Relaciones entre las Clases Principales del Dominio
- **Composición en el Jugador:** `YariCombatController` posee por composición directa una instancia de `StructureSystem` (su barra de postura), una instancia de `IllaTalismanSystem` (su edad y resurrección) y se coordina con `YariTraversal` para las acciones de parkour sobre obstáculos.
- **Composición y Extensión en los Enemigos:** Cada `EnemyController` posee su propio `StructureSystem`. En el caso de los jefes, la clase `AmaruHunter` extiende el comportamiento de `EnemyController` durante la segunda fase para añadir el salto evasivo hacia atrás y la ráfaga de tres dardos envenenados.
- **Mediación del Juicio Ayni:** `AyniPurificationManager` actúa como mediador entre el jugador, el jefe aturdido (`CanBeJudged == true`), el talismán (`DecreaseDeathCounter()`) y los sistemas del mundo (`AyniReforestation` y `AyniDynamicLighting`).

### 4.2. Descripción de la Máquina de Estados de la IA Enemiga
Los rivales y jefes transitan por ocho estados controlados en `EnemyController`:
1. **`Idle` (Reposo):** El rival aguarda en su posición dentro de la kancha hasta que Yari entra en el rango de detección (`aggroRange = 18 m`).
2. **`Chase` (Persecución):** El enemigo se desplaza hacia Yari (`3.2 m/s` base) y lo encara hasta situarse a distancia de golpe (`attackRange = 2.2 m`).
3. **`Windup` (Anticipación con Telégrafo):** Antes de descargar el golpe (`0.6 s`), el rival emite un destello **rojo** si prepara un ataque alto (70%) o **amarillo** si prepara un barrido bajo (30%), dando margen de lectura para esquivar o hacer *Parry*.
4. **`Recover` (Recuperación):** Pausa breve de `0.7 s` tras lanzar un golpe (se amplía `+0.4 s` si Yari esquiva el ataque con éxito).
5. **`Stagger` (Descolocado):** Interrupción de `0.3 s` al recibir un impacto ligero, o de `1.0 s` cuando Yari le aplica un Desvío Perfecto (*Parry*).
6. **`Downed` (Derribado):** Caída al suelo durante `0.6 s` tras recibir un golpe pesado contundente o durante la transición de fase de un jefe (`PhaseTransitionRoutine`).
7. **`Stunned` (Postura Rota):** Se activa cuando `StructureSystem.IsBroken` es verdadero (`4.0 s` en rivales comunes, o retenido indefinidamente con `HoldBroken = true` cuando un jefe está en su fase final con `≤ 40%` de vida).
8. **`Dead` (Resuelto):** Estado final alcanzado tras derrotar a un rival común o resolver el **Juicio Ayni** del jefe (por Venganza o por Perdón).

---

## 🦿 5. SUBSISTEMA DE ANIMACIÓN PROCEDURAL Y CALIBRACIÓN ANTI-PATINAJE

1. **Horneado de Tiempos y Velocidades ([`AyniAttackTimingBaker.cs`](Assets/Editor/AyniAttackTimingBaker.cs)):**
   - Al construir el `AnimatorController`, el editor simula cada clip de locomoción sobre el esqueleto de Yari, mide cuántos metros por segundo cubre cada zancada y serializa los datos en `Assets/Resources/YariAttackTimings.asset`.
   - En tiempo de ejecución, [`YariCombatController`](Assets/Scripts/Player/YariCombatController.cs) ajusta `animator.speed = velocidadReal / velocidadBaseDelClip` dentro de topes seguros (`maxRunCadence = 1.05x`, `maxSprintCadence = 1.3x`), reduciendo el patinaje de pies de ~94% a un rango imperceptible de **1% a 9%**.
2. **La Fragua de Animaciones ([`AyniAnimationForge.cs`](Assets/Editor/AyniAnimationForge.cs)):**
   - Sintetiza **11 clips `.anim`** en `Assets/Art/Characters/Animations/Generadas/` (`Yari_Avoid_Duck`, `Yari_Avoid_Jump`, `Yari_Avoid_SwayL/R`, `Yari_Back_Kick`, `Yari_Vault`, `Yari_Climb`, `Yari_Fall_Loop`, `Yari_Fall_Back`, `Yari_Land_Hard`, `Gen_Roar`) manipulando curvas de músculos del `HumanPoseHandler`.
3. **Aislamiento con Etiqueta `SinIK`:**
   - Como los clips generados por código no incluyen pistas de metas de IK de pies (*Foot IK goals*), [`AyniAnimatorUpgrade`](Assets/Editor/AyniAnimatorUpgrade.cs) etiqueta todos esos estados (incluidos `Traverse_Vault` y `Traverse_Climb`) con el tag `SinIK`, de modo que tanto `FootIK` como `AndeanCombatStanceModifier` ceden el control limpiamente durante esquivas, caídas, patadas traseras y movimientos de parkour.

---

## 🎨 6. PIPELINE DE 6 SHADERS URP A MEDIDA (`Assets/Shaders/`)

| Shader | Técnica Gráfica Implementada |
| :--- | :--- |
| [`AyniTerrenoAndino.shader`](Assets/Shaders/AyniTerrenoAndino.shader) | Mapeo **Triplanar** sin estiramiento en los acantilados verticales de la garganta (`y = -18m`), dibujo procedural de losas del *Qhapaq Ñan* y mampostería en los taludes de los andenes agrícolas mediante máscara de control (`Ayni_Control.png`). |
| [`AyniMuroInca.shader`](Assets/Shaders/AyniMuroInca.shader) | Genera mampostería incaica 100% en el *Fragment Shader* usando coordenadas UV en metros reales: modo **0 = Pirca rústica**, **1 = Sillería imperial** y **2 = Piedra monolítica**. |
| [`AyniRayosSol.shader`](Assets/Shaders/AyniRayosSol.shader) | Haces de luz solar volumétricos (*God Rays*) para URP con fundido suave por profundidad (`SampleSceneDepth`) y ángulo de visión sobre el puente colgante, cañones y portadas incas. |
| [`AyniAguaQuebrada.shader`](Assets/Shaders/AyniAguaQuebrada.shader) | Superficie de río de montaña con desplazamiento de ondas normales, espuma en las orillas y reflejo cálido de Hora Dorada. |
| [`AyniNiebla.shader`](Assets/Shaders/AyniNiebla.shader) | Planos de niebla volumétrica suave para el fondo del cañón con desvanecimiento por profundidad. |
| [`AyniMontanasLejanas.shader`](Assets/Shaders/AyniMontanasLejanas.shader) | Siluetas perimetrales de la cordillera de los Andes con nieve en las cumbres según altitud y perspectiva atmosférica. |

---

## 🛠️ 7. AUTOMATIZACIÓN DE EDITOR Y PUENTE DE AGENTES (`Assets/Editor/`)

El directorio `Assets/Editor/` contiene **29 herramientas de automatización** que permiten reconstruir el nivel, configurar la iluminación, auditar el rendimiento y ejecutar pruebas automatizadas:

- **Construcción del Mundo e Iluminación:** [`AyniEnvironmentBuilder.cs`](Assets/Editor/AyniEnvironmentBuilder.cs), [`AyniAtmosphere.cs`](Assets/Editor/AyniAtmosphere.cs), [`AyniLightingSetup.cs`](Assets/Editor/AyniLightingSetup.cs), [`AyniVillageBuilder.cs`](Assets/Editor/AyniVillageBuilder.cs), [`AyniVillageGeometry.cs`](Assets/Editor/AyniVillageGeometry.cs), [`AyniVillageObstacles.cs`](Assets/Editor/AyniVillageObstacles.cs) y [`AyniRivalSetup.cs`](Assets/Editor/AyniRivalSetup.cs).
- **Puente Asíncrono para Agentes ([`AyniAgentBridge.cs`](Assets/Editor/AyniAgentBridge.cs)):** Escucha comandos en `UserSettings/AyniAgent/cmd.txt` y descarta automáticamente la cola de Play si detecta errores de compilación para evitar bloqueos (`v0.8.0`).
- **Sondas de Verificación ([`AyniPlaytestProbe.cs`](Assets/Editor/AyniPlaytestProbe.cs) y [`AyniLocomotionProbe.cs`](Assets/Editor/AyniLocomotionProbe.cs)):** Incluyen `MeasureSlide` (patinaje de pies), `TraceSpeed` (registro de velocidad e inercia cada `0.05 s`), `Traversal` (auditoría de qué obstáculos puede saltar o trepar Yari), `GoToEncounter` (teletransporte a kanchas con rivales) y simulación completa de combates contra jefes.
- **Producción y Despliegue:** [`AyniTrailerRunner.cs`](Assets/Editor/AyniTrailerRunner.cs) (grabación automatizada en 1080p a 30 fps) y [`AyniWebBuild.cs`](Assets/Editor/AyniWebBuild.cs) (compilación optimizada WebGL para **Unity Play**).

---
*Documento de Arquitectura de Software — Proyecto Ayni: El Retorno del Guardián (`v0.8.0`).*
