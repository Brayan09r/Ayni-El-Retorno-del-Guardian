# 🦙 Nivel 1 — El camino: misiones, decisiones y recompensas

**AYNI: El Retorno del Guardián** · versiones `v0.11.0` a `v0.18.1` · noviembre de 2026

Este documento reúne todo lo que se añadió al Nivel 1 para que el camino hasta Amaru tenga gancho: misiones en cada kancha,
cosas que hacer entre una y otra, decisiones morales y recompensas que **cambian el duelo final**. El detalle técnico de cada
versión está en [`CHANGELOG.md`](CHANGELOG.md); las notas de implementación, en
[`NIVEL_1_PROLOGO_Y_JUGABILIDAD.md`](NIVEL_1_PROLOGO_Y_JUGABILIDAD.md).

---

## 1. La idea

Antes, el Nivel 1 era una fila de peleas hasta el jefe. Ahora sigue la regla del **Ayni** (reciprocidad): lo que Yari da en el
camino, el camino se lo devuelve en el puente.

- **Siempre se sabe qué hacer:** un panel de objetivos y un rombo dorado guían de kancha en kancha.
- **Cada kancha tiene su propia misión**, además de pelear.
- **Todo es opcional**, pero cada cosa que se hace (o se deja de hacer) se nota contra Amaru.
- **Las decisiones pesan:** perdonar o ejecutar al Capitán del Tambo cambia la apacheta, lo que dice Amaru y el duelo.
- **El camino también cuenta una historia:** los quipus de Amaru explican quién era el Cazador antes de servir a Sayri.

---

## 2. Recorrido del nivel

| Tramo | Qué pasa | Si lo haces | Si no |
| :--- | :--- | :--- | :--- |
| **Inicio** | Amaru se asoma a un cerro, ruge y le clava a Yari un dardo de aviso a los pies. | — | — |
| **K1 · Puesto de Chasquis** | Un Rastreador huye con el **quipu de los chasquis** para avisar a Amaru. | Lo alcanzas y lo vences: recuperas el quipu. | Escapa (60 m o 30 s): Amaru siembra su plaza de **trampas de lazo**. |
| | Tres **llamas de carga amarradas** en un corral. | Sueltas la tranca: las llamas vuelven a su ayllu, Yari se cura del todo y recibe **una ofrenda**. | Siguen amarradas (la tarjeta final lo recuerda). |
| **K2 · Kancha de los Tejedores** | Una casa arde con las tejedoras dentro: **55 s** para romper la tranca. | Las salvas: te dan un **chumpi**. | El fuego se lleva los telares. |
| **K3 · Tambo del Camino** | El **Capitán del Tambo** mira pelear a sus guardias y luego entra él. Al romperle la postura llega un **mini Juicio**. | **Ayni:** lo perdonas. Te da chicha (vida llena) y se lleva a su hermano, un Cazador de la Portada. | **Venganza:** te quedas su **champi de bronce**. |
| **Salida del Tambo** | La **apacheta**: Yari deja una piedra. | Con las manos limpias: **bendición del Apu**. | Si ejecutaste al capitán, la piedra rueda cuesta abajo. |
| **K4 · Portada del Cazador** | Los **trofeos vivos de Amaru** en tres jaulas: una cría de puma, un cóndor y dos vicuñas. | Cada animal liberado ayuda (ver §4). | Siguen enjaulados. |
| **En cada kancha** | Un **quipu de Amaru** escondido dentro de una casa. | Los lees; con los cuatro, Yari conoce a **Killa**. | Amaru sigue siendo solo el Cazador. |
| **Entre kanchas** | **Ofrendas a la Pachamama** escondidas fuera del camino y **patrullas** de guardias. | Cada ofrenda cura; cada tres, un año menos. | — |
| **Puente** | **Amaru el Cazador**, en dos fases, y el **Juicio Ayni**. | Perdón: el queñual reverdece el Antisuyo. | Venganza: el bosque sigue en cenizas. |

Mientras tanto, **Amaru acecha** a Yari: se asoma cuatro veces (al empezar y tras liberar K1, K2 y K3), le habla y comenta lo que
hiciste con su capitán. Yari le responde.

---

## 3. Las misiones en detalle

### 3.1. El quipu de los chasquis (K1) · `AyniQuipuChase`
Al empezar la pelea, un Rastreador agarra el quipu (se le ven las cuerdas de colores) y huye por el camino. Un rombo lo marca y el
panel dice cuántos metros le faltan. Si Yari lo alcanza y le pega, se gira a pelear; al vencerlo, el quipu es suyo. La distancia se
mide por el camino recorrido, no en línea recta.

### 3.2. Las llamas del chasqui (K1) · `AyniLlamaCorral`
Un corral de palos en el patio, entre el fogón y la casa de la izquierda, con tres llamas amarradas a una estaca. Yari suelta la
tranca manteniendo `V` / `LT` (1,3 s). Se puede hacer en plena pelea, pero cada golpe que recibe Yari le quita la mitad de lo
avanzado. Las llamas salen al galope por la entrada de la kancha. El panel muestra «llamas amarradas» o «llamas libres», y si Yari se
va sin soltarlas se lo recuerda.

### 3.3. La casa en llamas (K2) · `AyniWeaversFire`
La cuenta atrás de 55 s empieza al entrar en la kancha. La tranca se rompe manteniendo `V` / `LT` (1,6 s); los golpes también quitan
la mitad de lo avanzado. Hay que elegir entre despejar a los Saqueadores o arriesgarse en la puerta.

### 3.4. El Capitán del Tambo y la apacheta (K3) · `AyniTamboCaptain`, `AyniApacheta`
El capitán (penacho rojo y disco de oro) no muere a golpes: al romperle la postura se abre el Juicio con sus propios textos. Su
destino decide la apacheta: si lo perdonaste, está arrodillado junto a ella y ya dejó su piedra; si lo ejecutaste, la apacheta no
acepta la ofrenda de unas manos manchadas de *Hucha*.

### 3.5. Las jaulas de la Portada (K4) · `AyniCaptiveCages`, `AyniFreedAllies`
Tres jaulas de palos atados con soga. Se abren manteniendo `V` / `LT` (1,4 s) con la misma regla de los golpes. La cría y las
vicuñas huyen por la entrada; el cóndor se queda planeando sobre la plaza del jefe.

### 3.6. Los quipus de Amaru (las cuatro kanchas) · `AyniAmaruQuipus`
Uno dentro de una casa de cada kancha (nunca en la que arde), con una luz cálida. Se leen con `V` / `LT` cuando no hay pelea cerca; un
rombo «Quipu» los marca a menos de 16 m. La narradora lee los nudos:

1. **Puesto de Chasquis:** Amaru de niño, cuidando vicuñas en un ayllu del Antisuyo.
2. **Kancha de los Tejedores:** la sequía; los guardianes del Cusco no subieron y Amaru enterró solo a su madre. Yari responde:
   «Nadie subió... Yo era uno de esos guardianes.»
3. **Tambo del Camino:** Sayri sí subió, y lo convirtió en el Cazador.
4. **Portada del Cazador:** el nudo de **Killa**, su hermana menor, que le pide volver a casa.

### 3.7. Las ofrendas a la Pachamama (entre kanchas) · `AyniOfferings`
Seis ofrendas escondidas a los lados del Qhapaq Ñan, fuera del empedrado y lejos de las kanchas, más la que traen las llamas: **7 en
total**. Cada una es un atado de tela roja con hojas de coca, una concha de mullu y flores sobre una laja, junto a una pequeña pirca
con una tira de tela; brilla y suelta motas doradas. Un rombo «Ofrenda» la marca a menos de 28 m. Se acepta manteniendo `V` / `LT`
(0,7 s) cuando no hay rivales cerca. Se colocan solas al empezar, siguiendo el eje del camino.

---

## 4. Cómo cambia el duelo con Amaru

| Lo que hiciste | Efecto en el duelo o en Yari | Dónde se ve |
| :--- | :--- | :--- |
| Recuperaste el **quipu de los chasquis** | Un anillo verde se cierra sobre el pecho de Amaru y marca el momento justo de esquivar cada dardo. | Ficha «Quipu · lees sus dardos» |
| El Rastreador **escapó** | Trampas de lazo en la plaza (se evitan con el saltito: Guardia + arriba). Amaru lo recuerda. | Ficha roja «Amaru avisado» |
| Salvaste a las **tejedoras** | El veneno de los dardos dura la mitad. | Ficha «Chumpi · veneno a la mitad» |
| **Perdonaste** al capitán | Un Cazador menos en la Portada. | Ficha «Capitán · un Cazador menos» |
| **Ejecutaste** al capitán | +20 % de daño contra Amaru, pero la apacheta te rechaza. | Ficha «Champi · +20 % de daño a Amaru» |
| **Bendición del Apu** | Vida llena y la próxima caída no cuesta años. | Ficha «Apu · tu próxima caída no cuesta años» |
| Liberaste a la **cría de puma** | En la segunda fase, la puma madre baja del cerro y le rompe la postura a Amaru. | Ficha «Puma · su madre acudirá» |
| Liberaste al **cóndor** | Cada ráfaga de dardos llega con su aviso y cada dardo tarda 0,35 s más. | Ficha «Kuntur · avisa los dardos» |
| Liberaste a las **vicuñas** | En el momento: vida llena y la Illa devuelve 2 años. | — |
| Leíste los **cuatro quipus de Amaru** | En la última fase, Yari grita «¡Killa te espera en el Antisuyo, Amaru!»: Amaru duda y pierde un 40 % de su postura. El desenlace habla de Killa. | Ficha «Killa · Amaru dudará» |
| Soltaste a las **llamas** | En el momento: vida llena y una ofrenda. | Objetivo de K1 y tarjeta final |
| Cada **ofrenda** | Cura un 30 % de la vida; cada tres, la Illa devuelve un año. | Ficha «Ofrendas · n / 7» |

Cuando Amaru queda a merced de Yari, los rivales que seguían en pie (de una kancha que se saltó o que la siguieron al puente) **se
rinden y se arrodillan**, para que nadie interrumpa el Juicio ni el desenlace.

---

## 5. Interfaz

- **Panel de objetivos** (arriba a la derecha, `AyniLevelObjectives`): qué hacer ahora, rivales en pie o metros al destino, kanchas
  liberadas (x / 4) y notas de cada kancha (jaulas, llamas, «el capitán observa», la apacheta). Se oculta en las escenas.
- **Rombos en pantalla** (`AyniHoldPrompt`): dorado para el destino y las ofrendas, jade para jaulas y llamas, rojo para Amaru. Si
  caen en el mismo sitio se apartan; los que quedan fuera de la vista se pegan al borde.
- **Avisos de «Mantén»** con barra de progreso junto a cada objeto.
- **Fichas de lo que llevas** (`AyniRewardsHUD`), encima del nombre de Yari: lo ganado para el duelo y, en rojo, lo que juega en contra.
- **Tarjeta final** (`AyniLevelOutcome`): decisión, edad, caídas y tiempo; qué pasó con el quipu, las tejedoras, el capitán y la
  apacheta; quipus de Amaru leídos; y una fila con **jaulas, llamas y ofrendas**.

---

## 6. Voces

- **96 líneas habladas** (narradora, Amaru, el Capitán del Tambo, la tejedora y Yari). Cada subtítulo busca su voz por el texto
  (`AyniVoice`, hash FNV-1a del texto normalizado), y el ambiente baja mientras alguien habla.
- Las teclas entre corchetes (`[V]`, `[LT]`) no cuentan para la clave: el mismo aviso tiene voz con teclado y con mando. En esos
  avisos la narradora dice la frase sin la tecla.
- **Yari responde** a Amaru en cada aparición y en el puente, al capitán, a la tejedora, en la apacheta y en los quipus.
- Se generan con Piper a partir de `Herramientas/Audio/voces_guion.txt`:
  `python Herramientas/Audio/generar_voces.py` (solo crea las que faltan; `--todas` las rehace).
- Los gritos sintéticos de esfuerzo y dolor del combate están desactivados (`CombatFeedback.GruntsEnabled = false`).

---

## 7. Combate y cámara (lo que se pulió en el camino)

- **Turnos entre rivales:** cuando uno empieza un golpe, el siguiente espera entre 0,7 y 1,15 s; mientras uno encadena su combo, los
  demás esperan a que termine.
- **Combos justos:** si un golpe deja a Yari aturdida, el siguiente del combo espera a que se recupere y le deja **0,42 s** para
  esquivar o desviar. Vale también para Amaru.
- **Cámara** (`ThirdPersonSifuCamera`): no atraviesa muros ni terreno, se acerca al hombro bajo techo, sube sobre el hombro si un muro
  la deja pegada y encuadra mejor al rival fijado.
- **Selva quemada** a los lados del Qhapaq Ñan (`Editor/AyniBurntForest.cs`), que el queñual hace reverdecer si Yari perdona a Amaru.

---

## 8. Fundamento cultural

| Elemento | Qué es | Cómo aparece en el juego |
| :--- | :--- | :--- |
| **Chasqui** | Mensajero que corría por relevos el Qhapaq Ñan llevando noticias y quipus. | El Puesto de Chasquis (K1) y sus llamas de carga. |
| **Quipu** | Cuerdas anudadas con las que los incas llevaban cuentas y registros. | El quipu robado y los cuatro quipus que cuentan la vida de Amaru. |
| **Llama** | Animal de carga de las caravanas andinas, criado por el ayllu. | Las llamas amarradas del corral. |
| **Ayllu** | La comunidad familiar andina, base del trabajo recíproco. | Adonde vuelven las llamas, las vicuñas y Amaru (si se le perdona). |
| **Despacho / pago a la tierra** | Ofrenda a la Pachamama con hojas de coca, flores y otros elementos. | Las ofrendas escondidas del camino. |
| **Mullu** | Concha *Spondylus*, sagrada y muy valorada en los Andes. | Parte de cada ofrenda. |
| **Apacheta** | Montículo de piedras en los caminos donde el viajero deja una piedra para los Apus. | La apacheta a la salida del Tambo. |
| **Apu** | Espíritu protector de las montañas. | La bendición que evita perder años. |
| **Chumpi** | Faja tejida, prenda de gran valor ritual. | El regalo de las tejedoras. |
| **Hucha** | Culpa, carga negativa que rompe el equilibrio. | Por qué la apacheta rechaza a quien ejecutó al capitán. |
| **Kuntur** | El cóndor andino, mensajero entre el cielo y la tierra. | El cóndor liberado que avisa los dardos. |

Todo el nivel gira en torno al **ODS 15 (Vida de ecosistemas terrestres)**: liberar fauna, sanar la tierra con ofrendas y, al perdonar a
Amaru, ver reverdecer el bosque quemado.

---

## 9. Archivos

| Archivo | Qué hace |
| :--- | :--- |
| `Scripts/UI/AyniLevelObjectives.cs` | Panel de objetivos y rombo hacia el destino |
| `Scripts/Story/AyniHunterStalker.cs` | Las apariciones de Amaru en el camino |
| `Scripts/World/AyniQuipuChase.cs` | El Rastreador que huye con el quipu (K1) |
| `Scripts/World/AyniLlamaCorral.cs` | El corral de las llamas del chasqui (K1) |
| `Scripts/World/AyniWeaversFire.cs` | La casa en llamas de las tejedoras (K2) |
| `Scripts/World/AyniTamboCaptain.cs` | El Capitán del Tambo y su mini Juicio (K3) |
| `Scripts/World/AyniApacheta.cs` | La apacheta y la bendición del Apu |
| `Scripts/World/AyniCaptiveCages.cs` | Las jaulas de la Portada (K4) |
| `Scripts/Story/AyniFreedAllies.cs` | La puma madre y el cóndor en el duelo |
| `Scripts/Story/AyniHunterTraps.cs` | Las trampas de lazo si Amaru fue avisado |
| `Scripts/World/AyniAmaruQuipus.cs` | Los cuatro quipus de Amaru y el momento de Killa |
| `Scripts/World/AyniOfferings.cs` | Las ofrendas a la Pachamama del camino |
| `Scripts/World/AyniPathRewards.cs` | Dónde se guarda lo ganado en el camino |
| `Scripts/UI/AyniRewardsHUD.cs` | Las fichas de lo que llevas |
| `Scripts/UI/AyniHoldPrompt.cs` | Avisos de «Mantén» y rombos en pantalla |
| `Scripts/Story/AyniLevelOutcome.cs` | Desenlace, rendición de los rivales y tarjeta final |
| `Scripts/Core/AyniVoice.cs` | Voces de los subtítulos |
| `Herramientas/Audio/generar_voces.py` · `voces_guion.txt` | Guion y generador de voces |

Todo se añade solo en Play desde `SifuCombatHUD`: **no hace falta tocar la escena.**

---

## 10. Para probar (con Unity abierto y el puente de agentes)

```
call Ayni.Editor.AyniObjectivesProbe.Report                 objetivo actual y estado de todo
call Ayni.Editor.AyniObjectivesProbe.ClearSite 1            derrota a los rivales de una kancha (1 a 4)
call Ayni.Editor.AyniObjectivesProbe.KeepYariAlive 300      Yari no pierde vida durante 300 s
call Ayni.Story.AyniHunterStalker.Test 0                    fuerza una aparición de Amaru (0 a 3)
call Ayni.World.AyniQuipuChase.TestEscape / TestRecover     el Rastreador escapa / recuperas el quipu
call Ayni.World.AyniLlamaCorral.TestGoTo / TestFree         Yari junto al corral / suelta a las llamas
call Ayni.World.AyniWeaversFire.TestGoToDoor / TestRescue   Yari ante la tranca / salva a las tejedoras
call Ayni.World.AyniTamboCaptain.TestBreak                  rompe la postura del capitán
call Ayni.World.AyniTamboCaptain.TestResolve 1              Ayni (0 = Venganza)
call Ayni.World.AyniApacheta.TestGoTo / TestOffer           Yari junto a la apacheta / deja la piedra
call Ayni.Editor.AyniObjectivesProbe.GoToCage 0             Yari ante una jaula (0 puma, 1 cóndor, 2 vicuñas)
call Ayni.World.AyniCaptiveCages.TestFree 0                 abre una jaula
call Ayni.World.AyniAmaruQuipus.TestGoTo 1 / TestRead 1     Yari junto a un quipu de Amaru / lo lee
call Ayni.World.AyniOfferings.TestFromRoad 1                Yari en el camino frente a una ofrenda (1 a 6)
call Ayni.World.AyniOfferings.TestGoTo 1 / TestTake 1       Yari junto a una ofrenda / la acepta
```

Cuidado al probar con teletransporte: `TeleportLocal K1 0 -14 0` deja a Yari **fuera** de la kancha (no cuenta como entrar), y al
entrar en K1 se abre el tutorial de combate, que pausa el juego (`AyniPlaytestProbe.CloseTutorial`).

---

## 11. Pendiente

- **Tejedoras:** con los refuerzos de Saqueadores, abrir la puerta en 55 s puede ser demasiado duro; falta probarlo a mano. La idea
  es que los golpes pausen el progreso en vez de cortarlo a la mitad.
- **Acceso al puente:** la presentación de Amaru empieza solo por cercanía, así que se le puede alcanzar sin liberar las kanchas.
  Falta decidir si el puente se cierra hasta liberar la Portada.
- **Modelo de Sayri** para el prólogo (ahora el golpe llega desde fuera de cuadro).
