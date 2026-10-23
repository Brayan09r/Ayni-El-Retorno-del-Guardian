# Iluminación andina: rayos de sol volumétricos, braseros ceremoniales y ciclo dinámico

Nota de trabajo de la rama/módulo de **Iluminación** en **AYNI: El Retorno del Guardián**.
Completa los elementos de iluminación pendientes en `ENTORNO_ACANTILADOS.md` (*rayos de sol volumétricos*, *antorchas*) y `ALDEA_INCA.md` (*fogones vivos de patio y portadas*) sin alterar la geometría ni la lógica de combate existente.

---

## 1. Resumen de Componentes de Iluminación

| Pieza | Archivo | Función |
| :--- | :--- | :--- |
| **Shader de Rayos Volumétricos** | `Assets/Shaders/AyniRayosSol.shader` | Shader HLSL (`Ayni/Rayos de Sol`) para haces de luz solar con fundido por profundidad (`SampleSceneDepth`) y ángulo de visión en URP. |
| **Luces de Fuego y Brasas** | `Assets/Scripts/World/AyniFireLight.cs` | Parpadeo orgánico con doble octava de ruido Perlin (intensidad, color térmico y posición), partículas de brasas y *Distance Culling* para respetar el límite de 4 luces adicionales por objeto de URP. |
| **Iluminación Dinámica Narrativa** | `Assets/Scripts/World/AyniDynamicLighting.cs` | Transiciones suaves de sol, cielo, luz ambiente *Trilight* y niebla según la fase del combate y el desenlace moral del *Juicio Ayni*. |
| **Constructor y Menú de Editor** | `Assets/Editor/AyniLightingSetup.cs` | Menú `Ayni > Iluminación` para levantar braseros, encender fogones de patio, colocar rayos volumétricos y previsualizar climas. |

---

## 2. Qué se integra en la escena (`Ayni_Entorno/Iluminacion_Andina`)

Al ejecutar **`Ayni ➔ Iluminación ➔ 1. Construir Iluminación Andina (Antorchas, Fogones y Rayos de Sol)`**:

1. **Braseros Ceremoniales Incas (`Antorchas_Y_Braseros`)**:
   - **16 braseros en las portadas de la aldea (`K1` a `K4`)**: flanquean la entrada y salida de *Puesto de Chasquis*, *Kancha de los Tejedores*, *Tambo del Camino* y la *Portada Monumental del Cazador*.
   - **4 braseros en los estribos del puente colgante**: iluminan el acceso oeste y este sobre la garganta.
   - **4 braseros en la plaza de Amaru `(612, 468)`**: delimitan la arena principal del jefe.
   - Cada brasero genera su pedestal trapezoidal de piedra y una luz puntual cálida con `AyniFireLight` y brasas ascendentes.

2. **Fogones Exteriores e Interiores (`Fogones_Patios` y `Luz_Fogon`)**:
   - Enciende los **5 fogones exteriores (`FirePit`)** construidos en los patios de `K1`, `K2`, `K3` y `K4`.
   - Mejora automáticamente las luces interiores (`Luz_Fogon` y `Luz_Lampara`) de las casas incas para que parpadeen orgánicamente en lugar de permanecer estáticas.

3. **Rayos de Sol Volumétricos (`Rayos_Sol_Volumetricos`)**:
   - Coloca **6 haces volumétricos** orientados en el ángulo solar de la Hora Dorada `(17°, 106°, 0°)` sobre el puente colgante, los cañones norte/sur, la plaza de Amaru y las portadas de `K3` y `K4`.

---

## 3. Estados de Iluminación Dinámica (`AyniDynamicLighting`)

Durante la partida (y desde los menús de previsualización del Editor), el sistema gestiona cinco estados lumínicos:

1. **Hora Dorada (Por defecto)**:
   - Sol cálido a `17°` sobre el horizonte oeste, sombras suaves (`0.78`) y bruma dorada andina.
2. **Combate en Kancha**:
   - Al entrar en un patio con rivales (`AyniEncounterSite.OnPlayerEntered`), acentúa ligeramente el contraste cálido del sol y aviva los braseros un `15%`.
3. **Segunda Fase del Jefe (`FaseDosBrasas`)**:
   - Al activarse la segunda fase de Amaru (*"Desatado"*), el sol desciende a `12°` con tono cobrizo/ámbar, las sombras se endurecen y los braseros arden con un `45%` más de intensidad.
4. **Amanecer Sagrado (`AmanecerAyni` — Perdón / ODS 15)**:
   - Cuando Yari perdona a Amaru, la luz acompaña la lluvia purificadora y el renacer de los queñuales: el sol sube a `26°`, el cielo se vuelve cristalino, la niebla se despeja y la lluvia atenúa las llamas exteriores.
5. **Ocaso de Cenizas (`OcasoVenganza` — Venganza / Hucha)**:
   - Cuando Yari ejecuta a Amaru, el cielo se oscurece (`skyExposure = 0.62`), el sol cae a un rojo ceniza (`8.5°`) y la niebla volcánica se espesa.

---

## 4. Menús de Prueba en Unity

- `Ayni > Iluminación > 1. Construir Iluminación Andina (Antorchas, Fogones y Rayos de Sol)`
- `Ayni > Iluminación > 2. Previsualizar Clima: Hora Dorada (Por defecto)`
- `Ayni > Iluminación > 3. Previsualizar Clima: Segunda Fase del Jefe (Brasas)`
- `Ayni > Iluminación > 4. Previsualizar Clima: Amanecer Sagrado (Perdón / Ayni)`
- `Ayni > Iluminación > 5. Previsualizar Clima: Ocaso de Cenizas (Venganza / Hucha)`
- `Ayni > Iluminación > 6. Capturar Vistas de Iluminación (DebugCaptures)`
- `Ayni > Iluminación > Quitar Iluminación Andina`
