# 📘 DOCUMENTACIÓN INTEGRAL DEL PROYECTO DE VIDEOJUEGO
# 🌄 AYNI: EL RETORNO DEL GUARDIÁN (`v0.8.0`)

> **Curso:** Desarrollo de Videojuegos — Ingeniería de Sistemas  
> **Motor de Desarrollo:** Unity 6 (`6000.3.23f1`) · **Pipeline Gráfico:** Universal Render Pipeline (URP `17.3.0`)  
> **Repositorio Oficial:** [https://github.com/Brayan09r/Ayni-El-Retorno-del-Guardian](https://github.com/Brayan09r/Ayni-El-Retorno-del-Guardian)  
> **Versión Web Jugable (Unity Play):** [https://play.unity.com/en/games/66b8abca-71ca-4703-8580-d51948d2be29/ayninivel1](https://play.unity.com/en/games/66b8abca-71ca-4703-8580-d51948d2be29/ayninivel1)

---

## 📑 ÍNDICE GENERAL DE LA DOCUMENTACIÓN

1. [Resumen Ejecutivo y Ficha Técnica del Proyecto](#1-resumen-ejecutivo-y-ficha-técnica-del-proyecto)
2. [Historia, Narrativa y Fundamentación Cultural Andina](#2-historia-narrativa-y-fundamentación-cultural-andina)
3. [Diseño de Juego (GDD), Parkour y Mecánicas de Combate](#3-diseño-de-juego-gdd-parkour-y-mecánicas-de-combate)
4. [Arquitectura Técnica y de Software](#4-arquitectura-técnica-y-de-software)
5. [Diseño del Nivel 1, Aldea Inca, Iluminación, Fauna y Niveles 2–5](#5-diseño-del-nivel-1-aldea-inca-iluminación-fauna-y-niveles-25)
6. [Plan de Pruebas de Calidad (QA), Métricas y Catálogo de Recursos](#6-plan-de-pruebas-de-calidad-qa-métricas-y-catálogo-de-recursos)
7. [Conclusiones del Proyecto](#7-conclusiones-del-proyecto)

> **Documentos Especializados por Módulo en el Repositorio:**
> - 🏗️ [Arquitectura Técnica y Módulos del Sistema (`ARQUITECTURA_TECNICA.md`)](ARQUITECTURA_TECNICA.md)
> - 🧪 [Plan de Pruebas QA y Matrices de Balanceo (`PLAN_DE_PRUEBAS_QA.md`)](PLAN_DE_PRUEBAS_QA.md)
> - 🏔️ [Narrativa, Glosario Quechua y Enfoque ODS (`NARRATIVA_Y_CULTURA_ANDINA.md`)](NARRATIVA_Y_CULTURA_ANDINA.md)
> - 🗺️ [Diseño de Niveles 2 al 5 (`DISENO_NIVELES_2_AL_5.md`)](DISENO_NIVELES_2_AL_5.md) y [Prompts 3D de Jefes (`ConceptArt/Jefes_y_Prologo/prompts.md`)](ConceptArt/Jefes_y_Prologo/prompts.md)
> - 📦 [Catálogo de Recursos Multimedia y Licencias (`CATALOGO_RECURSOS_Y_LICENCIAS.md`)](CATALOGO_RECURSOS_Y_LICENCIAS.md)
> - 🔥 [Iluminación Andina, Braseros y Rayos de Sol (`ILUMINACION_ANDINA.md`)](ILUMINACION_ANDINA.md)

---

## 1. RESUMEN EJECUTIVO Y FICHA TÉCNICA DEL PROYECTO

**AYNI: El Retorno del Guardián** es un videojuego tridimensional de acción táctica y artes marciales en tercera persona (género *Beat 'em up* táctico), inspirado en referentes contemporáneos como *Sifu* y *Sekiro: Shadows Die Twice*, ambientado en un universo andino mítico durante la época del Tawantinsuyu. El proyecto integra mecánicas avanzadas de combate cuerpo a cuerpo basadas en el arte marcial ancestral del **Rumi Maki** (*Puño de Piedra* / *Takanakuy*), desplazamiento de parkour contextual, iluminación dinámica reactiva y una reflexión ética y ecológica fundamentada en el principio de reciprocidad andina (**Ayni**) y los **Objetivos de Desarrollo Sostenible (ODS)**.

| Parámetro Técnico | Especificación del Proyecto |
| :--- | :--- |
| **Título Oficial** | AYNI: El Retorno del Guardián |
| **Versión Actual** | `v0.8.0` (Game feel, movimiento con inercia, parkour estilo Sifu, prólogo escénico, iluminación andina y fauna) |
| **Género y Perspectiva** | 3D Martial Arts Beat 'em up / Acción Táctica en Tercera Persona sobre el hombro (*Over-The-Shoulder*) |
| **Motor Gráfico y Pipeline** | Unity 6 (`6000.3.23f1`) con Universal Render Pipeline (`URP 17.3.0`) |
| **Lenguajes de Programación** | C# (`87.7%` — Lógica y Editor), Python 3 (`4.8%` — Síntesis de audio y video), ShaderLab/HLSL (`3.3%` — Shaders URP) |
| **Plataformas Soportadas** | PC (Windows / Linux / macOS) y Navegador Web (WebGL publicado en Unity Play) |
| **Dispositivos de Entrada** | Teclado y Ratón / Mando de Xbox con soporte de vibración háptica XInput (`AyniInput` y `AyniRumble`) |

---

## 2. HISTORIA, NARRATIVA Y FUNDAMENTACIÓN CULTURAL ANDINA

### 2.1. Sinopsis Argumental
En las altas cumbres del Tawantinsuyu, la orden de los Guardianes del Sol preservaba la armonía entre las comunidades humanas, los *Apus* (espíritus tutelares de las montañas) y la *Pachamama* (Madre Tierra) bajo la ley sagrada del **Ayni** (*"Hoy por ti, mañana por mí"*). Sin embargo, la ambición corrompió al **General Sayri**, quien junto a cuatro lugartenientes renegados traicionó a la orden durante la llamada **"Noche de las Cenizas"**, usurpando los recursos vitales del imperio: la fauna y los bosques del Antisuyo, los metales de las canteras, el agua de los acueductos y el oro ritual.

Durante la secuencia inicial en el puente colgante sobre la garganta sagrada, el joven guardián **Yari** (de 20 años de edad) es emboscado en medio del incendio por el General Sayri y su guardia (`AyniPrologue` y `AyniPrologueCast`), siendo arrojado al abismo. Al borde de cruzar al *Uku Pacha* (el mundo subterráneo), el espíritu ancestral despierta en su pecho el **Talismán Sagrado "Illa"**, una reliquia tallada en piedra y oro solar que sella un pacto visual y le impide morir en combate: cada vez que Yari cae abatido, el talismán lo resucita en el acto, pero cobra como tributo años de su propia vida. Armado con el arte marcial del **Rumi Maki**, Yari despierta al inicio del **Qhapaq Ñan** (Gran Camino Inca) para enfrentar a los usurpadores y decidir, mediante el **Juicio Ayni**, si sucumbir a la venganza o perdonar para restaurar la naturaleza.

### 2.2. Glosario Cultural y Terminología Quechua Aplicada
| Término Andino | Significado Histórico y Cultural | Implementación en el Videojuego |
| :--- | :--- | :--- |
| **Ayni** | Principio incaico de reciprocidad simétrica y ayuda mutua entre los miembros de una comunidad y con la naturaleza. | Mecánica de resolución moral (Perdón vs. Venganza) que purifica el contador de muertes del jugador y reforesta el ecosistema del nivel. |
| **Illa** | Amuleto sagrado de piedra o metal que condensa la energía vital (*Kamaq*) y otorga protección y fertilidad. | Sistema de resurrección y envejecimiento progresivo (de 20 a 75 años) controlado por la clase `IllaTalismanSystem`. |
| **Rumi Maki / Takanakuy** | Arte marcial tradicional andino (*"Puño de Piedra"*) y combate ritual de resolución comunitaria practicado en los Andes. | Sistema de combate cuerpo a cuerpo de Yari y modificador esquelético procedural (`AndeanCombatStanceModifier`). |
| **Qhapaq Ñan** | Red vial incaica que articulaba longitudinalmente el Tawantinsuyu a través de cordilleras, quebradas y puentes. | Ruta principal de 630 metros del Nivel 1 excavada sobre el terreno con losas de piedra, andenes, puente colgante y obstáculos. |
| **Kancha y Tambo** | Recinto arquitectónico rectangular amurallado con viviendas alrededor de un patio central; posada de abastecimiento en el camino. | Cuatro arenas de combate (`AyniEncounterSite`) generadas proceduralmente con muros trapezoidales, fogones y techos de ichu. |
| **Chasqui** | Mensajero corredor de relevos del imperio incaico, entrenado para desplazarse a gran velocidad por las alturas. | Primer arquetipo de rival (*Rastreador*) ubicado en el Puesto de Chasquis (Kancha K1), caracterizado por su agilidad. |
| **Queñual (*Polylepis*) y Cantuta** | Árbol nativo altoandino que regula las cabeceras de cuenca y flor sagrada de los incas. | Especies botánicas que brotan proceduralmente en tiempo real (`AyniReforestation`) cuando el jugador elige el perdón. |
| **Llamas y Pumas** | Camélido doméstico de carga de los Andes y felino sagrado símbolo de fuerza y guardián del *Kay Pacha* (mundo terrenal). | Fauna andina integrada en los campos abiertos cercanos a la aldea (llamas) y en los bordes de montaña para delimitar el camino (pumas). |

### 2.3. Alineación con los Objetivos de Desarrollo Sostenible (Agenda 2030)
El funcionamiento del flujo ambiental y ético del juego vincula directamente la decisión del jugador al final de cada misión con un Objetivo de Desarrollo Sostenible:
- **Funcionamiento de la Decisión Moral:** Al derrotar al jefe de un nivel y quebrar su estructura, se presenta la disyuntiva del **Juicio Ayni**. Si se opta por la **Venganza (Ejecución)**, el entorno sufre una degradación climática y visual (*Ocaso de Cenizas*). Si se opta por el **Camino del Ayni (Perdón)**, se activa la restauración ecológica del bioma (*Amanecer Sagrado*).
- **ODS 15 (Vida de Ecosistemas Terrestres — Nivel 1):** Implementado íntegramente contra **Amaru el Cazador**, responsable de la caza furtiva y la quema de los bosques del Antisuyo. Al perdonarlo, cae una lluvia purificadora, la iluminación cambia al estado de *Amanecer Sagrado* y una ola de regeneración ecológica recorre los 630 metros del camino, haciendo brotar un bosque de queñuales, matas de ichu y flores de cantuta, además de reverdecer los 61 árboles quemados del valle.
- **ODS 8 y ODS 12 (Trabajo Decente y Producción Responsable — Niveles 2 y 4):** Proyectados para las *Canteras de Bronce* (contra Túpac) y el *Palacio Alquímico* (contra Mama Churi), enfocados en erradicar la explotación minera y la contaminación de hornos.
- **ODS 6 (Agua Limpia y Saneamiento — Nivel 3):** Proyectado para las *Terrazas y Acueductos* (contra Apo Rumi el Acaparador), centrado en restituir el caudal de los canales de riego hacia los andenes agrícolas.
- **ODS 16 (Paz, Justicia e Instituciones Sólidas — Nivel 5):** Representado transversalmente mediante `AyniPurificationManager` y el duelo final contra el General Sayri, donde la justicia restaurativa reemplaza al ciclo de venganza.

---

## 3. DISEÑO DE JUEGO (GDD), PARKOUR Y MECÁNICAS DE COMBATE

### 3.1. Flujo del Bucle Central de Jugabilidad y Combate
El ciclo de juego se estructura en tres fases continuas que el jugador experimenta en cada tramo del camino:
1. **Fase de Travesía y Parkour Contextual:** Yari recorre el Qhapaq Ñan sorteando obstáculos mediante el sistema `YariTraversal` (saltando troncos caídos y muros bajos con apoyo de mano, o trepando cornisas de piedra) hasta ingresar a una de las cuatro *Kanchas* incas.
2. **Fase de Intercambio Marcial y Postura:** Dentro de la arena, los ataques ligeros y pesados del estilo **Rumi Maki**, junto con los desvíos perfectos (*Parry* en ventana de `0.22 s`) y los contraataques tras esquiva, incrementan la barra de Estructura del oponente. Si Yari cae derrotado o se precipita a la garganta profunda, el **Talismán Illa** lo resucita de inmediato incrementando su edad y modificando sus atributos físicos (más daño espiritual a cambio de menor salud máxima).
3. **Fase de Ruptura y Resolución Ayni:** Cuando la barra de estructura del enemigo llega a su límite (`100` puntos en rivales comunes o `150` puntos en jefes), el rival queda aturdido (`Stunned`). El jugador puede entonces ejecutar un remate letal (`F` / `B`) o aplicar el desarme compasivo del **Ayni** (`X` / `A`), el cual reduce en 1 el contador de muertes del talismán y, en el caso del jefe, desencadena la reforestación del nivel.

### 3.2. Sistema de Estructura / Postura y Cancelación de Golpes (`v0.8.0`)
- **Capacidad y Recuperación:** Los rivales comunes poseen **100 puntos** de estructura y los jefes **150 puntos**. Tras **1.2 segundos** sin recibir impactos, la postura se regenera a **18 puntos por segundo**.
- **Desvío Perfecto (*Parry*):** Activar la guardia dentro de los **0.22 segundos** previos al impacto neutraliza por completo el daño, inflige **35 puntos de daño de postura** al agresor y lo deja expuesto durante **1.0 segundo**.
- **Cancelación de Recuperación de Golpes (*Animation Cancel*):** Desde la versión `v0.8.0`, en cuanto un golpe de Yari conecta y completa su recorrido activo (*follow-through*), subir la guardia, esquivar o inclinar el stick de movimiento corta de inmediato la animación sobrante de recoger el brazo (eliminando la espera pasiva previa de `0.46 s` en golpes ligeros y `0.66 s` en pesados). Si el jugador tiene otro ataque en cola, el combo continúa con prioridad.

### 3.3. Defensa Táctica y Esquivas en el Sitio (Estilo *Sifu*)
Al mantener el botón de guardia, Yari planta los pies en el suelo sin caminar hacia atrás. Al combinar la guardia con una dirección se ejecutan tres esquivas en el sitio (*Avoid*):
1. **Agacharse (*Avoid Duck* — Guardia + Abajo):** Ventana de invulnerabilidad de **0.45 s** que evade ataques altos (puñetazos, codazos y ganchos).
2. **Saltito Evasivo (*Avoid Jump* — Guardia + Arriba / Espacio):** Ventana de **0.45 s** que esquiva barridos bajos (aviso amarillo).
3. **Balanceo Lateral (*Avoid Sway Left/Right* — Guardia + Izquierda/Derecha):** Ventana de **0.36 s** que desplaza el torso y la cabeza 12 cm fuera de la línea de impacto, evitando ataques altos, bajos y dardos envenenados.

Toda esquiva o desvío exitoso otorga una **ventana de contraataque de 0.8 segundos** donde el siguiente golpe aplica **×1.6 (+60%) de daño a la postura** y extiende la recuperación del rival en **0.4 s**.

### 3.4. Movimiento con Inercia y Parkour Contextual (`YariTraversal` — `v0.8.0`)
- **Inercia Física de Locomoción:** Yari acelera de forma progresiva a **20 m/s²** (alcanzando carrera en `~0.25 s`) y frena a **26 m/s²** (`~0.17 s`). Al realizar un cambio brusco de sentido (media vuelta), primero frena mientras gira el torso y luego arranca, evitando cualquier deslizamiento de espaldas.
- **Parkour Contextual (Sin Salto Libre):** Al igual que en *Sifu*, el botón `Espacio` / `A` analiza el obstáculo situado frente a Yari (recordando la pulsación durante `0.25 s`):
  - **Saltar Obstáculo (`Traverse_Vault`, de `0.3 a 1.3 m`):** Frente a troncos quemados o muros caídos, Yari apoya una mano y pasa las piernas recogidas por encima (`Yari_Vault`), manteniendo el impulso de carrera al aterrizar.
  - **Subirse a Plataforma:** Si el obstáculo es bajo pero ancho (sin suelo inmediato al otro lado), Yari se encarama sobre él.
  - **Trepar Cornisa (`Traverse_Climb`, de `1.3 a 2.4 m`):** Yari se aferra al borde superior, tracciona con los brazos, apoya la rodilla y se incorpora arriba (`Yari_Climb`).
  - **Salto Largo sobre Huecos:** Se activa únicamente al correr hacia un borde con vacío y suelo alcanzable al otro lado (hasta `3.6 m`), saliendo impulsado un `30%` más rápido. Mientras salta o trepa un obstáculo, Yari es invulnerable a los golpes.

### 3.5. Mecánica del Talismán Sagrado "Illa" (Envejecimiento Kármico)
| Etapa Vital (`AgeStage`) | Rango de Edad | Multiplicador de Daño | Multiplicador de Velocidad | Multiplicador de Salud Máxima |
| :--- | :---: | :---: | :---: | :---: |
| **Juventud (`Youth`)** | 20 – 35 años | `1.00×` (100%) | `1.15×` (+15% agilidad) | `1.00×` (100 HP) |
| **Madurez (`Prime`)** | 36 – 55 años | `1.25×` (+25%) | `1.00×` (Velocidad base) | `0.85×` (85 HP) |
| **Maestro Anciano (`Elder`)** | 56 – 75 años | `1.60×` (+60% daño espiritual) | `0.85×` (-15% velocidad) | `0.60×` (60 HP) |

### 3.6. Esquema Unificado de Controles (`v0.8.0`)
| Acción del Jugador | Teclado y Ratón (PC) | Mando de Xbox | Descripción Técnica del Comportamiento |
| :--- | :--- | :--- | :--- |
| **Moverse (Caminar / Correr)** | `W` `A` `S` `D` | Stick Izquierdo (3 tramos) | Desplazamiento con inercia (`20 m/s²`) y ajuste automático de cadencia de pasos. |
| **Correr Rápido / Sprint** | Mantener `Shift Izq.` | Mantener `RT` o `L3` | Aumenta la velocidad hasta 4.9 m/s base (5.6 m/s joven) y abre el FOV de cámara. |
| **Saltar Obstáculo / Trepar** | `Espacio` | Botón `A` | Parkour contextual (`YariTraversal`): salta troncos/muros (`0.3–1.3 m`), trepa cornisas (`1.3–2.4 m`) o salta huecos corriendo. |
| **Agacharse / Sigilo** | `C` o `Ctrl Izq.` | Botón `B` | Reduce la cápsula de colisión de 2.0 m a 1.15 m de altura. |
| **Guardia y Desvío (*Parry*)** | `Clic Derecho` o `G` | Mantener `LB` | Planta a Yari en el sitio, corta la recuperación de golpes previos y ejecuta Parry en ventana de `0.22 s`. |
| **Esquivas en el Sitio (*Avoid*)** | Guardia + `S` / `W` / `A`·`D` | Guardia + Stick Izquierdo | Agacharse (golpes altos), saltito (barridos bajos) o balanceo lateral (ambos). |
| **Ataque Ligero Rumi Maki** | `Clic Izquierdo` | Botón `X` | Combo de hasta 4 golpes rápidos (18 pts de daño base) o patada hacia atrás si el rival está a la espalda. |
| **Ataque Pesado Rumi Maki** | `Q` o `E` | Botón `Y` | Golpes contundentes (codazo, uppercut, cabezazo, patada frontal, empujón — 35 pts de daño base). |
| **Fijar Blanco (*Lock-On*)** | `Tab` o `Clic Central` | Botón `RB` o `R3` | Fija la cámara y la locomoción lateral (*Strafe*) sobre el rival más cercano (hasta 14 m). |
| **Juicio: Venganza / Ayni** | `F` (Venganza) / `X` (Ayni) | Botón `B` / Botón `A` | Ejecución letal o desarme compasivo sobre rivales con la postura rota. |
| **Mostrar/Ocultar Ayuda HUD** | `H` | Cruceta Arriba (`D-Pad Up`) | Muestra u oculta el panel de controles en pantalla (inicia oculto por defecto). |
| **Menú de Pausa y Opciones** | `Escape` | Botón `Menu` (Start) | Pausa la partida, ajusta volúmenes persistentes, muestra el tutorial o reinicia el nivel. |

---

## 4. ARQUITECTURA TÉCNICA Y DE SOFTWARE

### 4.1. Organización en Cuatro Capas y Patrones de Ingeniería
La arquitectura de software de **AYNI: El Retorno del Guardián** se divide en cuatro capas funcionales bajo el espacio de nombres `Ayni.*`, diseñadas para mantener cada responsabilidad aislada y evitar conflictos de trabajo en equipo:

1. **Capa de Entrada, Vibración y Estado Global (`Ayni.Core`):**
   - Centraliza la lectura de periféricos en `AyniInput`, traduciendo pulsaciones de teclado, ratón y mando de Xbox a acciones lógicas (*LightAttack*, *HeavyAttack*, *Guard*, *Jump*, *Mercy*, *Execute*).
   - Incluye el sistema de vibración háptica XInput (`AyniRumble`), que modula los motores grave y agudo del mando en golpes, bloqueos, desvíos y aterrizajes, desactivándose automáticamente en pausa o cinemáticas.
2. **Capa de Jugador, Parkour y Combate (`Ayni.Player` y `Ayni.Combat`):**
   - `YariCombatController` gestiona la locomoción con inercia, los combos, la guardia plantada y las esquivas, delegando en `YariTraversal` la detección geométrica de obstáculos para saltar o trepar cornisas.
   - `StructureSystem` administra la barra de postura, `IllaTalismanSystem` controla la edad y multiplicadores de Yari, `AndeanCombatStanceModifier` aplica la postura ritual *Takanakuy* sobre el esqueleto y `CombatFeedback` junto a `ThirdPersonSifuCamera` aplican el empujón direccional de cámara con ruido Perlin y el *Hitstop*.
3. **Capa de Inteligencia Artificial, Iluminación y Mundo (`Ayni.Enemy` y `Ayni.World`):**
   - `EnemyController` gobierna los 8 estados de comportamiento de los rivales (**Idle**, **Chase**, **Windup**, **Recover**, **Stagger**, **Stunned**, **Downed** y **Dead**) y el sistema de jefes en dos fases (`bossPhaseHealth`), apoyado por `AmaruHunter` para el salto atrás y disparo de dardos envenenados en la segunda fase.
   - `AyniEncounterSite` activa los rivales al entrar en cada una de las 4 kanchas, `AyniAbyssRescue` gestiona las caídas mortales a la quebrada (`y < -3 m`), y los componentes `AyniFireLight` y `AyniDynamicLighting` controlan los braseros incas y las 5 transiciones climáticas del cielo y el sol.
4. **Capa de Narrativa, Audio y Presentación (`Ayni.Story` y `Ayni.UI`):**
   - Mediante el **Patrón de Auto-Ensamblaje en Tiempo de Ejecución (*Runtime Bootstrap*)**, el componente `SifuCombatHUD` instancia automáticamente en `Start()` los sistemas de historia (`AyniPrologue` con `AyniPrologueCast`, `AyniBossPhaseDirector`, `AyniJudgment`, `AyniLevelOutcome`, `AyniReforestation`) y de interfaz (`AyniTutorial`, `AyniPauseMenu`, `AyniLevelAmbience`), evitando modificar manualmente la escena principal.

### 4.2. Funcionamiento de la Máquina de Estados de IA y Secuencia de Combate
El comportamiento de los enemigos y jefes sigue un flujo determinista durante el combate:
- **Detección y Persecución:** Los rivales permanecen en estado `Idle` (ocultos en su kancha) hasta que Yari cruza la portada (`AyniEncounterSite`), pasando al estado `Chase` dentro de un radio de 18 metros.
- **Anticipación y Telégrafos (`Windup`):** Al situarse a `2.2 m` de Yari, el enemigo prepara su golpe durante `0.6 s` emitiendo un aviso visual: **destello rojo** para ataques altos (70% de probabilidad, esquivables agachándose) o **destello amarillo** para barridos bajos (30% de probabilidad, esquivables con saltito).
- **Resolución de Impacto o Desvío:** Si Yari pulsa Guardia en los últimos `0.22 s`, el rival entra en estado `Stagger` durante `1.0 s`, recibe `35` puntos de daño a su postura y habilita el contraataque de Yari (`×1.6` de daño estructural).
- **Transición de Fase en Jefes (`PhaseTransitionRoutine`):** Cuando el jugador vacía la primera barra de vida de Amaru (`380 HP`), el jefe se vuelve temporalmente invulnerable, cae a cámara lenta (`Knockdown`), la cámara gira 360° a su alrededor (`AyniBossPhaseDirector`), se levanta rellenando su segunda barra (`460 HP`), lanza el grito de guerra `Gen_Roar` rodeado de brasas (`FaseDosBrasas` en la iluminación dinámica) y activa la cerbatana de `AmaruHunter`.
- **Ruptura de Postura y Juicio Ayni:** En la segunda fase, cuando la vida de Amaru baja al `40%` o menos y su postura de `150` puntos se llena, el estado `Stunned` se bloquea (`HoldBroken = true`), `AyniJudgment` ralentiza el tiempo a `0.15×` y `AyniPurificationManager` ejecuta la decisión del jugador (Venganza o Perdón Ayni con reforestación de queñuales).

### 4.3. Catálogo Completo de Scripts de Tiempo de Ejecución (33 Scripts C#)
| Espacio de Nombres | Script C# | Responsabilidad Técnica en el Sistema |
| :--- | :--- | :--- |
| `Ayni.Core` | `AyniInput.cs` | Abstracción unificada de Teclado/Ratón y Mando de Xbox; detección de dispositivo en uso y simulación por código. |
| `Ayni.Core` | `AyniRumble.cs` | Vibración háptica XInput para mando de Xbox diferenciando motores grave y agudo en cada acción de combate (`v0.8.0`). |
| `Ayni.Core` | `IllaTalismanSystem.cs` | Gestión de edad (20–75 años), etapas vitales, resurrección, contador de muertes y cálculo de multiplicadores. |
| `Ayni.Core` | `AyniPurificationManager.cs` | Singleton que resuelve el dilema moral (Venganza vs. Ayni) sobre enemigos con postura rota y notifica el desenlace. |
| `Ayni.Core` | `AyniAudio.cs` / `AyniFootsteps.cs` | Motor de reproducción de audio 2D/3D con variación aleatoria de tono, bucles ambientales con *crossfade* y pasos por zancada. |
| `Ayni.Core` | `AyniGameState.cs` / `AyniVersion.cs` | Banderas globales de bloqueo de entrada durante cinemáticas y control centralizado de la versión (`v0.8.0`). |
| `Ayni.Player` | `YariCombatController.cs` | Controlador principal: locomoción con inercia, cancelación de golpes, guardia, esquivas, parry, combos, stick direccional y patada trasera. |
| `Ayni.Player` | `YariTraversal.cs` | Sistema de parkour contextual estilo *Sifu*: salto de obstáculos con apoyo de mano (`Vault`), subida y trepada de cornisas (`Climb`) (`v0.8.0`). |
| `Ayni.Player` | `ThirdPersonSifuCamera.cs` | Cámara en tercera persona con colisión esférica, *Lock-On*, apertura de FOV por velocidad y empujón direccional en impactos (`v0.8.0`). |
| `Ayni.Combat` | `StructureSystem.cs` | Barra de postura (100/150 pts), regeneración pasiva (18/s tras 1.2 s) y estado de ruptura (`IsBroken` y `HoldBroken`). |
| `Ayni.Combat` | `AndeanCombatStanceModifier.cs` | Modificador procedural en `LateUpdate` que aplica la guardia ritual Takanakuy sobre el esqueleto Humanoid y gestiona el *Hitstop* (0.065 s). |
| `Ayni.Combat` | `FootIK.cs` | Cinemática inversa de pies para superficies inclinadas y escalones; se desactiva automáticamente en estados con etiqueta `SinIK`. |
| `Ayni.Combat` | `AttackTimingTable.cs` / `CombatTypes.cs` | Almacenamiento serializado de tiempos exactos de impacto y velocidades de locomoción horneadas por el editor. |
| `Ayni.Combat` | `CombatFeedback.cs` / `RootMotionRelay.cs` | Sacudida suave de cámara con ruido Perlin, destellos de impacto, *Slow Motion* y retransmisión de *Root Motion*. |
| `Ayni.Enemy` | `EnemyController.cs` | IA de combate (8 estados), telégrafos de ataque alto/bajo, jefes en múltiples fases (`bossPhaseHealth`) y apertura del Juicio Ayni. |
| `Ayni.Enemy` | `AmaruHunter.cs` | Comportamiento de segunda fase del jefe Amaru: salto hacia atrás de 5.5 m y disparo de 3 dardos envenenados con aviso verde. |
| `Ayni.Story` | `AyniPrologue.cs` / `AyniPrologueCast.cs` | Secuencia inicial *"La Noche de las Cenizas"* en el puente colgante con el General Sayri, su guardia, el incendio y paneles visuales del pacto (`v0.8.0`). |
| `Ayni.Story` | `AyniBossPhaseDirector.cs` | Cinemática de transición a la Fase 2 del jefe: cámara lenta, órbita de 360°, grito de guerra (`Gen_Roar`) y aura de brasas. |
| `Ayni.Story` | `AyniJudgment.cs` / `AyniLevelOutcome.cs` | Ralentización del tiempo (0.15×) para el dilema moral del jefe, cinemática final del nivel, cartel ODS 15 y tarjeta de resumen. |
| `Ayni.Story` | `AyniReforestation.cs` / `AyniReforestationRevival.cs` | Generación procedural de mallas y texturas del bosque de queñuales, ichu y cantutas, y renacimiento de los 61 árboles quemados. |
| `Ayni.Story` | `AyniLevelAmbience.cs` | Gestión de sonido ambiental del cañón y tambores de combate al ingresar en las kanchas. |
| `Ayni.World` | `AyniEncounterSite.cs` | Control de cada una de las 4 kanchas incas: aparición de rivales al cruzar la portada y detección de despeje del recinto. |
| `Ayni.World` | `AyniDynamicLighting.cs` / `AyniFireLight.cs` | Iluminación dinámica reactiva de 5 climas narrativos y luces de braseros/fogones con ruido Perlin y *Distance Culling*. |
| `Ayni.World` | `AyniAbyssRescue.cs` / `AyniBurntTree.cs` | Rastreo de último suelo seguro, muerte por caída a la quebrada (`y < -3 m`) y puntos de rebrote de árboles quemados. |
| `Ayni.UI` | `SifuCombatHUD.cs` / `AyniPauseMenu.cs` / `AyniTutorial.cs` / `AyniScreenFX.cs` / `AyniGamepadTester.cs` | Interfaz gráfica de combate (con ayuda ocultable con `H`), menú de pausa con volúmenes persistentes, tutorial en el primer combate y efectos de cine. |

### 4.4. Pipeline de 6 Shaders HLSL en Universal Render Pipeline (URP)
1. **`AyniTerrenoAndino.shader`:** Aplica proyección *Triplanar* en las paredes verticales de la garganta para evitar el estiramiento de texturas, dibuja proceduralmente las losas de piedra del Qhapaq Ñan y esculpe sillería en los taludes de los andenes agrícolas.
2. **`AyniMuroInca.shader`:** Genera tres estilos de mampostería incaica en el *Fragment Shader* usando coordenadas en metros reales: *Pirca rústica*, *Sillería poligonal imperial* y *Piedra monolítica* para jambas y dinteles trapezoidales.
3. **`AyniRayosSol.shader`:** Renderiza haces de luz solar volumétricos (*God Rays*) orientados en el ángulo de la Hora Dorada con fundido por profundidad (`SampleSceneDepth`) y ángulo de visión en URP.
4. **`AyniAguaQuebrada.shader`:** Superficie animada del río en el fondo del cañón (`y = -18 m`) con espuma en las orillas y reflejos cálidos.
5. **`AyniNiebla.shader`:** Capas de niebla volumétrica suave en la quebrada con desvanecimiento por profundidad.
6. **`AyniMontanasLejanas.shader`:** Cordillera perimetral de los Andes con nieve en las cumbres según la altitud.

---

## 5. DISEÑO DEL NIVEL 1, ALDEA INCA, ILUMINACIÓN, FAUNA Y NIVELES 2–5

### 5.1. Las Cuatro Kanchas Incas, Fauna Andina y Arquetipos de Rivales del Nivel 1
A lo largo de los **630 metros** del Qhapaq Ñan, el entorno integra **8 obstáculos de parkour y navegación** (troncos quemados, muros de pirca a medio caer, derrumbes de peñascos y empalizadas dobles), **fauna andina autóctona** (**llamas** pastando en las planicies abiertas cercanas a la aldea y **pumas** custodiando los bordes escarpados para delimitar la ruta sin atajos) y **4 conjuntos arquitectónicos incas (*Kanchas*)**:

| Recinto Inca (`AyniEncounterSite`) | Ubicación y Diseño Espacial | Arquetipo de Rival Asignado | Perfil Visual y Táctico |
| :--- | :--- | :--- | :--- |
| **K1 · Puesto de Chasquis** | `(350, 381)` — Primer control del camino con patio abierto, braseros y huanca ceremonial. | **Rastreador** (`Rival_1_Rastreador`) | Ex-chasqui ágil de silueta delgada, túnica gris ceniza y honda a la cintura; ataques rápidos de aproximación. |
| **K2 · Kancha de los Tejedores** | `(312, 492)` — Conjunto residencial con telares, vasijas y fogones interiores navegables. | **Saqueador** (`Rival_2_Saqueador`) | Combatiente robusto de centro de gravedad bajo, faja tejida roja robada y hachuela al cinto; golpes contundentes. |
| **K3 · Tambo del Camino** | `(370, 560)` — Gran posada de abastecimiento con almacenes (*qollqas*), rayos solares y patio amplio. | **Guardia del Tambo** (`Rival_3_GuardiaTambo`) | Guerrero corpulento con chaleco acolchado crudo, casco andino y antebrazos protegidos; alta resistencia de postura. |
| **K4 · Portada del Cazador** | `(485, 535)` — Antesala fortificada previa a la plaza principal de Amaru. | **Cazador de Élite** (`Rival_4_CazadorElite`) | Guerrero alto y atlético con piel de jaguar sobre el hombro, faldellín verde bosque y pintura de serpiente; ataques agresivos. |

### 5.2. Sistema de Iluminación Dinámica Andina (`ILUMINACION_ANDINA.md`)
El escenario cuenta con **24 braseros ceremoniales de piedra**, **5 fogones exteriores de patio**, luces interiores con parpadeo orgánico (`AyniFireLight`) y **6 haces de rayos solares volumétricos**, gobernados por `AyniDynamicLighting` a través de cinco estados lumínicos reactivos:
1. **Hora Dorada (Estado Inicial):** Sol cálido a `17°` sobre el horizonte oeste con bruma dorada andina.
2. **Combate en Kancha:** Al entrar en un patio con rivales, incrementa el contraste solar y aviva un `15%` la intensidad de los braseros.
3. **Segunda Fase del Jefe (`FaseDosBrasas`):** Cuando Amaru entra en su segunda fase (*"Desatado"*), el sol baja a `12°` con tono cobrizo y los braseros arden un `45%` más fuerte.
4. **Amanecer Sagrado (`AmanecerAyni` — Perdón / ODS 15):** Al perdonar a Amaru, el sol asciende a `26°`, el cielo se vuelve cristalino, la niebla se despeja y la lluvia purificadora atenúa las llamas mientras brota el bosque de queñuales.
5. **Ocaso de Cenizas (`OcasoVenganza` — Venganza):** Al ejecutar al jefe, el cielo se oscurece (`skyExposure = 0.62`), el sol cae a un tono rojo ceniza (`8.5°`) y la niebla volcánica se espesa.

### 5.3. Proyección de Misiones y Jefes (Niveles 2 al 5)
| Misión | Región / Escenario Andino | Jefe Principal | Recurso Usurpado y Enfoque ODS |
| :--- | :--- | :--- | :--- |
| **Misión 2** | Canteras de Piedra y Minas de Cobre | **Túpac el Capataz de Bronce** | Herramientas, metales del pueblo y trabajo forzado (**ODS 8 y ODS 12**). |
| **Misión 3** | Terrazas Agrícolas y Acueductos Secos | **Apo Rumi el Acaparador** | Aguas sagradas y canales de irrigación de los valles (**ODS 6**). |
| **Misión 4** | Palacio Alquímico y Cámaras Solares | **Mama Churi la Alquimista** | Oro ceremonial, ofrendas rituales y contaminación de hornos (**ODS 12**). |
| **Misión 5 (Clímax)** | Cumbre Nevada del Intihuatana (5,000 m.s.n.m.) | **General Sayri el Usurpador** | El Trono Solar y el equilibrio cósmico del Tawantinsuyu (**ODS 16**). |

---

## 6. PLAN DE PRUEBAS DE CALIDAD (QA), MÉTRICAS Y CATÁLOGO DE RECURSOS

### 6.1. Métricas de Locomoción, Inercia y Auditoría Anti-Patinaje
| Estado de Locomoción | Velocidad Base del Clip (1×) | Tope de Cadencia | Velocidad Real Medida en Play | Porcentaje de Patinaje Medido |
| :--- | :---: | :---: | :---: | :---: |
| **Caminar (Stick al 50%)** | `1.07 m/s` (Ciclo 1.17 s) | `0.70× – 1.30×` | `1.23 m/s` | **1%** (Óptimo) |
| **Correr (Stick a fondo / Teclado)** | `3.70 m/s` (Ciclo 0.53 s) | `1.05×` | `4.14 m/s` | **2% – 9%** (Óptimo) |
| **Sprint (`RT` / `L3` / `Shift`)** | `3.96 m/s` (Ciclo 0.53 s) | `1.30×` | `5.60 m/s` | **3% – 9%** (Óptimo) |
| **Caminata Agachado** | `0.95 m/s` (Ciclo 1.03 s) | `1.40×` | `1.27 m/s` | **~10%** (Aceptable) |
| **Desplazamiento Lateral (*Lock-On*)** | `3.01 m/s` / `2.37 m/s` | `1.30×` | `2.69 m/s` / `2.76 m/s` | **3% – 5%** (Óptimo) |
| **Retroceso con Rival Fijado** | `1.67 m/s` (Ciclo 0.80 s) | `1.30×` | `2.33 m/s` | **10%** (Aceptable) |

### 6.2. Matriz Resumen de Casos de Prueba Funcionales (QA Test Cases — `v0.8.0`)
| ID | Módulo Evaluado | Caso de Prueba y Condición de Entrada | Resultado Esperado y Criterio de Aceptación | Estado |
| :--- | :--- | :--- | :--- | :---: |
| **TC-01** | `AyniPrologue` / `AyniPrologueCast` | Iniciar partida con el prólogo activo (*La Noche de las Cenizas*). | Se muestra al General Sayri, su guardia y el incendio en el puente colgante, el pacto visual de la Illa y el despertar al inicio del camino. | **PASS** |
| **TC-02** | `AyniInput` / `AyniTutorial` | Llegar al primer combate y alternar entre Teclado/Ratón y Mando de Xbox. | El tutorial se abre al iniciar el primer combate (no tras el prólogo) y actualiza sus iconos al instante según el periférico activo. | **PASS** |
| **TC-03** | `YariTraversal` | Presionar `Espacio` / `A` frente a troncos (`0.6 m`), muros (`1.25 m`) y cornisas (`1.3–2.4 m`). | Yari salta obstáculos bajos apoyando la mano (`Traverse_Vault`) sin perder carrera y trepa cornisas altas (`Traverse_Climb`). | **PASS** |
| **TC-04** | `YariCombatController` | Subir la guardia o mover el stick inmediatamente después de que un golpe conecte. | La animación de recuperación del brazo se corta al instante, permitiendo bloquear, esquivar o desplazarse sin demora. | **PASS** |
| **TC-05** | `YariCombatController` / `AyniRumble` | Ejecutar golpes, bloqueos y Desvío Perfecto (*Parry* en `< 0.22 s`) con mando. | El rival recibe `35` de daño de postura en Parry, la cámara recibe empujón direccional con ruido Perlin y el mando vibra en XInput. | **PASS** |
| **TC-06** | `AyniAnimationForge` / `FootIK` | Ejecutar esquivas *Duck*, *Jump*, *Sway L/R*, *Vault* y *Climb* sobre terreno inclinado. | La etiqueta `SinIK` desactiva `FootIK` y la postura andina en los clips generados, evitando deformaciones en las piernas. | **PASS** |
| **TC-07** | `YariCombatController` | Inclinar el stick hacia un rival situado a la espalda (`>115°`, `<2.6 m`) y pulsar ataque. | Yari ejecuta la patada hacia atrás (`Atk_BackKick`) sin girarse previamente y luego encara al objetivo. | **PASS** |
| **TC-08** | `AyniAbyssRescue` / `IllaTalismanSystem` | Caer a la garganta profunda (`y < -3 m`) o morir en combate repetidas veces. | Se activa `FallToDeath` o resurrección en el sitio sumando años (`20` a `75` años) y ajustando los multiplicadores de etapa vital. | **PASS** |
| **TC-09** | `AyniEncounterSite` | Cruzar la portada de entrada de cada una de las 4 kanchas incas (`K1` a `K4`). | Los rivales aparecen en el patio, suenan los tambores, la iluminación sube su contraste y al derrotarlos se emite `OnCleared`. | **PASS** |
| **TC-10** | `EnemyController` / `AyniBossPhaseDirector` | Vaciar la primera barra de salud de Amaru el Cazador (`380 HP`). | Amaru cae a cámara lenta, la cámara lo rodea 360°, ruge (`Gen_Roar`), se activa el clima `FaseDosBrasas` y pasa a Fase 2 (`460 HP`). | **PASS** |
| **TC-11** | `AmaruHunter` | Combatir contra Amaru en la Fase 2 a media distancia. | Amaru salta `5.5 m` hacia atrás y dispara 3 dardos envenenados precedidos por un destello verde de `0.5 s`. | **PASS** |
| **TC-12** | `AyniJudgment` / `AyniReforestation` / `AyniDynamicLighting` | Romper la postura de Amaru en Fase 2 (`≤40%` HP) y elegir Perdón Ayni (`X` / `A`). | Clima cambia a `AmanecerAyni`, llueve, brota el bosque de queñuales, renacen los 61 árboles quemados y aparece el mensaje del **ODS 15**. | **PASS** |

---

## 7. CONCLUSIONES DEL PROYECTO

1. **Integración Exitosa de Ingeniería de Software y Diseño de Videojuegos:** El desarrollo de **AYNI: El Retorno del Guardián (`v0.8.0`)** demuestra la viabilidad de construir un videojuego de acción táctica en tercera persona sobre **Unity 6 (`6000.3.23f1`)** con **Universal Render Pipeline (URP)** empleando una arquitectura modular desacoplada por eventos (`Ayni.*`) y un patrón de auto-ensamblaje en tiempo de ejecución (`SifuCombatHUD`). Este enfoque permitió que todo el equipo colaborara simultáneamente en sistemas de combate, iluminación, fauna y documentación sin generar conflictos sobre la escena principal.
2. **Respuesta Táctica, Parkour Contextual y Animación Procedural (`v0.8.0`):** La combinación de las 29 animaciones de captura de movimiento con los 11 clips generados por código en la Fragua (`AyniAnimationForge`, incluyendo las nuevas animaciones de saltar obstáculos `Yari_Vault` y trepar `Yari_Climb`), la inercia física de locomoción, la cancelación de recuperación de golpes y el aislamiento con la etiqueta `SinIK` redujeron el patinaje de pies a un rango óptimo del **1% al 9%**, logrando un *game feel* fluido con empujón direccional de cámara y vibración háptica.
3. **Producción Autónoma de Activos Multimedia, Iluminación y Fauna Andina:** La generación procedural de las cuatro *Kanchas* incas, los seis shaders HLSL a medida (incluyendo rayos solares volumétricos), el sistema de iluminación dinámica de cinco climas (`AyniDynamicLighting`), la ambientación con fauna autóctona (llamas y pumas) y la síntesis matemática en Python de los **42 efectos de sonido y música andina (`generar_sfx.py`)** garantizan un producto tecnológicamente autónomo y libre de infracciones de propiedad intelectual.
4. **Valor Cultural e Impacto Educativo Sostenible (Agenda 2030):** Más allá del desafío mecánico del arte marcial **Rumi Maki** y el envejecimiento del **Talismán Illa**, el proyecto convierte el principio andino del **Ayni** en una mecánica de decisión moral significativa que vincula directamente la acción del jugador con la restauración ecológica de los bosques altoandinos de queñuales (**ODS 15**), consolidando al videojuego como un medio de difusión cultural e impacto ambiental positivo.

---
*Documentación Integral del Proyecto — Ayni: El Retorno del Guardián (`v0.8.0`).*
