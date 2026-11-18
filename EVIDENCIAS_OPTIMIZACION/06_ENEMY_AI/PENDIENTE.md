# Caching de GetComponent en EnemyAI: NO IMPLEMENTADO

Revisado el 2026-11-17 sobre `main` (822a74e), la rama `michael` y `origin/*`.

- No existe ninguna clase `EnemyAI` (ni en el código actual ni en ningún commit del historial: `git log --all -S EnemyAI` vacío).
- Los "enemigos" del Relato 1 (`Assets/_Project/Scripts/Events/Stalker.cs`, `RiverCreature.cs`, `HangedFigure.cs`)
  no llaman a `GetComponent` dentro de `Update`.
- Las llamadas a `GetComponent` del código de juego están en `Awake`/`Start`/`Reset` o en código de construcción de UI
  (p. ej. `FirstPersonController.cs:106`, `Interactor.cs:22`, `SoundEvent.cs:46`, `DogBarker.cs:17`), es decir,
  el patrón de caché ya se usa de forma general, pero no como una optimización con un ANTES medible.
- Única llamada por frame a destacar: `Interactor.cs:43` usa `GetComponentInParent<IInteractable>()` sobre el collider del raycast.

Por eso no hay métricas ANTES/DESPUÉS. Para documentarla: crear un caso ANTES (GetComponent en Update) y DESPUÉS (referencia
en Awake) en un commit separado y medir con el Profiler en Deep Profile el coste de `GetComponent` por frame.
