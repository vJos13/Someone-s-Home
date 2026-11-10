# Documento de diseño · *Someone's Home*

> Terror narrativo en primera persona, inspirado en el género "slice-of-life horror".
> Lo cotidiano se vuelve inquietante. El miedo viene de la **anticipación**, no del susto.

---

## 0. Concepto

> **SOMEONE'S HOME** · *Based on true accounts*
> El título tiene doble sentido: "alguien está en casa" y "hay alguien en la casa".

Una **antología**: cada relato es un **nivel independiente**, con su propio lugar, personaje y atmósfera.
Lo que une a todos es la premisa del título: siempre hay *alguien* en la casa.
Cada nivel abre con "Basado en el relato de…" y cierra con un epílogo real.

| Nivel | Relato | Escenario | Base real | Toque fantástico |
|---|---|---|---|---|
| 1 | **Not My Dog** | Casa de campo, cuidando la casa de unos dueños que están de viaje | Pareja de Tennessee que despertó con un perro ajeno en la cama (noticia real) + avistamientos de figuras con cara de perro | El perro empieza a imitar gestos humanos: lo ves de pie en la ventana |
| 2 | **Knock Twice** | Casa rural de una mujer sola | Testimonios de voces imitadas y golpes que cambian de puerta a ventana | Alguien toca con la voz de su esposo… que está de viaje |
| 3 | **The Visit** | Por definir | Relatos de intrusos reales | ¿Era sobrenatural o una persona? |

> ⚠️ Los relatos todavía son **borradores**. Hay que reemplazarlos o validarlos con posts reales de Reddit
> (con su enlace) antes del informe. Ver la sección 1.

> **Para el Hito 3:** pulir al 100 % **el Nivel 1** (una zona bien resuelta puntúa más que todo a medias,
> según la práctica S12). Los niveles 2 y 3 quedan como extensión o como teaser en el video.

---

## 1. Fuentes de relatos

### Las más valiosas para el informe: relatos de primera mano
- **Entrevisten a familiares, abuelos o gente de provincia.** En Arequipa, Puno, Cusco y otras regiones hay muchísimos
  relatos de condenados, perros que "ven cosas", luces en los cerros y visitas nocturnas. Graben o anoten la entrevista
  (con permiso). Es la mejor forma de justificar que el juego está basado en **relatos reales**.

### Folklore peruano / andino (con equivalentes a lo que buscan)
| Ser / leyenda | Similar a… | Úsenlo para |
|---|---|---|
| **El Condenado** | Skinwalker (ser que vaga de noche, toca puertas, imita) | Nivel 2 |
| **La Jarjacha** | Criatura con sonido característico ("jar-jar") | Sonido que se acerca |
| **La Uma** | Cabeza voladora | Susto final / easter egg |
| **El Muki** | Duende de las minas | Relato extra |
| **El Pishtaco** | Desconocido que acecha | Ambigüedad humano / sobrenatural |

Fuentes: recopilaciones de José María Arguedas (cuentos quechuas), publicaciones del Ministerio de Cultura sobre
tradición oral, y los relatos que les cuenten sus entrevistados.

### Internet (testimonios en primera persona)
- **Reddit:** r/Thetruthishere, r/Paranormal, r/skinwalkers, r/LetsNotMeet (encuentros reales, sin nada sobrenatural) y r/Glitch_in_the_Matrix.
- **Perro que actúa como persona:** hay muchos testimonios en r/Thetruthishere; también el fenómeno *Dogman*
  (Michigan, Wisconsin) y los libros de Linda Godfrey (*The Beast of Bray Road*).
- **Skinwalkers:** los relatos del Skinwalker Ranch (Utah) y testimonios de imitación de voces.

### Ética (suma puntos en el informe)
- Cambien **nombres, fechas y lugares exactos**. Usen frases como "Basado en el relato de M., 2014".
- *Skinwalker* es un concepto sagrado y tabú para el pueblo **Diné (Navajo)**. Recomendación: usar la idea general
  de "algo que imita" sin apropiarse de sus elementos, o usar el **Condenado** andino, que encaja igual de bien y es nuestro.
- Nada de morbo con víctimas reales.

---

## 2. Eventos de tensión (punto 1)

Sistema central: **TensionManager** + un conjunto de eventos. Regla de oro: **la mayoría son falsas alarmas.**

| Evento | Disparador | Variantes de resultado | Nivel |
|---|---|---|---|
| El perro ladra a un punto fijo | Tiempo / tensión | Nada · deja de ladrar de golpe · el perro ya no está | 1 |
| Teléfono fijo suena | Tarea completada | Silencio · respiración · voz del dueño | 1-2 |
| Golpes en la puerta | Tensión alta | No hay nadie · huellas de lodo hacia dentro | 2 |
| Luz del pasillo parpadea | Trigger de zona | Parpadea · se apaga · el foco desaparece | 1-3 |
| Grifo abierto | Aleatorio | Lo cierras y vuelve a sonar al salir | 1 |
| Mensaje en el celular | Guion | "¿Por qué apagaste la luz?" | 3 |
| TV se enciende sola | Guion | Estática · canal con la casa vista desde fuera | 3 |
| Objeto cambiado de sitio | Al volver a una habitación | Silla girada · cuadro torcido · puerta abierta | 1-3 |

**Variedad:** cada evento tiene varios resultados y se elige uno según la tensión, así ninguna partida es idéntica.

---

## 3. Mecánicas que alimentan el ambiente (punto 2)

- **Tareas cotidianas** (lista en un cuaderno, no un HUD): dar de comer al perro, cerrar ventanas, revisar el granero,
  apagar las luces. Te obligan a recorrer el mapa y exponerte.
- **Repetición con cambios:** revisar la misma habitación varias veces y que algo haya cambiado.
- **Recursos limitados:** linterna con batería y celular con poca carga.
- **Decisiones de no-acción:** no abrir la puerta, no contestar. A veces lo correcto es **no hacer nada**.
- **Vistas limitadas:** mirilla de la puerta, ventana empañada (shader) y cámara de seguridad del corral.
- **Consecuencias dentro del nivel:** si al inicio dejaste una ventana abierta, más tarde algo entra por ahí.

**Controlador:** primera persona con `CharacterController` y gravedad manual (−20, como en S9), movimiento lento
(sin correr o con carrera muy corta), *head bob* sutil y pasos que cambian según la superficie.

---

## 4. Ambientación, luz y sonido (punto 3)

### Iluminación (rúbrica: "la luz guía al jugador")
| Capa | Luz | Uso |
|---|---|---|
| Key | Directional "LUNA" azul pálido, Soft Shadows | Exterior nocturno |
| Fill | Skybox nocturno, Intensity Multiplier ~0.4 | Que las sombras no sean negro puro |
| Accent | Spot cálida sobre el **objetivo de cada tarea** | Guía sin flechas: lo más brillante es a donde ir |
| Interior | Point cálidas (ámbar) **Baked** | Refugio. Cálido = seguro, frío = amenaza |

- **Weenie** (truco de Disney): el **granero** con un foco parpadeante, visible desde todo el mapa.
- Máximo 1 o 2 luces Realtime con sombras (la linterna y la luna). El resto, Baked.

### Post-procesamiento (un Volume Profile por nivel)
- `VP_Nivel1_NotMyDog`: Bloom suave, Vignette 0.3, Color Grading frío y algo desaturado, Film Grain ligero.
- `VP_Tension`: un Local Volume o un peso que sube con la tensión (más viñeta, aberración cromática, menos saturación).
- Sin flashes ni parpadeos rápidos (advertencia de fotosensibilidad de S10).

### Sonido
- **Silencio** como base: refrigerador, reloj, viento y grillos.
- **Audio 3D** (`Spatial Blend = 1`) para que el jugador ubique de dónde viene cada sonido.
- **La ausencia asusta:** los grillos se callan de golpe antes de un evento.

### Shaders propios (`Assets/_MyShaders/Graphs/`)
Hechos:
- **SG_Agua** (río): ondas con ruido animado y Fresnel profundo/superficie.
- **SG_Criatura** (piel de La Cosa): PBR, borde Fresnel y respiración por vértice.
- **SG_OjosCriatura**: emisión HDR que pulsa y brilla más cuando te mira de frente.

Ideas para más adelante:
- Ventana empañada o con escarcha donde aparece una silueta o una huella de mano.
- Distorsión o "presencia" en el aire cuando algo está cerca.
- Ojos que reflejan la linterna (el perro en la oscuridad).
- Disolución cuando la figura desaparece.
- Pulso de emisión en el foco del granero (weenie).

---

## 5. Estructura narrativa (punto 4)

Arco espacial de S12 aplicado al Nivel 1 (*Not My Dog*):

| Fase | Espacio | Qué pasa | Emoción |
|---|---|---|---|
| **Establecimiento** | Cocina iluminada y cálida | Llegas, lees la nota de los dueños y conoces al perro | Calma |
| **Tensión** | Pasillo estrecho, luz baja | Tareas. El perro ladra hacia el corral, el teléfono suena | Inquietud |
| **Clímax** | Ventana / patio | **Revelación con Cinemachine:** el perro está de pie en dos patas mirándote | Terror |
| **Resolución** | Cuarto, cama | El perro está dormido a tus pies… ¿qué viste afuera? | Duda |

- **Intro (Timeline):** texto con voz en off del tipo "Basado en el relato de R., Arequipa, 2016".
- **Epílogo real:** "R. vendió la casa meses después. El perro nunca fue encontrado."
- **Regla de las 3 pistas** (S12): para que se entienda que "el perro no es el perro" debe haber
  1. un **objeto**: el plato de comida intacto,
  2. una **marca en el entorno**: huellas en el barro que pasan de cuatro patas a dos,
  3. un **detalle de iluminación**: los ojos que reflejan la luz desde una altura humana.

### Storyboard de 3 paneles (para entregar / informe)
1. **Antes:** el jugador sale al patio con la linterna. Se ve el granero iluminado al fondo. *Inquietud.*
2. **El momento:** cruza el umbral del corral (trigger), la cámara cinemática encuadra la silueta de pie. *Terror.*
3. **Después:** vuelve a la casa y el perro está dentro, durmiendo. *Duda.*

---

## 6. Checklist contra la rúbrica

| Criterio | Cómo lo cubrimos |
|---|---|
| Mundo 3D y control | Casa + patio + granero a escala, colliders en todos los props, controlador FPS con gravedad |
| Iluminación y post | Key/Fill/Accent, luz guía en las tareas, Bloom + Color Grading + Vignette por nivel |
| Shaders y materiales | Al menos un SG propio con valor mecánico; materiales PBR (Base, Normal, Smoothness) con texturas de ambientCG |
| Narrativa | Environmental storytelling (3 capas), regla de 3 pistas, Timeline (intro) + Cinemachine (revelación) |
| Optimización | Luces Baked, Occlusion Culling (interiores), LODs en props, 60 FPS en WebGL |

## 7. Entregables
- [ ] Build WebGL en .zip (o en itch.io si pesa más de 10 MB), probada en el navegador
- [ ] Video de 1 a 2 minutos (OBS): recorrido, revelación y shader luciéndose
- [ ] Repositorio con el tag `U3-Hito3-Final`
- [ ] Informe PDF (1 o 2 páginas): historia implícita y qué objetos y luces la cuentan
