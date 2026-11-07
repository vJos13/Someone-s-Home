"""
Someone's Home · Generador de texturas PBR procedurales (Blender 5.x, Cycles en GPU).

Uso (desde la raíz del proyecto):
    blender -b --factory-startup --python Tools/Blender/generate_pbr_textures.py -- [--size 1024] [--only Madera,Roca]

Para cada material:
  1. Construye un shader procedural en Blender (ruido, voronoi, vetas…).
     Los patrones son PERFECTAMENTE REPETIBLES (tileables): las coordenadas UV se proyectan sobre un
     toro en 4D (cos/sin de u y de v) y se alimentan a Noise/Voronoi 4D.
  2. Hace bake (EMIT) de Base Color, Roughness y Height.
  3. Con numpy calcula Normal (OpenGL, +Y, como espera Unity) y Ambient Occlusion a partir del Height.
  4. Guarda en Assets/_Project/Art/Textures/PBR/<Nombre>/:
       T_<Nombre>_BaseColor.png   (sRGB)
       T_<Nombre>_Normal.png      (normal map)
       T_<Nombre>_MetallicSmoothness.png  (R = metallic, A = smoothness)  → URP Lit
       T_<Nombre>_AO.png
       T_<Nombre>_Height.png
       T_<Nombre>_TerrainMask.png (R metal, G AO, B height, A smoothness) → Terrain Layers de URP
"""
import math
import os
import sys

import bpy
import numpy as np

TAU = math.tau
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "Textures", "PBR")

# ──────────────────────────────────────────────────────────────────────────────
# Argumentos
# ──────────────────────────────────────────────────────────────────────────────
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SIZE = int(argv[argv.index("--size") + 1]) if "--size" in argv else 1024
ONLY = set(argv[argv.index("--only") + 1].split(",")) if "--only" in argv else None


# ──────────────────────────────────────────────────────────────────────────────
# Escena de bake
# ──────────────────────────────────────────────────────────────────────────────
def setup_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    device = "CPU"
    for kind in ("OPTIX", "CUDA"):
        try:
            prefs.compute_device_type = kind
            prefs.get_devices()
            gpus = [d for d in prefs.devices if d.type == kind]
            if gpus:
                for d in prefs.devices:
                    d.use = d.type == kind
                device = "GPU"
                print(f"[PBR] Usando GPU ({kind}): {gpus[0].name}")
                break
        except Exception:
            pass
    scene.cycles.device = device
    scene.cycles.samples = 4
    scene.cycles.bake_type = "EMIT"
    scene.render.bake.margin = 0

    bpy.ops.mesh.primitive_plane_add(size=2)
    return bpy.context.active_object


def srgb_to_linear(col):
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in col)


# ──────────────────────────────────────────────────────────────────────────────
# Ayudantes de nodos
# ──────────────────────────────────────────────────────────────────────────────
class G:
    """Pequeño DSL para armar árboles de nodos sin perderse."""

    def __init__(self, mat):
        self.nt = mat.node_tree
        self.nt.nodes.clear()
        self.out = self.nt.nodes.new("ShaderNodeOutputMaterial")
        self.emit = self.nt.nodes.new("ShaderNodeEmission")
        self.link(self.emit.outputs["Emission"], self.out.inputs["Surface"])
        uv = self.nt.nodes.new("ShaderNodeTexCoord")
        sep = self.nt.nodes.new("ShaderNodeSeparateXYZ")
        self.link(uv.outputs["UV"], sep.inputs["Vector"])
        self.u, self.v = sep.outputs["X"], sep.outputs["Y"]
        self._torus_cache = {}

    def link(self, a, b):
        self.nt.links.new(a, b)

    def math(self, op, a, b=None, c=None):
        n = self.nt.nodes.new("ShaderNodeMath")
        n.operation = op
        for i, val in enumerate((a, b, c)):
            if val is None:
                continue
            if isinstance(val, (int, float)):
                n.inputs[i].default_value = val
            else:
                self.link(val, n.inputs[i])
        return n.outputs[0]

    def torus(self, su, sv, offset=0.0):
        """Coordenadas 4D tileables: su/sv = repeticiones del patrón por tile en u/v."""
        key = (su, sv, offset)
        if key in self._torus_cache:
            return self._torus_cache[key]
        au = self.math("MULTIPLY", self.u, TAU)
        av = self.math("MULTIPLY", self.v, TAU)
        ru, rv = su / TAU, sv / TAU
        x = self.math("MULTIPLY", self.math("COSINE", au), ru)
        y = self.math("MULTIPLY", self.math("SINE", au), ru)
        z = self.math("MULTIPLY", self.math("COSINE", av), rv)
        w = self.math("MULTIPLY", self.math("SINE", av), rv)
        comb = self.nt.nodes.new("ShaderNodeCombineXYZ")
        self.link(x, comb.inputs["X"])
        self.link(y, comb.inputs["Y"])
        self.link(z, comb.inputs["Z"])
        vec = comb.outputs["Vector"]
        if offset:
            add = self.nt.nodes.new("ShaderNodeVectorMath")
            add.operation = "ADD"
            self.link(vec, add.inputs[0])
            add.inputs[1].default_value = (offset * 7.13, offset * 3.71, offset * 5.29)
            vec = add.outputs[0]
            w = self.math("ADD", w, offset * 9.17)
        self._torus_cache[key] = (vec, w)
        return vec, w

    def noise(self, su, sv, detail=6.0, rough=0.55, distortion=0.0, seed=0.0, scale=1.0):
        vec, w = self.torus(su, sv, seed)
        n = self.nt.nodes.new("ShaderNodeTexNoise")
        n.noise_dimensions = "4D"
        self.link(vec, n.inputs["Vector"])
        self.link(w, n.inputs["W"])
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = rough
        n.inputs["Distortion"].default_value = distortion
        return n.outputs["Fac"]

    def voronoi(self, su, sv, feature="F1", seed=0.0, randomness=1.0, scale=1.0, out="Distance"):
        vec, w = self.torus(su, sv, seed)
        n = self.nt.nodes.new("ShaderNodeTexVoronoi")
        n.voronoi_dimensions = "4D"
        n.feature = feature
        self.link(vec, n.inputs["Vector"])
        self.link(w, n.inputs["W"])
        n.inputs["Scale"].default_value = scale
        n.inputs["Randomness"].default_value = randomness
        return n.outputs[out]

    def ramp(self, fac, stops):
        """stops: [(pos, (r,g,b)), ...] → color. Los colores se escriben en sRGB (como en un selector de color)."""
        stops = [(pos, srgb_to_linear(col)) for pos, col in stops]
        n = self.nt.nodes.new("ShaderNodeValToRGB")
        els = n.color_ramp.elements
        while len(els) > 1:
            els.remove(els[-1])
        els[0].position, els[0].color = stops[0][0], (*stops[0][1], 1)
        for pos, col in stops[1:]:
            e = els.new(pos)
            e.color = (*col, 1)
        self.link(fac, n.inputs["Fac"])
        return n.outputs["Color"]

    def remap(self, fac, a, b, lo=0.0, hi=1.0):
        n = self.nt.nodes.new("ShaderNodeMapRange")
        n.clamp = True
        self.link(fac, n.inputs["Value"])
        n.inputs["From Min"].default_value = a
        n.inputs["From Max"].default_value = b
        n.inputs["To Min"].default_value = lo
        n.inputs["To Max"].default_value = hi
        return n.outputs["Result"]

    def lerp_color(self, a, b, f):
        sub = self.nt.nodes.new("ShaderNodeVectorMath"); sub.operation = "SUBTRACT"
        self.link(b, sub.inputs[0]); self.link(a, sub.inputs[1])
        sc = self.nt.nodes.new("ShaderNodeVectorMath"); sc.operation = "SCALE"
        self.link(sub.outputs[0], sc.inputs[0]); self.link(f, sc.inputs["Scale"])
        add = self.nt.nodes.new("ShaderNodeVectorMath"); add.operation = "ADD"
        self.link(a, add.inputs[0]); self.link(sc.outputs[0], add.inputs[1])
        return add.outputs[0]

    def mul_color(self, c, f):
        sc = self.nt.nodes.new("ShaderNodeVectorMath"); sc.operation = "SCALE"
        self.link(c, sc.inputs[0]); self.link(f, sc.inputs["Scale"])
        return sc.outputs[0]

    def fract(self, x):
        return self.math("FRACT", x)

    def set_output(self, socket):
        self.link(socket, self.emit.inputs["Color"])


# ──────────────────────────────────────────────────────────────────────────────
# Recetas de materiales. Cada una devuelve (base_color, roughness, height) como sockets.
# Height en 0..1 (0.5 = plano).
# ──────────────────────────────────────────────────────────────────────────────
def madera_vieja(g):
    """Tablas de madera gastada y reseca (casa, columpio, puerta). Tablas a lo largo de u."""
    planks = 5
    row = g.math("FLOOR", g.math("MULTIPLY", g.v, planks))
    row_rand = g.fract(g.math("MULTIPLY", g.math("SINE", g.math("MULTIPLY", row, 12.9898)), 43758.5453))
    # Uniones de testa desplazadas por fila (tileable: 2 tablas por tile en u).
    end = g.fract(g.math("ADD", g.math("MULTIPLY", g.u, 2.0), row_rand))
    seam_v = g.fract(g.math("MULTIPLY", g.v, planks))
    seam_line = g.math("MINIMUM", g.math("MULTIPLY", g.math("MINIMUM", seam_v, g.math("SUBTRACT", 1.0, seam_v)), 60.0),
                       g.math("MULTIPLY", g.math("MINIMUM", end, g.math("SUBTRACT", 1.0, end)), 120.0))
    seam = g.math("MINIMUM", seam_line, 1.0)                 # 0 en la junta, 1 en la tabla
    grain_n = g.noise(1.0, 3.0, detail=4, distortion=1.5, seed=1)
    grain = g.math("SINE", g.math("ADD", g.math("MULTIPLY", g.v, TAU * 40), g.math("MULTIPLY", grain_n, 9.0)))
    grain = g.math("MULTIPLY", g.math("ADD", grain, 1.0), 0.5)
    fibers = g.noise(2.0, 60.0, detail=8, rough=0.7, seed=2)
    cracks = g.voronoi(3.0, 40.0, feature="DISTANCE_TO_EDGE", seed=3)
    crack = g.remap(cracks, 0.0, 0.035, 0.0, 1.0)            # 0 dentro de la grieta
    weather = g.noise(3.0, 3.0, detail=5, seed=4)

    tone = g.math("ADD", g.math("MULTIPLY", grain, 0.35), g.math("MULTIPLY", fibers, 0.45))
    tone = g.math("ADD", tone, g.math("MULTIPLY", row_rand, 0.25))
    wood = g.ramp(tone, [(0.25, (0.20, 0.13, 0.08)), (0.55, (0.36, 0.25, 0.16)), (0.85, (0.50, 0.40, 0.30))])
    grey = g.ramp(fibers, [(0.3, (0.33, 0.31, 0.28)), (0.8, (0.52, 0.50, 0.46))])
    base = g.lerp_color(wood, grey, g.remap(weather, 0.45, 0.7, 0.0, 0.75))
    base = g.mul_color(base, g.math("MULTIPLY", seam, g.math("ADD", g.math("MULTIPLY", crack, 0.6), 0.4)))

    rough = g.math("ADD", 0.72, g.math("MULTIPLY", fibers, 0.2))
    height = g.math("ADD", g.math("MULTIPLY", grain, 0.15), g.math("MULTIPLY", fibers, 0.25))
    height = g.math("ADD", height, 0.35)
    height = g.math("MULTIPLY", height, g.math("ADD", g.math("MULTIPLY", seam, 0.7), 0.3))
    height = g.math("MULTIPLY", height, g.math("ADD", g.math("MULTIPLY", crack, 0.4), 0.6))
    return base, rough, height


def roca(g):
    """Roca de río/colina con líquenes (sendero)."""
    cells = g.voronoi(4.0, 4.0, feature="F1", seed=1)
    edges = g.voronoi(4.0, 4.0, feature="DISTANCE_TO_EDGE", seed=1)
    big = g.noise(3.0, 3.0, detail=10, rough=0.6, distortion=0.4, seed=2)
    fine = g.noise(24.0, 24.0, detail=6, rough=0.7, seed=3)
    lichen = g.remap(g.noise(6.0, 6.0, detail=5, seed=4), 0.55, 0.62, 0.0, 1.0)

    tone = g.math("ADD", g.math("MULTIPLY", big, 0.6), g.math("MULTIPLY", fine, 0.4))
    stone = g.ramp(tone, [(0.3, (0.22, 0.21, 0.2)), (0.55, (0.38, 0.37, 0.35)), (0.75, (0.52, 0.5, 0.47))])
    lich = g.ramp(fine, [(0.3, (0.35, 0.36, 0.2)), (0.7, (0.55, 0.52, 0.32))])
    base = g.lerp_color(stone, lich, g.math("MULTIPLY", lichen, 0.85))
    gaps = g.remap(edges, 0.0, 0.06, 0.45, 1.0)
    base = g.mul_color(base, gaps)

    rough = g.math("SUBTRACT", 0.9, g.math("MULTIPLY", fine, 0.15))
    height = g.math("ADD", g.math("MULTIPLY", g.math("SUBTRACT", 1.0, cells), 0.35), g.math("MULTIPLY", big, 0.4))
    height = g.math("ADD", height, g.math("MULTIPLY", fine, 0.15))
    height = g.math("MULTIPLY", height, g.remap(edges, 0.0, 0.08, 0.5, 1.0))
    return base, rough, height


def corteza(g):
    """Corteza de árbol con surcos verticales (troncos). Surcos a lo largo de v."""
    ridges = g.voronoi(10.0, 2.0, feature="DISTANCE_TO_EDGE", seed=1)
    plates = g.voronoi(10.0, 2.0, feature="F1", seed=1)
    fibers = g.noise(30.0, 4.0, detail=8, rough=0.65, distortion=0.8, seed=2)
    moss = g.remap(g.noise(2.0, 4.0, detail=4, seed=3), 0.58, 0.66, 0.0, 1.0)

    groove = g.remap(ridges, 0.0, 0.12, 0.0, 1.0)
    tone = g.math("ADD", g.math("MULTIPLY", fibers, 0.5), g.math("MULTIPLY", groove, 0.5))
    bark = g.ramp(tone, [(0.15, (0.07, 0.05, 0.04)), (0.5, (0.2, 0.15, 0.11)), (0.85, (0.33, 0.28, 0.23))])
    mossc = g.ramp(fibers, [(0.3, (0.12, 0.16, 0.07)), (0.7, (0.22, 0.27, 0.12))])
    base = g.lerp_color(bark, mossc, g.math("MULTIPLY", moss, 0.7))

    rough = g.math("ADD", 0.82, g.math("MULTIPLY", fibers, 0.15))
    height = g.math("ADD", g.math("MULTIPLY", groove, 0.6), g.math("MULTIPLY", fibers, 0.25))
    height = g.math("ADD", height, g.math("MULTIPLY", g.math("SUBTRACT", 1.0, plates), 0.15))
    return base, rough, height


def pasto_seco(g):
    """Pasto seco del campo al atardecer (terreno)."""
    # Trazos orgánicos (sin dirección fija): ruido con mucha distorsión + celdas finas.
    blades = g.noise(22.0, 22.0, detail=8, rough=0.75, distortion=3.0, seed=1)
    blades2 = g.noise(48.0, 48.0, detail=4, rough=0.6, distortion=8.0, seed=5)
    clumps = g.noise(5.0, 5.0, detail=6, seed=2)
    patches = g.noise(1.5, 1.5, detail=3, seed=3)
    soil = g.remap(g.noise(3.0, 3.0, detail=5, seed=4), 0.6, 0.7, 0.0, 1.0)

    straw = g.math("MAXIMUM", blades, blades2)
    tone = g.math("ADD", g.math("MULTIPLY", straw, 0.6), g.math("MULTIPLY", clumps, 0.4))
    dry = g.ramp(tone, [(0.3, (0.3, 0.25, 0.12)), (0.55, (0.52, 0.45, 0.22)), (0.8, (0.7, 0.62, 0.36))])
    olive = g.ramp(tone, [(0.3, (0.2, 0.22, 0.1)), (0.75, (0.38, 0.4, 0.2))])
    base = g.lerp_color(dry, olive, g.remap(patches, 0.4, 0.65, 0.0, 0.8))
    dirt = g.ramp(clumps, [(0.3, (0.22, 0.17, 0.12)), (0.7, (0.32, 0.26, 0.19))])
    base = g.lerp_color(base, dirt, g.math("MULTIPLY", soil, 0.8))

    rough = g.math("ADD", 0.8, g.math("MULTIPLY", straw, 0.15))
    height = g.math("ADD", g.math("MULTIPLY", straw, 0.6), g.math("MULTIPLY", clumps, 0.4))
    height = g.math("MULTIPLY", height, g.math("SUBTRACT", 1.0, g.math("MULTIPLY", soil, 0.5)))
    return base, rough, height


def tierra(g):
    """Tierra con piedritas (pendiente de la colina)."""
    ground = g.noise(4.0, 4.0, detail=8, rough=0.6, distortion=0.3, seed=1)
    fine = g.noise(30.0, 30.0, detail=4, seed=2)
    pebbles = g.voronoi(14.0, 14.0, feature="F1", seed=3, randomness=0.9)
    peb = g.remap(pebbles, 0.0, 0.3, 1.0, 0.0)
    peb = g.math("MULTIPLY", peb, g.remap(g.noise(5.0, 5.0, seed=4), 0.45, 0.6, 0.0, 1.0))

    tone = g.math("ADD", g.math("MULTIPLY", ground, 0.7), g.math("MULTIPLY", fine, 0.3))
    dirt = g.ramp(tone, [(0.3, (0.2, 0.15, 0.11)), (0.6, (0.33, 0.26, 0.19)), (0.85, (0.44, 0.36, 0.27))])
    stone = g.ramp(fine, [(0.3, (0.34, 0.33, 0.3)), (0.7, (0.5, 0.48, 0.44))])
    base = g.lerp_color(dirt, stone, peb)

    rough = g.math("SUBTRACT", 0.92, g.math("MULTIPLY", peb, 0.2))
    height = g.math("ADD", g.math("MULTIPLY", tone, 0.5), g.math("MULTIPLY", peb, 0.5))
    return base, rough, height


def barro(g):
    """Barro húmedo de la orilla: zonas mojadas con menos rugosidad."""
    ground = g.noise(3.0, 3.0, detail=7, rough=0.55, seed=1)
    fine = g.noise(20.0, 20.0, detail=5, seed=2)
    wet = g.remap(g.noise(2.0, 2.0, detail=4, seed=3), 0.45, 0.6, 0.0, 1.0)
    prints = g.voronoi(8.0, 8.0, feature="F1", seed=4)

    tone = g.math("ADD", g.math("MULTIPLY", ground, 0.7), g.math("MULTIPLY", fine, 0.3))
    mud = g.ramp(tone, [(0.3, (0.15, 0.12, 0.09)), (0.7, (0.3, 0.25, 0.18))])
    base = g.mul_color(mud, g.math("SUBTRACT", 1.0, g.math("MULTIPLY", wet, 0.35)))

    rough = g.math("SUBTRACT", 0.9, g.math("MULTIPLY", wet, 0.6))
    height = g.math("ADD", g.math("MULTIPLY", tone, 0.6), g.math("MULTIPLY", g.remap(prints, 0.0, 0.25, 0.0, 0.3), 1.0))
    height = g.math("SUBTRACT", height, g.math("MULTIPLY", wet, 0.15))
    return base, rough, height


def yeso(g):
    """Pared de yeso/estuco pintado, con manchas de humedad y grietas finas (casa)."""
    stain = g.noise(2.0, 2.0, detail=5, seed=1)
    drip = g.noise(8.0, 1.0, detail=6, distortion=0.5, seed=2)
    fine = g.noise(40.0, 40.0, detail=4, seed=3)
    cracks = g.voronoi(5.0, 5.0, feature="DISTANCE_TO_EDGE", seed=4)
    crack_mask = g.remap(g.noise(3.0, 3.0, seed=5), 0.55, 0.65, 0.0, 1.0)
    crack = g.math("MULTIPLY", g.remap(cracks, 0.0, 0.012, 1.0, 0.0), crack_mask)

    paint = g.ramp(g.math("ADD", g.math("MULTIPLY", fine, 0.3), 0.5),
                   [(0.4, (0.66, 0.6, 0.5)), (0.8, (0.78, 0.73, 0.62))])
    dirty = g.ramp(drip, [(0.3, (0.5, 0.44, 0.35)), (0.8, (0.6, 0.55, 0.45))])
    base = g.lerp_color(paint, dirty, g.remap(g.math("MULTIPLY", stain, g.math("ADD", g.math("MULTIPLY", drip, 0.6), 0.6)), 0.35, 0.7, 0.0, 0.7))
    base = g.mul_color(base, g.math("SUBTRACT", 1.0, g.math("MULTIPLY", crack, 0.6)))

    rough = g.math("ADD", 0.82, g.math("MULTIPLY", fine, 0.12))
    height = g.math("ADD", 0.5, g.math("MULTIPLY", fine, 0.15))
    height = g.math("SUBTRACT", height, g.math("MULTIPLY", crack, 0.4))
    return base, rough, height


def _periodic(g, coord, n):
    """sin(2π·n·coord) → 0..1, tileable si n es entero."""
    return g.math("MULTIPLY", g.math("ADD", g.math("SINE", g.math("MULTIPLY", coord, TAU * n)), 1.0), 0.5)


def tela(g):
    """Tejido de punto neutro (sudadera, suéter, camisa). Gris claro: el color lo pone _BaseColor en Unity."""
    rows, cols = 90, 70
    # Punto jersey: columnas de "V" (chevrons) apiladas en v.
    x = g.fract(g.math("MULTIPLY", g.u, cols))
    vee = g.math("MULTIPLY", g.math("ABSOLUTE", g.math("SUBTRACT", x, 0.5)), 1.2)
    loop = g.fract(g.math("ADD", g.math("MULTIPLY", g.v, rows), vee))
    stitch = g.math("SINE", g.math("MULTIPLY", loop, math.pi))           # 0 en bordes, 1 al centro
    col_gap = g.math("SINE", g.math("MULTIPLY", x, math.pi))
    fiber = g.noise(40.0, 40.0, detail=8, rough=0.75, seed=1)
    pill = g.noise(6.0, 6.0, detail=4, seed=2)
    knit = g.math("MULTIPLY", stitch, g.math("POWER", col_gap, 0.5))
    tone = g.math("ADD", g.math("MULTIPLY", knit, 0.4), g.math("MULTIPLY", fiber, 0.35))
    tone = g.math("ADD", tone, g.math("MULTIPLY", pill, 0.25))
    base = g.ramp(tone, [(0.2, (0.62, 0.62, 0.62)), (0.7, (0.86, 0.86, 0.86)), (1.0, (0.95, 0.95, 0.95))])
    rough = g.math("ADD", 0.9, g.math("MULTIPLY", fiber, 0.08))
    height = g.math("ADD", g.math("MULTIPLY", knit, 0.7), g.math("MULTIPLY", fiber, 0.3))
    return base, rough, height


def mezclilla(g):
    """Mezclilla (jeans): sarga diagonal índigo con desgaste."""
    twill = _periodic(g, g.math("ADD", g.u, g.v), 110)
    weft = _periodic(g, g.u, 220)
    fiber = g.noise(30.0, 30.0, detail=8, rough=0.7, seed=1)
    wear = g.remap(g.noise(3.0, 3.0, detail=5, seed=2), 0.45, 0.75, 0.0, 1.0)
    tone = g.math("ADD", g.math("MULTIPLY", twill, 0.55), g.math("MULTIPLY", fiber, 0.3))
    tone = g.math("ADD", tone, g.math("MULTIPLY", weft, 0.15))
    dark = g.ramp(tone, [(0.25, (0.13, 0.17, 0.30)), (0.8, (0.32, 0.39, 0.55))])
    faded = g.ramp(tone, [(0.25, (0.38, 0.44, 0.56)), (0.8, (0.62, 0.67, 0.75))])
    base = g.lerp_color(dark, faded, g.math("MULTIPLY", wear, 0.6))
    rough = 0.88
    rough = g.math("ADD", rough, g.math("MULTIPLY", fiber, 0.08))
    height = g.math("ADD", g.math("MULTIPLY", twill, 0.65), g.math("MULTIPLY", fiber, 0.35))
    return base, rough, height


def metal_oxidado(g):
    """Metal pintado (verde viejo) descascarado y con óxido: columpio. Devuelve también metallic."""
    rust_n = g.noise(4.0, 4.0, detail=8, rough=0.65, distortion=0.3, seed=1)
    rust = g.remap(rust_n, 0.52, 0.6, 0.0, 1.0)                    # 1 = óxido
    chip = g.remap(rust_n, 0.6, 0.63, 0.0, 1.0)                    # metal desnudo dentro del óxido
    fine = g.noise(30.0, 30.0, detail=6, seed=2)
    streak = g.noise(2.0, 14.0, detail=5, seed=3)
    paint = g.ramp(g.math("ADD", g.math("MULTIPLY", fine, 0.4), g.math("MULTIPLY", streak, 0.6)),
                   [(0.3, (0.24, 0.30, 0.24)), (0.7, (0.36, 0.42, 0.34))])
    rust_c = g.ramp(fine, [(0.2, (0.25, 0.12, 0.06)), (0.6, (0.45, 0.22, 0.09)), (0.9, (0.6, 0.35, 0.16))])
    bare = g.ramp(fine, [(0.3, (0.45, 0.44, 0.42)), (0.8, (0.6, 0.6, 0.58))])
    base = g.lerp_color(g.lerp_color(paint, rust_c, rust), bare, g.math("MULTIPLY", chip, 0.9))
    rough = g.math("ADD", g.math("MULTIPLY", rust, 0.35), 0.55)
    height = g.math("ADD", g.math("MULTIPLY", g.math("SUBTRACT", 1.0, rust), 0.3), g.math("MULTIPLY", rust, g.math("MULTIPLY", fine, 0.5)))
    height = g.math("ADD", height, 0.3)
    metallic = g.math("MULTIPLY", chip, 0.85)
    return base, rough, height, metallic


def mantel(g):
    """Mantel a cuadros rojo/blanco (cocina): 8×8 cuadros por tile, con trama."""
    cu = _periodic(g, g.u, 8)
    cv = _periodic(g, g.v, 8)
    band_u = g.remap(cu, 0.48, 0.52, 0.0, 1.0)
    band_v = g.remap(cv, 0.48, 0.52, 0.0, 1.0)
    weave = g.math("MULTIPLY", _periodic(g, g.u, 300), _periodic(g, g.v, 300))
    fiber = g.noise(20.0, 20.0, detail=6, seed=1)
    stain = g.remap(g.noise(2.0, 2.0, detail=4, seed=2), 0.6, 0.7, 0.0, 0.35)
    red = g.math("ADD", g.math("MULTIPLY", band_u, 0.5), g.math("MULTIPLY", band_v, 0.5))   # 0, 0.5 o 1
    white = (0.86, 0.83, 0.76)
    base = g.ramp(red, [(0.0, white), (0.45, (0.72, 0.36, 0.33)), (0.55, (0.72, 0.36, 0.33)), (1.0, (0.55, 0.12, 0.12))])
    base = g.mul_color(base, g.math("ADD", 0.85, g.math("MULTIPLY", fiber, 0.15)))
    base = g.mul_color(base, g.math("SUBTRACT", 1.0, stain))
    rough = 0.9
    rough = g.math("ADD", rough, g.math("MULTIPLY", weave, 0.05))
    height = g.math("ADD", g.math("MULTIPLY", weave, 0.6), g.math("MULTIPLY", fiber, 0.4))
    return base, rough, height


def piel_criatura(g):
    """Piel de la criatura: cuero pardo, casi sin pelo, con arrugas, venas y parches de pelaje ralo."""
    mottled = g.noise(5.0, 5.0, detail=8, rough=0.6, distortion=0.6, seed=1)
    wrinkle = g.voronoi(26.0, 26.0, feature="DISTANCE_TO_EDGE", seed=2)
    veins = g.voronoi(4.0, 4.0, feature="DISTANCE_TO_EDGE", seed=3)
    fur = g.noise(4.0, 120.0, detail=6, rough=0.7, seed=4)          # pelo corto en una dirección
    patch = g.remap(g.noise(3.0, 3.0, detail=4, seed=5), 0.5, 0.62, 0.0, 1.0)
    hide = g.ramp(mottled, [(0.25, (0.20, 0.15, 0.12)), (0.55, (0.36, 0.28, 0.22)), (0.8, (0.45, 0.38, 0.32))])
    hair = g.ramp(fur, [(0.3, (0.16, 0.12, 0.09)), (0.7, (0.42, 0.33, 0.24))])
    base = g.lerp_color(hide, hair, g.math("MULTIPLY", patch, 0.85))
    vein = g.remap(veins, 0.0, 0.015, 1.0, 0.0)
    base = g.mul_color(base, g.math("SUBTRACT", 1.0, g.math("MULTIPLY", vein, 0.18)))
    crease = g.remap(wrinkle, 0.0, 0.05, 0.0, 1.0)
    base = g.mul_color(base, g.math("ADD", 0.86, g.math("MULTIPLY", crease, 0.14)))
    rough = g.math("SUBTRACT", 0.75, g.math("MULTIPLY", g.math("SUBTRACT", 1.0, patch), 0.25))   # cuero algo brillante (húmedo)
    height = g.math("ADD", g.math("MULTIPLY", crease, 0.2), g.math("MULTIPLY", mottled, 0.45))
    height = g.math("ADD", height, g.math("MULTIPLY", vein, 0.15))
    height = g.math("ADD", height, g.math("MULTIPLY", g.math("MULTIPLY", fur, patch), 0.2))
    return base, rough, height


def revestimiento(g):
    """Revestimiento exterior de tablas solapadas (lap siding) pintado de blanco hueso, con pintura descascarada.
    10 tablas por tile en v (tile de 2 m → tablas de 20 cm)."""
    boards = 10
    f = g.fract(g.math("MULTIPLY", g.v, boards))
    lap = g.math("POWER", f, 0.8)                                   # cada tabla sobresale abajo (cuña)
    shadow = g.remap(f, 0.0, 0.08, 0.55, 1.0)                       # sombra bajo el solape
    grain = g.noise(1.0, 40.0, detail=6, rough=0.6, distortion=1.0, seed=1)
    peel_n = g.noise(5.0, 5.0, detail=8, rough=0.65, seed=2)
    peel = g.remap(peel_n, 0.66, 0.69, 0.0, 1.0)                    # 1 = madera expuesta
    dirt = g.noise(2.0, 2.0, detail=4, seed=3)
    drip = g.remap(g.noise(6.0, 1.0, detail=5, seed=4), 0.5, 0.8, 0.0, 1.0)
    paint = g.ramp(g.math("ADD", g.math("MULTIPLY", grain, 0.25), g.math("MULTIPLY", dirt, 0.75)),
                   [(0.3, (0.70, 0.68, 0.62)), (0.7, (0.84, 0.82, 0.76))])
    wood = g.ramp(grain, [(0.3, (0.32, 0.27, 0.22)), (0.7, (0.45, 0.39, 0.32))])
    base = g.lerp_color(paint, wood, peel)
    base = g.mul_color(base, g.math("SUBTRACT", 1.0, g.math("MULTIPLY", drip, 0.18)))
    base = g.mul_color(base, shadow)
    rough = g.math("ADD", 0.7, g.math("MULTIPLY", peel, 0.2))
    height = g.math("ADD", g.math("MULTIPLY", lap, 0.8), g.math("MULTIPLY", g.math("SUBTRACT", 1.0, peel), 0.08))
    height = g.math("ADD", height, g.math("MULTIPLY", grain, 0.05))
    return base, rough, height


def tejas(g):
    """Tejas asfálticas (shingles) gastadas: hileras escalonadas de 3 lengüetas, con musgo."""
    rows = 8
    rv = g.math("MULTIPLY", g.v, rows)
    row = g.math("FLOOR", rv)
    fv = g.fract(rv)
    off = g.math("MULTIPLY", g.math("MODULO", row, 2.0), 0.5)
    tabs = g.math("ADD", g.math("MULTIPLY", g.u, 6.0), off)
    tab_id = g.math("FLOOR", tabs)
    fu = g.fract(tabs)
    rnd = g.fract(g.math("MULTIPLY", g.math("SINE", g.math("ADD", g.math("MULTIPLY", tab_id, 12.9898), g.math("MULTIPLY", row, 78.233))), 43758.5453))
    slot = g.remap(g.math("MINIMUM", fu, g.math("SUBTRACT", 1.0, fu)), 0.0, 0.015, 0.0, 1.0)
    edge = g.remap(fv, 0.0, 0.07, 0.35, 1.0)
    grit = g.noise(60.0, 60.0, detail=4, rough=0.8, seed=1)
    moss = g.remap(g.noise(4.0, 4.0, detail=6, seed=2), 0.6, 0.7, 0.0, 1.0)
    tone = g.math("ADD", g.math("MULTIPLY", grit, 0.5), g.math("MULTIPLY", rnd, 0.5))
    shing = g.ramp(tone, [(0.2, (0.16, 0.15, 0.15)), (0.6, (0.27, 0.25, 0.24)), (0.9, (0.36, 0.33, 0.31))])
    moss_c = g.ramp(grit, [(0.3, (0.22, 0.26, 0.14)), (0.8, (0.34, 0.36, 0.2))])
    base = g.lerp_color(shing, moss_c, g.math("MULTIPLY", moss, 0.8))
    base = g.mul_color(base, g.math("MULTIPLY", edge, slot))
    rough = 0.92
    rough = g.math("SUBTRACT", rough, g.math("MULTIPLY", grit, 0.05))
    height = g.math("ADD", g.math("MULTIPLY", fv, 0.6), g.math("MULTIPLY", grit, 0.2))
    height = g.math("MULTIPLY", height, slot)
    return base, rough, height


def azulejo(g):
    """Azulejo cerámico blanco crema de cocina (4×4 por tile), con junta sucia."""
    n = 4
    fu = g.fract(g.math("MULTIPLY", g.u, n))
    fv = g.fract(g.math("MULTIPLY", g.v, n))
    du = g.math("MINIMUM", fu, g.math("SUBTRACT", 1.0, fu))
    dv = g.math("MINIMUM", fv, g.math("SUBTRACT", 1.0, fv))
    grout = g.remap(g.math("MINIMUM", du, dv), 0.0, 0.03, 0.0, 1.0)  # 0 = junta
    bevel = g.remap(g.math("MINIMUM", du, dv), 0.0, 0.08, 0.6, 1.0)
    tile_id = g.math("ADD", g.math("FLOOR", g.math("MULTIPLY", g.u, n)), g.math("MULTIPLY", g.math("FLOOR", g.math("MULTIPLY", g.v, n)), 7.0))
    rnd = g.fract(g.math("MULTIPLY", g.math("SINE", g.math("MULTIPLY", tile_id, 12.9898)), 43758.5453))
    stain = g.noise(3.0, 3.0, detail=5, seed=1)
    tile = g.ramp(g.math("ADD", g.math("MULTIPLY", rnd, 0.4), g.math("MULTIPLY", stain, 0.6)),
                  [(0.3, (0.80, 0.78, 0.70)), (0.8, (0.90, 0.88, 0.80))])
    grout_c = g.ramp(stain, [(0.3, (0.36, 0.33, 0.28)), (0.8, (0.52, 0.48, 0.42))])
    base = g.lerp_color(grout_c, tile, grout)
    rough = g.math("SUBTRACT", 0.85, g.math("MULTIPLY", grout, 0.7))     # cerámica brillante, junta mate
    height = g.math("MULTIPLY", bevel, grout)
    return base, rough, height


def concreto(g):
    """Concreto / bloque de cimiento con manchas de humedad y poros."""
    big = g.noise(3.0, 3.0, detail=6, rough=0.6, seed=1)
    pores = g.voronoi(50.0, 50.0, feature="F1", seed=2)
    pore = g.remap(pores, 0.0, 0.08, 0.0, 1.0)
    damp = g.remap(g.noise(2.0, 2.0, detail=5, distortion=0.5, seed=3), 0.45, 0.7, 0.0, 1.0)
    fine = g.noise(40.0, 40.0, detail=4, seed=4)
    base = g.ramp(g.math("ADD", g.math("MULTIPLY", big, 0.6), g.math("MULTIPLY", fine, 0.4)),
                  [(0.3, (0.42, 0.41, 0.39)), (0.7, (0.58, 0.57, 0.54))])
    base = g.mul_color(base, g.math("SUBTRACT", 1.0, g.math("MULTIPLY", damp, 0.3)))
    base = g.mul_color(base, g.math("ADD", 0.75, g.math("MULTIPLY", pore, 0.25)))
    rough = g.math("SUBTRACT", 0.92, g.math("MULTIPLY", damp, 0.15))
    height = g.math("ADD", g.math("MULTIPLY", big, 0.5), g.math("MULTIPLY", pore, 0.3))
    height = g.math("ADD", height, g.math("MULTIPLY", fine, 0.2))
    return base, rough, height


def follaje(g):
    """Follaje denso visto de cerca: hojas/acículas en racimos con huecos oscuros (copas de árboles, arbustos).
    Verde oliva neutro: cada material lo tiñe (pino azulado, álamo amarillento, artemisa gris)."""
    leaves = g.voronoi(30.0, 30.0, feature="F1", seed=1)
    edges = g.voronoi(30.0, 30.0, feature="DISTANCE_TO_EDGE", seed=1)
    cell = g.voronoi(30.0, 30.0, feature="F1", seed=1, out="Color")
    clump = g.noise(5.0, 5.0, detail=6, rough=0.6, distortion=0.8, seed=2)
    fine = g.noise(80.0, 80.0, detail=4, seed=3)
    hole = g.remap(clump, 0.3, 0.45, 0.0, 1.0)                          # 0 = hueco profundo entre racimos
    vein = g.remap(edges, 0.0, 0.06, 0.0, 1.0)
    sep = g.nt.nodes.new("ShaderNodeSeparateColor")
    g.link(cell, sep.inputs["Color"])
    tone = g.math("ADD", g.math("MULTIPLY", sep.outputs[0], 0.5), g.math("MULTIPLY", fine, 0.3))
    tone = g.math("ADD", tone, g.math("MULTIPLY", clump, 0.3))
    leaf = g.ramp(tone, [(0.2, (0.20, 0.26, 0.12)), (0.55, (0.34, 0.40, 0.20)), (0.9, (0.52, 0.55, 0.30))])
    base = g.mul_color(leaf, g.math("MULTIPLY", g.math("ADD", 0.35, g.math("MULTIPLY", hole, 0.65)),
                                    g.math("ADD", 0.55, g.math("MULTIPLY", vein, 0.45))))
    rough = g.math("SUBTRACT", 0.8, g.math("MULTIPLY", vein, 0.2))
    height = g.math("MULTIPLY", g.math("ADD", g.math("MULTIPLY", g.math("SUBTRACT", 1.0, leaves), 0.5), 0.3), hole)
    height = g.math("ADD", height, g.math("MULTIPLY", fine, 0.1))
    return base, rough, height


RECIPES = {
    "MaderaVieja": madera_vieja,
    "Roca": roca,
    "Corteza": corteza,
    "PastoSeco": pasto_seco,
    "Tierra": tierra,
    "Barro": barro,
    "Yeso": yeso,
    "Tela": tela,
    "Mezclilla": mezclilla,
    "MetalOxidado": metal_oxidado,
    "Mantel": mantel,
    "PielCriatura": piel_criatura,
    "Revestimiento": revestimiento,
    "Tejas": tejas,
    "Azulejo": azulejo,
    "Concreto": concreto,
    "Follaje": follaje,
}

# Intensidad del relieve del normal map por material.
NORMAL_STRENGTH = {"MaderaVieja": 5.0, "Roca": 7.0, "Corteza": 9.0, "PastoSeco": 4.0, "Tierra": 5.0, "Barro": 3.0, "Yeso": 2.5,
                   "Tela": 3.0, "Mezclilla": 2.0, "MetalOxidado": 4.0, "Mantel": 1.0, "PielCriatura": 3.5,
                   "Revestimiento": 6.0, "Tejas": 6.0, "Azulejo": 3.0, "Concreto": 4.0, "Follaje": 5.0}


# ──────────────────────────────────────────────────────────────────────────────
# Bake + post-proceso
# ──────────────────────────────────────────────────────────────────────────────
def bake_channel(obj, mat, g, socket, name, non_color):
    img = bpy.data.images.new(name, SIZE, SIZE, alpha=False, float_buffer=True)
    img.colorspace_settings.name = "Non-Color" if non_color else "Linear Rec.709"
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = img
    mat.node_tree.nodes.active = tex
    g.set_output(socket)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.bake(type="EMIT")
    px = np.array(img.pixels[:], dtype=np.float32).reshape(SIZE, SIZE, 4)
    mat.node_tree.nodes.remove(tex)
    bpy.data.images.remove(img)
    return px[..., :3]


def linear_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    nx, ny, nz = -dx * strength * (SIZE / 256), -dy * strength * (SIZE / 256), np.ones_like(h)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    n = np.stack([nx / length, ny / length, nz / length], axis=-1)
    return n * 0.5 + 0.5  # OpenGL (+Y arriba): lo que espera Unity


def blur(a, radius):
    out = a.copy()
    for axis in (0, 1):
        acc = np.zeros_like(out)
        for k in range(-radius, radius + 1):
            acc += np.roll(out, k, axis=axis)
        out = acc / (2 * radius + 1)
    return out


def ao_from_height(h):
    cavity = np.clip((blur(h, max(2, SIZE // 128)) - h) * 6.0, 0, 1)
    broad = np.clip((blur(h, max(4, SIZE // 48)) - h) * 3.0, 0, 1)
    return np.clip(1.0 - cavity * 0.6 - broad * 0.35, 0, 1)


def save_png(path, rgba):
    h, w, ch = rgba.shape
    if ch == 3:
        rgba = np.concatenate([rgba, np.ones((h, w, 1), np.float32)], axis=-1)
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    img.colorspace_settings.name = "Non-Color"  # ya escribimos los valores finales
    img.pixels[:] = np.clip(rgba, 0, 1).astype(np.float32).ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def generate(obj, name, recipe):
    mat = bpy.data.materials.new(name)
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    g = G(mat)
    result = recipe(g)
    base, rough, height = result[:3]
    metal_sock = result[3] if len(result) > 3 else None

    base_lin = bake_channel(obj, mat, g, base, f"{name}_base", non_color=False)
    rough_v = bake_channel(obj, mat, g, rough, f"{name}_rough", non_color=True)[..., 0]
    height_v = bake_channel(obj, mat, g, height, f"{name}_height", non_color=True)[..., 0]

    hmin, hmax = float(height_v.min()), float(height_v.max())
    height_n = (height_v - hmin) / max(hmax - hmin, 1e-5)
    normal = normal_from_height(height_n, NORMAL_STRENGTH.get(name, 5.0))
    ao = ao_from_height(height_n)
    smooth = 1.0 - np.clip(rough_v, 0, 1)
    zeros = np.zeros_like(smooth)
    metal = (np.clip(bake_channel(obj, mat, g, metal_sock, f"{name}_metal", non_color=True)[..., 0], 0, 1)
             if metal_sock is not None else zeros)

    folder = os.path.join(OUT, name)
    os.makedirs(folder, exist_ok=True)
    p = lambda suffix: os.path.join(folder, f"T_{name}_{suffix}.png")
    save_png(p("BaseColor"), linear_to_srgb(base_lin))
    save_png(p("Normal"), normal)
    save_png(p("MetallicSmoothness"), np.stack([metal, metal, metal, smooth], axis=-1))
    save_png(p("AO"), np.stack([ao, ao, ao], axis=-1))
    save_png(p("Height"), np.stack([height_n] * 3, axis=-1))
    save_png(p("TerrainMask"), np.stack([metal, ao, height_n, smooth], axis=-1))
    print(f"[PBR] {name}: listo → {folder}")


def main():
    obj = setup_scene()
    for name, recipe in RECIPES.items():
        if ONLY and name not in ONLY:
            continue
        generate(obj, name, recipe)
    print("[PBR] Terminado.")


main()
