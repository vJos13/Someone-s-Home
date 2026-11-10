# Relato 1 · *Across the River* (Al otro lado del río)

## Origen
- Testimonio en primera persona de dos hermanos que aseguran haber visto algo al otro lado de un río.
- **Lugar:** Wenatchee, estado de Washington (EE. UU.)
- En el juego **no se nombra a la criatura**: solo se ve "algo parecido a un venado".
  El nombre aparece únicamente en el epílogo, citando lo que dijo el hermano.

## Personajes
| En el relato | En el juego | Rol |
|---|---|---|
| Narrador/a (sin nombre) | **Alex** (jugador) | Va a buscar a su hermano |
| Hermano | **Sam** | Está en el columpio junto al río |
| "Tammy" (seudónimo del autor) | Tammy | Madre de la amiga; voz en la cocina |
| "Emily" (seudónimo del autor) | Emily | Amiga; dice dónde está Sam |
| "Deer thing" | La cosa | Figura de venado de pie en dos patas, al otro lado del río |

## Fidelidad: qué viene del relato y qué agregamos
| Del relato (real) | Agregado para el juego |
|---|---|
| Se quedaban en casa de una amiga mientras sus padres estaban fuera | Intro con texto "Basado en hechos reales" |
| "La cena está lista, ¿puedes ir por tu hermano?" | Objetivo en pantalla |
| El patio trasero es un campo | Viento y grillos procedurales |
| La colina "estúpidamente empinada": hay que pisar las rocas o sales volando | **Mecánica:** la pendiente no se puede caminar; solo el sendero de rocas |
| El hermano está en un columpio de 2-3 asientos | Columpio que se mece solo |
| Oye su nombre a sus espaldas y no hay nadie en el campo | Voz 3D desde lo alto de la colina |
| El hermano se pone de pie mirando al otro lado del río | Revelación con **Cinemachine** |
| Algo "tipo venado" de pie en dos patas | Criatura greybox (la reemplaza el equipo) |
| Aparta la mirada unos segundos y al volver la boca se mueve diciendo su nombre | **Mecánica de mirada:** solo cambia cuando no lo miras |
| El hermano grita al verlo y tienen el diálogo | Diálogo con subtítulos, literal del post |
| Entran, cenan y no dicen nada | Fundido a negro + epílogo |
| Un año después: "investigué… era un skinwalker" · nunca volvieron | Epílogo y créditos con la fuente |
| — | Al volver, la voz imita a Emily llamándote desde el río (toque fantástico) |
| — | Los grillos se callan cuando aparece la cosa (la ausencia asusta) |

## Regla de las 3 pistas: "no es un venado"
1. **Objeto/forma:** está de pie en dos patas y su silueta queda recortada contra el sol poniente.
2. **Entorno:** los grillos se callan de golpe y el columpio deja de crujir.
3. **Personaje:** Sam tiembla y no aparta la vista; dice "No era un venado".

## Flujo del nivel (estados del `Level1Director`)
| # | Estado | Espacio | Disparador | Emoción |
|---|---|---|---|---|
| 0 | Intro | Negro → cocina | Inicio | Calma |
| 1 | Encargo | Cocina cálida | Diálogo Tammy/Emily | Calma |
| 2 | Campo | Campo dorado, atardecer | Puerta trasera | Paz |
| 3 | Bajada | Colina, sendero de rocas | Trigger en el borde | Cuidado |
| 4 | Columpio | Orilla del río | Llegar detrás de Sam | Inquietud |
| 5 | La voz | — | Esperar 2 s: "Alex…" desde arriba | Inquietud |
| 6 | Nadie | — | El jugador se da vuelta | Duda |
| 7 | **Revelación** | Al otro lado del río | El jugador vuelve a mirar al río → cámara Cinemachine | Terror |
| 8 | La boca | — | Mirar a otro lado ≥ 2.5 s y volver a mirar | Terror |
| 9 | El grito | — | Sam se da vuelta, grita, diálogo | Pánico |
| 10 | Regreso | Colina hacia arriba, sol casi oculto | Objetivo "Vuelve a la casa" + voz imitando a Emily | Huida |
| 11 | Cena y epílogo | Fundido | Entrar a la casa | Duda |

## Storyboard (3 paneles)
1. **Antes:** Alex baja por las rocas. Abajo se ve a Sam en el columpio y el río brilla con el sol. *Emoción: calma.*
2. **El momento:** Sam está de pie y la cámara cruza el río: una figura de venado erguida, a contraluz. *Emoción: terror.*
3. **Después:** Alex sube corriendo por las rocas mientras una voz igual a la de Emily lo llama desde el río. *Emoción: pánico.*


## Escenas agregadas (tensión de a poco, estilo "basado en hechos reales")
| Momento | Qué pasa | Para qué |
|---|---|---|
| Cocina | Mensajes de mamá al celular; Tammy pide poner la mesa y sacar la basura | Rutina normal antes del miedo |
| El tacho de afuera | El perro de los vecinos ladra, se pone loco y se calla de golpe. En el borde de la colina hay algo parado muy derecho; cuando vuelves a mirar ya no está | Primer aviso, ambiguo |
| Llamar a Sam | Suena y suena: buzón de voz | Inquietud antes de salir |
| El regreso | La cosa nos sigue: cada vez que miras atrás está más cerca. Pasos detrás de ti en el pasto | Persecución sin persecución |
| La noche | Sam te escribe desde su saco ("¿oyes eso?"). Golpes en la ventana, algo pegado al vidrio. Una voz con la voz de Tammy (y después la de Sam) pide que abras | Clímax dentro de la casa |
| Asegurar la casa | Cerrar la cortina, poner el cerrojo, apagar la luz, acostarse | Tareas bajo presión |
| Final malo | Si abres la puerta de noche | Consecuencia de la decisión |
