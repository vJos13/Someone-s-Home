# Object Pooling de proyectiles: NO IMPLEMENTADO

Revisado el 2026-11-17 sobre `main` (822a74e), la rama `michael` y `origin/*` (16 commits en total).

- No existe ningún script de proyectiles, balas ni pool (`ObjectPool`, `Projectile`, `Proyectil`, `Bullet`, `IObjectPool`).
  Se buscó con `git log --all -S` y `grep` en todo `Assets/`.
- En el código de juego (`Assets/_Project/Scripts`, sin `Editor/`) no hay ninguna llamada a `Instantiate(` ni `Destroy(`
  en tiempo de ejecución.
- El juego (terror narrativo en primera persona) no tiene mecánica de disparo.

Por eso no hay capturas ni métricas ANTES/DESPUÉS. Para documentarla en el futuro:
1. Implementar el sistema de proyectiles sin pool (estado ANTES) y hacer commit.
2. Grabar con el Profiler (CPU Usage + Memory, GC Alloc) N disparos/s durante 30 s con la misma cámara.
3. Implementar `UnityEngine.Pool.ObjectPool<T>` (estado DESPUÉS) y repetir exactamente la misma prueba.
4. Guardar aquí las capturas del Profiler (GC Alloc, `Instantiate`/`Destroy` en la jerarquía de CPU) y los .data.
