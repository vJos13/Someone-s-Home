# OPT-07 · Caching de GetComponent (no hay EnemyAI)

El proyecto no tiene una clase `EnemyAI` (ver informe v1: `git log --all -S EnemyAI` vacío) y, por indicación del equipo,
no se implementó la práctica del guardia con NavMesh. Se revisó todo el código de juego buscando `GetComponent`, `Find` y
`FindObjectOfType` dentro de `Update`:

- `Interaction/Interactor.cs`: llamaba a `GetComponentInParent<IInteractable>()` **cada frame** sobre el collider del raycast.
  **Corregido:** se guarda el último collider y su interactuable; la búsqueda solo se repite cuando el jugador mira otro objeto.
  Los interactuables solo se añaden en el Editor (Relato1Builder/Relato1ExtrasSetup), nunca en juego, así que la caché no se queda vieja.
- `Environment/Weather.cs`: usa `Camera.main` en `Update`. En Unity 6 `Camera.main` ya está cacheado por el motor; no se cambió.
- El resto de `GetComponent`/`Find*` están en `Awake`/`Start` o en código de editor.

## Medición (Editor  200 000 iteraciones  mismo collider apuntado)

| Indicador | ANTES (GetComponentInParent cada frame) | DESPUÉS (caché por collider) | Ganancia |
| --- | --- | --- | --- |
| Coste por frame | 218 ns | 18 ns | −200 ns (−91 8 %) |
| Objeto medido | `Panel -> PuertaTrasera (bisagra)` (profundidad 1 en la jerarquía) | | |

Es una ganancia pequeña en tiempo absoluto (una sola llamada por frame)  pero cumple la regla del checklist de la Semana 14:
nada de `GetComponent` ni `Find` en `Update`. Datos: `07_COMPARATIVAS/sprint_s14/datos_crudos/INTERACTOR.json`.
