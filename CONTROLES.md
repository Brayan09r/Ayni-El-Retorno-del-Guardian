# 🎮 GUÍA VISUAL DE CONTROLES Y MOVIMIENTOS
# 🌄 AYNI: EL RETORNO DEL GUARDIÁN

> **Guía Oficial de Comportamiento y Asignación de Teclas (Teclado & Ratón y Mando de Xbox)**
>
> El HUD y el tutorial muestran solos los botones del dispositivo que estés usando: cambian al tocar el mando o el teclado.

## 🎮 Mando de Xbox (resumen)

| Acción | Mando de Xbox | Teclado y ratón |
| :--- | :--- | :--- |
| Caminar | Stick izquierdo hasta el 75 % | `W` `A` `S` `D` |
| Cámara | Stick derecho | Ratón |
| Correr | Stick izquierdo a fondo | Mantener `Shift Izq.` mientras te mueves |
| Esprintar | Mantener `RT` (o pulsar el stick izquierdo, `L3`) | — (solo con mando) |
| Saltar | `A` | `Espacio` |
| Salto largo | `A` mientras corres o esprintas | `Espacio` mientras corres con `Shift` |
| Agacharse | `B` (alternar) | `C` / `Ctrl Izq.` |
| Golpe ligero | `X` | `Clic Izq.` |
| Golpe pesado | `Y` | `Q` / `E` |
| Guardia (Yari se planta) | Mantener `LB` | Mantener `Clic Der.` / `G` |
| Guardia + agacharse (evita golpes altos) | `LB` + stick ↓ | Guardia + `S` |
| Guardia + saltito (evita barridos) | `LB` + stick ↑ (o `A`) | Guardia + `W` / `Espacio` |
| Guardia + balanceo (evita cualquier golpe) | `LB` + stick ← → | Guardia + `A` / `D` |
| Fijar rival | `RB` o `R3` | `Tab` / `Clic central` |
| Elegir rival al golpear | Stick hacia el rival + `X` / `Y` | `WASD` hacia el rival + golpe |
| Patada hacia atrás | Stick hacia un rival que está a tu espalda + golpe | `WASD` hacia él + golpe |
| Juicio Ayni: Venganza | `B` (botón rojo) | `F` |
| Juicio Ayni: Ayni (perdón) | `A` (botón verde) | `X` |
| Tutorial | `View` | `F1` |
| Saltar escena / tutorial · Reintentar | `Menu` | `Tab` / `Esc` · `R` |
| Mostrar u ocultar controles | Cruceta ↑ | `H` |
| Pausa y volumen (en pleno juego; en escenas y en el tutorial, `Menu` / `Esc` los saltan) | `Menu` | `Esc` |
| Panel de prueba del mando | — | `F9` |

**Primera vez con el mando:** el menú `Ayni > Mando > Configurar Ejes del Mando Xbox` deja listos los ejes (ya vienen configurados en el repositorio). Para comprobar que Unity lee cada botón, entra en Play y pulsa `F9`.

---

## 1. Desplazamiento y Locomoción Básica: `[W] [A] [S] [D]`
Permite mover a Yari de forma tridimensional fluida con la cámara en tercera persona sobre el hombro. Cuando Yari no se mueve, adopta automáticamente su postura relajada natural con los brazos abajo.

![Control Movimiento WASD](Assets/Art/Controls_Art/01_Control_Movimiento_WASD.jpg)

- **Teclas:** `W` (Avanzar), `A` (Izquierda), `S` (Retroceder), `D` (Derecha).
- **Animación:** *Jog_Forward_InPlace* al avanzar; *Neutral_Idle* en reposo.

---

## 2. Correr: Mantener `[SHIFT IZQUIERDO]`
Con el teclado Yari **camina** con `W` `A` `S` `D` (unos `1.4 m/s`) y **corre** mientras se mantiene `Shift` (`3.6 m/s`; unos `4.1 m/s` con la Illa joven), para cruzar grandes distancias, escapar de emboscadas o cerrar la distancia contra un oponente. El sprint (`4.9 m/s`) es solo del mando (`RT` o `L3`).

![Control Sprint con Shift](Assets/Art/Controls_Art/02_Control_Sprint_Shift.jpg)

- **Teclas:** Mantener `Shift Izquierdo` mientras te mueves con `W`.
- **Efecto:** Si estás agachado, echar a correr te pone de pie automáticamente.

---

## 3. Agacharse / Cuclillas y Sigilo: `[C]` / `[CTRL IZQUIERDO]`
Reduce la altura de la cápsula de colisión de Yari (de `2.0 m` a `1.15 m`), permitiendo pasar por debajo de arcos derruidos, vigas bajas y avanzar sigilosamente sin alertar a patrullas lejanas.

![Control Agacharse con C o Ctrl](Assets/Art/Controls_Art/03_Control_Agacharse_C_Ctrl.jpg)

- **Teclas:** `C` (Alternar postura agachada) o `Ctrl Izquierdo` (Mantener).
- **Animación:** *Crouch_Idle* (cuclillas en espera) y *Crouch_Walk_InPlace* (caminar agachado).

---

## 4. Salto Dinámico: `[ESPACIO]`
Permite sortear desniveles, grietas en la calzada incaica y abismos entre puentes colgantes con impulso físico y aceleración por gravedad.

![Control Salto con Espacio](Assets/Art/Controls_Art/04_Control_Salto_Espacio.jpg)

- **Tecla:** `Espacio` (cuando no estás en guardia).
- **Efecto:** Salto vertical en parábola con detección de suelo (`IsGrounded`).
- **Salto largo:** si saltas corriendo (o esprintando con el mando), Yari no se frena: sale lanzado hacia delante un 30 % más rápido de lo que venía y cae a unos 3.5 m (unos 4.5 m en sprint). En el aire la dirección se puede corregir un poco.

---

## 5. Guardia Activa, Desvío (Parry) y Esquivas Sifu: `[CLIC DERECHO]` / `[LB]` + dirección
El pilar defensivo táctico del combate. Mantener la guardia reduce el daño recibido pero aumenta la barra de estructura propia. Desviar en el último milisegundo produce un **Parry perfecto**.

Como en *Sifu*, **en guardia Yari se planta y no camina**: solo gira para encarar al rival. La dirección ya no lo desplaza, solo elige la esquiva, y todas se hacen sin moverse del sitio. Tras esquivar o desviar, el siguiente golpe es un **contraataque** que daña mucho más la postura.

![Control Guardia y Esquiva Sifu](Assets/Art/Controls_Art/05_Control_Guardia_Esquiva_Sifu.jpg)

- **Guardia:** Mantener `Clic Derecho`, tecla `G` o `LB`.
- **Esquiva Duck (Agachada rápida bajo golpes altos, aviso rojo):** En guardia + `S` (stick ↓).
- **Esquiva Jump (Saltito sobre barridos bajos, aviso amarillo):** En guardia + `W` o `Espacio` (stick ↑ o `A`).
- **Balanceo lateral (evita cualquier golpe, con menos margen):** En guardia + `A` / `D` (stick ← →).
- Hay que volver a inclinar la dirección para encadenar otra esquiva (mantenerla pulsada no repite).

---

## 6. Artes Marciales Rumi Maki: `[CLIC IZQUIERDO]` & `[Q]` / `[E]`
El sistema ofensivo del "Puño de Piedra" incaico, combinando impactos rápidos para presionar y golpes pesados de rotura de postura.

![Control Ataques Rumi Maki](Assets/Art/Controls_Art/06_Control_Ataques_Rumi_Maki.jpg)

- **Ataque Ligero (Puñetazo Rumi Maki):** `Clic Izquierdo` (combo rápido de puños y codazos).
- **Ataque Pesado (Patada Circular y Golpe Sísmico):** Tecla `Q` o `E` (alto daño a la postura del rival).
- **Juicio Ayni (al quebrar la postura enemiga):** Tecla `F` / `B` (Golpe Letal de Venganza) · Tecla `X` / `A` (Desarme y Purificación).
- **Jefes:** no mueren a golpes. Romperles la postura antes de su fase final solo los deja expuestos (los golpes duelen más); en la fase final (≤ 40 % de vida) la acción se congela a cámara lenta y se decide el **Juicio Ayni**.
