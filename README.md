# SOMEONE'S HOME
### Across the River · Relato 01

**Un juego de terror narrativo en primera persona donde el miedo no siempre viene de lo que ves, sino de aquello que intenta imitar lo que conoces.**

<p align="center">
  <em>Hito 3 — La Atmósfera Narrativa</em><br>
  Desarrollo de Videojuegos · 24UC00320
</p>

---

## Sobre el proyecto

**Someone's Home** es una experiencia de terror narrativo en primera persona basada en relatos reales, combinados con elementos sobrenaturales, exploración ambiental y tensión psicológica.

El jugador se adentra en escenarios aparentemente cotidianos que esconden situaciones inquietantes. La narrativa se desarrolla mediante la exploración, los diálogos, los sonidos ambientales, los eventos inesperados y las decisiones del jugador.

El proyecto busca construir una atmósfera inmersiva en la que el entorno también cuenta la historia.

### Información técnica

| Característica | Detalle |
|---|---|
| Motor | Unity 6000.6.4f1 |
| Renderizado | Universal Render Pipeline (URP) |
| Plataforma objetivo | WebGL |
| Perspectiva | Primera persona |
| Género | Terror narrativo / Suspenso psicológico |
| Lenguaje | C# |
| Modelado 3D | Blender 5.x |
| Control de versiones | Git / GitHub |

> **Importante:** utiliza exactamente Unity `6000.6.4f1`. No abras el proyecto con otra versión, ya que podría provocar reimportaciones, modificaciones de assets o incompatibilidades.

---

## Relato 01 — Across the River

<em>«Hay cosas que es mejor no mirar al otro lado del río».</em>

<details>
<summary><strong>Sinopsis</strong></summary>

Alex y su hermano menor Sam llegan a una casa rural para pasar unos días lejos de la ciudad. Todo parece normal hasta que descubren una advertencia inquietante: después del anochecer, no deben abrirle la puerta a nadie.

Durante una exploración cerca del río, los hermanos encuentran algo que no deberían haber visto. A partir de ese momento, la tranquilidad del lugar desaparece y una presencia comienza a acecharlos, utilizando voces y apariencias familiares para atraerlos.

La noche pondrá a prueba su capacidad para distinguir entre lo real y lo que intenta engañarlos.

</details>

<details>
<summary><strong>Características narrativas y jugables</strong></summary>

- Exploración en primera persona.
- Ambientación rural y escenarios de terror.
- Sistema de interacción con objetos y elementos del entorno.
- Diálogos con texto progresivo mediante TextMeshPro.
- Sistema de tensión narrativa y eventos dinámicos.
- Uso de teléfono celular para determinadas interacciones.
- Secuencias cinematográficas mediante Timeline.
- Eventos de sonido que orientan la exploración.
- Criatura con comportamiento y presencia sobrenatural.
- Diferentes desenlaces narrativos.

</details>

### Cómo ejecutar el relato

1. Abrir el proyecto con Unity `6000.6.4f1`.
2. Esperar a que finalice la importación de recursos.
3. Abrir la escena:

   `Assets/_Project/Scenes/Hito3_Main.unity`

4. Presionar **Play**.

### Controles

| Tecla | Acción |
|---|---|
| W / A / S / D | Movimiento |
| Flechas | Movimiento alternativo |
| Ratón | Control de cámara |
| Shift | Correr |
| E | Interactuar |
| Tab | Abrir celular |
| Q | Llamar cuando la historia lo solicita |
| Esc | Liberar el cursor |
| R | Reiniciar al finalizar el relato |

---

## Tecnologías y herramientas

El proyecto utiliza herramientas y sistemas de Unity para integrar sus componentes visuales, narrativos y jugables.

| Tecnología | Uso |
|---|---|
| Unity 6 | Motor principal |
| URP | Renderizado y configuración gráfica |
| Cinemachine | Control y composición de cámaras |
| Timeline | Secuencias cinematográficas |
| Shader Graph | Desarrollo de shaders personalizados |
| Input System | Gestión de controles |
| AI Navigation | Navegación de personajes y agentes |
| TextMeshPro | Sistema de diálogos y textos |
| Blender 5.x | Modelado y generación de recursos 3D |
| C# | Programación de sistemas y mecánicas |
| Git / GitHub | Control de versiones y colaboración |

---

## Estructura del proyecto

La organización del proyecto separa los sistemas principales, recursos artísticos, herramientas de desarrollo y elementos narrativos.

```text
Someone's-Home/
│
├── Assets/
│   │
│   ├── _Project/
│   │   ├── Scenes/
│   │   │   ├── Hito3_Main.unity
│   │   │   ├── Relatos/
│   │   │   └── Testing/
│   │   │
│   │   ├── Scripts/
│   │   │   ├── Core/
│   │   │   ├── Player/
│   │   │   ├── Interaction/
│   │   │   ├── Events/
│   │   │   ├── Narrative/
│   │   │   ├── Audio/
│   │   │   ├── Cinematics/
│   │   │   ├── UI/
│   │   │   └── Editor/
│   │   │
│   │   ├── Art/
│   │   │   ├── Models/
│   │   │   ├── Materials/
│   │   │   ├── Textures/
│   │   │   └── Animations/
│   │   │
│   │   ├── Audio/
│   │   │   ├── Ambience/
│   │   │   ├── SFX/
│   │   │   ├── Voices/
│   │   │   ├── Music/
│   │   │   └── Mixers/
│   │   │
│   │   ├── Prefabs/
│   │   │   ├── Player/
│   │   │   ├── Props/
│   │   │   ├── Environment/
│   │   │   ├── Events/
│   │   │   └── Lighting/
│   │   │
│   │   ├── Timelines/
│   │   ├── Rendering/
│   │   ├── Data/
│   │   └── UI/
│   │
│   ├── _MyShaders/
│   │   ├── Graphs/
│   │   ├── SubGraphs/
│   │   └── Materials/
│   │
│   ├── _MyNarrative/
│   │   ├── SetDressing/
│   │   ├── Notes/
│   │   └── Particles/
│   │
│   ├── ThirdParty/
│   └── Settings/
│
├── Tools/
│   └── Blender/
│       ├── generate_pbr_textures.py
│       ├── build_characters.py
│       ├── build_environment.py
│       └── build_nature.py
│
├── docs/
│   ├── DISENO.md
│   ├── RELATO_01_EL_RIO.md
│   ├── Informe.pdf
│   └── Storyboard/
│
└── README.md
```

---

## Desarrollo artístico

Los recursos visuales se generan mediante scripts de Python ejecutados en Blender 5.x. Esto permite mantener procesos de creación reproducibles y facilita la modificación de los modelos y materiales.

### Texturas PBR

Script: `Tools/Blender/generate_pbr_textures.py`

Genera conjuntos de texturas tileables para materiales como:

- Madera y corteza.
- Roca y piedra.
- Pasto, tierra y barro.
- Yeso y tela.
- Mezclilla y metal oxidado.
- Mantel y piel de la criatura.

Ubicación de salida:

`Assets/_Project/Art/Textures/PBR/<Set>/`

### Personajes

Script: `Tools/Blender/build_characters.py`

Genera los modelos de:

- Sam: sentado, de pie y corriendo.
- Tammy.
- Emily.
- La criatura: cabeza y mandíbula independientes.

Ubicación de salida:

`Assets/_Project/Art/Models/Characters/`

### Entorno y naturaleza

Scripts:
- `Tools/Blender/build_environment.py`
- `Tools/Blender/build_nature.py`

Incluyen la generación de:

- Casa rural y elementos interiores.
- Columpio de metal oxidado.
- Árboles y vegetación.
- Rocas y piedras del sendero.
- Cercas, postes, troncos y tocones.
- Elementos complementarios del terreno.

Ubicación de salida:

`Assets/_Project/Art/Models/`

### Generación de recursos

Ejecutar desde la raíz del repositorio:

```bash
blender -b --factory-startup --python Tools/Blender/generate_pbr_textures.py -- [--only Tela,Mantel]

blender -b --factory-startup --python Tools/Blender/build_characters.py

blender -b --factory-startup --python Tools/Blender/build_environment.py

blender -b --factory-startup --python Tools/Blender/build_nature.py
```

---

## Shaders propios (Shader Graph, URP)
Están en `Assets/_MyShaders/Graphs/`:

| Shader | Tipo | Material | Objeto en escena | Qué hace |
|---|---|---|---|---|
| `SG_Agua` | Lit, transparente | `M_Water` | `Water_Rio` | Dos capas de ruido que se desplazan forman las ondas (normal map). Un Fresnel mezcla el color profundo y el de superficie y vuelve el agua más opaca en ángulos rasantes. |
| `SG_Criatura` | Lit | `M_Creature`, `M_CreatureDark` | La Cosa y el Acechador | Usa la textura PBR `PielCriatura` con tinte. Un borde Fresnel tenue la recorta contra la oscuridad y el vértice se desplaza con la normal para que "respire". |
| `SG_OjosCriatura` | Unlit | `M_CreatureEyes` | Ojos de La Cosa | Emisión HDR con Bloom. Brilla más cuando mira de frente (1 − Fresnel) y pulsa lento. |

Todas las propiedades (`_ColorProfundo`, `_Respiracion`, `_Brillo`, etc.) se pueden ajustar en el Inspector del material.
Para editar un grafo: doble clic en el `.shadergraph`.
Los grafos se generaron con `ShaderGraphBuilder.BuildAll()` (`Scripts/Editor/ShaderGraphBuilder/`).
⚠️ Volver a ejecutarlo sobrescribe los cambios hechos a mano en los grafos.

---

## Herramientas de integración en Unity

Los métodos de configuración y generación están organizados dentro del espacio de nombres `SomeonesHome.EditorTools`.

| Herramienta | Función |
|---|---|
| `PBRMaterialSetup.Apply()` | Configuración de materiales PBR |
| `CharacterSetup.ApplyAll()` | Aplicación de modelos de personajes |
| `CharacterSetup.ReplaceInOpenScene()` | Sustitución de modelos en la escena abierta |
| `EnvironmentSetup.ApplyAll()` | Integración de modelos del entorno |
| `NatureSetup.ApplyAll()` | Configuración de recursos naturales |
| `NatureSetup.Populate()` | Regeneración de vegetación y elementos del terreno |
| `Relato1ExtrasSetup.Apply()` | Integración de tareas, celular, acechador y eventos nocturnos |
| `Relato1Builder.Build()` | Generación inicial del greybox |

> **Precaución:** `Relato1Builder.Build()` sobrescribe la escena. No debe ejecutarse nuevamente después de realizar modificaciones manuales en ella.

`Relato1ExtrasSetup.Apply()` debe ejecutarse después de `EnvironmentSetup.ApplyAll()`.

`NatureSetup.Populate()` regenera el objeto `Naturaleza` y la vegetación del terreno utilizando una semilla fija.

### Consideraciones del escenario

Algunos objetos del greybox se mantienen intencionalmente como colliders:

- Las cajas originales de la casa.
- Los elementos de colisión del columpio.
- Las piedras `Roca_xx` del sendero.

Los modelos visuales se colocan sobre estos objetos. No deben eliminarse los colliders ni sus referencias.

También existen zonas que deben permanecer despejadas para conservar la composición visual y narrativa: la casa, el camino hacia el sendero, el sendero, el columpio y el fondo de la criatura.

La vegetación y los juncos se instancian como detalles del terreno para reducir el coste de renderizado en WebGL.

---

## Narrativa y audio

### Sistema de diálogos

Los diálogos se presentan mediante TextMeshPro con un efecto de escritura progresiva.

Actualmente no cuentan con voces grabadas. El sonido de escritura se genera mediante código y puede sustituirse desde:

`HUD → Type Blip`

### Integración de sonidos

Los sonidos externos deben colocarse en:

`Assets/_Project/Audio/SFX/`

Formatos recomendados: `.wav` y `.ogg`.

Cada sonido debe asignarse al `AudioSource` correspondiente. Si el `AudioSource` ya tiene un clip asignado, `ProceduralAudio` no genera uno adicional.

### Eventos narrativos

El componente `SoundEvent` permite crear acontecimientos sonoros que motivan al jugador a explorar determinadas zonas.

Cada evento puede configurar:

- Clip de audio.
- Momento de activación.
- Pensamiento o mensaje narrativo.
- Objetivo asociado.
- Nivel de tensión.

Los eventos pueden activarse automáticamente o mediante el evento `onEnter` de un `NarrativeTrigger`.

---

## Pruebas y depuración

El proyecto incluye herramientas de desarrollo para comprobar el funcionamiento del relato y detectar problemas durante las pruebas.

### AutoPlaythrough

Ubicación: `Scripts/Dev`

Permite ejecutar automáticamente el relato completo. Es útil para comprobar la progresión narrativa y grabar partidas de demostración.

Incluye la opción:

```csharp
badEnding = true;
```

Esta configuración permite probar el final alternativo abriendo la puerta durante la noche.

### AutoPlaytest

Ubicación: `Scripts/Dev`

Permite comprobar rutas y desplazamientos mediante un bot que utiliza el mismo `CharacterController` del jugador.

Detecta situaciones como:

- Personaje atascado.
- Caídas fuera del mapa.
- Problemas de navegación.

Los mensajes de diagnóstico aparecen en la consola con el prefijo `[Playtest]`.

No forma parte de la escena de entrega: se incorpora durante las pruebas mediante código.

### Compatibilidad con mandos

El componente `GamepadGuard` ignora inicialmente mandos cuyos sticks presentan desviación al conectarse, evitando movimientos involuntarios.

**Nota para Linux:** algunos mandos DualShock 4 conectados por Bluetooth pueden enviar entradas incorrectas al Editor de Unity. Si el personaje se mueve solo o se activan botones involuntariamente, desconectar el mando y repetir la prueba.

En WebGL, la lectura del mando se realiza mediante la API del navegador.

---

## Instalación y ejecución

### Requisitos

- Unity Hub.
- Unity `6000.6.4f1`.
- Módulo de compilación WebGL.
- Git.
- Blender 5.x (para regenerar recursos artísticos).

### Clonar el repositorio

```bash
git clone <URL_DEL_REPOSITORIO>
cd Someone's-Home
```

### Abrir el proyecto

1. Iniciar Unity Hub.
2. Seleccionar **Add project from disk**.
3. Elegir la carpeta del repositorio.
4. Abrir con Unity `6000.6.4f1`.
5. Esperar a que termine la importación.
6. Abrir `Assets/_Project/Scenes/Hito3_Main.unity`.
7. Presionar **Play**.

### Compilación para WebGL

1. Abrir `File → Build Profiles`.
2. Seleccionar Web.
3. Comprobar que la escena principal esté incluida en la compilación.
4. Verificar los ajustes de URP y los recursos.
5. Ejecutar **Build**.

---

## Flujo de trabajo colaborativo

El proyecto utiliza Git y GitHub para organizar el desarrollo en equipo.

### Reglas de colaboración

1. No mover ni renombrar archivos desde el explorador del sistema. Utilizar la ventana **Project** de Unity para preservar los archivos `.meta`.
2. Una sola persona debe modificar `Hito3_Main.unity` a la vez.
3. Para desarrollar y probar cambios individuales, utilizar escenas independientes dentro de `Scenes/Testing/`.
4. Convertir los elementos terminados en prefabs cuando corresponda.
5. Trabajar mediante ramas `feature/` y fusionar los cambios a través de Pull Requests.
6. Antes de subir cambios, ejecutar `git pull` y comprobar que Unity no presente errores en la consola.

### Convención de ramas

```text
main
├── feature/player-controller
├── feature/shader-presencia
├── feature/narrative-system
├── feature/environment
└── feature/audio-design
```

### Convención de commits

Se utiliza un formato basado en Conventional Commits:

```text
feat(player): implementar movimiento en primera persona
fix(lighting): corregir iluminación del sendero
art(props): integrar modelos del entorno
feat(narrative): agregar evento del río
refactor(audio): mejorar sistema de sonidos
```

### Convención de nombres

| Recurso | Prefijo | Ejemplo |
|---|---|---|
| Material | `M_` | `M_MaderaVieja` |
| Shader Graph | `SG_` | `SG_PresenciaDistorsion` |
| Prefab | `P_` | `P_TelefonoFijo` |
| Textura | `T_` | `T_Madera_BaseMap` |
| Timeline | `TL_` | `TL_RevelacionGranero` |
| Volume Profile | `VP_` | `VP_Noche1` |
| Audio | `SFX_`, `AMB_`, `VO_` | `SFX_GolpePuerta` |

### Roles de desarrollo

| Rol | Responsabilidad |
|---|---|
| Set Dresser | Props, ambientación y colliders |
| Lighting Artist | Iluminación, bake y postprocesado |
| Gameplay Developer | Jugador, eventos, triggers y Timeline |
| Technical Artist | Shader Graph, materiales y texturas PBR |

### Unity Smart Merge (opcional)

Se recomienda configurar UnityYAMLMerge para facilitar la resolución de conflictos en archivos compatibles.

Agregar a `~/.gitconfig`:

```ini
[merge "unityyamlmerge"]
    name = Unity SmartMerge
    driver = '<RutaUnity>/Editor/Data/Tools/UnityYAMLMerge' merge -p %O %B %A %A
```

---

## Documentación adicional

La documentación complementaria del proyecto se encuentra en la carpeta `docs/`.

| Documento | Contenido |
|---|---|
| `DISENO.md` | Diseño general y estructura de la experiencia |
| `RELATO_01_EL_RIO.md` | Guion y desarrollo narrativo del primer relato |
| `Informe.pdf` | Documentación académica |
| `Storyboard/` | Referencias visuales y planificación de escenas |

---

## Entrega

**Hito 3 — La Atmósfera Narrativa**

Proyecto desarrollado para el curso de Desarrollo de Videojuegos, orientado a la creación de una experiencia de terror narrativo en Unity.

```bash
git tag U3-Hito3-Final
git push origin --tags
```

---

<p align="center">
  <strong>SOMEONE'S HOME</strong><br>
  <em>Sometimes, the thing outside knows your name.</em><br><br>
  Desarrollo de Videojuegos · 24UC00320
</p>