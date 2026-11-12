# Fauna andina: cóndores, camélidos, vizcachas y zorros

Nota de trabajo del módulo de **Fauna Andina** en **AYNI: El Retorno del Guardián** (rama `feat/fauna-andina`).
Da vida al Nivel 1 con animales de la sierra del Perú **sin cambiar la temática, la geometría ni la lógica del juego**:
todo es código nuevo, decorativo y se puede quitar por completo con una opción del menú.

---

## 1. Principios del módulo

- **Solo agrega.** No modifica scripts existentes del juego, del combate, de la aldea ni de la iluminación.
- **Sin colisionadores.** Los animales no bloquean a Yari ni interfieren con el combate, las cámaras o los rivales.
- **Autoría propia, sin licencias externas.** Todos los modelos se construyen por código con primitivas low-poly.
  No se usa `Llama.obj` (licencia en auditoría, ver `CATALOGO_RECURSOS_Y_LICENCIAS.md`) ni el paquete *Animals FREE*.
- **Reactivos a Yari.** Los animales notan su presencia y se apartan, cada especie a su manera.
- **Respeto cultural.** Cada grupo está ubicado en un lugar con sentido dentro de la historia de la aldea.

---

## 2. Componentes

| Pieza | Archivo | Función |
| :--- | :--- | :--- |
| **Utilidades compartidas** | `Assets/Scripts/World/Fauna/AyniFaunaPrimitives.cs` | Materiales URP mate cacheados por color, primitivas sin colisionador y pivotes de articulación. |
| **Modelo del cóndor** | `Assets/Scripts/World/Fauna/AyniCondorModel.cs` | Kuntur con collar blanco, cabeza rosada, alas de ~3 m con banda blanca y plumas primarias abiertas como dedos. |
| **Vuelo del cóndor** | `Assets/Scripts/World/Fauna/AyniCondorFlight.cs` | Planeo en círculos sobre térmicas que derivan, alabeo en el giro, altura oscilante y aleteos ocasionales. |
| **Modelo de camélidos** | `Assets/Scripts/World/Fauna/AyniCamelidModel.cs` | Llama, alpaca y vicuña con pivotes de patas, cuello y cabeza; pelajes variados por semilla. |
| **Pastoreo y huida** | `Assets/Scripts/World/Fauna/AyniGrazingAnimal.cs` | Pastar, mirar, caminar y huir de Yari. Paso de andadura (camélidos) o trote diagonal (zorro). |
| **Modelos menores** | `Assets/Scripts/World/Fauna/AyniCritterModel.cs` | Vizcacha sentada con cola enroscada y zorro andino (Atoq) con cola de punta negra. |
| **Vizcacha** | `Assets/Scripts/World/Fauna/AyniVizcacha.cs` | Toma sol en rocas, salta entre ellas, vigila a Yari y huye a saltos para agazaparse. |
| **Menú de Editor** | `Assets/Editor/AyniFaunaSetup.cs` | Menú `Ayni > Fauna Andina` para colocar y quitar la fauna en la escena. |

---

## 3. Especies y comportamiento

### Cóndor andino (*Vultur gryphus*, Kuntur)
- Casi no aletea: planea en círculos amplios (10–12 m/s) sobre corrientes térmicas, inclinándose hacia el centro del giro.
- La térmica se desplaza despacio dentro de un radio de paseo, así el vuelo no se repite.
- Cada 9–18 s da dos aletazos lentos y profundos, como el ave real.

### Llama, alpaca y vicuña
| Especie | Rasgos del modelo | Radio de alerta ante Yari |
| :--- | :--- | :---: |
| **Llama** | Alta (lomo 1,15 m), orejas largas en forma de plátano y **borlas de lana de colores** en las orejas, como en los rebaños de pastores. Pelaje blanco, café, negro o vicuñado, a veces con manchas. | ~5 m (acostumbrada a la gente) |
| **Alpaca** | Más baja y lanuda, orejas cortas y copete de lana en la frente. Pelaje blanco, crema, café claro o gris. | ~7 m |
| **Vicuña** | Silvestre y esbelta, color canela con pechera y vientre blancos. | ~16 m (muy asustadiza) |

- Alternan **pastar** (cuello abajo y mordisqueo), **mirar** (cabeza alta, girando a los lados) y **caminar** hacia otro pasto.
- Caminan en **paso de andadura**: mueven a la vez las dos patas del mismo lado, rasgo propio de los camélidos.
- Siguen la altura y la inclinación del terreno y solo eligen destinos sobre terreno descubierto (nunca dentro de casas, muros o rocas).
- Si Yari se acerca, levantan la cabeza y se alejan trotando sin salir demasiado de su zona; luego se quedan mirando.

### Vizcacha de la sierra (*Lagidium peruanum*)
- Toma sol sentada sobre **rocas o muros de piedra** y mueve las orejas cada pocos segundos.
- A ratos salta a otra roca cercana con saltos cortos.
- Si Yari se acerca (~14 m) se yergue y lo vigila; si llega muy cerca (~7 m) huye con saltos rápidos y se queda agazapada.

### Zorro andino (*Lycalopex culpaeus*, Atoq)
- Ronda los bordes de la aldea y del cerro con **trote diagonal**, se detiene a **olfatear el suelo** y mueve la cola.
- Se aparta de Yari desde ~11 m, corriendo más rápido que los camélidos.

---

## 4. Ubicación en el Nivel 1

| Grupo | Lugar (x, z) | Sentido en la historia |
| :--- | :--- | :--- |
| 5 cóndores | Garganta del puente `(651, 466)`, cañón norte `(638, 495)`, muy alto sobre la plaza de Amaru `(612, 468)` y sobre la aldea `(520, 480)` | El Kuntur, mensajero del Hanan Pacha, vigila el cañón. Sobre la plaza vuela a 70 m para no distraer en el combate contra el jefe. |
| Rebaño mixto (3 llamas, 2 alpacas) | Junto al Puesto de Chasquis `(312, 530)` | Recibe a Yari al inicio del camino. |
| 6 alpacas y 1 llama guardiana | Junto a la Kancha de los Tejedores `(360, 364)` | Su fibra es la del tejido; la llama guardiana es una práctica real de los pastores. |
| Caravana de 5 llamas | Junto al Tambo del Camino `(460, 467)` | Los tambos recibían caravanas de llamas de carga. |
| 5 vicuñas | Cerca de la Portada del Cazador `(523, 550)` | Silvestres y lejanas; memoria del *chaccu*, el arreo ancestral de vicuñas. |
| Colonias de vizcachas (3 + 3 + 4) | Acantilado norte `(628, 492)`, garganta sur `(636, 446)` y cerca del Tambo `(455, 410)` | Roquedales del cañón y muros de piedra. |
| 2 zorros andinos | Bordes de la aldea `(420, 470)` y cerro sur `(590, 430)` | El Atoq de los cuentos quechuas ronda a distancia. |

Los centros están en `AyniFaunaSetup` (`CondorSpots`, `HerdSpots`, `VizcachaColonies`, `FoxRanges`). Si un grupo no encuentra
lugar despejado, la consola muestra un aviso con su nombre para mover su centro.

---

## 5. Menús en Unity

- `Ayni > Fauna Andina > 0. Soltar Toda la Fauna Andina`
- `Ayni > Fauna Andina > 1. Soltar Cóndores sobre el Cañón y la Aldea`
- `Ayni > Fauna Andina > 2. Soltar Rebaños (Llamas, Alpacas y Vicuñas)`
- `Ayni > Fauna Andina > 3. Soltar Vizcachas y Zorros Andinos`
- `Ayni > Fauna Andina > Quitar Fauna Andina`

Todo se crea bajo `Ayni_Entorno/Fauna_Andina` con soporte de **Deshacer (Ctrl+Z)**. Repetir una opción reconstruye su grupo sin
duplicarlo. Después de colocar la fauna hay que **guardar la escena (Ctrl+S)**.

---

## 6. Prueba rápida

1. Abrir `Assets/network of paths/Scenes/SampleScene.unity`.
2. Ejecutar `Ayni > Fauna Andina > 0. Soltar Toda la Fauna Andina` y revisar el resumen en la consola.
3. Entrar en Play y comprobar:
   - Los cóndores planean de frente (no de lado) y al aletear las dos alas suben juntas.
   - Los camélidos pastan, levantan la cabeza y caminan sin atravesar casas ni muros.
   - Al acercarse Yari, las vicuñas huyen primero (desde lejos) y las llamas al final.
   - Las vizcachas se yerguen al ver a Yari y huyen a saltos si se acerca más.
4. Para retirar todo: `Ayni > Fauna Andina > Quitar Fauna Andina`.

---

## 7. Rendimiento

- 39 criaturas (5 cóndores, 22 camélidos, 10 vizcachas y 2 zorros) con mallas de primitivas y materiales compartidos por color (pocos materiales en total).
- Sin colisionadores ni física: cada animal solo actualiza su transformación y consulta la altura del terreno.
- La búsqueda de Yari se hace una vez cada 2 s (compartida) y la comprobación de distancia cada 0,2–0,25 s.
