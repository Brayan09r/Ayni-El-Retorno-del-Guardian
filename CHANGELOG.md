# Historial de versiones — AYNI: El Retorno del Guardián

Formato del equipo (ver `COLLABORATORS.md`): **MAYOR.MENOR.PARCHE**
- **MAYOR**: hitos (Alpha, Beta, demo jugable).
- **MENOR**: mejoras de jugabilidad, sistemas, niveles.
- **PARCHE**: arreglos, ajustes de balance, cambios menores.

La versión en uso está en `Assets/Scripts/Core/AyniVersion.cs`. El HUD la muestra junto al título.
Al publicar una versión en `main` se marca con un tag: `git tag -a v0.6.0 -m "..."` y luego `git push origin v0.6.0`.

---

## v0.9.0 — Inicio cinematográfico, cuenta de la Illa, Amaru en el puente y HUD inca · 1 de noviembre de 2026

**Prólogo "La Noche de las Cenizas"** (PR #8)
- El General Sayri y dos soldados de su Guardia de Bronce cruzan el puente hacia Yari. Sayri lleva la Champi y tiene su propio título; desde
  `083d2e0` usa su modelo propio (Brayan).
- Pelea coreografiada: mazazo de Sayri, bloqueo, contraataque de Yari y el codazo de la Champi que lo lanza al abismo.
- Noche de verdad: cielo oscuro y rojizo, sol bajo, niebla de brasas, ceniza que cae e incendios con humo en el valle (sobre una copia del
  material del cielo; todo se restaura al terminar).
- Sin pantalla negra: el título sobre el puente en llamas, el pacto con un primer plano de la Illa latiendo, "AÑOS DESPUÉS" mientras la cámara
  se eleva sobre el camino, y Yari se levanta sin salir de cuadro. Yari empieza al principio del camino (PR #7).
- El tutorial se abre en el primer combate; al empezar solo hay una ayuda corta de movimiento. El panel de controles empieza recogido.

**La Illa cobra sus años** (`AyniDeathAging`)
- Al caer: cámara lenta, la pantalla se apaga, franjas de cine y la cámara rodea a Yari a ras de suelo mientras el medallón de la Illa cuenta,
  año a año con golpes de tambor, la edad que tendrá al levantarse ("La Illa cobra un año · Próxima caída: +2"). Avisa si madura, si
  envejece o si la Illa se rompe. Al final el medallón baja a su sitio en el HUD.

**Amaru se interpone en el camino** (`AyniBossIntro`)
- Amaru espera de pie en la cabeza del puente colgante (sobre el estribo del lado de la plaza). Al acercarse Yari: plano por encima de su
  hombro con Amaru cerrando el paso, contrapicado de Amaru rugiendo con su título, y baja del estribo para pelear. Se salta con Tab / Esc / Menu.

**HUD minimalista inca** (`AyniInkaUI`, `SifuCombatHUD`)
- Abajo a la izquierda, el medallón de la Illa (piedra, anillo de oro y chakana) con la edad y una cuenta roja con lo que cobrará la próxima
  caída; a su lado, la estructura (fina, de oro) y la vida (grana), con remates escalonados, el tramo recién perdido en claro y una franja de
  tocapu.
- El rival arriba y centrado: nombre en mayúsculas espaciadas, un rombo por fase del jefe y dos barras finas.
- Los avisos en bandas de piedra con filetes de oro. Controles, tutorial y versión en una línea discreta arriba a la izquierda.

**Herramientas**
- Sondas: `GoToEncounter`, `HurtYari`; `PlaceYariOnGround` deja a Yari en el camino y no encima de lo más alto.

## v0.8.0 — Game feel: golpes con peso, movimiento fluido y parkour estilo Sifu · 25 de octubre de 2026

**Controles más rápidos**
- **La guardia corta la recuperación de los golpes:** en cuanto el golpe conecta y termina su recorrido, subir la guardia (o
  esquivar) es inmediato. Antes Yari quedaba bloqueado 0.46 s tras un golpe ligero y 0.66 s tras uno pesado.
- **Moverse también la corta:** inclinar el stick tras el impacto termina el golpe y Yari echa a andar. Si hay otro golpe en cola,
  manda el golpe (los combos no se rompen).

**Movimiento fluido**
- **Inercia:** Yari ya no pasa de 0 a velocidad máxima en un fotograma ni se clava en seco. Acelera a 20 m/s² (corre en ~0.25 s) y
  frena a 26 m/s² (~0.17 s). En una media vuelta primero frena mientras se gira y luego arranca: ya no patina de espaldas.

**Golpes con peso**
- **Empujón de cámara:** cada golpe empuja la cámara hacia donde va (el puñetazo la mete hacia el rival, el golpe recibido la echa
  hacia Yari), la hace asentir e inclinarse, y vuelve con un rebote. La sacudida de antes (un punto al azar en cada fotograma, que
  se sentía como un zumbido) pasa a ser un temblor suave de ruido Perlin.
- **Vibración del mando** (XInput, Windows): golpes ligeros y fuertes, remates, bloqueos, desvíos, esquivas, golpes recibidos y
  aterrizajes pesados, cada uno con su mezcla de motor grave y agudo. Solo con el mando y nunca en pausa ni en escenas.

**Parkour estilo Sifu (sin salto libre)**
- `A` / `Espacio` hace lo que haya delante (`YariTraversal`): **salta** troncos y muros bajos (0.3–1.3 m) apoyando la mano y
  sigue corriendo, **se sube** a lo bajo y ancho y **trepa** cornisas de 1.3 a 2.4 m. El salto largo queda solo para cruzar un
  hueco corriendo. Sin nada delante, el botón no hace nada. Aviso `[A] Saltar` / `[A] Trepar` fuera de combate.
- Animaciones nuevas de la Fragua: `Yari_Vault` y `Yari_Climb` (estados `Traverse_Vault` y `Traverse_Climb`).
- Tutorial, HUD, `CONTROLES.md`, `README.md` y `ALDEA_INCA.md` actualizados.

**Herramientas**
- El puente de agentes ya no se queda atascado pidiendo Play cuando hay errores de compilación: descarta la cola y recompila.
- Sondas nuevas: `TraceSpeed` (velocidad fotograma a fotograma), `PlaceYariOnGround`, `SpawnTestBlock` y `Traversal` (qué puede
  saltar o trepar Yari y, si no puede, por qué).

## v0.7.0 — Jefes en dos fases, menú de pausa y balanceo nuevo · 6 de octubre de 2026

**Jefes en dos fases (como en Sifu)**
- Amaru tiene **dos barras de vida**: 380 en la fase 1 y 460 en la fase 2. Antes tenía 220 y caía demasiado rápido.
  Su postura también aguanta más: 150 en vez de 100.
- **Al vaciar la primera barra:**
  1. La acción se detiene y Amaru cae a cámara lenta. Yari queda apartado en guardia.
  2. La cámara lo rodea mientras se levanta y su barra se rellena.
  3. Se vuelve hacia Yari y lanza un **grito de guerra** (animación nueva `Gen_Roar`), con el título "SEGUNDA FASE · Desatado".
- **En la segunda fase** ataca más seguido, pega más fuerte y se mueve más rápido. Le rodea un **aura de brasas**,
  y es cuando usa la **cerbatana** (dardos envenenados).
- El **Juicio Ayni** solo llega al final de la segunda fase. La barra del jefe muestra "FASE 1/2".
- Ajustes en `EnemyController` (sección *Jefe: fases*). La escena del cambio de fase está en `AyniBossPhaseDirector`.

**Menú de pausa con volumen** (Esc / Menu del mando)
- Volumen general, de efectos y de música y ambiente. Se guardan entre partidas.
- Opciones para volver a ver el tutorial y reiniciar el nivel. Se maneja con el ratón, con el teclado o con el mando.
- El volumen de fábrica baja al 75 % general, 80 % efectos y 70 % música, porque los efectos sonaban fuertes.

**Balanceo lateral nuevo**
- Ahora sale de la esquiva real de Mixamo (`Dodge_Left` / `Dodge_Right`): se toma su tramo central (rodillas, tronco que
  se inclina y gira, cabeza fuera de la línea del golpe), acelerado a 0.46 s y con los brazos en guardia. La versión anterior era procedural y se veía rígida.

## v0.6.0 — Sonido, esquivas pulidas y patada hacia atrás · 6 de octubre de 2026

**Sonido (antes el juego no tenía ninguno)**
- 42 sonidos propios, sintetizados por código (`Herramientas/Audio/generar_sfx.py`), sin licencias de terceros:
  - **Combate:** golpes ligeros y pesados (3 variantes cada uno), remate, bloqueo, desvío (parry), zumbidos de los golpes, esquiva, postura rota y golpe recibido.
  - **Movimiento:** pasos sobre piedra, salto, aterrizaje normal y pesado, viento de la caída y chapuzón en el río.
  - **Amaru:** dardo de la cerbatana, impacto del dardo y veneno.
  - **Historia:** tambor del Juicio Ayni, campanas del perdón, golpe de la Venganza y brillo de la Illa.
  - **Ambiente en bucle:** viento de la cordillera, fuego y lluvia.
  - **Música:** quena en pentatónica andina para el prólogo.
  - **Interfaz:** sonidos al pasar y confirmar en los menús.
- `AyniAudio` los reproduce por nombre, con variantes al azar y pequeñas variaciones de tono, en 3D para el mundo y en 2D para la interfaz. Los pasos suenan según la zancada.

**Esquivas**
- Arreglado: las esquivas se veían como una bolita flotando. El apoyo de pies (FootIK) usaba "metas de IK" que los clips generados no traen. Ahora los estados generados llevan la etiqueta `SinIK` y ni el FootIK ni la postura andina los deforman.
- Agacharse ahora es un amago rápido al estilo Sifu: media flexión, tronco abajo, 0.44 s.
- El balanceo lateral es más marcado: la cabeza se desplaza unos 12 cm.

**Combate**
- **Elegir rival con el stick:** al atacar inclinando el stick (o WASD), el golpe va al rival que hay en esa dirección.
- **Patada hacia atrás:** si el rival elegido está a la espalda, Yari lanza una patada sin girarse (clip nuevo generado) y luego se gira hacia él.

**Herramientas**
- Grabadora de fotogramas dentro del juego (`AyniPlaytestProbe.Record`), para revisar animaciones como se ven al jugar.
- El puente de agentes silencia el audio en sus pruebas y vuelve a intentar entrar en Play si Unity lo ignora.

## v0.5.0 — Nivel 1 completo: prólogo, mando, aldea y rivales · 5 de octubre de 2026

- **Prólogo "La Noche de las Cenizas"**: Sayri arroja a Yari del puente y la Illa sella el pacto.
- **Mando de Xbox completo**, HUD y tutorial según el dispositivo, y panel de prueba (F9).
- **Guardia y esquivas estilo Sifu**: Yari se planta; agacharse, saltito y balanceo sin moverse del sitio; contraataque.
- **Animaciones generadas** (la Fragua): esquivas, caída en el aire, caída de espaldas y aterrizaje pesado.
- **Amaru en fases**: dardos envenenados y Juicio Ayni a cámara lenta. Desenlace con ODS 15 (perdón) o ceniza (venganza).
- *(Brayan)* Golpes que no se cortan, caminar/correr/esprintar con Mixamo sin que patinen los pies, salto en carrera,
  **caída mortal** a la quebrada, **queñual del perdón**, **aldea inca** con 4 conjuntos, 8 obstáculos y **4 rivales nuevos**.

## v0.4.0 — Combate Rumi Maki y Amaru 3D · 4 de octubre de 2026

- 29 animaciones de combate de Mixamo para Yari, modelo 3D riggeado de Amaru el Cazador, entorno andino de acantilados,
  feedback de impacto y postura andina procedural.

## v0.3.0 — Interfaz y locomoción · 29 de septiembre de 2026

- Interfaz uGUI en URP, correcciones de animación y física de locomoción, GDD e infografías de controles.
