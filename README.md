# Someone's Home

Juego de terror narrativo en primera persona basado en relatos reales con toques sobrenaturales.
Hito 3 · *La Atmósfera Narrativa* · Desarrollo de Videojuegos (24UC00320).

- **Unity:** 6000.6.4f1 (usar EXACTAMENTE esta versión: otra versión reimporta y rompe archivos)
- **Pipeline:** URP · **Plataforma de entrega:** WebGL
- **Paquetes clave:** Cinemachine, Timeline, Shader Graph, Input System, AI Navigation

Diseño completo: [`docs/DISENO.md`](docs/DISENO.md) · Relato 1: [`docs/RELATO_01_EL_RIO.md`](docs/RELATO_01_EL_RIO.md)

## Relato 1 · *Across the River* (jugable)
- Escena: `Assets/_Project/Scenes/Hito3_Main.unity` → **Play**.
- Controles: WASD/flechas · ratón · Shift correr · E interactuar · Esc soltar ratón · R reiniciar al final.
- El greybox se generó con `Relato1Builder.Build()` (herramienta de editor, sin menú).
  ⚠️ SOBRESCRIBE la escena: no volver a ejecutarlo si se editó la escena a mano.
- La casa y el columpio son modelos de Blender; las cajas del greybox siguen ahí **sin MeshRenderer** como colliders
  (no borrarlas). Lo mismo con las piedras del sendero (`Roca_xx`): el modelo va encima, el cubo es el collider.
  Placeholders restantes: audio procedural
  (`VO_*` necesitan voces grabadas), materiales `M_Creature`, `M_Water`, `M_CreatureEyes` (shaders propios;
  `M_Creature` ya trae la textura PBR `PielCriatura` como base).

### Texto y sonidos
- Los diálogos **no tienen voz**: el texto sale letra a letra (TextMeshPro) con un "tic" generado por código
  (se puede cambiar por un clip propio en `HUD → Type Blip`).
- Sonidos externos: poner los `.wav/.ogg` en `Assets/_Project/Audio/SFX/` y arrastrarlos al `AudioSource`
  correspondiente (si tiene clip, `ProceduralAudio` ya no genera el suyo).
- Eventos "algo suena, ve a revisarlo": GameObject vacío donde debe sonar + componente **`SoundEvent`**
  (clip, cuándo, pensamiento, objetivo, tensión). Se dispara solo o desde el `onEnter` de un `NarrativeTrigger`.

### Arte generado en Blender (Blender 5.x, por código)
| Qué | Script | Salida | Aplicar en Unity |
|---|---|---|---|
| Texturas PBR tileables (madera, roca, corteza, pasto, tierra, barro, yeso, tela, mezclilla, metal oxidado, mantel, piel de la criatura) | `Tools/Blender/generate_pbr_textures.py` | `Art/Textures/PBR/<Set>/T_<Set>_*.png` | `PBRMaterialSetup.Apply()` |
| Personajes: Sam (sentado/de pie/corriendo), Tammy, Emily, la criatura (cabeza y mandíbula separadas) | `Tools/Blender/build_characters.py` (+ `Characters.blend` para retocar a mano) | `Art/Models/Characters/*.fbx` | `CharacterSetup.ApplyAll()` |
| Naturaleza: pinos, álamos, árbol seco (3 LODs), artemisa, rocas, piedra plana del sendero, pasto, juncos, cerca, poste de luz, tronco caído, tocón | `Tools/Blender/build_nature.py` (+ `Nature.blend`) | `Art/Models/Nature/*.fbx` | `NatureSetup.ApplyAll()` |
| Casa (revestimiento, techo de tejas, ventanas, porche, cocina amueblada, puerta con bisagra) y columpio (glider de metal oxidado) | `Tools/Blender/build_environment.py` (+ `Environment.blend`) | `Art/Models/Environment/*.fbx` | `EnvironmentSetup.ApplyAll()` |

```bash
blender -b --factory-startup --python Tools/Blender/generate_pbr_textures.py -- [--only Tela,Mantel]
blender -b --factory-startup --python Tools/Blender/build_characters.py
blender -b --factory-startup --python Tools/Blender/build_environment.py
blender -b --factory-startup --python Tools/Blender/build_nature.py
```
Los métodos de Unity están en `SomeonesHome.EditorTools` (Editor/); se ejecutan desde código (Unity Pipeline o un script de editor).
`CharacterSetup.ReplaceInOpenScene()` cambia los modelos sin tocar el resto de la escena ni las referencias.
`NatureSetup.Populate()` regenera todo el objeto `Naturaleza` (semilla fija → mismo resultado) y el pasto del terreno.
Deja libres a propósito: casa, camino casa → sendero, sendero, columpio y el fondo de la criatura (su silueta debe
recortarse contra el cielo). Pasto y juncos son *detalles del terreno* instanciados (baratos en WebGL).

### Pruebas
- `AutoPlaytest` (Scripts/Dev): bot que camina rutas con el mismo `CharacterController` del jugador y avisa en consola
  `[Playtest] Atascado…` / `Se cayó del mapa`. No va en la escena; se añade en Play desde código.
- Mandos: `GamepadGuard` ignora un mando cuyo stick ya está desviado al conectarse (drift) hasta que se mueva.
  En Linux, el DualShock 4 por Bluetooth puede mandar datos basura al Editor (camina solo / "pulsa" botones):
  si pasa, desconéctenlo para probar. En WebGL el navegador lee el mando por otra vía.

## Estructura de carpetas

```
Assets/
├── _Project/                 Todo lo propio del juego
│   ├── Scenes/               Hito3_Main.unity (escena de entrega)
│   │   ├── Relatos/          Una escena por relato
│   │   └── Testing/          Escenas personales de prueba (Test_<Nombre>.unity)
│   ├── Scripts/
│   │   ├── Core/             GameManager, TensionManager, utilidades
│   │   ├── Player/           Controlador FPS (CharacterController), linterna
│   │   ├── Interaction/      Raycast de interacción, puertas, objetos revisables
│   │   ├── Events/           Eventos de tensión ("algo suena, ve a revisar")
│   │   ├── Narrative/        Tareas, diálogos, notas, progresión de noches
│   │   ├── Audio/            Ambiente, pasos, sonidos 3D
│   │   ├── Cinematics/       CinematicTrigger, control de Timeline
│   │   ├── UI/               HUD mínimo, subtítulos, menú
│   │   └── Editor/           Herramientas solo de editor
│   ├── Art/                  Models · Materials (PBR) · Textures · Animations
│   ├── Audio/                Ambience · SFX · Voices · Music · Mixers
│   ├── Prefabs/              Player · Props · Environment · Events · Lighting · VFX
│   ├── Timelines/            Secuencias de Timeline (intro, revelaciones, final)
│   ├── Rendering/            VolumeProfiles (post-proceso) · Lighting (lightmaps/settings)
│   ├── Data/                 ScriptableObjects: Relatos y Events
│   └── UI/                   Fonts · Sprites
├── _MyShaders/               Shader Graph propios (requisito de rúbrica)
│   ├── Graphs/               SG_*.shadergraph
│   ├── SubGraphs/
│   └── Materials/            M_* que usan los shaders propios
├── _MyNarrative/             Pedido en la práctica S12
│   ├── SetDressing/          Prefabs de las 3 capas narrativas
│   ├── Notes/                Notas, periódicos, fotos del entorno
│   └── Particles/            Polvo en haces de luz, niebla, etc.
├── ThirdParty/               Packs importados (Kenney, Quaternius, ambientCG…)
└── Settings/                 Assets de URP (no tocar sin avisar)
docs/                         Diseño, informe PDF, storyboard
```

### Convenciones de nombres
| Tipo | Prefijo | Ejemplo |
|---|---|---|
| Material | `M_` | `M_MaderaVieja` |
| Shader Graph | `SG_` | `SG_PresenciaDistorsion` |
| Prefab | `P_` | `P_TelefonoFijo` |
| Textura | `T_` + sufijo de mapa | `T_Madera_BaseMap`, `T_Madera_Normal` |
| Timeline | `TL_` | `TL_RevelacionGranero` |
| Volume Profile | `VP_` | `VP_Noche1` |
| Audio | `SFX_` / `AMB_` / `VO_` | `SFX_GolpePuerta` |

## Flujo de trabajo en equipo (Git)

1. **Nunca muevan/renombren archivos desde el explorador**: siempre desde la ventana *Project* de Unity (los `.meta` deben viajar juntos).
2. **Una persona por escena a la vez.** Los conflictos en `.unity` son casi imposibles de resolver. Avisen en el grupo antes de abrir `Hito3_Main.unity`.
   - Para probar cosas usen `Scenes/Testing/Test_<SuNombre>.unity` y conviertan lo que hagan en **prefabs**.
3. **Ramas:** `main` siempre funciona. Trabajen en `feature/<tema>` (ej. `feature/player-controller`, `feature/shader-presencia`) y fusionen con Pull Request.
4. Antes de hacer push: `git pull`, abrir Unity, verificar que **no hay errores en consola**.
5. Commits con formato: `feat(player): ...`, `fix(lighting): ...`, `art(props): ...`.
6. Entrega final: `git tag U3-Hito3-Final && git push origin --tags`.

### Roles (según práctica S12)
| Rol | Responsable de |
|---|---|
| Set Dresser | Props, capas narrativas, colliders |
| Lighting Artist | Luces, bake, Volume / post-proceso |
| Gameplay Dev | Player, eventos, triggers, Cinemachine/Timeline |
| Tech Artist | Shader Graph propios, materiales PBR |

### Merge de escenas/prefabs (opcional, recomendado)
Configurar UnityYAMLMerge una vez en cada PC (`~/.gitconfig`):
```
[merge "unityyamlmerge"]
    name = Unity SmartMerge
    driver = '<RutaUnity>/Editor/Data/Tools/UnityYAMLMerge' merge -p %O %B %A %A
```
