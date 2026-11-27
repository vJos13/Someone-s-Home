# OPT-06 · Object Pooling de sonidos puntuales

El juego no tiene proyectiles (es terror narrativo en primera persona), pero sí creaba y destruía objetos en tiempo de juego:
`AudioSource.PlayClipAtPoint` crea un GameObject con un AudioSource por cada sonido y lo destruye al terminar.
Se usaba en `SimpleInteractable` (cada interacción con un objeto) y en dos momentos de `Level1Director` (clic de la luz y grito del hermano).

**Cambio:** `Assets/_Project/Scripts/Audio/OneShotAudio.cs` usa `UnityEngine.Pool.ObjectPool<AudioSource>` (4 precalentados, máximo 16).
Las fuentes que terminan de sonar vuelven al pool en `Update`, sin corrutinas ni `Destroy`.

## Medición (Development Build, Vulkan, 23 de noviembre de 2026)

Prueba: un clip de 0,1 s por frame durante 100 frames, después de 40 llamadas de calentamiento. Las asignaciones GC son
`GC Allocated In Frame` y `GC Allocation In Frame Count` sumados en los 100 frames, menos el control (los mismos frames sin sonido).

| Indicador | ANTES (PlayClipAtPoint) | DESPUÉS (pool) | Ganancia |
| --- | --- | --- | --- |
| GC Alloc / 100 sonidos | 7808 B | 2928 B | −4 880 B (−62,5 %) |
| Asignaciones GC / 100 sonidos | 300 | 84 | −216 (−72,0 %) |
| CPU por llamada (mediana) | 34,8 µs | 21,9 µs | −12,9 µs (−37,1 %) |
| CPU por llamada (p95) | 60,6 µs | 65,8 µs | +5,2 µs (+8,6 %) |
| AudioSources extra vivos a mitad de prueba | 38 | 25 (reutilizados) | — |

Control sin sonidos: 1464 B y 6 asignaciones en los mismos 100 frames.

Datos: `07_COMPARATIVAS/sprint_s14/datos_crudos/POOL.json`. Gráfica: `grafica_object_pooling.png`.
