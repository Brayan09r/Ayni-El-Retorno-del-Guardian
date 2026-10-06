# Aldea inca del camino: casas, portadas y puntos de rival

Nota de trabajo para quien toque el recorrido del Nivel 1 (personas y agentes).
Referencia visual: las casas de Machu Picchu (muros de pirca inclinados, hastiales, puertas, ventanas y hornacinas trapezoidales, techo de paja).

## Qué hay ahora

Como en Sifu, antes del jefe hay salas con rivales. Aquí las salas son **cuatro conjuntos de casas (kanchas) a caballo sobre el Qhapaq Ñan**:
el camino entra por una portada, cruza el patio y sale por otra. El patio es el sitio principal de la pelea, y **en las casas se puede
entrar**: cada una es un cuarto con su fogón encendido, poyo para dormir, hornacinas y cántaros. Hay un punto para un rival delante de
cada puerta y otro dentro del cuarto. En las colcas y los torreones no se entra.

Yari ya no despierta en la plaza: despierta **al principio del camino, en (248, 540)**, junto a una apacheta, a 370 m de Amaru en línea
recta y **630 m siguiendo el camino**. Amaru sigue donde estaba, en la plaza (612, 468).

| # | Conjunto | Centro (x, z) | Qué tiene | Puntos en el patio | Puntos dentro de casas |
| --- | --- | --- | --- | --- | --- |
| 1 | **K1 · Puesto de Chasquis** | (331, 468) | 2 casas, 1 colca, fogón | 3 | 2 |
| 2 | **K2 · Kancha de los Tejedores** | (392, 388) | 3 casas, tendederos de tejidos | 5 | 4 |
| 3 | **K3 · Tambo del Camino** | (478, 434) | kallanka de 3 puertas, 2 casas, 2 colcas | 6 | 5 |
| 4 | **K4 · Portada del Cazador** | (556, 519) | 2 casas, 2 torreones y la portada monumental de doble jamba | 6 | 3 |

Distancia a la plaza siguiendo el camino: inicio 630 m · K1 495 · K2 378 · K3 250 · K4 87. Entre un conjunto y el siguiente hay de 115 a
165 m (antes eran de 55 a 110), y por el camino hay obstáculos.

## Obstáculos del camino

Ocho, entre el inicio y la Portada del Cazador. Son lo que dejó Amaru a su paso: bosque quemado, derrumbes y barricadas de su gente.
Cada uno se supera con algo que Yari ya sabe hacer.

| # | A cuánto de la plaza | Tipo | Cómo se pasa |
| --- | --- | --- | --- |
| O1 | 592 m | Tronco quemado caído | Saltando (en carrera o en parado) |
| O2 | 550 m | Derrumbe de peñascos | Serpenteando: tres hileras con el paso alternado |
| O3 | 450 m | Empalizada doble | En ese: se entra por un lado y se sale por el contrario |
| O4 | 420 m | Dos troncos seguidos | Dos saltos |
| O5 | 335 m | Muro de pirca a medio caer | Saltando, mejor por el tramo más bajo |
| O6 | 312 m | Derrumbe de peñascos | Serpenteando |
| O7 | 205 m | Empalizada doble | En ese |
| O8 | 168 m | Dos troncos seguidos | Dos saltos |

- Están en `Editor/AyniVillageObstacles.cs` (tabla `Obstacles`: tipo, posición sobre el camino, sentido de la marcha). Se construyen y se
  borran con la aldea, y cuelgan de `Aldea_Inca/Obstaculos`.
- A cada lado, una hilera de peñascos sube 10 m por el talud. No es un muro infinito: quien dé un rodeo largo por el cerro los evita.
- Al construir la aldea se comprueban solos con la física (`Ayni > Entorno > Comprobar Obstáculos del Camino`): se busca el camino más
  corto **a pie** de un lado a otro. Troncos y muro no deben tener ninguno (hay que saltar); derrumbes y empalizadas deben tener uno,
  más largo que ir recto (ahora 31 y 29 m, frente a 20).
- Yari salta 1,60 m en parado y 1,05 m en carrera (unos 3,4 m de largo). Los troncos asoman 0,6 m; el muro, de 0,5 a 1,25 m.

## Cómo está hecho

Todo lo genera `Ayni > Entorno > Construir Aldea Inca`. No hay modelos importados: muros, techos, cántaros y tejidos son código.

- **`Editor/AyniVillageBuilder.cs`**: dónde va cada conjunto y qué tiene (`LayoutChasquis`, `LayoutTejedores`, `LayoutTambo`, `LayoutPortada`),
  el aplanado del terreno, los materiales y el punto de inicio de Yari.
- **`Editor/AyniVillageGeometry.cs`**: las piezas. `Wall` hace un muro inclinado con vanos trapezoidales (puertas, ventanas, hornacinas) y su
  dintel; `GableRoof` y `ConeRoof`, los techos de ichu por tandas; `RoundWall`, colcas y torreones.
- **`Editor/AyniVillageTextures.cs`**: pinta la paja, la madera, los tejidos y la cerámica y los guarda como PNG.
- **`Shaders/AyniMuroInca.shader`** (`Ayni/Muro Inca`): la mampostería se dibuja en el shader, sin texturas. `_Fine = 0` es pirca,
  `_Fine = 1` sillería en hiladas y `_Solid = 1` una sola piedra (dinteles, umbrales). Usa las UV de la malla **en metros**.
- **`Scripts/World/AyniEncounterSite.cs`**: el componente de cada conjunto (ver abajo).

Lo generado se guarda en `Assets/Art/Environment/Aldea/` y cuelga de `Ayni_Entorno/Aldea_Inca` en la escena.

### Reglas para no romper nada

1. **`Construir Aldea Inca` se puede repetir**: devuelve el terreno a como estaba, borra la aldea anterior y la vuelve a levantar.
   Si un conjunto cambia de sitio en el código, sus rivales se mudan con él: cada uno vuelve al punto de aparición que ocupaba.
   Por eso **no edites a mano lo que hay dentro de `Aldea_Inca`**: se pierde al reconstruir. Para cambiar algo, cambia el código del trazado.
2. **El terreno se aplana** bajo cada conjunto. Lo que había queda en `Aldea_TerrenoOriginal.bytes`; `Quitar Aldea Inca` lo restaura y
   devuelve a Yari a la plaza.
3. Si se ejecuta `Ayni > Entorno > Crear Acantilados y Quebradas` o `Restaurar Terreno Original`, **hay que reconstruir la aldea después**
   (esos menús reescriben el relieve y el segundo borra todo `Ayni_Entorno`).
4. Yari mide 1,30 m: las casas (puerta de 1,80 m, muro de 2,30 m) y el tamaño de las piedras están pensados para esa escala.
5. **En las casas, lo que se ve y lo que choca son mallas distintas** (`Casa_n/Colision`). La cápsula de Yari y de los rivales mide 1 m de
   ancho y 2 m de alto, bastante más que su cuerpo: por eso el hueco de paso de cada puerta es más ancho y alto que la puerta dibujada,
   y el techo solo choca por dentro (los aleros no, o no dejarían llegar a la puerta). Si se cambia una casa, hay que cambiar las dos mallas.
6. Cada casa es un objeto propio (`Casa_1`, `Casa_2`...) con su luz de fogón. Son luces puntuales sin sombras; el proyecto admite 4 luces
   adicionales por objeto, y por eso las casas no van fundidas en una sola malla.

## Para integrar los rivales (Antigravity)

Cada conjunto tiene en la escena un objeto **`Ayni_Entorno/Aldea_Inca/K?_.../Encuentro_K?`** con el componente `AyniEncounterSite` y estos hijos:

- `PuntoRival_1`, `PuntoRival_2`...: delante de cada puerta y junto a la salida. Su `forward` mira al patio.
- `PuntoInterior_1`, `PuntoInterior_2`...: dentro de las casas, uno por puerta, mirando hacia ella. Para rivales que esperan dentro.
- `Entrada` y `Salida`: 2 m fuera de cada portada, sobre el eje del camino.

Pasos:

1. Coloca cada rival (un objeto con `EnemyController`, como el de Amaru pero con `isBoss` desactivado) en la posición y rotación de un `PuntoRival`.
   **Déjalo en la raíz de la escena o en un objeto propio (por ejemplo `Rivales`), nunca dentro de `Aldea_Inca`.**
2. Arrástralo a la lista **Rivales** del `Encuentro_K?` de su conjunto.
3. Listo: los rivales de la lista quedan ocultos hasta que Yari entra en el patio; entonces aparecen. Desmarca
   *Hide Rivals Until Entered* si prefieres que se vean desde lejos.

La lista de rivales se conserva al reconstruir la aldea.

Desde código:

```csharp
foreach (AyniEncounterSite site in AyniEncounterSite.All) { ... }   // site.Order: 1 a 4 en el sentido de la marcha
Transform donde = site.GetSpawnPoint(0);                            // punto de aparición en el patio
Transform dentro = site.GetInteriorPoint(0);                        // punto dentro de una casa
site.AddRival(nuevoRival);                                          // rival instanciado por código
AyniEncounterSite.OnPlayerEntered += site => { ... };               // Yari acaba de entrar en el patio
AyniEncounterSite.OnCleared += site => { ... };                     // todos sus rivales derrotados (muertos o perdonados)
```

### Al importar un rival nuevo (modelo de Tripo + Mixamo)

`Editor/AyniRivalSetup.cs` (`Ayni > Rivales > Configurar e Integrar Rival 1: Rastreador`) es el modelo a seguir para los demás. Dos cosas
que hace y que no se pueden saltar:

- **Avatar automático y a la escala del modelo.** En el `.meta` del FBX, `humanDescription` debe quedar con `human: []`, `skeleton: []` y
  la misma `globalScale` que la malla (como `Yari_Rigged` y `Amaru_Rigged`). Si no, en Play el esqueleto se estira cien veces: el rival
  no se ve y cada golpe lo lanza decenas de metros. `NormalizeAvatar` lo comprueba y lo corrige.
- **Talla respecto a Yari**, medida en el hueso de la cabeza (`RastreadorHeightVsYari = 1`). La lámina de rivales
  (`ConceptArt/Rivales/rivales_lamina.png`) da las proporciones: Rastreador 1,00 · Saqueador 1,07 · Guardia 1,15 · Cazador de élite 1,24.

Para comprobarlo sin mirar la pantalla: `call Ayni.Editor.AyniVillageBuilder.Rivals` lista a todos los personajes con su altura real,
su vida, su estado y su conjunto. En Play, la cabeza de un rival debe quedar a 1 m del suelo más o menos, no a 100.

En el editor los rivales asignados a un conjunto están desactivados (aparecen al entrar Yari): para verlos hay que activarlos a mano en la Jerarquía.

Cosas a tener en cuenta:

- Los rivales persiguen en línea recta (`EnemyController`): dentro del patio va bien; no saben rodear un muro. Un rival que espera
  dentro de una casa solo sale si Yari está más o menos frente a la puerta: conviene darle poco `aggroRange` (5-6 m) para que
  reaccione cuando Yari entra o se asoma, o enseñarle a ir primero a la puerta.
- El `aggroRange` por defecto es 18 m; el patio más grande mide 32 m de largo.
- El HUD de jefe y el Juicio Ayni son de Amaru (`isBoss`). Un rival normal muere a golpes o se le perdona con la postura rota.

## Probar sin caminarlo entero (con Unity en Play)

Por el puente de agentes (`UserSettings/AyniAgent/cmd.txt`, ver `NIVEL_1_PROLOGO_Y_JUGABILIDAD.md`):

```
call Ayni.Editor.AyniVillageBuilder.TeleportLocal K3 0 -20 10     lleva a Yari ante la portada de K3 (x derecha, z marcha, giro)
call Ayni.Editor.AyniVillageBuilder.Where                         dice en qué conjunto está y en cuáles ha entrado
call Ayni.Editor.AyniVillageBuilder.TeleportObstacle O1 0 -8 10   lleva a Yari 8 m antes de un obstáculo
call Ayni.Editor.AyniVillageBuilder.WhereObstacle O1              dónde está Yari respecto a ese obstáculo
call Ayni.Editor.AyniVillageBuilder.ShotLocal K4 0 1.7 -4 0 2.4 13 62 nombre   foto desde un punto del conjunto (fuera de Play)
```

`Ayni > Entorno > Capturar Vistas de la Aldea` guarda cuatro vistas de cada conjunto en `DebugCaptures/aldea_*.png`.

## Comprobado

- Prólogo completo: Yari despierta en el nuevo inicio y la presentación de Amaru sigue funcionando.
- Corriendo por el eje del camino se atraviesan los cuatro conjuntos por sus dos portadas, y cada uno detecta la entrada de Yari.
- Con la ruta larga: sin saltar, el tronco detiene a Yari; saltando en carrera lo pasa, y el muro se salta en parado.
- Yari entra y sale de las casas caminando (probado en una casa de cada conjunto y en la kallanka) y sube a sus plataformas.
- Dentro de un cuarto la cámara se acerca a Yari en vez de atravesar muros o techo.

## Pendiente / ideas

- Los rivales de cada conjunto (los desarrolla Brayan; los integra Antigravity con los pasos de arriba).
- Cerrar la salida hasta derrotar a los rivales (hay eventos para hacerlo, no está montado).
- Vegetación, antorchas y gente del pueblo; el patio es el propio terreno.
- Los techos cónicos de colcas y torreones y los aleros no frenan la cámara.
- El mapa es abierto: se puede rodear un conjunto por fuera en vez de cruzarlo.
