# 🗺️ DISEÑO DE NIVELES 2 AL 5 Y EXPANSIÓN DEL MUNDO
# 🌄 AYNI: EL RETORNO DEL GUARDIÁN (`v0.7.0`)

> **Documento de Diseño de Niveles (Level Design), Jefes Multifase y Progresión Ecológica**  
> **Referencia Base:** [`GDD_AYNI.md`](GDD_AYNI.md) · [`ALDEA_INCA.md`](ALDEA_INCA.md) · [`NIVEL_1_PROLOGO_Y_JUGABILIDAD.md`](NIVEL_1_PROLOGO_Y_JUGABILIDAD.md)

---

## 📌 1. ARQUITECTURA DE PROGRESIÓN DEL JUEGO COMPLETO

Tras la implementación completa de la **Misión 1 (Valle de las Sombras — Amaru el Cazador)**, el diseño de los **Niveles 2 al 5** mantiene el estándar arquitectónico modular ya probado en código ([`AyniEncounterSite`](Assets/Scripts/World/AyniEncounterSite.cs), [`EnemyController`](Assets/Scripts/Enemy/EnemyController.cs) con `bossPhaseHealth` en dos fases, [`AyniBossPhaseDirector`](Assets/Scripts/Story/AyniBossPhaseDirector.cs) y [`AyniJudgment`](Assets/Scripts/Story/AyniJudgment.cs)).

```mermaid
flowchart LR
    P["Misión 0: Prólogo\nPuente Colgante\n(General Sayri)"] --> N1["Misión 1: Antisuyo\nValle de las Sombras\n(Amaru · ODS 15)"]
    N1 --> N2["Misión 2: Qullasuyu\nCanteras de Bronce\n(Túpac · ODS 8/12)"]
    N2 --> N3["Misión 3: Kuntisuyu\nTerrazas y Acueductos\n(Apo Rumi · ODS 6)"]
    N3 --> N4["Misión 4: Chinchaysuyu\nPalacio Alquímico\n(Mama Churi · ODS 12)"]
    N4 --> N5["Misión 5: Cusco Sagrado\nCumbre del Intihuatana\n(General Sayri · ODS 16)"]
```

---

## ⛏️ 2. MISIÓN 2: LAS CANTERAS DE PIEDRA Y BRONCE (QULLASUYU)

### 2.1. Ambientación y Diseño Espacial
- **Bioma:** Desfiladero árido de roca granítica rosada y minas a tajo abierto a 3,800 m.s.n.m., con hornos de fundición (*huayras*) que emiten humo espeso.
- **Tramo del Qhapaq Ñan (700 m):** Calzada tallada directamente en la pared del cañón con rampas de arrastre de bloques megalíticos, andamios de troncos y socavones iluminados por antorchas.
- **Obstáculos del Camino:**
  1. Bloques de granito suspendidos y rodillos de madera que obligan a usar el salto en carrera (`Run_Jump`).
  2. Zanjas de escoria caliente que exigen esquiva lateral o paso por cornisas estrechas (`Crouch`).
  3. Portadas megalíticas a medio labrar (`AyniMuroInca.shader` en modo `2 = Monolítico`).

### 2.2. Kanchas y Arquetipos de Rivales del Nivel 2
| Recinto (`AyniEncounterSite`) | Arquetipo de Rival | Perfil de Combate |
| :--- | :--- | :--- |
| **K1 · Puesto de Picapedreros** | **Quebrantador de Roca** (`160 HP`, `110 Postura`) | Combate cuerpo a cuerpo con guardias pesadas de cuero y golpes descendentes lentos pero de alto daño estructural (`25 pts`). |
| **K2 · Patio de las Huayras** | **Fundidor de Cobre** (`150 HP`, `100 Postura`) | Ataques rápidos con barridos bajos frecuentes (`lowAttackChance = 0.45`) entre los hornos de viento. |
| **K3 · Depósito de Lingotes** | **Capataz de Cuadrilla** (`180 HP`, `120 Postura`) | Enemigo corpulento con pectoral de bronce que encadena combos de dos golpes seguidos. |
| **K4 · Umbral de la Gran Cantera** | **Guardia de Bronce de Élite** (`200 HP`, `130 Postura`) | Custodio previo a la arena del jefe con ventana de anticipación reducida (`windupTime = 0.50 s`). |

### 2.3. Jefe del Nivel 2: **Túpac el Capataz de Bronce**
- **Referencia Visual:** `Assets/Art/Characters/04_jefe_tupac_pose_neutral.jpg`.
- **Estadísticas por Fases (`bossPhaseHealth`):**
  - **Fase 1 (`420 HP`, `160 Postura`):** Combate de fuerza bruta con guanteletes de bronce; sus golpes pesados generan ondas de polvo si Yari los bloquea pasivamente en lugar de desviarlos con *Parry* o esquivarlos con *Duck / Sway*.
  - **Fase 2 · *"Forja Incandescente"* (`500 HP`, `160 Postura`):** Tras la cinemática de `AyniBossPhaseDirector`, los guanteletes de Túpac se tornan incandescentes. Incorpora un pisotón sísmico de onda baja (que exige esquiva de salto `Avoid_Jump`) y embestidas de hombro.
- **Resolución del Juicio Ayni (ODS 8 y ODS 12):**
  - **Perdón Ayni:** Se apagan los hornos contaminantes, se liberan las herramientas para las comunidades agrícolas y el humo negro se disipa dejando ver el cielo limpio de la puna.
  - **Venganza:** Los hornos colapsan cubriendo la cantera de hollín y escoria ardiente.

---

## 💧 3. MISIÓN 3: TERRAZAS Y ACUEDUCTOS SAGRADOS (KUNTISUYU)

### 3.1. Ambientación y Diseño Espacial
- **Bioma:** Complejo monumental de andenes agrícolas escalonados y fuentes ceremoniales (*Tipón / Ollantaytambo*) actualmente secos y agrietados por el bloqueo de la represa superior.
- **Tramo del Qhapaq Ñan (680 m):** Escalinatas de piedra integradas en los muros de contención de los andenes (requiere uso intensivo de [`FootIK`](Assets/Scripts/Combat/FootIK.cs)), canales secos de sillería fina y caídas verticales hacia terrazas inferiores.

### 3.2. Jefe del Nivel 3: **Apo Rumi el Acaparador**
- **Referencia Visual:** `Assets/Art/Characters/02_jefe_apo_rumi_pose_neutral.jpg`.
- **Estadísticas por Fases (`bossPhaseHealth`):**
  - **Fase 1 (`440 HP`, `165 Postura`):** Maestro defensivo de postura monolítica; posee alta recuperación de estructura (`recoveryRate = 22 pts/s`) que obliga al jugador a ser agresivo y encadenar contraataques tras *Parry*.
  - **Fase 2 · *"Torrente Contenido"* (`520 HP`, `165 Postura`):** Apo Rumi rompe las compuertas laterales de la arena; combate con barridos circulares amplios y proyectiles de piedra de honda.
- **Resolución del Juicio Ayni (ODS 6: Agua Limpia y Saneamiento):**
  - **Perdón Ayni:** Las compuertas sagradas se abren, el agua cristalina (`AyniAguaQuebrada.shader`) vuelve a fluir por todos los acueductos y cascadas del nivel, y los andenes secos reverdecen con cultivos de maíz y quinua.
  - **Venganza:** La represa se fractura violentamente inundando de lodo turbio las terrazas inferiores.

---

## 🔥 4. MISIÓN 4: EL PALACIO ALQUÍMICO DEL SOL CORRUPTO (CHINCHAYSUYU)

### 4.1. Ambientación y Diseño Espacial
- **Bioma:** Recinto palaciego de sillería imperial almohadillada (`AyniMuroInca.shader` modo `1`) con patios ceremoniales, espejos de agua contaminados por mercurio/cinabrio y braseros rituales.
- **Tramo del Qhapaq Ñan (650 m):** Corredores trapezoidales interconectados, puentes de losa de piedra sobre patios hundidos y cámaras de fundición de oro.

### 4.2. Jefe del Nivel 4: **Mama Churi la Alquimista**
- **Referencia Visual:** `Assets/Art/Characters/03_jefe_mama_churi_pose_neutral.jpg`.
- **Estadísticas por Fases (`bossPhaseHealth`):**
  - **Fase 1 (`400 HP`, `155 Postura`):** Combatiente sumamente veloz y acrobática; utiliza fintas y ataques rápidos en cadena que premian el uso del balanceo lateral (`Avoid_SwayL/R`).
  - **Fase 2 · *"Transmutación Solar"* (`480 HP`, `155 Postura`):** Salta hacia atrás de forma similar a `AmaruHunter` y lanza polvos alquímicos cegadores/corrosivos en abanico que deben esquivarse antes de acortar distancia.
- **Resolución del Juicio Ayni (ODS 12: Producción y Consumo Responsables):**
  - **Perdón Ayni:** Los vapores tóxicos se purifican con lluvia dorada y las ofrendas solares son devueltas a los templos comunitarios.

---

## ☀️ 5. MISIÓN 5 (CLÍMAX): LA CUMBRE DEL INTIHUATANA

### 5.1. Ambientación y Diseño Espacial
- **Bioma:** Santuario de alta montaña en la cordillera nevada a **5,000 m.s.n.m.** (`AyniMontanasLejanas.shader` y `AyniNiebla.shader`), coronado por la plaza circular del reloj solar **Intihuatana** bajo una tormenta eléctrica andina.
- **Conexión Narrativa con el Prólogo:** El jugador cruza nuevamente un puente colgante monumental, cerrando el círculo iniciado en *"La Noche de las Cenizas"*, pero esta vez con el dominio completo del **Rumi Maki** y la sabiduría acumulada en el Talismán Illa.

### 5.2. Jefe Final: **General Sayri el Usurpador**
- **Referencia Visual:** `Assets/Art/Characters/05_jefe_general_sayri_pose_neutral.jpg`.
- **Estadísticas por Fases (`bossPhaseHealth`):**
  - **Fase 1 (`500 HP`, `180 Postura`):** General maestro del Rumi Maki corrupto y portador de la maza real (*Champi*). Combina ataques altos demoledores con barridos bajos y desvíos propios si el jugador repite el mismo golpe sin variar el combo.
  - **Fase 2 · *"El Caudillo de las Cenizas"* (`600 HP`, `200 Postura`):** Sayri invoca el fuego de la traición; sus ataques tienen un multiplicador de velocidad de `×1.20` y exigen dominar las tres esquivas (`Duck`, `Jump`, `Sway`) y el *Parry* perfecto (`0.22 s`) para quebrar su estructura de 200 puntos.
- **Resolución Final del Juego (ODS 16: Paz, Justicia e Instituciones Sólidas):**
  - **Camino del Ayni (Final Verdadero — *Sumak Kawsay*):** Yari desarma la Champi de Sayri sin quitarle la vida, restaurando el pacto cósmico en el Intihuatana. El sol emerge entre las nubes nevadas iluminando los cuatro *suyus* restaurados. Si el jugador además perdonó a los jefes anteriores y llegó con menos de 35 años, obtiene el rango legendario **Guardián del Sumak Kawsay**.
  - **Camino de la Venganza (Final Kármico):** Yari ejecuta a Sayri, pero el Talismán Illa se resquebraja consumido por la ira, dejando a Yari solitario en una cumbre cubierta de ceniza perpetua.

---
*Documento de Diseño de Niveles y Expansión — Proyecto Ayni: El Retorno del Guardián.*
