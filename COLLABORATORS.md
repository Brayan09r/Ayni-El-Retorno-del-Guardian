# 🛡️ Guía Confidencial para Colaboradores — AYNI: El Retorno del Guardián

> **AVISO DE CONFIDENCIALIDAD:** Este repositorio, su código fuente, modelos 3D, arte conceptual, animaciones y diseño de juego son de uso exclusivo y privado para los colaboradores autorizados del equipo de desarrollo. Queda prohibida su distribución pública, filtración o copia externa sin consentimiento del equipo directivo.

---

## 📌 1. Formato Oficial de Control de Versiones

Todo el equipo debe regirse por la convención de versiones acordada para mantener la trazabilidad de los avances del juego:

* **`+1.x.x` (Versión Mayor - Major):** 
  * Cambios de arquitectura profundos.
  * Hitos troncales del proyecto (Milestone Alpha, Beta, Lanzamiento Demo jugable).
  * Cambios drásticos en el motor o rediseño integral de sistemas base.

* **`x.+1.x` (Mejora de Gameplay - Minor):**
  * Mejoras que aportan directamente al gameplay y experiencia del jugador.
  * Incorporación de nuevos sistemas o mecánicas (ej. nuevos combos de Rumi Maki, árbol de habilidades del Talismán Illa, nuevos tipos de enemigos con IA avanzada).
  * Nuevos escenarios, niveles o sistemas de interfaz (Canvas UI moderno).

* **`x.x.+1` (Parche / Bugs - Patch):**
  * Corrección de bugs o errores de programación.
  * Solución a trabas en animaciones o transiciones del Animator.
  * Ajustes de balance numérico (ventanas de parry, tiempos de recuperación, daño, velocidades).
  * Mejoras cosméticas o cambios menores.

---

## 🛠️ 2. Guía Rápida para el Colaborador

### Requisitos Técnicos
* **Unity:** `6000.3.23f1` (o versión Unity 6 compatible fijada por el equipo).
* **Render Pipeline:** Universal Render Pipeline (URP).
* **Control de Versiones:** Git con soporte para Git LFS (para modelos FBX, texturas y audios pesados).

### Primeros Pasos al Clonar
1. Clonar el repositorio:
   ```bash
   git clone https://github.com/Brayan09r/Ayni-El-Retorno-del-Guardian.git
   ```
2. Abrir el proyecto en Unity Hub con la versión correspondiente.
3. Asegurarse de que el render pipeline esté activo:
   * Menú superior en Unity: `Ayni` ➔ `0. Arreglar Color Magenta del Terreno (Activar URP)`.
4. Cargar la configuración de combate y escena:
   * Menú superior en Unity: `Ayni` ➔ `2. Integrar Combate en el Mapa de Caminos`.
5. Abrir la escena principal:
   `Assets/network of paths/Scenes/SampleScene.unity`.

---

## 📸 3. Sistema de Capturas y Depuración Visual

Para que los agentes y el equipo puedan inspeccionar el juego en ejecución:
* Unity ejecuta capturas automáticas al entrar en Play Mode guardándolas en `DebugCaptures/play_1.png`, `play_2.png`, `play_3.png` junto al log `capture_log.txt`.
* No borres la carpeta `DebugCaptures/` ya que es utilizada para auditorías visuales del rendimiento y posturas de los personajes.

---

## 🤝 4. Reglas de Contribución en Git

1. **Ramas de trabajo:**
   * Trabajar en ramas de características: `feat/nombre-mecanica` o `fix/nombre-bug`.
   * La rama `main` debe mantenerse siempre ejecutable sin errores de compilación de Unity.
2. **Archivos `.meta` de Unity:**
   * **OBLIGATORIO:** Cada vez que agregues un archivo (`.cs`, `.fbx`, `.png`, `.prefab`, etc.), asegúrate de hacer `git add` también a su archivo correspondiente `.meta`.
3. **Mensajes de Commit:**
   * Usar prefijos claros: `feat:`, `fix:`, `refactor:`, `style:`, `docs:`.
