# 📜 NOTA DE TRASPASO PARA CLAUDE: ANIMACIONES DE COMBATE, AMARU 3D Y POSTURA ANDINA
**Proyecto:** AYNI: El Retorno del Guardián (Unity 6000.3.23f1, URP)  
**Fecha:** 4 de Octubre de 2026  
**Autor:** Antigravity (Pair Programming Agent)  
**Destinatario:** Claude (Combat & Gameplay Systems Agent)  
**Rama de respaldo remoto:** `feat/combat-animations` (en `origin`)

---

## 📌 1. RESUMEN EJECUTIVO DE LO REALIZADO

1. **29 Nuevas Animaciones de Combate (Takanakuy / Rumi Maki / Sifu):**
   * Descargadas de Mixamo e importadas en `Assets/Art/Characters/Animations/` utilizando el esqueleto Humanoid de `Yari_Rigged.fbx`.
   * Todas con `Bake Into Pose` en Root Transform (Rotación, Y, XZ), `In Place` en desplazamientos/esquivas, y `Loop Time` estrictamente restringido a los 5 clips cíclicos.
   * Calculados los tiempos exactos de duración y el instante de impacto/contacto para los combos de Yari.

2. **Modelo 3D Riggeado del Primer Usurpador ("Amaru el Cazador"):**
   * Modelo 3D original en T-Pose extraído en máxima resolución PBR desde Tripo3D.
   * Auto-rigging completado en Mixamo (esqueleto estándar Humanoid de 65 huesos).
   * Importado en `Assets/Art/Characters/Amaru_Rigged.fbx` con Avatar Humanoid, material URP Lit `M_Amaru_PBR.mat` y texturas PBR (Albedo, Normal y Roughness).
   * Creado `Assets/Art/Characters/Amaru_AnimatorController.controller` configurado para interactuar con `EnemyController.cs`.
   * Creado el script de editor `Assets/Editor/AyniAmaruSetup.cs` (con menú `Ayni > Jefes > Integrar Jefe Amaru (Reemplazar Cápsula)`), el cual sustituyó permanentemente la cápsula primitiva `Jefe_ApoRumi_Test` en `SampleScene.unity` por el modelo real `Jefe_Amaru_ElCazador`.

3. **Modificador de Postura Andina y "Game Feel" (`AndeanCombatStanceModifier.cs`):**
   * Componente procedural añadido en `Assets/Scripts/Combat/AndeanCombatStanceModifier.cs`.
   * En tiempo de ejecución ("LateUpdate"), ajusta el esqueleto Humanoid para transformar animaciones genéricas en pelea ritual andina:
     * **Centro de masa bajo:** Desplaza `Hips` -12 cm para flectar rodillas y clavar el peso en la tierra.
     * **Inclinación agresiva:** Rota `Spine` 8° hacia adelante.
     * **Guardia abierta de Takanakuy:** Abre los codos (`UpperArms`) 14° lateralmente hacia afuera.
     * **Hitstop Procedural:** Micro-pausa de 0.065s al conectar un impacto (`ApplyHitstop()`), otorgando la sensación pesada de "Puño de Piedra" (*Rumi Maki*) estilo *Sifu*.

---

## 🥊 2. TABLA TÉCNICA DE LAS 29 ANIMACIONES IMPORTADAS

Todas se encuentran en: `Assets/Art/Characters/Animations/`

| # | Archivo FBX | Nombre Original Mixamo | Duración | Contacto Estimado | Loop Time | Notas de Uso |
|---|---|---|---|---|---|---|
| **A** | **Combo Ligero** | | | | | |
| 1 | `Light_Punch_1_L.fbx` | Lead Jab | 1.33 s | **0.56 s** | No | Jab directo de izquierda seco |
| 2 | `Light_Punch_2_R.fbx` | Cross Punch | 2.00 s | **0.88 s** | No | Directo pesado con cadera |
| 3 | `Light_Punch_3_L.fbx` | Hook | 1.30 s | **0.62 s** | No | Gancho lateral corto de izquierda |
| 4 | `Light_Punch_4_R.fbx` | Punching | 1.00 s | **0.50 s** | No | Remate descendente rápido de derecha |
| **B** | **Golpes Pesados** | | | | | |
| 5 | `Heavy_Overhand.fbx` | Right Hook *(sustituyó pose estática)* | 1.10 s | **0.57 s** | No | Overhand pesado con todo el torso |
| 6 | `Heavy_Uppercut.fbx` | Uppercut | 1.33 s | **0.60 s** | No | Gancho ascendente vertical al mentón |
| 7 | `Heavy_Elbow.fbx` | Elbow Punch | 1.73 s | **0.69 s** | No | Codazo frontal seco y contundente |
| 8 | `Heavy_Headbutt.fbx` | Headbutt | 2.10 s | **0.97 s** | No | Cabezazo frontal de choque ritual |
| 9 | `Heavy_FrontKick.fbx` | Kicking | 1.60 s | **0.83 s** | No | Patada frontal baja de empuje |
| 10 | `Shove_Push.fbx` | Pushing | 3.97 s | **1.51 s** | No | Empujón con ambas manos |
| **C** | **Defensa & Esquivas** | | | | | |
| 11 | `Guard_Idle.fbx` | Body Block | 3.43 s | — | **SÍ** | Guardia cerrada frontal en bucle |
| 12 | `Guard_BlockHit.fbx` | Standing Block React Large | 1.33 s | — | No | Absorción de impacto en bloqueo |
| 13 | `Parry_Deflect.fbx` | Outward Block | 2.53 s | — | No | Desvío rápido de antebrazo (Parry) |
| 14 | `Dodge_Duck.fbx` | Ducking | 1.53 s | — | No | Esquiva Sifu: Agacharse bajo golpe alto |
| 15 | `Dodge_HopBack.fbx` | Dodging Back | 2.67 s | — | No | Salto corto de retroceso (In Place) |
| 16 | `Dodge_Left.fbx` | Dodging Right *(Mirror: 1)* | 1.43 s | — | No | Esquiva lateral izquierda espejada |
| 17 | `Dodge_Right.fbx` | Dodging Right | 1.43 s | — | No | Esquiva lateral derecha |
| **D** | **Movimiento en Guardia (Fijación de Blanco)** | | | | | |
| 18 | `Strafe_Left.fbx` | Walk Strafe Left | 1.47 s | — | **SÍ** | Strafe lateral izquierdo en bucle |
| 19 | `Strafe_Right.fbx` | Walk Strafe Right | 1.47 s | — | **SÍ** | Strafe lateral derecho en bucle |
| 20 | `Walk_Back.fbx` | Walking Backwards | 0.87 s | — | **SÍ** | Retroceso defensivo en bucle |
| **E** | **Reacciones de Impacto & Caída** | | | | | |
| 21 | `Hit_Head.fbx` | Head Hit | 1.43 s | — | No | Sacudida de cabeza por golpe alto |
| 22 | `Hit_Body.fbx` | Stomach Hit | 1.20 s | — | No | Flexión por impacto al abdomen |
| 23 | `Hit_Heavy.fbx` | Big Hit To Head | 1.63 s | — | No | Impacto pesado con retroceso |
| 24 | `Stunned_Loop.fbx` | Dizzy Idle | 4.27 s | — | **SÍ** | Mareo / postura rota tambaleante |
| 25 | `Knockdown.fbx` | Knocked Out | 4.93 s | — | No | Caída de espaldas al suelo |
| 26 | `GetUp.fbx` | Getting Up | 8.33 s | — | No | Levantarse del suelo |
| **F** | **Juicio Ayni y Remates** | | | | | |
| 27 | `Finisher_Punch.fbx` | Double Leg Takedown - Attacker | 7.23 s | **4.20 s** | No | Derribo al suelo y golpe final (Venganza) |
| 28 | `Mercy_Offer.fbx` | Standing Greeting | 5.10 s | — | No | Manos abiertas de paz y perdón (Ayni) |
| 29 | `Enemy_Kneel.fbx` | Kneeling Down | 2.77 s | — | No | El rival cae de rodillas rendido |

---

## 🦅 3. AMARU EL CAZADOR (JEFE 1) - DETALLES DE INTEGRACIÓN

* **Malla Riggeada:** `Assets/Art/Characters/Amaru_Rigged.fbx`
  * Rig: `Humanoid` con avatar auto-generado (`CreateFromThisModel`).
  * Altura normalizada a escala humana: ~1.85 m.
* **Material URP:** `Assets/Art/Characters/M_Amaru_PBR.mat`
  * Asignado a todos los `SkinnedMeshRenderer`.
  * Texturas en `Assets/Art/Characters/Textures/`: `Amaru_Diffuse.png`, `Amaru_Normal.png`, `Amaru_MetallicRoughness.png`.
* **Animator Controller:** `Assets/Art/Characters/Amaru_AnimatorController.controller`
  * **Parámetros integrados con `EnemyController.cs`:**
    * `Speed` (Float) → `Idle` (Combat_Idle) ↔ `Move` (Strafe_Right).
    * `Attack` (Trigger) → `Heavy_Elbow` (Codazo frontal).
    * `Hit` (Trigger) → `Hit_Body` (Impacto).
    * `IsStunned` (Bool) → `Stunned_Loop` (Postura rota).
    * `Die` (Trigger) → `Knockdown` (Muerte / Venganza).
    * `MercyKneel` (Trigger) → `Enemy_Kneel` (Perdón Ayni).
* **Objeto en Escena:** `Jefe_Amaru_ElCazador`
  * Componentes configurados: `CharacterController` (h=2, r=0.5), `StructureSystem` (100 postura), `EnemyController` (Amaru el Cazador, IsBoss=true, 220 HP, 18 Daño), `AndeanCombatStanceModifier`.

---

## ⚡ 4. CÓMO USAR EL MODIFICADOR DE POSTURA ANDINA (`AndeanCombatStanceModifier.cs`)

Para darle a los golpes ese impacto "crujiente" de puño de piedra (*Rumi Maki*) estilo *Sifu*:

```csharp
// Al conectar un golpe en YariCombatController o EnemyController:
var stanceMod = GetComponentInChildren<AndeanCombatStanceModifier>();
if (stanceMod != null)
{
    // Micro-pausa de impacto (Hitstop)
    stanceMod.ApplyHitstop(0.065f); // 65 milisegundos de congelación
}
```

Para activar o desactivar la guardia andina (por ejemplo, reducir el peso procedural a 0 durante carreras rápidas o caídas):
```csharp
stanceMod.SetStanceWeight(0f); // Postura neutral
stanceMod.SetStanceWeight(1f); // Postura ritual Takanakuy completa
```

---

## 🌿 5. RECOMENDACIONES PARA EL SIGUIENTE PASO DE CLAUDE

1. **Tabla de Tiempos de Impacto:**  
   Puedes ejecutar el menú `Ayni > Herramientas > Medir Tiempos de Impacto de los Ataques` para recalcular `YariAttackTimings.asset` con la tabla de los 29 clips si deseas ajustar los tiempos de cancelación milimétricos.
2. **Setup Automatizado:**  
   `AyniAmaruSetup.cs` cuenta con el método estático `Ayni.Editor.AyniAmaruSetup.SetupAmaruBoss()` que se encarga de rearmar a Amaru, corregir cualquier inconsistencia de avatar y guardar la escena limpia sin intervención manual.
3. **Control de Versiones:**  
   Todos los cambios y assets nuevos están comiteados y respaldados en la rama remota `feat/combat-animations`.
