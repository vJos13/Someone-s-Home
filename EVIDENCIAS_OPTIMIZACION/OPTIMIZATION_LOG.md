# Optimization Log Sheet · Someone's Home

Sprint de optimización de la Semana 14 (23 de noviembre de 2026). Medido en una **Development Build** de Linux (Vulkan, 1600x900), no en el Editor. Equipo: Intel(R) Core(TM) 7 240H + NVIDIA GeForce RTX 5070 Laptop GPU, con el cargador conectado.

Cada fila se midió por separado, aplicando una optimización a la vez sobre la anterior. Las cifras son la media de 6 vistas fijas del juego, y cada vista es la mediana de 3 rondas intercaladas (ANTES y DESPUÉS se alternan para repartir el ruido).

| # | Optimización | Antes | Después | Ganancia | Quién | Evidencia |
| --- | --- | --- | --- | --- | --- | --- |
| OPT-00 | GPU Instancing en vegetación (sprint anterior) | 1,41 M tri visibles | 7,03 M tri con +0,60 ms CPU | ×5 vegetación por el mismo presupuesto | vJos13 | 04_GPU_INSTANCING/, informe v1 |
| OPT-01 | SSAO: de DepthNormals a resolución completa a Depth a media resolución | GPU 3,35 ms · 1 543 DC · 10,98 M tri · 657 sombras | GPU 2,55 ms · 1 097 DC · 8,74 M tri · 657 sombras | GPU −0,80 ms (−23,8 %); DC −446 (−28,9 %) | vJos13 | 07_COMPARATIVAS/sprint_s14/datos_crudos/r*_C1_SSAO.json |
| OPT-02 | Sombras: 229 objetos pequeños o lejanos sin sombra y 2 cascadas en vez de 4 | GPU 2,55 ms · 1 098 DC · 8,74 M tri · 656 sombras | GPU 2,03 ms · 769 DC · 5,89 M tri · 323 sombras | GPU −0,52 ms (−20,4 %); DC −329 (−30,0 %) | vJos13 | 07_COMPARATIVAS/sprint_s14/datos_crudos/r*_CFG2.json |
| OPT-03 | Occlusion Culling horneado (antes activado pero sin datos) | GPU 2,03 ms · 769 DC · 5,89 M tri · 323 sombras | GPU 1,97 ms · 613 DC · 5,77 M tri · 323 sombras | GPU −0,06 ms (−3,1 %); DC −156 (−20,3 %) | vJos13 | 07_COMPARATIVAS/sprint_s14/datos_crudos/r*_CFG3.json |
| OPT-04 | Distancias: LOD Bias 2 → 1,25, far plane 400 → 250 m, pasto 55 → 45 m | GPU 1,97 ms · 613 DC · 5,77 M tri · 323 sombras | GPU 1,67 ms · 567 DC · 4,30 M tri · 312 sombras | GPU −0,29 ms (−14,9 %); DC −46 (−7,6 %) | vJos13 | 07_COMPARATIVAS/sprint_s14/datos_crudos/r*_CFG4.json |
| OPT-05 | Terreno con Draw Instanced y sin Opaque Texture (no la usaba ningún shader) | GPU 1,67 ms · 567 DC · 4,30 M tri · 312 sombras | GPU 1,68 ms · 523 DC · 4,30 M tri · 272 sombras | GPU +0,01 ms (+0,4 %); DC −43 (−7,6 %) | vJos13 | 07_COMPARATIVAS/sprint_s14/datos_crudos/r*_CFG5.json |
| OPT-06 | Object Pooling de sonidos puntuales (OneShotAudio) en vez de AudioSource.PlayClipAtPoint | 7808 B y 300 asignaciones GC / 100 sonidos; 34,8 µs por llamada | 2928 B y 84 asignaciones GC / 100 sonidos; 21,9 µs por llamada | GC −4 880 B (−62,5 %); CPU −12,9 µs (−37,1 %) | vJos13 | 05_OBJECT_POOLING/RESULTADOS.md |
| OPT-07 | Interactor: caché del IInteractable apuntado (sin GetComponentInParent cada frame) | 218 ns por frame | 18 ns por frame | −200 ns (−91,8 %) | vJos13 | 06_ENEMY_AI/RESULTADOS.md |
| OPT-08 | Menú de gráficos (Baja/Media/Alta/Ultra) para equipos modestos | Alta: GPU 1,69 ms · 342 MB | Baja: GPU 0,79 ms · 231 MB | GPU −0,90 ms (−53,4 %) | vJos13 | 08_AJUSTES_GRAFICOS/RESULTADOS.md |

## Resultado total (ANTES → DESPUÉS)

| Métrica | ANTES | DESPUÉS | Ganancia |
| --- | --- | --- | --- |
| Frame time medio | 3,45 ms | 2,37 ms | −1,08 ms (−31,4 %) |
| Frame time p95 | 4,88 ms | 4,07 ms | −0,80 ms (−16,5 %) |
| GPU frame time | 3,35 ms | 1,68 ms | −1,66 ms (−49,7 %) |
| CPU hilo principal | 1,50 ms | 1,46 ms | −0,04 ms (−3,0 %) |
| CPU hilo de render | 0,65 ms | 0,41 ms | −0,24 ms (−36,9 %) |
| Draw calls | 1 543 | 524 | −1 020 (−66,1 %) |
| SetPass calls | 86 | 51 | −34 (−40,1 %) |
| Triángulos (millones) | 10,98 | 4,30 | −6,68 (−60,9 %) |
| Shadow casters | 657 | 272 | −384 (−58,5 %) |
| GC Alloc por frame (KB) | 0,01 | 0,01 | −0,00 (−31,0 %) |
| Memoria gráfica (MB) | 375 | 342 | −33 (−8,9 %) |
| FPS (desde el frame time medio) | 290 | 422 | +132 (+45,7 %) |

## Cómo repetir la medición

1. `File > Build Profiles`, Linux, **Development Build** (o `Builds_Perf/tools/build.cs` desde la CLI de Unity).
2. `./SomeonesHome.x86_64 -force-vulkan -screen-width 1600 -screen-height 900 -benchmark -benchmarkReps 1 -benchmarkLabel X -benchmarkOut /ruta/ABSOLUTA/X.json` (la ruta debe ser absoluta).
3. `-benchmarkConfig N` deshace en memoria las optimizaciones posteriores a la N (0 a 6) para medir cada paso con la misma build. El SSAO (OPT-01) no se puede deshacer en tiempo de ejecución: por eso se mide con dos builds (ANTES y OPT1_ssao).
4. `-benchmarkPool -benchmarkNoViews` corre solo la prueba de Object Pooling.
5. El código está en `Assets/_Project/Scripts/Dev/PerfBenchmark.cs`; las optimizaciones se aplican con `Assets/_Project/Scripts/Editor/PerfSettings.cs`.

Notas: medir siempre **con el cargador conectado**. Con batería la laptop limita la GPU y el frame time queda fijo en ~6,5 ms sin importar la carga, así que no sirve para comparar.
