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
   - Ayni: llueve, brotan plantones de queñua y aparece el título "ODS 15".
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

## Caminar con el stick

- El stick tiene **dos marchas**: inclinado menos del 75 % Yari camina (0.8 a 1.1 m/s en base; ~0.92 a 1.26 m/s con Illa Youth); a fondo corre a 4.0 m/s (4.6 m/s con Illa Youth) igual que con el teclado.
- **Animaciones integradas de Mixamo** (con rig de Yari, In Place, Without Skin, 30 fps):
  - Caminar: **"Standard Walk"** (`Assets/Art/Characters/Animations/Walk_Forward_InPlace.fbx`, clip interno `Walk_Forward_InPlace`). Paso erguido y natural hacia adelante.
  - Correr: **"Running"** (`Assets/Art/Characters/Animations/Run_Forward_InPlace.fbx`, clip interno `Run_Forward_InPlace`). Carrera atlética hacia adelante.
- Al caminar la animación conserva la zancada completa y cambia la **cadencia** (velocidad del Animator entre 0.8x y 1.2x) para que los pies pisen a la velocidad real.
- `walkStrideSpeed` = **1.05 m/s** (cobertura natural medida del paso de "Standard Walk"; ratio paso/animator $X / Y$).
- Con `walkSpeedMin = 0.8 m/s` y `walkSpeedMax = 1.1 m/s`, la cadencia en los extremos se mantiene calibrada dentro de 0.85x–1.20x.
- **Resultados de patinaje medidos en Play Mode (`AyniLocomotionProbe.MeasureSlide`)**:
  - Stick al 20 % (caminar lento): **5 %** de patinaje (Yari 0.97 m/s · pies 0.93 m/s · Animator 0.93x).
  - Stick al 45 % (caminar medio): **0 %** de patinaje (Yari 1.09 m/s · pies 1.10 m/s · Animator 1.00x).
  - Stick al 70 % (caminar rápido): **0 %** de patinaje (Yari 1.24 m/s · pies 1.24 m/s · Animator 1.18x).
  - Stick al 100 % (correr): **56 %** de patinaje (Yari 4.60 m/s · pies 2.03 m/s · Animator 1.00x). Bajó significativamente del 68 % anterior que daba el trote suave.

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
