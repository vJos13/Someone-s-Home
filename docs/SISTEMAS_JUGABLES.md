# Sistemas jugables · Relato 1 (Across the River)

Menú principal, inventario, inspección de pistas, linterna con batería, señal del celular y brillo.
Ambientación: **agosto de 1998**, como si fuera una cinta VHS grabada esa noche.
Todo se arma por código al cargar la escena: no hace falta arrastrar prefabs ni tocar `Hito3_Main.unity`.

## Controles

| Acción | Teclado / ratón | Mando |
|---|---|---|
| Caminar / correr | WASD · Shift (o doble toque de W) | Stick izquierdo · L3 |
| Saltar | Espacio | Y |
| Mirar | Ratón (vertical limitado a ±85°) | Stick derecho |
| Interactuar / agarrar (`[E] Agarrar linterna`) | E | A |
| Soltar lo que tienes en la mano | Q | X |
| Elegir espacio del inventario (también vacíos) | 1 – 5 · rueda (da la vuelta) | Cruceta ← → |
| Manos vacías | Elegir un espacio vacío | — |
| Linterna: sacar y prender / apagar | F · clic izquierdo (con la linterna en la mano) | RB |
| Enfocar (zoom suave mientras se mantiene) | Clic derecho | Gatillo izquierdo |
| Zoom libre | Ctrl + rueda · pellizco | — |
| Celular | Tab · elegirlo en el inventario | — |
| Inspección: girar / acercar / dejar | Arrastrar con clic izquierdo · rueda · E o clic derecho | Stick derecho · stick izquierdo · B |
| Pausa | ESC | Start |

## Qué hace cada sistema

**Menú principal** (`UI/MainMenuController.cs`). Funciona dentro de la misma escena del relato: la cámara pasea despacio
junto al columpio del río, que se mece solo, con luciérnagas y polvo en el aire. JUGAR funde a negro con una ráfaga de
viento y el relato arranca ahí mismo, sin pantalla de carga, porque la intro ya empieza en negro. OPCIONES abre la
Configuración del juego: cámara, audio, accesibilidad, gráficos, y pantalla con **Objetivos** (`UI/HUD.cs`). `SetObjective` cambia el texto al instante. `CompleteObjective()` tacha y atenúa el
objetivo en el mismo momento en que el jugador lo cumple y suena un acorde suave.

- En las listas de tareas cada línea se tacha sola al cumplirse.
- El objetivo anterior, ya tachado, queda un momento encima del siguiente.
- Ejemplos: agarrar la mochila, dejarla en la sala, tirar la bolsa (antes del vistazo a la colina), abrir el candado
  y subir el breaker.

**Resaltado de objetos de misión** (`Interaction/ObjectiveHighlighter.cs`, `Resources/VFX/ObjectiveGlow.shader`).
`ObjectiveHighlighter.Mark(objeto)` le da un borde cálido que pulsa despacio y que la niebla apaga de lejos.

- Solo brilla mientras el objeto está activo y disponible, así que se apaga al agarrarlo o usarlo.
- En las entregas el destino solo está disponible si llevas el objeto, así que el brillo pasa solo al destino.
  Si sueltas el objeto lejos, brilla la copia que quedó en el suelo.
- Los objetos de malla propia brillan con su forma. Los que forman parte del lote estático de la escena (tacho,
  caja del breaker, interruptor) y las zonas sin malla se envuelven con una caja o un cilindro más tenue.
- Resaltan: mochila y sala, platos y mesa, bolsa y tacho, linterna y llave, candado y breaker, cortina, cerrojo e
  interruptor, saco de dormir, collar y camioneta de papá.

**Subtítulos**: salen centrados justo encima de la barra de objetos (`HUD.SubtitleBottom` = 172 px de referencia).
El aviso `[E] …` sigue debajo de la mira, lejos de los dos.

**Cinta VHS de 1998**

- **Filtro** (`Shaders/VHS.shader`, `Rendering/VHSRendererFeature.cs`, `Rendering/VhsLook.cs`). Es un pase de
  pantalla completa después del post. Se calcula en sRGB, como la señal de video.
  - Luma nítida, con el color más ancho y corrido a la derecha.
  - La imagen tiembla un poco línea a línea, cada tanto baja una banda de tracking y hay ruido de cabezal abajo.
  - Grano, dropouts y líneas de barrido muy suaves.
  - Negros levantados y saturación de cámara casera.
  - Corre solo en la última cámara de la pila, así que también filtra lo que se tiene en mano.
  - En Opciones → PANTALLA: «Filtro de cinta (VHS)» con Apagado, Suave (el predeterminado, deja ver el detalle en
    alta resolución) o Cinta VHS.
  - Saltos de tracking con `VhsLook.Pulse()`: al darle JUGAR, en cada tarjeta de corte de escena y, de forma
    continua, cuando la entidad está cerca (la misma medida que hace titilar la linterna).
- **OSD** (`UI/TapeOsd.cs`):
  - Arriba a la izquierda: `PLAY ▶ SP 0:00:00`, que pasa a `PAUSE` (parpadea) en la pausa y a `STOP` en el menú.
  - Abajo a la derecha: `PM 6:12` / `AUG.14 1998`. La hora avanza con cada momento del relato: el atardecer del
    14, la noche, el apagón cerca de las 3:07 del reloj parado y la mañana del 15.
  - Se oculta con «Fecha y contador de la cinta».
- **Post** (`VP_Relato01_Base`):
  - El grano y la viñeta del post bajaron porque ya los pone la cinta.
  - Se agregó una distorsión de barril leve, como el lente de un camcorder.
  - Sombras algo más frías y densas, con altas luces cálidas que se queman un poco (sensor CCD).
  - El SSAO pasó de 0,4 a 0,65 para más contacto entre objetos.
- **Celular de época** (`UI/Phone.cs`, `InspectModels.PhoneModel`, `ItemIcons`):
  - Teléfono de barra con antena, LCD verde monocromo con rejilla de píxeles, señal y batería.
  - Mensajes en MAYÚSCULAS sin tildes y teclas programables (`Q:LLAMAR  TAB:SALIR`).
  - Teclado 4×3 con letras, timbre electrónico de dos tonos y tres bips para los SMS.
- **Menú y tarjetas**:
  - Tipografía monoespaciada (Noto Sans Mono, licencia OFL, en `Resources/Fonts`).
  - Etiqueta de cinta `VHS · SP · T-120`.
  - Al darle JUGAR suena el golpe de la casetera y salta el tracking.
- **Instalación**: el filtro ya está agregado a `PC_Renderer` y `Mobile_Renderer`. Si se pierde, menú
  *Someone's Home → Instalar filtro VHS*.

**Calidad Ultra / 4K y atmósfera**

- **Preset Ultra** (`Core/GraphicsQuality.cs`):
  - Se renderiza a resolución nativa, con sombras de 4096 px en 4 cascadas hasta 70 m y SMAA en calidad Alta.
  - Filtrado anisótropo forzado, distancia de visión lejana y pasto al 100 %.
  - No hay TAA: URP lo desactiva cuando hay cámaras apiladas, y la cámara de lo que se tiene en mano siempre lo
    está.
- **Resolución**: en Opciones → PANTALLA, la lista sale de lo que el monitor soporta y marca 4K y QHD. No aparece en
  WebGL.
- **Rayos de luz** (`Shaders/SunShafts.shader`, `Rendering/LightShafts.cs`, `Rendering/SunShaftsRendererFeature.cs`):
  - Pase de pantalla antes del post: desde el sol en la imagen se acumula cielo brillante (profundidad lejana) en
    rayos radiales, con dither para que no haya bandas.
  - Solo se encola con el sol delante de la cámara y es más fuerte con el sol bajo, en el atardecer.
  - Se activa con la opción «Rayos de luz» en GRÁFICOS y desaparece si las sombras están desactivadas.
- **Haz de la linterna** (`Resources/VFX/LightCone.shader`, `Rendering/FlashlightHaze.cs`):
  - Dispersión simple: en cada píxel se integra a lo largo del rayo de la cámara cuánta luz del foco se dispersa,
    con caída de spot y de distancia, hasta la pared o el objeto más cercano.
  - Funciona en primera persona, desde adentro del cono. Sigue los parpadeos de la linterna y es más marcado con
    más niebla.
- **Reflejos del río** (`Rendering/WaterReflections.cs`):
  - Una sonda de reflexión en tiempo real sobre `Water_Rio` se recaptura en cada momento del relato y cada 20 s,
    repartida en varios frames.
  - Así el agua refleja la orilla real y no un cielo celeste.
  - Necesita «sondas en tiempo real» en QualitySettings: se encienden solo mientras se usan y se restauran al salir.
  - Se activa con la opción «Reflejos del agua» en GRÁFICOS; requiere sombras Medias o más.
- **Instalación**: *Someone's Home → Instalar filtro VHS y rayos de luz* agrega los dos pases a `PC_Renderer` y
  `Mobile_Renderer`. Ya está hecho.

**Personajes animados (Blender)**

- **Origen**: `Tools/Blender/build_rigged_characters.py` (Blender 5.x).
  - Genera una malla con esqueleto por personaje, con pesos calculados por distancia a los huesos.
  - Exporta `Resources/Characters/<Nombre>_Rig.fbx`.
  - Para verlos: `blender -b --factory-startup --python Tools/Blender/build_rigged_characters.py`. La hoja de
    control sale de `Tools/Blender/preview_rigs.py -- <carpeta>`.
  - Más detalle que los modelos anteriores: subdivisión 2, ojos con blanco, párpados, cejas, boca, pulgar, suelas,
    cinturón, cordones y bolsillo del buzo, y pelo en capas.
- **Clips por personaje**:

  | Personaje | Clips |
  |---|---|
  | Sam | Idle, Walk, Run, Sit, Shiver, Scream |
  | Tammy y Emily | Idle, Walk, Talk |
  | La cosa | Idle, Walk, Run, con huesos Head y Jaw que mueve el juego |

- **Importación**: `Editor/RiggedCharacterImport.cs` los importa con animación Legacy y remapea los materiales.
  Si hace falta, menú *Someone's Home → Reimportar personajes animados*.
- **`Characters/RiggedCharacter.cs`**:
  - Reemplaza en tiempo de ejecución al modelo quieto de la escena, que queda oculto, sin tocar la escena.
  - Elige el clip solo: Run o Walk según la velocidad real, medida en LateUpdate, y si no, el clip base.
  - Si falta el FBX, todo vuelve a las poses fijas de antes.
- **`Characters/NpcLife.cs`** (Tammy y Emily):
  - En la cocina caminan entre puntos con pausas, giran la cabeza hacia el jugador si está cerca y delante, y
    gesticulan cuando el subtítulo las nombra (`HUD.CurrentSpeaker`).
  - Tienen cuerpo sólido (cápsula, capa Ignore Raycast para no tapar el [E]).
  - Si el jugador se les pega, se corren y nunca bloquean un paso.
  - En la llegada caminan de verdad hacia la puerta de atrás antes de "entrar".
- **Sam** (`BrotherNPC`): sentado en el columpio (Sit), tiembla (Shiver), grita (Scream), habla temblando y corre
  (Run). El grito ahora suena: antes no tenía clip y se sintetiza una voz con formantes de /a/.
- **La cosa** (`RiverCreature`, `Stalker`): respira y se balancea; la cabeza sigue al jugador y la boca se abre
  sobre la pose animada.

**Persecución por el campo** (`Level1Director.FieldChase`)

- **Cuándo**: en el regreso del río, ya arriba de la colina, la cosa deja de esconderse. Aparece en el borde,
  chilla y corre hacia el jugador por el campo hasta la puerta de atrás.
- **Velocidades**: el jugador corre a 4,4 m/s ([Shift]) y la cosa a 5,6 m/s (6,8 m/s si queda a más de 20 m).
  - Corriendo derecho a la casa se llega con unos metros de ventaja.
  - Caminando o deteniéndose a mirar, te alcanza.
- **Señales**: pezuñas en el pasto cada vez más rápidas y el tracking de la cinta que se rompe al acercarse.
- **Si te alcanza** (a 1,5 m):
  - Se planta delante, la cámara gira hacia su cara, chilla, la cinta se rompe y aparece «Te alcanzó.».
  - Se reintenta desde el borde del campo, con ventaja.
- **Bot**: no se detiene a mirar atrás mientras dura la persecución (`Level1Director.Chasing`).

**Colisiones**

- 42 rocas grandes (`Naturaleza/Rocas`, algunas de 9 m) no tenían collider y se atravesaban. Ahora tienen
  MeshCollider; para repetirlo: *Someone's Home → Arreglar colisiones (rocas)*.
- Los arbustos de artemisa siguen sin collider a propósito: se camina entre ellos.
- Tammy y Emily ya no se atraviesan.
- El límite del mapa está verificado en 16 direcciones: no hay salidas.

**Movimiento** (`Player/FirstPersonController.cs`).

- Salto con Espacio: gravedad de -20 m/s² y unos 0,6 m de altura. El suelo se detecta con el CharacterController
  más una esfera en los pies.
  - Tiene un margen de 0,12 s al salir de un borde y de 0,15 s si se aprieta justo antes de tocar el suelo.
  - En el aire no se puede volver a saltar y el control es menor. Al chocar con el techo corta la subida.
  - Al aterrizar de una caída la cámara se hunde un poco.
- Shift corre con aceleración suave y abre el lente unos 4°. El cambio se escala con "Movimiento de cámara".
- El cursor queda capturado durante el juego. Se libera en la pausa, las opciones, con el celular abierto y en la
  inspección.

**Brillo**. SALIR se oculta en WebGL.
"Volver al menú principal" (pausa) recarga la escena con el menú. "Reiniciar relato" y [R] lo saltan.

**Inventario** (`Player/InventorySystem.cs`, `UI/InventoryHotbar.cs`, `UI/ItemIcons.cs`). Tiene 5 espacios.

- **Agarrar con E**: todo objeto recogible muestra `[E] Agarrar <nombre>`. Al agarrarlo desaparece de la escena,
  entra al primer espacio libre y queda en la mano, con un destello en su espacio, un sonido y el texto
  "Recogiste: …". Si los 5 espacios están ocupados, avisa que sueltes algo.
- **Soltar con Q**: lo que está en la mano cae delante del jugador como objeto físico (Rigidbody con colisión
  continua, así que no atraviesa el terreno). Si hay una pared cerca, cae más cerca para no meterse en ella.
  Se vuelve a agarrar con E. El celular no se suelta.
- **Entregas**: la mochila (en la sala), los platos (en la mesa) y la bolsa (en el tacho) se entregan con E sobre
  el destino o soltándolos con Q a menos de ~1,8 m. Si se sueltan en otro lado, quedan en el suelo hasta que los recojas.

- Siempre hay un espacio activo. Se nota por su borde ámbar más grueso, un halo, el fondo más claro y la escala de 1,08.
  Encima tiene una etiqueta con el nombre del objeto (`MOCHILA  [Q] soltar`) o `Vacío`.
- El objeto del espacio activo se ve en la mano, en la parte baja a la derecha, con inercia al girar.
- La barra de abajo se atenúa sola y se aviva al cambiar de objeto o recoger algo.
- El espacio de la linterna muestra la batería y el del celular las barras de señal.

Objetos y cuándo entran y salen:

| Objeto | Entra | Sale |
|---|---|---|
| Celular | Al empezar | — |
| Mochila | Al agarrarla de la camioneta | Al dejarla en la sala |
| Platos | Al agarrarlos de la mesada | Al ponerlos en la mesa |
| Bolsa de basura | Al recogerla | Al tirarla al tacho |
| Linterna | Durante el apagón | A la mañana siguiente |
| Llave del candado | Durante el apagón | — |
| Collar de Buster | A la mañana siguiente | — |

**Inspección** (`Interaction/InspectionView.cs`, `Interaction/InspectModels.cs`). La pista aparece delante de la cámara,
sobre la escena oscurecida, y se gira con el ratón. Se examinan estas pistas:

- La foto vieja, con la figura alta y flaca entre los árboles. La textura se genera por código: sepia, grano y rayas.
- La nota del refri.
- El reloj parado a las **3:07**.
- El collar con la placa **BUSTER**.
- Las huellas donde la pezuña pasa a pie descalzo. Es opcional: `[E] Examinar las huellas` en el campo, a la mañana siguiente.

La foto y el reloj de la casa forman parte de la malla combinada de la cocina, por eso su versión para examinar
se construye aparte.

**Capa "en mano"** (`Player/ViewModelLayer.cs`). Lo que sostienes y lo que inspeccionas se dibuja con una cámara Overlay
apilada sobre la principal, en la capa **Inspeccion**. Así nunca atraviesa una pared y el zoom no lo agranda.

**Linterna** (`Player/Flashlight.cs`). Solo alumbra en la mano. La batería dura unos 6 minutos encendida:

- Por debajo del 25 % alumbra menos y titila.
- Al llegar a 0 se apaga.
- Apagada, una pila agotada se recupera hasta un 12 %, así nunca te quedas a oscuras del todo.

Cerca de la entidad visible (el acechador o la cosa del río) el haz tiembla y se corta a ratos.

**Celular** (`UI/Phone.cs`). La señal depende de la altura. En la casa y el campo, a unos 20 m, hay 4 barras.
Al bajar la colina hacia el río, a unos 6 m, aparece "SIN SERVICIO". Los mensajes que llegan sin señal entran todos
juntos al subir.

**Candado del cobertizo** (`Narrative/Level1Director.cs`). En el apagón, Tammy dice que la llave está junto a la
linterna. La caja del breaker tiene un candado que solo se abre con la llave en el inventario; al abrirlo, el candado
cae al suelo.

**Brillo** (`Core/GraphicsQuality.cs`, `Core/GameSettings.cs`). Lo aplica un volumen global propio con solo el ajuste
*Lift Gamma Gain*: el color grading de la escena no se toca.

## Estructura de archivos

```
Assets/_Project/
├── Resources/
│   ├── UI/InspectBackdrop.shader       velo oscuro con viñeta detrás de la pista
│   └── VFX/SoftGlow.shader             luciérnagas y polvo del menú (aditivo, respeta la niebla)
└── Scripts/
    ├── Core/        GameSettings (+ Brillo) · GraphicsQuality (+ volumen de brillo)
    ├── Interaction/ Interactor · SimpleInteractable · Door · InspectionView · InspectModels
    ├── Narrative/   Level1Director (menú, inventario, llave y candado, pistas, huellas)
    ├── Player/      FirstPersonController (+ enfoque) · Flashlight · InventorySystem · ViewModelLayer
    └── UI/          MainMenuController · InventoryHotbar · ItemIcons · GameMenu · Phone · HUD
```

Algunos nombres del pedido original ya existían en el proyecto con otro nombre, así que se ampliaron esos archivos
en vez de duplicarlos:

| Pedido | En el proyecto |
|---|---|
| `PlayerController.cs` | `FirstPersonController.cs` (ya tenía CharacterController, head bob y pendientes) |
| `InteractableItem.cs` | `SimpleInteractable.cs` + `Interactor.cs` (raycast y `[E] …`) |
| `FlashlightController.cs` | `Flashlight.cs` |
| `DoorController.cs` | `Door.cs` (abierta, cerrada, trabada) + el cerrojo de la noche en el director |
| `InventorySystem.cs` | `InventorySystem.cs` |
| `MainMenuController.cs` | `MainMenuController.cs` |

## Montaje en el Editor

No hay pasos manuales. Lo único que cambió en la configuración del proyecto:

1. **Tags and Layers**: capa 8 = `Inspeccion`. Si faltara, el código usa la 8 igual.
2. **URP**: *Light Layers* ya estaba activo en `PC_RPAsset` y `Mobile_RPAsset`. La luz de relleno de la mano usa la
   Light Layer 7, así que las pistas se ven igual de día y de noche.
3. Los shaders nuevos están en `Resources`, así entran en el build sin referenciarlos desde una escena.

### Agregar un objeto al inventario

```csharp
// 1. En ItemId (InventorySystem.cs) agrega el valor, y en Defs su nombre y cómo se sostiene.
// 2. En ItemIcons.cs, su forma (opcional: sin forma no se ve ícono).
// 3. Desde el relato:
inventory.RegisterModel(ItemId.MiObjeto, () => InspectModels.CloneVisual(renderer, "Mi objeto"));
inventory.Add(ItemId.MiObjeto, equip: true);
```

### Agregar una pista para examinar

```csharp
InspectionView.Open(InspectModels.CloneVisual(renderer, "Pista"), "Nombre visible");
yield return hud.SayAndWait("", "Lo que piensa Alex…");     // las frases corren mientras se mira
yield return InspectionView.WaitClosed();
```

## Notas

- La **rueda** ahora cambia de objeto. El zoom libre pasó a **Ctrl + rueda** o pellizco, y el clic derecho es el enfoque.
- En el Editor está desactivado *Enter Play Mode → Reload Scene*. Por eso el menú se instala también con
  `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` y los estáticos se reinician al entrar en Play.
- El menú se salta con `-skipMenu` o `-benchmark` (mediciones de rendimiento) y lo salta el bot de pruebas (`AutoPlaythrough`).

## Atardecer y noche con luna (Atmosphere)

- **Atardecer guiado por el relato**: el sol ya no se detiene a 0.8°. Su altura sigue al beat de la historia (`DuskByBeat`: llegada 0 → regreso del río 0.9 → persecución 0.96) y avanza despacio (`duskSeconds`). Baja de ~9° hasta -5° (crepúsculo).
- Colores: la luz pasa de dorada a naranja y a roja; con el sol bajo el horizonte queda un resplandor carmesí y luego violeta. El cielo procedural cambia `_SkyTint`, `_AtmosphereThickness` y `_Exposure`, y el disco del sol se esconde en el horizonte.
- Un Volume creado en tiempo de ejecución (`Atardecer_Gradacion`) aplica split toning (luces rojo-anaranjadas, sombras violetas), con un peso que sigue al avance del atardecer.
- Ambiente y niebla se tiñen y oscurecen poco a poco (mínimo 40 %). Nunca llega al negro.
- **Noche con luna**: la intensidad de la luna nunca baja de `moonLight` (0.9). Ambiente y niebla tienen un mínimo (`nightAmbientMin`, `nightFogMin`). El cielo es azul con disco lunar y poca dispersión (sin franja amarilla en el horizonte).
- Un Volume `Luna_Gradacion` sube la exposición +0.7, desatura y tiñe de azul frío. Las nubes y la tensión ya no apagan la luna.
