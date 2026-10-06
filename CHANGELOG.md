# Historial de versiones — AYNI: El Retorno del Guardián

Formato del equipo (ver `COLLABORATORS.md`): **MAYOR.MENOR.PARCHE**
- **MAYOR**: hitos (Alpha, Beta, demo jugable).
- **MENOR**: mejoras de jugabilidad, sistemas, niveles.
- **PARCHE**: arreglos, ajustes de balance, cambios menores.

La versión en uso está en `Assets/Scripts/Core/AyniVersion.cs`. El HUD la muestra junto al título.
Al publicar una versión en `main` se marca con un tag: `git tag -a v0.6.0 -m "..."` y luego `git push origin v0.6.0`.

---

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
