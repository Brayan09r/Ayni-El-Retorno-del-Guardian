# Historial de versiones — AYNI: El Retorno del Guardián

Formato del equipo (ver `COLLABORATORS.md`): **MAYOR.MENOR.PARCHE**
- **MAYOR**: hitos (Alpha, Beta, demo jugable).
- **MENOR**: mejoras de jugabilidad, sistemas, niveles.
- **PARCHE**: arreglos, ajustes de balance, cambios menores.

La versión en uso está en `Assets/Scripts/Core/AyniVersion.cs`. El HUD la muestra junto al título.
Al publicar una versión en `main` se marca con un tag: `git tag -a v0.6.0 -m "..."` y luego `git push origin v0.6.0`.

---

## v0.15.0 — Voces en los subtítulos · 21 de noviembre de 2026

- **Los subtítulos tienen voz:** 78 líneas habladas (narradora, Amaru, el Capitán del Tambo, la tejedora y Yari) en
  `Assets/Resources/AyniVoces/v_xxxxxxxx.ogg`. `AyniScreenFX.Caption` busca la voz de cada texto y alarga el subtítulo si la voz
  dura más; las líneas sin voz se siguen mostrando igual y avisan en la consola (`[Ayni Voz] Sin voz`).
- `Scripts/Core/AyniVoice.cs`: el nombre del archivo es un hash FNV-1a del texto normalizado (sin etiquetas, sin el prefijo
  `NOMBRE:`, solo letras y números), así que cambiar comillas o colores no rompe la voz, pero cambiar las palabras sí pide
  regenerarla. Una voz corta la anterior, y el ambiente baja al 55 % mientras alguien habla (`AyniAudio.Duck`).
- **Yari responde:** a Amaru en cada aparición del acecho y en el puente («Y yo huelo en ti el humo de mi bosque, Amaru.»), al
  capitán cuando entra en la pelea y cuando lo perdona, a la tejedora al recibir el chumpi, en la apacheta y a Amaru vencido
  («Sayri te mintió, como nos mintió a todos.»). Lo que dice a Amaru tras el tambo depende de si perdonó o ejecutó al capitán.
- Diálogos encadenados: cada línea espera a que termine la voz anterior (`AyniVoice.HoldFor`), y la ofrenda de la apacheta espera
  a que Yari acabe su oración.
- Prólogo: la primera narración entra antes y «En el puente sobre la garganta…» pasa al plano del título de Sayri, para que las
  voces no se pisen.
- Voces sintéticas generadas con Piper (`Herramientas/Audio/generar_voces.py` a partir de `voces_guion.txt`). Los modelos se
  descargan solos en `Herramientas/Audio/modelos_voz/` (ignorada en git). Licencias en `CATALOGO_RECURSOS_Y_LICENCIAS.md`.

---

## v0.14.2 — Fauna: galope sin estirones · 21 de noviembre de 2026

- **La panza ya no se estira al correr:** el "Run" del FBX arquea y estira mucho la columna en cada salto; con las patas desfasadas
  el vientre quedaba tirante. El galope generado (`AyniFauna3DSetup.BuildGallop`) conserva ahora solo un 35 % de la flexión de
  Pelvis y Spine, un 45 % del giro y un 55 % del vaivén de Root, y un 85 % del recorrido de hombros y caderas. Los giros se mezclan
  como cuaterniones hacia la pose media del ciclo.
- La velocidad de la animación de carrera se ajusta a lo que avanza de verdad el galope (llama 1,3 m/s y vicuña 0,6 m/s a escala 1,
  hasta 3 veces más rápida): las pezuñas no patinan. Por eso las vicuñas huyen algo más despacio.
- `AyniFaunaProbe`: escribe sus resultados también en el registro del puente, y `LoggerStatus` comprueba la consola.

---

## v0.14.1 — Fauna: galope, pastar y alas del cóndor · 21 de noviembre de 2026

- **Llamas y vicuñas galopan:** en el "Run" del FBX las dos patas delanteras iban juntas y las dos traseras también, como troncos.
  `AyniFauna3DSetup.BuildGallop` crea `Assets/Art/Fauna/Clips/{Llama,Vicuna}_Run_Galope.anim` con las patas derechas adelantadas un
  22 % del ciclo, y el estado "Run" lo usa (se rehace con *Ayni > Fauna Andina > Rehacer galopes* o regenerando la fauna).
- **Pastar sin morderse el cuello:** antes la cabeza seguía todo el giro del cuello y quedaba doblada hacia el pecho. Ahora el cuello
  baja recto, por delante de las patas, y la cabeza mantiene su orientación y solo inclina el hocico hacia el ichu.
- **Cóndor posado con las alas recogidas** (`AyniCondorWings`): el FBX solo trae poses de alas abiertas; en la jaula se pliegan pegadas
  al lomo y se abren al despegar.
- **El cóndor aletea más:** tandas de 3 aletazos cada 3,5 a 8 s (antes 2 cada 9 a 18 s, y casi no se veía).

---

## v0.14.0 — El Capitán del Tambo y la apacheta · 21 de noviembre de 2026

Cuarto paso del gancho del Nivel 1: en el Tambo del Camino hay que decidir el destino de un capitán, y la decisión se nota en el camino
y en el duelo contra Amaru.

**El Capitán del Tambo y su mini Juicio** (`AyniTamboCaptain`)
- Un capitán con penacho rojo y disco de oro espera junto a la salida del tambo mirando cómo pelean sus guardias ("el capitán observa"
  en el panel de objetivos). Cuando caen todos, entra él solo: más vida, más aguante y golpes algo más fuertes.
- No muere a golpes: al romperle la postura llega un **Juicio Ayni** como el de los jefes, a cámara lenta y con sus propios textos.
  - **Ayni:** lo perdona. Cuenta por qué servía a Sayri, le da a Yari la chicha del tambo (vida llena) y se lleva a su hermano, uno de los
    Cazadores de la Portada del Cazador: **un rival menos** en la última kancha.
  - **Venganza:** lo remata. Yari se queda su **champi de bronce: +20 % de daño contra Amaru**.
- Amaru, al asomarse tras el Tambo, comenta lo que hiciste con su capitán.

**La apacheta del camino** (`AyniApacheta`)
- Un montículo de piedras con lanas de colores junto al camino, al salir del Tambo. Yari deja una piedra manteniendo `V` / `LT`.
- **Bendición del Apu:** vida llena y **la próxima caída no cobra años** (`IllaTalismanSystem.ProtectedFalls`).
- Si perdonaste al capitán, está arrodillado ante la apacheta: ya dejó la suya. Si lo ejecutaste, la piedra rueda cuesta abajo y no hay
  bendición.

**Interfaz**
- Fichas nuevas sobre el nombre de Yari: "Capitán · un Cazador menos", "Champi · +20 % de daño a Amaru" y "Apu · tu próxima caída no
  cuesta años". El panel de objetivos recuerda la apacheta camino de la Portada, y la tarjeta final dice qué pasó con el capitán y la ofrenda.
- `AyniJudgment.Open` abre el Juicio para un rival que no es jefe; `EnemyController.JudgedLikeBoss` e `IncomingDamageMultiplier`.

---

## v0.13.1 — Fauna 3D: patas que pisan bien · 21 de noviembre de 2026

Arreglos de movimiento en los animales riggeados (llama, alpaca, vicuña, vizcacha y cóndor de `Assets/Art/Fauna`).

- **Llamas, alpacas y vicuñas caminaban "patinando":** en los clips `Walk` y `Run` del FBX las patas apoyadas avanzan hacia delante
  (medido con `AyniFaunaProbe.GaitSpeed`). Esos dos estados se reproducen ahora al revés (`speed = -1` en `AC_Fauna_Llama` y
  `AC_Fauna_Vicuna`, y `AyniFauna3DSetup` lo deja así si se regeneran).
- **La velocidad de la animación sigue a la del animal** según lo que avanza cada clip (escalado por el tamaño del modelo), y un animal
  no corre más de lo que dan sus patas (vicuñas y llamas huyen algo más despacio, sin deslizarse).
- **Pastar:** el clip `Graze` doblaba el cuello hacia atrás, sobre el lomo. Ahora parte de `Idle` y el cuello baja por código hasta el
  ichu, con un pequeño mordisqueo; al mirar alrededor giran la cabeza.
- **Ya no dan vueltas en círculo:** si el destino queda de lado, primero giran casi en el sitio y luego aceleran.
- En cuestas fuertes se inclinan menos (16°) y de forma suave: antes se veían echados hacia atrás.
- **Cóndor:** el modelo está posado, con el cuerpo vertical, y volaba "de pie". En vuelo se tiende 50° hacia delante
  (`AyniCondorModel.SetPitch`). El cóndor de la jaula se va tendiendo al despegar y entra en su giro sobre la plaza sin saltar 100 m.
- **Vizcachas:** cada salto reproduce el clip `Hop` completo al ritmo del salto (despegue y aterrizaje coinciden).
- **Animales de las jaulas:** las vicuñas liberadas corren al ritmo de sus patas y la cría de puma ajusta su carrera a su tamaño.
- Herramienta: `AyniFaunaProbe` (editor) graba animales en Play (`Watch`), mide la marcha de los clips (`GaitSpeed`) y la posición de
  los huesos (`ClipBones`).

---

## v0.13.0 — El quipu de los chasquis, las tejedoras y las trampas de Amaru · 21 de noviembre de 2026

Tercer paso del gancho del Nivel 1: lo que pasa en las dos primeras kanchas también cambia el duelo contra Amaru.

**Puesto de Chasquis: el quipu** (`AyniQuipuChase`)
- Al empezar la pelea, un Rastreador agarra el quipu de los mensajeros (se le ven las cuerdas de colores) y huye por el camino a avisar
  a Amaru. Un rombo dorado lo marca y el panel de objetivos dice cuántos metros le faltan. Esquiva lo que tiene delante.
- Si Yari lo alcanza y le pega, el Rastreador se gira a pelear; al derrotarlo, Yari recupera el quipu.
- **Quipu recuperado:** en el duelo, mientras Amaru apunta cada dardo, un anillo verde se cierra sobre su pecho y marca el momento justo
  de esquivar ("¡Esquiva!", y qué dardo de la ráfaga es) (`AyniDartReadCue`).
- **Si escapa** (60 m o 30 s sin que lo toquen): Amaru queda avisado y siembra su plaza de **trampas de lazo con estacas** tapadas con
  hojas (`AyniHunterTraps`). Pisar una es un golpe bajo: se evita con el saltito (Guardia + arriba). Al empezar el duelo, Amaru lo recuerda.

**Kancha de los Tejedores: la casa en llamas** (`AyniWeaversFire`)
- Una casa arde desde el principio (llamas en el techo y columna de humo). Al entrar en la kancha empieza una cuenta atrás de 55 s:
  hay que romper la tranca de la puerta manteniendo `V` / `LT` (si golpean a Yari, pierde la mitad de lo avanzado).
- **A tiempo:** las tejedoras le dan un chumpi y el veneno de los dardos de Amaru le dura la mitad (`AmaruHunter.PoisonSecondsMultiplier`).
- **Tarde:** el fuego se lleva los telares (sin recompensa).

**Lo que llevas** (`AyniRewardsHUD`)
- Fichas encima del nombre de Yari con lo ganado para el duelo (quipu, chumpi, cóndor, puma) y, en rojo, si Amaru está avisado.
- La tarjeta final dice qué pasó con el quipu y con las tejedoras.

**Otros**
- `AyniPathRewards`: dónde se guarda lo ganado en el camino. `AyniHoldPrompt`: aviso de "mantén el botón" y marcas en pantalla compartidos.
- `AmaruHunter` expone el progreso del aviso de cada dardo (`WarningProgress`, `DartNumber`, `Shooter`).
- **Arreglo:** la presentación de Amaru no se podía saltar con `Tab` / `Esc` / `Menu` (`AyniBossIntro` miraba la tecla después de
  dar la escena por empezada). Ahora sí.
- Para probar: `AyniQuipuChase.TestEscape` / `TestRecover`, `AyniWeaversFire.TestGoToDoor` / `TestRescue`,
  `AyniHunterTraps.TestFaceTrap` (y andar hacia delante), `AyniObjectivesProbe.KeepYariAlive` (Yari no pierde vida unos segundos).

---

## v0.12.0 — Jaulas de la Portada del Cazador y animales aliados · 21 de noviembre de 2026

Segundo paso del gancho del Nivel 1: lo que haces en la última kancha cambia el duelo contra Amaru.

**Los trofeos vivos de Amaru** (`AyniCaptiveCages`)
- En el patio de la Portada del Cazador hay tres jaulas de palos atados con soga (hechas por código): una cría de puma, un cóndor y dos vicuñas.
- Se abren manteniendo `V` / `LT` junto a la puerta (1,4 s). Si un rival golpea a Yari mientras tanto, el nudo se le escapa y pierde la
  mitad de lo avanzado: abrirlas en plena pelea es un riesgo que se elige. Un rombo de jade marca cada jaula cerrada.
- Al abrirlas, la cría y las vicuñas huyen por la entrada de la kancha (lejos de Amaru) y el cóndor sale aleteando y se queda planeando
  sobre la plaza del jefe.
- **Vicuñas:** en el momento, la Pachamama sana a Yari y la Illa le devuelve 2 años (`IllaTalismanSystem.Rejuvenate`, `YariCombatController.Heal`).

**Ayni con la fauna en el duelo** (`AyniFreedAllies`)
- **La puma madre:** si liberaste a la cría, cuando Amaru empieza su segunda fase baja del cerro, se le lanza encima y le rompe la
  postura (momento para castigarlo). Luego vuelve al monte.
- **El cóndor:** si lo liberaste, cada ráfaga de dardos de Amaru llega con su aviso (un bufido y aletazos) y cada dardo tarda 0,35 s
  más en salir (`AmaruHunter.ExtraTelegraph`).
- La tarjeta final cuenta los animales liberados. El panel de objetivos muestra las jaulas abiertas en la Portada.

**Otros**
- Nueva acción `Interact` en `AyniInput` (`V` / `LT`).
- `AyniVisualClone`: copia visual (sin scripts ni colisiones) de un personaje de la escena; la usan el acecho de Amaru, la cría y la puma madre.
- Sonidos nuevos generados por `generar_sfx.py` (`make_fauna`): `jaula_rota`, `condor_aviso`, `puma_rugido`.
- Para probar: `call Ayni.Editor.AyniObjectivesProbe.GoToCage 0..2`, `call Ayni.World.AyniCaptiveCages.TestFree 0..2`,
  `call Ayni.Story.AyniFreedAllies.TestPuma`.

---

## v0.11.0 — Objetivos de misión y Amaru acechando el camino · 21 de noviembre de 2026

Primer paso para que el camino hasta el jefe tenga gancho: el jugador siempre sabe qué hacer y siente que Amaru lo caza.

**Objetivos de misión** (`AyniLevelObjectives`)
- Panel arriba a la derecha con el objetivo actual: ir a la siguiente kancha, liberarla, llegar al puente y derrotar a Amaru.
  Debajo, los rivales que quedan en pie (cuenta los refuerzos) o la distancia al destino, y cuántas kanchas van liberadas (x / 4).
- Rombo de oro en pantalla sobre el destino, con los metros; si queda fuera de la vista se pega al borde.
- Al entrar en una kancha sale su nombre y el motivo de la pelea ("Los Saqueadores queman los telares del ayllu"); al despejarla,
  "… LIBERADO" con una frase de lo que se recupera.
- Se oculta en el prólogo, el tutorial, la presentación del jefe, el Juicio y la cuenta de años al caer.

**Amaru acecha a Yari por el camino** (`AyniHunterStalker`)
- Cuatro apariciones: al echar a andar desde el inicio y tras liberar el Puesto de Chasquis, la Kancha de los Tejedores y el Tambo.
- Amaru se asoma a un cerro por delante (se elige un sitio alto que se vea desde la cámara), ruge, le habla a Yari y se va de un
  salto entre una nube de polvo, clavándole a los pies un dardo de aviso que no hace daño. Un rombo rojo con su nombre lo señala.
- Es una copia del modelo de Amaru sin IA ni colisiones: el jefe de verdad sigue esperando en el puente.
- Si hay pelea cerca espera a que se calme; con el duelo ya empezado no aparece.

**Para probar** (`Editor/AyniObjectivesProbe.cs`)
- `call Ayni.Editor.AyniObjectivesProbe.ClearSite 1` derrota a los rivales de una kancha; `Report` muestra el objetivo y el acecho.
- `call Ayni.Story.AyniHunterStalker.Test 0..3` fuerza una aparición.

## v0.10.0 — Rivales vivos, oleadas, pumas guardianes, Amaru que habla, límites del mapa y música · 17 de noviembre de 2026

Versión para la Feria Interna de Proyectos ISI 2026. Integra lo que el equipo subió del 6 al 12 de noviembre.

**Rivales que ya están ahí, como en Sifu** (`AyniRivalAmbient`, `AyniEncounterSite`)
- Nadie aparece de la nada: los rivales esperan a la vista en su kancha haciendo lo suyo. Uno hace guardia mirando a los lados, otro
  patrulla caminando (nuevo estado `Ambient_Walk` con `Walk_Forward_InPlace`) y otro descansa arrodillado.
- Pelean cuando ven a Yari (delante y sin muros en medio, ~11 m), lo oyen (más lejos si corre), él cruza la portada o lo golpean. El que lo
  descubre se gira y avisa; el resto se suma con unas décimas de diferencia. La barra del rival sale solo cuando ya pelea.

**Peleas más duras y con más gente, como en Sifu** (`EnemyController`, `AyniEncounterSite`, `AyniRoadPatrols`)
- Turnos de ataque: solo dos rivales atacan a la vez; el resto rodea a Yari en guardia a unos metros, sin amontonarse, y espera su turno.
- Oleadas: cuando cae la mitad de una kancha llegan refuerzos corriendo desde la portada (1, 2, 2 y 3 según la kancha).
- Segundo aliento: el rival más duro de cada kancha no se queda en el suelo; cae, brilla en rojo y se levanta furioso con más vida, más
  aguante y golpes más fuertes y seguidos (el HUD lo marca como "FURIOSO").
- Patrullas del camino: entre kancha y kancha una pareja de guardias va y viene por el Qhapaq Ñan y embosca a Yari; el camino ya no
  está vacío.
- Amaru llama a dos cazadores al pasar a su segunda fase, que llegan por la espalda de Yari.

**Pumas guardianes del Kay Pacha** (`PumaTerritory`, `AyniPumaPack`)
- Los pumas ya no mueren ni desaparecen: al "derrotarlos" huyen heridos a su guarida y se recuperan.
- Golpear a un puma enfurece a la manada, como las gallinas de Zelda: los cercanos y hasta tres más que bajan del cerro persiguen a Yari
  un rato y luego se van. "La Pachamama defiende a sus hijos" (ODS 15).

**Límites del nivel** (`AyniLevelBounds`)
- Un pasillo alrededor del Qhapaq Ñan, del inicio al puente: ya no se puede retroceder desde el inicio ni salir del mapa y caer al vacío.
  Cerca del borde sale un aviso y, si Yari insiste, el puma más cercano baja a cortarle el paso. Las quebradas siguen siendo mortales.

**Correr sin tirones** (`YariCombatController`)
- Al girar corriendo la velocidad rota sin perder rapidez (antes perdía ~40 % en cada curva y los pasos iban a cámara lenta); solo la media
  vuelta frena. Con teclado, cambiar de tecla ya no reinicia la carrera desde caminar.

**Música del nivel** (`AyniLevelMusic`, pistas de Suno)
- Tres pistas propias hechas con Suno (camino, combate y Amaru), importadas en streaming. Calla en el prólogo y en el Juicio.
- Como van a tempos distintos, no se mezclan: cuando un rival da la voz el combate entra directo en su primer golpe fuerte; al acabar
  la pelea hay un respiro con viento y el camino sigue por donde se había quedado (se oye la pista entera, no siempre su comienzo); el
  combate también retoma donde iba. El golpe fuerte de la pista de Amaru cae sobre su título.

**Amaru en el puente** (`AyniBossIntro`, `AyniBossBarks`)
- Mientras espera camina de un lado a otro del estribo y mira el camino; al ver llegar a Yari ruge, lo sigue con la mirada y le habla.
- Cada plano de la presentación lleva su frase y baja caminando del estribo. En el duelo provoca a Yari, grita en la segunda fase, se
  burla cuando la Illa le cobra años y flaquea con poca vida (subtítulos con su nombre).

**Recorrido con vida**
- Corriendo contra un tronco o un muro bajo, Yari lo salta solo, sin frenar (trepar sigue pidiendo el botón).
- Las 10 llamas de `Llama.obj` que estaban quietas como estatuas se cambian al empezar por llamas animadas de la fauna de Jhosue.

**Del equipo**
- Junior: menú principal con opciones (audio por canales, pantalla completa, resolución, restablecer), créditos animados y respuesta de los
  botones; puma nuevo de Tripo en lugar del anterior; tronco del O1 restaurado.
- Antonnyy: documentación sincronizada (MainMenu, General Sayri 3D, catálogo y QA).
- Jhosue: fauna andina procedural y atmósfera por clima (abajo), ya colocada en el nivel: 5 cóndores, 22 camélidos y 12 vizcachas y zorros.

**Herramientas**
- Sondas: `TraceRun`, `ScanGround`, `TopDown`, `ListRivals`, `HitNearest`, `WhereIsYari`, `CloseTutorial`, `PlaceCamera`; el puente de
  Unity guarda capturas con supermuestreo (`screenshot nombre 3`).

### Fauna andina y atmósfera por clima (Jhosue, rama `feat/fauna-andina`)

Detalle en `FAUNA_ANDINA.md` y en la sección 5 de `ILUMINACION_ANDINA.md`.

**Atmósfera que acompaña al clima** (`AyniClimateBlend`, `AyniClimateGrade`, `AyniLightMotes`, `AyniCloudShadows`)
- La gradación de color cambia con cada clima: contraste en el combate, tono cobrizo y bloom de brasas en la Fase 2, luz limpia con
  verdes vivos en el Amanecer Ayni y color ceniciento con viñeta de sangre en el Ocaso de Venganza. La Hora Dorada no cambia.
- Motas doradas de polvo en el aire de la tarde y brasas que suben durante la Fase 2 de Amaru.
- Sombras de nubes arrastradas por el viento sobre el valle; el cielo se carga en el Ocaso y se abre en el Amanecer.
- Todo sigue las transiciones de `AyniDynamicLighting` sin modificarlo y se instala solo en Play. Menú `Ayni > Iluminación > En Play`.

**Cóndores sobre el cañón** (`AyniCondorFlight`, `AyniCondorModel`)
- Cinco Kuntur planean en térmicas sobre la garganta del puente, el cañón norte, la aldea y muy alto sobre la plaza de Amaru.
  Collar blanco, alas de ~3 m con plumas primarias abiertas y aleteos ocasionales.

**Rebaños de camélidos** (`AyniCamelidModel`, `AyniGrazingAnimal`)
- Llamas con borlas de lana de colores, alpacas lanudas y vicuñas silvestres pastan, miran y caminan en paso de andadura.
- Alpacas con llama guardiana junto a la Kancha de los Tejedores, caravana de llamas en el Tambo del Camino, rebaño mixto en el
  Puesto de Chasquis y vicuñas lejanas cerca de la Portada del Cazador. Se apartan cuando Yari se acerca (la vicuña desde más lejos).

**Vizcachas y zorros andinos** (`AyniVizcacha`, `AyniCritterModel`)
- Colonias de vizcachas toman sol en rocas y muros de piedra, vigilan a Yari y huyen a saltos.
- Dos zorros (Atoq) rondan los bordes con trote diagonal y olfatean el suelo.

**Herramientas**
- Menú `Ayni > Fauna Andina` para soltar toda la fauna o cada grupo, y para quitarla por completo.
- Todo es decorativo, sin colisionadores y de autoría propia (sin mallas externas). No cambia ningún sistema existente.

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
