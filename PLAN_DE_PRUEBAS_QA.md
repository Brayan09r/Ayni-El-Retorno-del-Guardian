# 🧪 PLAN DE PRUEBAS DE CALIDAD (QA) Y MATRICES DE BALANCEO
# 🌄 AYNI: EL RETORNO DEL GUARDIÁN (`v0.8.0`)

> **Documento de Aseguramiento de Calidad (QA), Balanceo Matemático y Verificación Funcional**  
> **Motor:** Unity 6 (`6000.3.23f1`) · **Herramientas de Sondeo:** `AyniPlaytestProbe`, `AyniLocomotionProbe`, `AyniAgentBridge`

---

## 📌 1. OBJETIVO Y ALCANCE DEL PLAN DE PRUEBAS

El presente documento establece el protocolo de aseguramiento de calidad (QA), las matrices de balanceo matemático y los casos de prueba funcionales aplicados al videojuego **AYNI: El Retorno del Guardián (`v0.8.0`)**. Su objetivo es verificar la estabilidad técnica, la precisión del sistema de combate marcial **Rumi Maki**, la fluidez del parkour contextual (`YariTraversal`), el sistema de iluminación dinámica andina (`AyniDynamicLighting`), la ausencia de deformaciones esqueléticas en animaciones procedurales y el correcto funcionamiento del recorrido completo del Nivel 1 tanto en compilación de escritorio como en navegador web (**WebGL / Unity Play**).

---

## ⚖️ 2. MATRICES DE BALANCEO MATEMÁTICO

### 2.1. Curva de Envejecimiento del Talismán Sagrado "Illa" ([`IllaTalismanSystem.cs`](Assets/Scripts/Core/IllaTalismanSystem.cs))

El personaje principal (**Yari**) inicia la partida con **20 años** y una edad límite de **75 años**. Cada derrota incrementa el contador consecutivo de muertes (`deathCounter++`) y suma `CurrentAge += deathCounter` años a su edad actual. Perdonar a un rival o jefe mediante el **Juicio Ayni** invoca `DecreaseDeathCounter()`, reduciendo en `1` el contador acumulado.

| Etapa Vital (`AgeStage`) | Rango de Edad | Multiplicador de Daño | Multiplicador de Velocidad | Multiplicador de Salud Máxima | Salud Efectiva (Base 100 HP) | Velocidad Carrera (Base 3.6 m/s) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Juventud (`Youth`)** | 20 – 35 años | `1.00×` | `1.15×` (+15%) | `1.00×` (100%) | **100 HP** | **4.14 m/s** |
| **Madurez (`Prime`)** | 36 – 55 años | `1.25×` (+25%) | `1.00×` (Base) | `0.85×` (-15%) | **85 HP** | **3.60 m/s** |
| **Maestro Anciano (`Elder`)** | 56 – 75 años | `1.60×` (+60%) | `0.85×` (-15%) | `0.60×` (-40%) | **60 HP** | **3.06 m/s** |

#### Progresión Acumulada de Edad por Muertes Consecutivas (Sin Purificación Ayni)
| N° de Muerte Consecutiva | Valor de `deathCounter` | Años Perdidos en esa Caída | Edad Resultante de Yari | Etapa Vital Activa |
| :---: | :---: | :---: | :---: | :--- |
| Inicio | `0` | `0` | **20 años** | `Youth` |
| 1ª Muerte | `1` | `+1` | **21 años** | `Youth` |
| 2ª Muerte | `2` | `+2` | **23 años** | `Youth` |
| 3ª Muerte | `3` | `+3` | **26 años** | `Youth` |
| 4ª Muerte | `4` | `+4` | **30 años** | `Youth` |
| 5ª Muerte | `5` | `+5` | **35 años** | `Youth` |
| 6ª Muerte | `6` | `+6` | **41 años** | `Prime` (Cambio de etapa) |
| 7ª Muerte | `7` | `+7` | **48 años** | `Prime` |
| 8ª Muerte | `8` | `+8` | **56 años** | `Elder` (Cambio de etapa) |
| 9ª Muerte | `9` | `+9` | **65 años** | `Elder` |
| 10ª Muerte | `10` | `+10` | **75 años** | **Muerte Definitiva (`OnTrueDeath`)** |

---

### 2.2. Parámetros de Combate, Defensa y Estructura ([`YariCombatController.cs`](Assets/Scripts/Player/YariCombatController.cs) y [`StructureSystem.cs`](Assets/Scripts/Combat/StructureSystem.cs))

| Parámetro Técnico | Valor en Código | Comportamiento y Regla de Negocio |
| :--- | :---: | :--- |
| **Daño Base Ataque Ligero** | `18.0` HP | Cadena rápida de hasta 4 golpes (`lightStrikeSpeed = 1.3×`). |
| **Daño Base Ataque Pesado** | `35.0` HP | Golpes de ruptura (codazo, uppercut, cabezazo, patada frontal, empujón a `1.2×`). |
| **Cancelación de Cola de Golpe** | Post-impacto | Al terminar la ventana de impacto, pulsar Guardia o mover el stick cancela la cola de recuperación del clip. |
| **Ventana de Desvío (*Parry*)** | `0.22 s` | Si se activa Guardia dentro de `0.22 s` antes del golpe, anula el daño e inflige `35` de postura al rival. |
| **Ventana Esquiva (*Duck / Jump*)** | `0.45 s` | Invulnerabilidad en el sitio ante ataques altos (`Duck`) o barridos bajos (`Jump`). |
| **Ventana Balanceo (*Sway L/R*)** | `0.36 s` | Invulnerabilidad lateral ante cualquier ataque (incluidos dardos de cerbatana). |
| **Bono de Contraataque** | `1.60×` (+60%) | Durante `0.80 s` tras una esquiva o *Parry*, el siguiente golpe multiplica por `1.6` su daño a la postura. |
| **Recuperación de Estructura** | `18.0 pts/s` | Se inicia tras `1.20 s` sin recibir daño en la barra de postura. |
| **Duración de Postura Rota** | `4.00 s` | Tiempo en estado `Stunned` para rivales comunes (`HoldBroken = true` indefinido en Juicio de Jefe). |
| **Selección Direccional con Stick** | `6.0 m` / `55°` | Al atacar inclinando el stick/WASD, Yari dirige el golpe al rival dentro de ese cono. |
| **Patada Hacia Atrás (`Atk_BackKick`)** | `2.6 m` / `>115°` | Si el rival elegido está a la espalda de Yari, lanza una patada trasera sin girarse previamente. |
| **Inercia de Locomoción (`v0.8.0`)** | `20 m/s²` / `26 m/s²` | Aceleración gradual (`20 m/s²`) y frenada con cambio brusco de dirección (`26 m/s²`). |

---

### 2.3. Balanceo de Rivales y Fases del Jefe Amaru ([`EnemyController.cs`](Assets/Scripts/Enemy/EnemyController.cs) y [`AmaruHunter.cs`](Assets/Scripts/Enemy/AmaruHunter.cs))

| Entidad / Fase | Salud Máxima (HP) | Estructura Máxima | Daño Base | Cooldown de Ataque | Velocidad | Mecánicas Especiales |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **Rastreador (`K1`)** | `150 HP` | `100 pts` | `15 HP` | `2.00 s` | `3.20 m/s` | Aproximación ágil en el Puesto de Chasquis. |
| **Saqueador (`K2`)** | `150 HP` | `100 pts` | `15 HP` | `2.00 s` | `3.20 m/s` | Combate cerrado en la Kancha de los Tejedores. |
| **Guardia del Tambo (`K3`)** | `150 HP` | `100 pts` | `15 HP` | `2.00 s` | `3.20 m/s` | Defensa de patio en el Tambo del Camino. |
| **Cazador de Élite (`K4`)** | `150 HP` | `100 pts` | `15 HP` | `2.00 s` | `3.20 m/s` | Custodio de la Portada del Cazador. |
| **Jefe: Amaru (Fase 1)** | `380 HP` | `150 pts` | `15 HP` | `2.00 s` | `3.20 m/s` | Combate cuerpo a cuerpo; al vaciar HP dispara `PhaseTransitionRoutine`. |
| **Jefe: Amaru (Fase 2 · Desatado)** | `460 HP` | `150 pts` | `18 HP` (`×1.2`) | `1.44 s` (`×0.72`) | `3.68 m/s` (`×1.15`) | Aura de brasas, salto atrás de `5.5 m` y ráfagas de **3 dardos envenenados**; abre **Juicio Ayni** al `≤ 40%` HP. |

---

## 🦶 3. AUDITORÍA DE LOCOMOCIÓN Y MEDICIÓN ANTI-PATINAJE

Mediante la herramienta [`AyniLocomotionProbe.MeasureSlide`](Assets/Editor/AyniLocomotionProbe.cs) y `TraceSpeed` se auditó en tiempo real la correspondencia entre el desplazamiento del `CharacterController`, la curva de aceleración/frenada y la cobertura real de los pies en los clips de Mixamo horneados en `YariAttackTimings.asset`:

| Prueba de Movimiento | Cobertura del Clip a `1×` | Tope de Cadencia (`maxCadence`) | Velocidad Medida (Illa Joven `1.15×`) | Deslizamiento / Patinaje Medido | Criterio de Calidad |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Caminar (Stick al 50%)** | `1.07 m/s` (1.17 s) | `0.70× – 1.30×` | `1.23 m/s` | **1 %** | Excelente (`< 10%`) |
| **Correr (Stick a fondo / Teclado)** | `3.70 m/s` (0.53 s) | `1.05×` | `4.14 m/s` | **2 % – 9 %** | Excelente (`< 10%`) |
| **Sprint (`RT` / `L3` / `Shift`)** | `3.96 m/s` (0.53 s) | `1.30×` | `5.60 m/s` | **3 % – 9 %** | Excelente (`< 10%`) |
| **Caminata Agachado (`C` / `B`)** | `0.95 m/s` (1.03 s) | `1.40×` | `1.27 m/s` | **~10 %** | Aceptable (`≤ 10%`) |
| **Fijado Lateral Izquierdo (`Strafe L`)** | `3.01 m/s` (0.67 s) | `1.30×` | `2.69 m/s` | **5 %** | Excelente (`< 10%`) |
| **Fijado Lateral Derecho (`Strafe R`)** | `2.37 m/s` (0.67 s) | `1.30×` | `2.76 m/s` | **3 %** | Excelente (`< 10%`) |
| **Fijado Hacia Atrás (`Walk Back`)** | `1.67 m/s` (0.80 s) | `1.30×` | `2.33 m/s` | **10 %** | Aceptable (`≤ 10%`) |
| **Fijado Hacia el Rival** | `3.70 m/s` (0.53 s) | `1.05×` | `4.03 m/s` | **4 %** | Excelente (`< 10%`) |

---

## ✅ 4. MATRIZ DE CASOS DE PRUEBA FUNCIONALES (TEST CASES)

| ID | Módulo / Componente | Descripción del Caso de Prueba | Pasos de Ejecución | Resultado Esperado | Estado |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **TC-01** | `AyniPrologue` / `AyniPrologueCast` | Ejecución y omisión del prólogo *"La Noche de las Cenizas"* con elenco en escena | 1. Iniciar Play Mode con prólogo activo.<br>2. Verificar presencia del General Sayri, su escolta y fuego en el puente.<br>3. Verificar láminas visuales del Pacto y Despertar.<br>4. Reiniciar y pulsar `Tab`/`Esc`/`Menu`. | Se observan Sayri y sus guardias sobre el puente, las tarjetas visuales del pacto con la Illa y al omitir se posiciona a Yari directamente al inicio del camino. | **PASS** |
| **TC-02** | `AyniInput` / `AyniTutorial` | Conmutación dinámica entre Teclado/Ratón y Mando de Xbox + apertura en primer combate | 1. Avanzar hasta la primera kancha.<br>2. Verificar apertura automática del tutorial en el primer encuentro.<br>3. Presionar un botón del mando Xbox. | El tutorial se abre al entrar en combate por primera vez y `AyniInput.UsingGamepad` cambia todas las leyendas en caliente a botones de Xbox. | **PASS** |
| **TC-03** | `YariTraversal` | Parkour contextual estilo Sifu (Salto de valla, Trepada y Salto de hueco) | 1. Pulsar `Espacio` / `A` en terreno llano sin obstáculos.<br>2. Pulsar `Espacio` / `A` frente a tronco o murete bajo (`0.3–1.3 m`).<br>3. Pulsar frente a cornisa media (`1.3–2.4 m`). | En llano no salta en vacío; frente a tronco bajo ejecuta `Traversal_Vault` (`0.55 s`) apoyando la mano; frente a cornisa ejecuta `Traversal_Climb` (`0.78 s`). | **PASS** |
| **TC-04** | `AyniAbyssRescue` | Muerte por caída a la garganta y rescate de la Illa | 1. Saltar hacia el cañón profundo (`y < -3 m`). | La cámara permanece arriba mostrando `Fall_Back` a cámara lenta, suena el chapuzón en el río y Yari resucita en el último suelo firme sumando años. | **PASS** |
| **TC-05** | `YariCombatController` | Desvío Perfecto (*Parry*), cancelación de recuperación y bono de contraataque | 1. Lanzar un golpe ligero y pulsar Guardia inmediatamente tras el impacto.<br>2. Ejecutar *Parry* (`< 0.22 s`) y contraatacar en `< 0.8 s`. | La guardia cancela la cola de recuperación del golpe al instante; el *Parry* anula el daño, inflige `35` de postura al rival y otorga bono `×1.6`. | **PASS** |
| **TC-06** | `AyniAnimationForge` / `FootIK` | Esquivas en el sitio (`Duck`, `Jump`, `Sway`) con etiqueta `SinIK` | 1. Mantener Guardia sobre pendiente.<br>2. Ejecutar `Guardia + Abajo`, `Arriba`, `Izquierda` y `Derecha`. | Yari no se desplaza del sitio; la etiqueta `SinIK` inhibe `FootIK` y `AndeanCombatStanceModifier`, evitando encogimiento de piernas. | **PASS** |
| **TC-07** | `YariCombatController` | Elección direccional de rival con stick y patada trasera | 1. Rodearse de dos rivales.<br>2. Inclinar el stick hacia el rival a la espalda (`>115°`, `<2.6 m`) y pulsar ataque ligero. | Yari ejecuta `Atk_BackKick` hacia atrás sin girarse primero y posteriormente encara al enemigo golpeado. | **PASS** |
| **TC-08** | `AyniEncounterSite` / `AyniDynamicLighting` | Activación de las 4 Kanchas Incas (`K1` a `K4`) y transición lumínica | 1. Avanzar por el Qhapaq Ñan y entrar al patio de cada kancha.<br>2. Derrotar a los rivales del recinto. | Los rivales se activan al cruzar el umbral, la iluminación transiciona a *Combate en Kancha*, suenan los tambores y al despejar vuelve a *Hora Dorada*. | **PASS** |
| **TC-09** | `EnemyController` / `AyniBossPhaseDirector` | Transición cinemática a la Segunda Fase de Amaru e iluminación de brasas | 1. Vaciar los `380 HP` de la primera barra de Amaru en la plaza principal. | Amaru cae a cámara lenta, la cámara orbita 360°, ruge (`Gen_Roar`), el cielo pasa a *FaseDosBrasas* (`#FF8C42`), gana aura de fuego y rellena `460 HP`. | **PASS** |
| **TC-10** | `AmaruHunter` | Ráfaga de dardos envenenados en Fase 2 | 1. En Fase 2, mantener distancia media frente a Amaru. | Amaru salta `5.5 m` atrás, brilla en verde (`0.5 s`) y dispara 3 dardos; si impactan, aplican veneno progresivo y viñeta verde en pantalla. | **PASS** |
| **TC-11** | `AyniJudgment` / `AyniPurificationManager` | Resolución del Juicio Ayni por el camino de la Venganza (`F` / `B`) | 1. En Fase 2 con Amaru `≤ 40%` HP, romper su postura (`150 pts`).<br>2. Pulsar `F` / `B` (Venganza). | El tiempo baja a `0.15×`; Yari ejecuta un remate letal, la iluminación pasa a *OcasoVenganza*, cae ceniza sobre el valle y se muestra el resumen final. | **PASS** |
| **TC-12** | `AyniReforestation` / `AyniReforestationRevival` | Resolución del Juicio Ayni por el camino del Perdón (`X` / `A`) y ODS 15 | 1. En el Juicio Ayni de Amaru, pulsar `X` / `A` (Perdón Ayni). | Amaru es desarmado sin morir, `deathCounter` baja en `1`, la luz pasa a *AmanecerAyni*, llueve, brota el bosque, reverdecen los 61 árboles quemados y aparece el **ODS 15**. | **PASS** |
| **TC-13** | `AyniAudio` / `AyniFootsteps` | Reproducción de los 42 efectos de sonido y pasos por zancada | 1. Caminar, correr, golpear, bloquear y entrar al agua o menús. | Todos los clips `.wav` de `Resources/AyniAudio/` se reproducen sin errores de referencia nula y los pasos coinciden con cada apoyo de pie. | **PASS** |
| **TC-14** | `AyniPauseMenu` / `AyniHud` | Persistencia de volúmenes y panel de controles oculto por defecto (`H` / Cruceta Arriba) | 1. Iniciar partida y verificar que el panel de controles inicia oculto.<br>2. Pulsar `H` / `Cruceta ↑` para desplegarlo.<br>3. Ajustar volúmenes en `Esc` y reiniciar. | El HUD luce limpio mostrando solo la pastilla `[H] Controles`; al pulsarla se despliega la tarjeta y los volúmenes persisten en `PlayerPrefs`. | **PASS** |
| **TC-15** | `AyniWebBuild` | Ejecución en navegador WebGL (Unity Play) | 1. Abrir la compilación WebGL en navegador.<br>2. Verificar texturas de vegetación (`Resources/AyniVegetacion`) y controles. | La descarga comprimida Gzip (~68 MB) carga correctamente y las hojas de queñua/cantuta mantienen su recorte alfa sin cuadros opacos. | **PASS** |
| **TC-16** | `AyniRumble` / `ThirdPersonSifuCamera` | Sacudida direccional de cámara y vibración XInput en impactos | 1. Conectar mando Xbox en Windows.<br>2. Ejecutar golpe ligero, golpe pesado, *Parry* y ruptura de postura. | La cámara aplica un *punch* direccional con ruido Perlin y los motores izquierdo/derecho del mando vibran con intensidades diferenciadas. | **PASS** |
| **TC-17** | `AyniFireLight` / `AyniRayosSol.shader` | Parpadeo orgánico de 24 antorchas, 5 fogatas y 6 rayos volumétricos | 1. Recorrer la aldea de Urqupampa desde el inicio hasta la plaza de Amaru. | Las 29 luminarias oscilan suavemente con ruido Perlin sin caer en *frame drops* y los 6 haces solares se desvanecen al acercar la cámara. | **PASS** |
| **TC-18** | Ambientación y Fauna Andina | Verificación de llamas, pumas delimitadores y limpieza cultural del mapa | 1. Explorar los campos abiertos de la aldea y los bordes del sendero. | Se observan llamas en las praderas, pumas custodiando los bordes para evitar atajos fuera de ruta, ruinas líticas y ausencia total de tigres o columnas grecorromanas. | **PASS** |

---

## 🤖 5. PROTOCOLO DE PRUEBAS AUTOMATIZADAS (`AyniAgentBridge`)

El proyecto dispone de un protocolo de verificación reproducible mediante comandos enviados a `UserSettings/AyniAgent/cmd.txt`:

```text
refresh
call Ayni.Editor.AyniStoryMenu.SetPrologue 0
play
wait 3
call Ayni.Core.AyniInput.Simulate Skip 0.3
call Ayni.Editor.AyniLocomotionProbe.Traversal
wait 3
call Ayni.Editor.AyniPlaytestProbe.PlaceYariNearBoss 3 0
call Ayni.Editor.AyniPlaytestProbe.HitBoss 400 0
wait 7
call Ayni.Editor.AyniPlaytestProbe.HitBoss 300 999
wait 3
call Ayni.Core.AyniInput.Simulate Mercy 0.15
wait 8
screenshot verificacion_ods15_reforestacion
stop
```

---
*Documento de Aseguramiento de Calidad (QA) — Proyecto Ayni: El Retorno del Guardián (`v0.8.0`).*
