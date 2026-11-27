# OPT-08 · Ajustes de gráficos en el menú de pausa

Menú de pausa → **Configuración → GRÁFICOS** y **PANTALLA**, para que el juego corra en laptops y PCs con gráfica integrada.
Código: `Assets/_Project/Scripts/Core/GraphicsQuality.cs` (aplica las opciones), `Core/GameSettings.cs` (las guarda en PlayerPrefs)
y `UI/GameMenu.cs` (las pestañas).

| Opción | Qué cambia |
| --- | --- |
| Calidad | Preset Baja / Media / Alta / Ultra. Tocar una opción suelta lo pasa a «Personalizada». **Alta** = la configuración medida en el sprint (por defecto). |
| Escala de resolución | 50–100 %: el 3D se dibuja a menos resolución y se reescala con AMD FSR 1. La interfaz sigue nítida. |
| Sombras | Desactivadas / Bajas / Medias / Altas / Muy altas (distancia, cascadas y resolución del mapa de sombras). |
| Oclusión ambiental (SSAO) | Activa o desactiva la Renderer Feature de SSAO. |
| Antialiasing | Desactivado / FXAA / SMAA. |
| Densidad del pasto | 20–100 % de las instancias de pasto del terreno. |
| Distancia de visión | Corta / Normal / Lejana: LOD bias, detalle del terreno y distancia del pasto. |
| Texturas | Media (mitad de resolución, menos VRAM) / Alta. |
| Pantalla | Pantalla completa, VSync, límite de FPS (30/60/120/sin límite) y contador de FPS en pantalla. |

## Medición (Development Build, Vulkan, 1600x900, media de 6 vistas, 2 rondas)

| Preset | GPU (frente a Alta) | Frame time | Draw calls | Triángulos | Shadow casters | Memoria gráfica |
| --- | --- | --- | --- | --- | --- | --- |
| Baja | 0,79 ms (−0,90 ms (−53,4 %)) | 2,31 ms | 331 | 2,11 M | 106 | 231 MB |
| Media | 1,46 ms (−0,23 ms (−13,5 %)) | 2,40 ms | 479 | 4,08 M | 224 | 335 MB |
| Alta | 1,69 ms (+0,00 ms (+0,0 %)) | 2,38 ms | 523 | 4,30 M | 272 | 342 MB |
| Ultra | 3,03 ms (+1,34 ms (+79,5 %)) | 3,16 ms | 846 | 9,34 M | 570 | 393 MB |

En este equipo (RTX 5070) el frame ya está limitado por la CPU a ~2,3 ms, así que bajar la calidad apenas cambia el frame time.
En una gráfica integrada, donde manda la GPU, la columna de GPU es la que se traslada a FPS.
