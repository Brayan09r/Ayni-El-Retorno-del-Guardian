# Nivel 1: prólogo, mando de Xbox, esquivas Sifu, caídas y final del Cazador

Nota de trabajo para quien toque el Nivel 1 (personas y agentes). Sigue la Biblia del juego
(`HISTORIA_Y_BIBLIA_3D_AYNI.md`): prólogo "La Noche de las Cenizas", Amaru el Cazador (ODS 15) y el Juicio Ayni.

## Cómo se juega ahora el nivel

1. **Prólogo "La Noche de las Cenizas"** (~43 s, se salta con `Tab` / `Esc` / `Menu`). Yari está en el puente colgante sobre la
   garganta. El General Sayri le quiebra la guardia con la Champi y lo arroja al abismo. En la caída la Illa se enciende y sella el pacto.
   "Años después" Yari despierta en el camino frente a **Amaru el Cazador**.
2. **Tutorial** (4 páginas) con los botones del dispositivo en uso.
3. **Combate contra Amaru en tres fases:**
   - Más del 75 % de vida: cuerpo a cuerpo.
   - Desde el 75 %: además salta hacia atrás y lanza **ráfagas de 3 dardos envenenados**. Antes de cada dardo se tiñe de verde.
     El dardo se esquiva agachándose o con el balanceo, se desvía con parry y se bloquea con la guardia. Si alcanza a Yari, lo envenena:
     pierde vida poco a poco y la pantalla se nubla de verde.
   - En el 40 % o menos: al romperle la postura llega el **Juicio Ayni**. Todo va a cámara lenta y se elige entre botón rojo
     (Venganza) y botón verde (Ayni). Los jefes no mueren a golpes: el final siempre pasa por el Juicio.
4. **Desenlace:**
   - Ayni: llueve, vuelve a brotar el queñual (ver *El queñual del perdón*) y aparece el título "ODS 15".
   - Venganza: cae ceniza y el cielo se oscurece.
   - Después sale la tarjeta "Nivel 1 completado" con la edad, las caídas, el tiempo y la decisión. Desde ahí se puede volver a jugar o seguir explorando.

## Esquivas y guardia estilo Sifu

- **En guardia Yari se planta**: no camina, solo gira para encarar al rival. Antes la tecla `S` también lo hacía retroceder.
- Guardia + ↓ = agacharse (evita golpes altos) · Guardia + ↑ o salto = saltito (evita barridos) · Guardia + ← → = balanceo (evita todo, con menos margen).
- Tras una esquiva o un parry, el siguiente golpe es un **contraataque**: +60 % de daño a la postura. El rival queda descolocado 0.4 s más.
- Las esquivas usan animaciones **en el sitio** generadas por la Fragua (abajo). Ninguna desplaza al personaje.

## Caídas

- Al caer más de 1.2 m a buena velocidad, Yari pasa a la animación de **caída en el aire** (`Fall_Loop`) y tiene un poco de control de dirección.
- Desde 3.5 m aterriza **pesado** (`Land_Hard`) y queda clavado un instante. Desde 9 m además pierde vida (6 por metro extra).
- Si cae a la quebrada, **muere en la caída**: la cámara se queda arriba y lo ve caer de espaldas (`Fall_Back`) a cámara lenta hasta el agua, la pantalla se va a negro y la Illa lo resucita en el último suelo firme. Se paga como una muerte en combate: suma años y cuenta para el contador de muertes; si la edad llega al límite, es el final. (`YariCombatController.FallToDeath`, lo lanza `AyniAbyssRescue` cuando Yari baja de y = -3.)

## Ritmo de los golpes

- Cada golpe **termina su recorrido antes de dejar paso al siguiente**: tras el impacto sigue 0.20 s (ligero) o 0.30 s (pesado) y solo entonces se puede encadenar. Antes el siguiente golpe cortaba al anterior a los 0.10 s de conectar.
- La pulsación hecha en mitad de un golpe **queda en cola** hasta ese momento: una pulsación = un golpe más, sin tener que acertar el instante.
- Los ajustes están en `YariCombatController`, sección *Ritmo del Combo* (`lightFollowThrough`, `heavyFollowThrough`, `lightWindup`…). Tienen nombres nuevos a propósito: así valen los valores del código y no los que quedaron guardados en la escena.

## Caminar, correr y esprintar (los pies ya no patinan)

- **Animaciones de Mixamo** (rig de Yari, In Place, sin malla, 30 fps), en `Assets/Art/Characters/Animations/`:
  caminar **"Standard Walk"** (`Walk_Forward_InPlace.fbx`), correr **"Fast Run"** (`Run_Forward_InPlace.fbx`), sprint **"Two Cycle Sprint"** (`Sprint_Run_InPlace.fbx`),
  laterales **"Left Strafe"** / **"Right Strafe"** (`Strafe_Left.fbx`, `Strafe_Right.fbx`) y retroceso **"Jog Backward"** (`Walk_Back.fbx`).
  En el Animator, `Speed` = 0 reposo · 0.5 caminar · 1 correr · 2 sprint. Los originales están en `AnimBackups/` (fuera de Assets).
- **La cadencia de los pasos se calcula sola.** Al generar el Animator, `AyniAttackTimingBaker` mide cuántos m/s cubren los pasos de
  cada clip con el cuerpo de Yari y lo guarda en `Assets/Resources/YariAttackTimings.asset`. En cada fotograma `YariCombatController`
  reproduce la animación a *velocidad real ÷ lo que cubre el clip*, hasta un tope para que las piernas no se vean a cámara rápida.

  | Clip | Cubre a 1x | Ciclo | Tope de cadencia | Cubre como mucho |
  |---|---|---|---|---|
  | Caminar | 1.07 m/s | 1.17 s | 0.7x – 1.3x | 1.39 m/s |
  | Correr | 3.70 m/s | 0.53 s | 1.05x | 3.9 m/s |
  | Sprint | 3.96 m/s | 0.53 s (el clip trae dos ciclos: 1.07 s) | 1.3x | 5.1 m/s |
  | Agachado | 0.95 m/s | 1.03 s | 1.4x | 1.3 m/s |
  | Lateral izquierdo | 3.01 m/s | 0.67 s | 1.3x | 3.9 m/s |
  | Lateral derecho | 2.37 m/s | 0.67 s | 1.3x | 3.1 m/s |
  | Hacia atrás | 1.67 m/s | 0.80 s | 1.3x | 2.2 m/s |

- **Velocidades de Yari** (base; con la Illa joven va un 15 % más rápido y de anciano un 15 % más lento): correr **3.6 m/s**,
  sprint **4.9 m/s**, agachado **1.1 m/s**; con el rival fijado, de lado **2.4 m/s** y hacia atrás **2.0 m/s**. Las originales
  (4.0, 6.5 y 2.4) casi doblaban lo que cubrían las animaciones y los pies patinaban entre el 50 y el 80 %. Están en
  `Assets/Editor/AyniTuning.cs` (menú **Ayni > 5**), con la cuenta explicada en su cabecera; si se suben, vuelven a patinar.
- **Mando, stick en tres tramos:** hasta el 75 % camina (0.85 a 1.2 m/s); pasado el 75 % corre suave (72 % de la velocidad) y
  acelera hasta el tope con el stick a fondo. `RT` o `L3` es el sprint.
- **Teclado, dos marchas:** `WASD` camina (1.2 m/s base) y con `Shift` corre a fondo. No hay sprint con teclado.
  `AyniInput.MoveFromStick` dice de dónde viene el movimiento; en las pruebas, `SimulateMove` imita el stick y `SimulateKeys` las teclas.
- **Salto en carrera:** saltando a un ritmo de 0.7 o más (1 = correr a fondo) y sin rival fijado, Yari conserva la dirección y sale
  un 30 % más rápido (`leapSpeedBoost`), con 1.05 m de altura: unos 3.5 m de largo corriendo y unos 4.5 m en sprint. En el aire
  solo se corrige la dirección (50°/s). Usa su propio clip, **"Running Jump"** de Mixamo (`Run_Jump.fbx`, estado `Run_Jump`):
  se reproduce desde el despegue (0.12 s) y a la velocidad justa para que el aterrizaje del clip (0.57 s) coincida con el real.
  El clip no es "In Place", pero no hace falta: el Animator no aplica el movimiento de la raíz. Si falta, se usa el clip `Jump`.
- **El salto ya no se pierde:** la pulsación se recuerda 0.15 s y el suelo 0.12 s. Antes, corriendo por terreno irregular el
  CharacterController perdía el suelo algún fotograma y, si coincidía con la pulsación, Yari no saltaba.
- **Con el rival fijado** la velocidad y la cadencia se reparten según la dirección (hacia él, de lado, hacia atrás), igual que
  mezcla los clips el BlendTree `LockOn_Locomotion`.
- **Medido en Play** con `AyniLocomotionProbe.MeasureSlide` (Illa joven):

  | Prueba | Yari avanza | Patinaje |
  |---|---|---|
  | Stick al 50 % (caminar) | 1.23 m/s | 1 % |
  | Stick a fondo / teclado (correr) | 4.14 m/s | 2 – 9 % (cadencia 1.05x) |
  | Sprint | 5.6 m/s | 3 – 9 % (cadencia 1.3x) |
  | Agachado | 1.27 m/s | en torno al 10 % |
  | Fijado, hacia la izquierda | 2.69 m/s | 5 % |
  | Fijado, hacia la derecha | 2.76 m/s | 3 % |
  | Fijado, hacia atrás | 2.33 m/s | 10 % |
  | Fijado, hacia el rival | 4.03 m/s | 4 % |

- **Al sustituir un clip por otro con el mismo nombre de archivo** (conservando su `.meta`) hay que regenerar el Animator
  (menú **Ayni > 1**): ajusta el rango del clip a la toma nueva. Sin eso el clip queda cortado con la duración del anterior
  (pasó con el sprint: se cortaba a media zancada y daba un salto en cada vuelta). Después, medir con
  `AyniLocomotionProbe.MeasureClips` y rehacer la cuenta de `AyniTuning.cs`.
- Los laterales, el paso atrás y el agachado se importan con la orientación original del clip (no la del torso): si no, Unity los
  gira y Yari da los pasos en diagonal.
- **Sensación de velocidad:** al correr a fondo la cámara abre el campo de visión 4° y se aleja un 6 %; en sprint, 10° y un 15 %
  (`ThirdPersonSifuCamera`, sección *Sensación de velocidad*). Yari no avanza más rápido, pero lo parece. No se aplica con el
  rival fijado, en los remates ni en las caídas, y las escenas reciben la cámara con su campo de visión normal.
- La consola muestra avisos amarillos *"Rig Error: Copied Avatar Rig Configuration mis-match"* al reimportar los clips de Mixamo:
  sus huesos difieren unos milímetros de los de Yari. No afecta a la animación.

## El queñual del perdón

Al perdonar al jefe, alrededor del lugar del Juicio brota un bosque en una ola que sale del centro hacia fuera (`AyniReforestation`):

- **Queñuas** (*Polylepis*) en tres tamaños: plantones cerca de los personajes, árboles jóvenes y, al fondo, árboles de 3 m con
  el tronco retorcido de corteza rojiza. Los altos se quedan a más de 10 m del centro para no tapar a Yari y al jefe ni cruzarse
  con la cámara, que gira a 4.5 – 7.5 m.
- **Brotes** recién nacidos, **matas de ichu** y **cantutas** (la flor sagrada de los incas) rojas, rosadas y amarillas.
- Cada árbol levanta su montículo de tierra y musgo; primero sube el tallo, fino, luego engorda y al final se abre la copa.
  Saltan tierra y hojas al brotar, el viento mece las copas en rachas y sobre el claro flotan motas de polen.
- **Todo se genera por código**: mallas (troncos, copas, hierba, flores) y texturas (corteza, ramitas de hojas, paletas). No hay
  modelos ni imágenes que importar. Los modelos se construyen una vez y las plantas los comparten.
- Las hojas son láminas con una ramita pintada y recortada por transparencia. Sus materiales parten de las plantillas de
  `Assets/Resources/AyniVegetacion` (menú **Ayni > Entorno > Recrear Materiales de la Vegetación**). **No borrar esa carpeta**:
  sin ella, en el juego compilado las hojas saldrían como cuadrados opacos (en el editor seguiría viéndose bien).
- Solo brota sobre el terreno (ni sobre el puente ni sobre las rocas), fuera de la quebrada y en pendientes suaves.
- Los árboles no tienen colisión: si el jugador sigue explorando, los atraviesa.
- Para verlo sin ganar el combate, en Play: `call Ayni.Editor.AyniOutcomePreview.Reforest` y
  `call Ayni.Editor.AyniOutcomePreview.Shot 60 13 4.5` (ángulo, distancia y altura de la cámara). El desenlace real se puede
  forzar con `call Ayni.Editor.AyniPlaytestProbe.HitBoss 200 999` seguido de `call Ayni.Core.AyniInput.Simulate Mercy 0.15`.

## Mando de Xbox

Toda la entrada pasa por `Assets/Scripts/Core/AyniInput.cs`. Los scripts preguntan por **acciones** ("golpe ligero", "guardia"…),
no por teclas. Así teclado y mando funcionan igual y el HUD y el tutorial muestran los botones del dispositivo que se esté usando.
Los ejes del mando están en el Input Manager (`ProjectSettings/InputManager.asset`, ejes `Ayni_*`).
Tabla completa de botones en `CONTROLES.md`. **F9 en Play** abre un panel que muestra en vivo qué lee Unity del mando.

## Animaciones generadas (la Fragua)

`Assets/Editor/AyniAnimationForge.cs` crea clips Humanoid nuevos **a partir de las poses de los clips de Mixamo de Yari**:
guardia, agachado, salto y golpe recibido. Encima añade movimiento procedural sobre los músculos del Avatar.
Antes de usar cada músculo mide automáticamente en qué sentido mueve el hueso, para no depender de convenciones.

| Clip (en `Animations/Generadas`) | Estado del Animator | Uso |
| --- | --- | --- |
| `Yari_Avoid_Duck` | `Avoid_Duck` | Agacharse en el sitio |
| `Yari_Avoid_Jump` | `Avoid_Jump` (y `Hunter_Leap` en Amaru) | Saltito con las piernas recogidas |
| `Yari_Avoid_SwayL` / `SwayR` | `Avoid_SwayL` / `Avoid_SwayR` | Balanceo lateral con los pies plantados |
| `Yari_Fall_Loop` | `Fall_Loop` | Caída en el aire (bucle) |
| `Yari_Fall_Back` | `Fall_Back` | Golpe y caída de espaldas al vacío (prólogo) |
| `Yari_Land_Hard` | `Land_Hard` | Aterrizaje pesado |

Menús en `Ayni > Animaciones`:
- `1. Generar Caídas y Esquivas (Sifu)` regenera los clips y los mete en el Animator.
- `2. Hojas de Fotogramas` dibuja cada clip de frente y de perfil en `DebugCaptures/forja_*.png`, para revisarlo sin dar Play.
- `3. Añadir Estados Nuevos al Animator` solo añade los estados.

`Ayni > 1. Generar Animator Controller` también los vuelve a añadir al final.

## Archivos nuevos

| Archivo | Qué hace |
| --- | --- |
| `Scripts/Core/AyniInput.cs` | Entrada única teclado / mando de Xbox |
| `Scripts/Story/AyniPrologue.cs` | Prólogo "La Noche de las Cenizas" (montado por código; no toca la escena) |
| `Scripts/Story/AyniJudgment.cs` | Pantalla del Juicio Ayni del jefe (cámara lenta, botón rojo / verde) |
| `Scripts/Story/AyniLevelOutcome.cs` | Desenlace del nivel según la decisión y tarjeta final |
| `Scripts/Story/AyniReforestation.cs` | El queñual que brota al perdonar: árboles, brotes, ichu y cantutas generados por código |
| `Editor/AyniVegetationMaterials.cs` | Crea las plantillas de material de esa vegetación en `Resources/AyniVegetacion` |
| `Editor/AyniOutcomePreview.cs` | Para ver el queñual en Play sin ganar el combate |
| `Scripts/Enemy/AmaruHunter.cs` | Segunda fase de Amaru: salto atrás y dardos envenenados |
| `Scripts/UI/AyniScreenFX.cs` | Fundidos, franjas de cine, subtítulos, títulos y tinte de ambiente |
| `Scripts/UI/AyniGamepadTester.cs` | Panel de prueba del mando (F9) |
| `Editor/AyniAnimationForge.cs` · `AyniAnimatorUpgrade.cs` | Fragua de animaciones y estados del Animator |
| `Editor/AyniGamepadSetup.cs` | Ejes del mando en el Input Manager |
| `Editor/AyniStoryMenu.cs` | `Ayni > Historia > Prólogo al dar Play` (desactivarlo para probar el combate rápido) |
| `Editor/AyniAgentBridge.cs` · `AyniPlaytestProbe.cs` | Puente para agentes y sondas de prueba (ver abajo) |

Todo se añade solo en Play desde `SifuCombatHUD`: prólogo, tutorial, Juicio, desenlace, panel del mando y el Cazador de Amaru.
**No hace falta tocar la escena.**

## Para agentes (Claude, Antigravity…): probar con Unity abierto

`AyniAgentBridge` permite manejar el editor desde la terminal **sin tomar el control de la pantalla**.
Se escriben comandos en `UserSettings/AyniAgent/cmd.txt` y la salida (con toda la consola) aparece en `UserSettings/AyniAgent/log.txt`.
Los dos archivos están en una carpeta que git ignora.

```
refresh                                         recompilar
call Ayni.Editor.AyniStoryMenu.SetPrologue 0     sin prólogo
play
wait 4
call Ayni.Core.AyniInput.Simulate Skip 0.3       cerrar el tutorial
call Ayni.Core.AyniInput.Simulate Guard 3        mantener la guardia 3 s
call Ayni.Core.AyniInput.SimulateMove 0 -1 0.3   stick abajo: agacharse
call Ayni.Editor.AyniPlaytestProbe.Report prueba posición, estado del Animator, vida… de Yari y Amaru
capture nombre                                   imagen de la cámara en DebugCaptures/nombre.png
screenshot nombre                                Game View completa, con HUD y textos
stop
```

## Otros cambios

- `AyniNewCombatAnimationsImporter` ahora corre una sola vez por sesión del editor y nunca al entrar en Play.
  Antes reimportaba las 29 animaciones en cada recompilación.
- `CombatFeedback.SlowMotion` / `Avoid`: cámara lenta breve al esquivar.
- `EnemyController`: `CanBeJudged`, `InFinalPhase`, `ExternalControl` y `Telegraph()` para los comportamientos especiales de los jefes.

## Pendiente / ideas

- Modelo de Sayri para el prólogo (ahora el golpe llega desde fuera de cuadro).
- Sonido (golpes, lluvia, quena del prólogo) y vibración del mando. La vibración necesita el paquete Input System.
- Trampas de red con estacas de Amaru (Biblia 4.3) y entorno de selva quemada del Antisuyo.
