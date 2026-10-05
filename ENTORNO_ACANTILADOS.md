# Entorno andino: acantilados, garganta, andenes y atmósfera

Nota de trabajo para quien toque el terreno o la escena (personas y agentes).
Referencia visual: el GDD de Figma (dirección artística: Machu Picchu, Sacsayhuamán, Ollantaytambo;
"calzadas ciclópeas, andenes y abismos cordilleranos"; "rayos de sol atravesando cañones, niebla de montaña").

## Qué hay ahora alrededor de la zona de combate (612, 468)

- **Garganta principal** de ~350 m: baja por el valle del norte, rodea la arena por el norte y el este y sigue hacia el sur. Unos 29 m de profundidad, con agua (y = -18) y niebla en capas.
- **Acantilados escalonados** en el cerro del noreste y en la cara este del cerro del sur.
- **Dos quebradas laterales** que bajan de esos cerros hacia la garganta.
- **Andenes** con muro de sillería poligonal: en la loma del norte (se ven al iniciar la partida) y en la ladera al suroeste de la arena.
- **Calzada inca**: todos los caminos del mapa (el Qhapaq Ñan) se dibujan como losas de piedra con juntas de tierra y pasto. La arena queda como una plaza empedrada.
- **Puente colgante** de sogas donde la garganta corta el camino, 26 m al este de la arena.
- **Hora dorada**: sol bajo del oeste, cielo de atardecer, bruma cálida y posprocesado con la paleta del GDD.
- **Cordillera nevada** de fondo, alrededor de todo el mapa.

## Cómo está hecho

| Pieza | Archivo |
| --- | --- |
| Herramienta del relieve (menú `Ayni > Entorno`) | `Assets/Editor/AyniEnvironmentBuilder.cs` |
| Atmósfera, niebla y cordillera | `Assets/Editor/AyniAtmosphere.cs` |
| Relieve nuevo (solo la zona modificada) | `Assets/Art/Environment/Ayni_HeightPatch.bytes` |
| Mapas de control del shader | `Assets/Art/Environment/Ayni_TerrainControl.png` y `Ayni_TerrainControl2.png` |
| Shader del terreno | `Assets/Shaders/AyniTerrenoAndino.shader` |
| Shaders de agua, niebla y cordillera | `Assets/Shaders/AyniAguaQuebrada.shader`, `AyniNiebla.shader`, `AyniMontanasLejanas.shader` |
| Red de seguridad al caer | `Assets/Scripts/World/AyniAbyssRescue.cs` |
| Prueba del puente (en Play) | `Assets/Editor/AyniEnvironmentDebug.cs` |

Puntos a tener en cuenta:

1. **El objeto Terrain está en y = -40** y todo el mapa de alturas está subido 40 m. Un terreno de Unity no puede bajar de su cero, y así se pudo excavar. El mundo queda igual: usar siempre `terrain.SampleHeight(p) + terrain.transform.position.y`.
2. **El terreno usa el material `Ayni_Terreno.mat`** (shader `Ayni/Terreno Andino`). Reproduce la mezcla del material original y añade: roca automática en pendientes fuertes (triplanar), losas de calzada sobre la máscara de caminos y muros de andén. `_Paved = 0` devuelve los caminos de tierra.
3. **Mapas de control**. El primero: R = borra el camino, G = lecho húmedo, B = roca forzada. El segundo: R = zona de andenes.
4. Todo cuelga del objeto **`Ayni_Entorno`** de la escena (agua, niebla, cordillera, puente, red de seguridad). `Crear Acantilados y Quebradas` se puede repetir: lo reconstruye sin duplicar y aplica también la atmósfera.
5. **La atmósfera cambia ajustes compartidos**: la luz direccional, el cielo (`Ayni_Cielo.mat`), la niebla y la luz ambiente de la escena, el perfil `Assets/Art/Ayni_PostProcess_Profile.asset`, el plano lejano de la cámara (6000) y, en el asset de URP, la textura de profundidad, la distancia de sombras (140 m) y 4 cascadas. `Ayni > 4. Mejorar Iluminación y Contraste` pisa parte de esto; después hay que volver a aplicar `Ayni > Entorno > Aplicar Atmósfera de Hora Dorada`.
6. **Copia del terreno original** en `EnvironmentBackups/Terrain.asset.original` (fuera de Assets e ignorada por git). `Ayni > Entorno > Restaurar Terreno Original` devuelve el relieve y el material; no deshace la atmósfera.
7. Quien cae por debajo de y = -8 vuelve al último suelo firme que pisó. Hoy no tiene coste para el jugador.

## Pendiente

- Arquitectura inca (muros, portadas trapezoidales, escalinatas, templo): necesita modelos; no se puede hacer bien solo con el terreno.
- Vegetación (ichu, queñuales), rocas sueltas, antorchas y cascadas.
- Rayos de sol volumétricos.
- El rival persigue en línea recta: puede caer a la garganta si Yari cruza el puente.
- Decidir si caer debe costar vida o años del Illa.
