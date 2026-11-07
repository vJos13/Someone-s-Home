"""
Someone's Home · Vegetación y rocas del Relato 1 (Wenatchee, WA: semiárido, pinos ponderosa, álamos junto al río,
artemisa, pasto seco, juncos en la orilla). Blender 5.x, todo por código.

Uso (desde la raíz del proyecto):
    blender -b --factory-startup --python Tools/Blender/build_nature.py

Salida (Assets/_Project/Art/Models/Nature/, un FBX por asset):
  Pino_A/B/C, Alamo_A/B, ArbolSeco_A     → con <Nombre>_LOD0/_LOD1/_LOD2 (Unity crea el LODGroup solo)
  Artemisa_A/B                            → _LOD0/_LOD1
  Roca_A..E (bloques), Roca_Plana (piedra del sendero, cara superior plana en y = 0)
  Pasto_A/B, Juncos                       → detalles del terreno (instanciados)
  Cerca_Tramo (2.5 m), Poste_Luz, Tronco_Caido, Tocon
+ Assets/_Project/Art/Textures/Nature/T_PastoHoja.png  (degradado por hoja de pasto)
+ Tools/Blender/Nature.blend

Se modela en ejes de Blender (Z arriba) centrado en el origen: al exportar con bakeAxisConversion=false en Unity
solo se espeja X / Z, irrelevante para piezas orgánicas. La cerca va a lo largo del eje X.
"""
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "Models", "Nature")
TEX_OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "Textures", "Nature")
BLEND = os.path.join(ROOT, "Tools", "Blender", "Nature.blend")

TILE = {"M_Nat_CortezaPino": 1.2, "M_Nat_Corteza": 1.2, "M_Nat_FollajePino": 1.5, "M_Nat_FollajeAlamo": 2.0,
        "M_Nat_Artemisa": 1.0, "M_Nat_Roca": 2.0, "M_Nat_MaderaCerca": 1.0, "M_Nat_Poste": 1.5}
PREVIEW = {"M_Nat_CortezaPino": (0.45, 0.28, 0.18), "M_Nat_Corteza": (0.3, 0.26, 0.22), "M_Nat_FollajePino": (0.15, 0.22, 0.15),
           "M_Nat_FollajeAlamo": (0.3, 0.38, 0.15), "M_Nat_Artemisa": (0.45, 0.5, 0.42), "M_Nat_Roca": (0.45, 0.43, 0.4),
           "M_Nat_Pasto": (0.6, 0.55, 0.3), "M_Nat_Juncos": (0.35, 0.45, 0.2), "M_Nat_MaderaCerca": (0.45, 0.42, 0.38),
           "M_Nat_Poste": (0.3, 0.24, 0.18), "M_Nat_Metal": (0.2, 0.2, 0.2), "M_Nat_Vidrio": (0.4, 0.55, 0.5)}

# ──────────────────────────────────────────────────────────────────────────────
# Utilidades
# ──────────────────────────────────────────────────────────────────────────────
def material(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    c = PREVIEW.get(name, (0.5, 0.5, 0.5))
    m.diffuse_color = (*c, 1)
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*c, 1)
    return m


def new_obj(name, bm, mat, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(material(mat))
    for p in o.data.polygons:
        p.use_smooth = smooth
    return o


def activate(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def join(name, objs):
    objs = [o for o in objs if o]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    return o


def tube(a, b, r0, r1, mat, verts=8, smooth=True):
    """Cono truncado entre a y b (rama, tronco)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=verts, radius1=r0, radius2=r1, depth=d.length)
    q = d.to_track_quat("Z", "Y")
    bmesh.ops.transform(bm, matrix=Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4(), verts=bm.verts)
    return new_obj("tube", bm, mat, smooth)


def blob(center, radii, mat, seed, amp=0.25, freq=1.6, subdiv=2, direction=None):
    """Ico-esfera deformada con ruido (racimo de hojas, arbusto). direction = eje X local del racimo."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    off = Vector((seed * 3.1, seed * 1.7, seed * 2.3))
    for v in bm.verts:
        n = v.co.normalized()
        d = noise.noise(n * freq + off) + 0.5 * noise.noise(n * freq * 2.7 + off)
        v.co = n * (1.0 + d * amp)
        v.co = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2]))
    m = Matrix.Translation(center)
    if direction is not None:
        m = m @ Vector(direction).to_track_quat("X", "Z").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return new_obj("blob", bm, mat)


def box_uv(o):
    me = o.data
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.verify()
    names = [m.name for m in me.materials]
    for f in bm.faces:
        tile = TILE.get(names[f.material_index], 1.0)
        ax = max(range(3), key=lambda i: abs(f.normal[i]))
        for l in f.loops:
            p = l.vert.co
            u, v = ((p.y, p.z), (p.x, p.z), (p.x, p.y))[ax]
            l[uv].uv = (u / tile, v / tile)
    bm.to_mesh(me)
    bm.free()


def cyl_uv(o, mats=("M_Nat_CortezaPino", "M_Nat_Corteza", "M_Nat_Poste")):
    """Troncos: UV cilíndrica (u = ángulo, v = altura) para que la corteza corra vertical."""
    me = o.data
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.verify()
    names = [m.name for m in me.materials]
    for f in bm.faces:
        if names[f.material_index] not in mats:
            continue
        tile = TILE.get(names[f.material_index], 1.0)
        us = []
        for l in f.loops:
            p = l.vert.co
            us.append(math.atan2(p.y, p.x) / (2 * math.pi) * 1.2)
        # evitar el salto en ±π dentro de una cara
        if max(us) - min(us) > 0.5:
            us = [u + 1.2 if u < 0 else u for u in us]
        for l, u in zip(f.loops, us):
            l[uv].uv = (u * 3.0 / tile, l.vert.co.z / tile)
    bm.to_mesh(me)
    bm.free()


def lods(base, name, ratios):
    """Crea <name>_LOD0.. a partir de base con Decimate."""
    base.name = f"{name}_LOD0"
    out = [base]
    for i, r in enumerate(ratios, start=1):
        c = base.copy()
        c.data = base.data.copy()
        bpy.context.collection.objects.link(c)
        c.name = f"{name}_LOD{i}"
        mod = c.modifiers.new("Dec", "DECIMATE")
        mod.ratio = r
        activate(c)
        bpy.ops.object.modifier_apply(modifier="Dec")
        out.append(c)
    return out


def export(objs, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, filename), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE")


def stash(objs, slot):
    """Separa en la .blend para que se vea ordenado (no afecta al FBX, que se exporta antes)."""
    for o in objs:
        o.location.x += (slot % 6) * 20
        o.location.y += (slot // 6) * 20


ASSETS = []


def publish(objs, filename):
    for o in objs:
        if o.data.materials and any(m.name in TILE for m in o.data.materials):
            box_uv(o)
            cyl_uv(o)
    export(objs, filename)
    stash(objs, len(ASSETS))
    ASSETS.append(filename)
    print(f"[NAT] {filename}: {sum(len(o.data.polygons) for o in objs if o.name.endswith(('LOD0',)) or 'LOD' not in o.name)} caras")


# ──────────────────────────────────────────────────────────────────────────────
# Árboles
# ──────────────────────────────────────────────────────────────────────────────
def pino(name, seed, height):
    """Pino ponderosa: fuste recto con corteza naranja en placas, sin ramas abajo, copa abierta e irregular."""
    rnd = random.Random(seed)
    B, F = "M_Nat_CortezaPino", "M_Nat_FollajePino"
    parts = [tube((0, 0, -0.3), (0, 0, height), 0.36 * height / 16, 0.03, B, verts=10),
             tube((0, 0, -0.3), (0, 0, 0.7), 0.55 * height / 16, 0.36 * height / 16, B, verts=10)]
    crown0 = height * rnd.uniform(0.42, 0.52)
    # Ramas muertas cortas bajo la copa (silueta inquietante).
    for i in range(rnd.randint(3, 6)):
        z = rnd.uniform(height * 0.25, crown0)
        a = rnd.uniform(0, 2 * math.pi)
        L = rnd.uniform(0.4, 1.1)
        r = 0.36 * height / 16 * (1 - z / height)
        parts.append(tube((math.cos(a) * r, math.sin(a) * r, z),
                          (math.cos(a) * (r + L), math.sin(a) * (r + L), z - L * 0.25), 0.03, 0.006, B, verts=5))
    z = crown0
    k = 0
    while z < height - 0.6:
        t = (z - crown0) / (height - crown0)
        for _ in range(rnd.randint(3, 5)):
            a = rnd.uniform(0, 2 * math.pi)
            L = (1 - t) ** 0.7 * rnd.uniform(2.2, 3.6) + 0.5
            if rnd.random() < 0.15:
                continue                     # huecos en la copa (ponderosa es rala)
            d = Vector((math.cos(a), math.sin(a), rnd.uniform(-0.25, 0.15))).normalized()
            r = 0.36 * height / 16 * (1 - z / height) + 0.02
            start = Vector((0, 0, z)) + d * r
            end = start + d * L
            parts.append(tube(start, end, 0.05 * (1 - t) + 0.02, 0.01, B, verts=5))
            # Mechones de acículas a lo largo de la mitad exterior de la rama (ponderosa: penachos en las puntas).
            n_tufts = max(1, int(L / 0.9))
            for j in range(n_tufts):
                f = 0.45 + 0.6 * (j + 1) / (n_tufts + 1)
                c = start + d * (L * f) + Vector((rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15), rnd.uniform(0.0, 0.25)))
                rr = rnd.uniform(0.45, 0.7) * (0.75 + 0.35 * (1 - t))
                parts.append(blob(c, (rr * 1.3, rr, rr * 0.8), F, seed * 31 + k, amp=0.5, freq=2.6, direction=d))
                k += 1
        z += rnd.uniform(0.7, 1.1)
    parts.append(blob(Vector((0, 0, height - 0.3)), (0.55, 0.55, 1.0), F, seed * 31 + k + 1, amp=0.3))
    tree = join(name, parts)
    return lods(tree, name, (0.35, 0.08))


def alamo(name, seed, height):
    """Álamo de río (cottonwood): tronco grueso que se abre en ramas grandes, copa ancha en masas."""
    rnd = random.Random(seed)
    B, F = "M_Nat_Corteza", "M_Nat_FollajeAlamo"
    parts = []
    tips = []

    def branch(a, d, length, r, depth):
        b = a + d * length
        parts.append(tube(a, b, r, r * 0.62, B, verts=8 if depth < 2 else 6))
        if depth >= 3 or length < 1.2:
            tips.append((b, length))
            return
        n = 2 if depth > 0 else 3
        for i in range(n):
            ang = (i / n) * 2 * math.pi + rnd.uniform(-0.6, 0.6)
            out = Vector((math.cos(ang), math.sin(ang), 0))
            nd = (d * rnd.uniform(0.9, 1.4) + out * rnd.uniform(0.55, 0.9)).normalized()
            branch(b, nd, length * rnd.uniform(0.6, 0.78), r * 0.62, depth + 1)

    trunk_h = height * 0.3
    parts.append(tube((0, 0, -0.3), (0, 0, 0.8), 0.75, 0.48, B, verts=10))
    branch(Vector((0, 0, 0.5)), Vector((rnd.uniform(-0.1, 0.1), rnd.uniform(-0.1, 0.1), 1)).normalized(), trunk_h, 0.48, 0)
    for i, (p, L) in enumerate(tips):
        rr = rnd.uniform(1.2, 1.8)
        parts.append(blob(p + Vector((0, 0, 0.3)), (rr * 1.15, rr * 1.15, rr * 0.85), F, seed * 17 + i, amp=0.45, freq=2.2))
        for j in range(3):   # masas pequeñas alrededor: silueta irregular, no "brócoli"
            off = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.6, 0.5))).normalized() * rr * 1.1
            r2 = rr * rnd.uniform(0.45, 0.65)
            parts.append(blob(p + off, (r2, r2, r2 * 0.85), F, seed * 17 + 100 + i * 3 + j, amp=0.5, freq=2.6))
    # masas interiores para que no se vea hueca
    for i in range(4):
        c = Vector((rnd.uniform(-2, 2), rnd.uniform(-2, 2), height * rnd.uniform(0.55, 0.75)))
        parts.append(blob(c, (2.2, 2.2, 1.7), F, seed * 17 + 50 + i, amp=0.3))
    tree = join(name, parts)
    # escalar a la altura pedida
    zmax = max(v.co.z for v in tree.data.vertices)
    s = height / zmax
    tree.scale = (s, s, s)
    activate(tree)
    bpy.ops.object.transform_apply(scale=True)
    return lods(tree, name, (0.3, 0.07))


def arbol_seco(name, seed, height):
    """Árbol muerto sin hojas (snag): ramas retorcidas contra el cielo."""
    rnd = random.Random(seed)
    B = "M_Nat_Corteza"
    parts = []

    def branch(a, d, length, r, depth):
        b = a + d * length
        parts.append(tube(a, b, r, r * 0.6, B, verts=7 if depth < 2 else 5))
        if depth >= 5 or r < 0.008:
            return
        for i in range(rnd.randint(2, 3)):
            ang = rnd.uniform(0, 2 * math.pi)
            out = Vector((math.cos(ang), math.sin(ang), rnd.uniform(-0.3, 0.2)))
            nd = (d * rnd.uniform(0.4, 1.0) + out).normalized()
            branch(b, nd, length * rnd.uniform(0.55, 0.75), r * 0.6, depth + 1)

    parts.append(tube((0, 0, -0.3), (0, 0, 0.6), 0.5, 0.33, B, verts=9))
    branch(Vector((0, 0, 0.4)), Vector((0.08, -0.05, 1)).normalized(), height * 0.38, 0.33, 0)
    tree = join(name, parts)
    return lods(tree, name, (0.4, 0.12))


def artemisa(name, seed):
    """Artemisa (sagebrush): matorral gris verdoso de ~1 m, tallos leñosos y masas pequeñas."""
    rnd = random.Random(seed)
    parts = []
    for i in range(rnd.randint(6, 9)):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0.1, 0.5)
        h = rnd.uniform(0.45, 0.95)
        base = Vector((math.cos(a) * r * 0.3, math.sin(a) * r * 0.3, 0))
        top = Vector((math.cos(a) * r, math.sin(a) * r, h))
        parts.append(tube(base, top * 0.85, 0.035, 0.012, "M_Nat_Corteza", verts=5))
        parts.append(blob(top, (rnd.uniform(0.25, 0.4),) * 2 + (rnd.uniform(0.2, 0.3),), "M_Nat_Artemisa", seed * 13 + i,
                          amp=0.4, freq=2.2, subdiv=2))
    b = join(name, parts)
    return lods(b, name, (0.3,))


# ──────────────────────────────────────────────────────────────────────────────
# Rocas
# ──────────────────────────────────────────────────────────────────────────────
def roca(name, seed, size=(1.6, 1.2, 0.9)):
    """Bloque facetado: esfera cortada por planos aleatorios + ruido fino. Base plana y algo enterrada."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1.0)
    for i in range(rnd.randint(7, 11)):
        n = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.4, 1))).normalized()
        co = n * rnd.uniform(0.62, 0.85)
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=n, clear_outer=True)
        edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        if edges:
            bmesh.ops.holes_fill(bm, edges=edges)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, -0.35), plane_no=(0, 0, -1), clear_outer=True)
    edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    if edges:
        bmesh.ops.holes_fill(bm, edges=edges)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    for v in bm.verts:
        v.co += v.normal * noise.noise(v.co * 3.0 + Vector((seed, 0, 0))) * 0.04
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], (v.co.z + 0.35) * size[2] - 0.15))
    o = new_obj(name, bm, "M_Nat_Roca", smooth=False)
    mod = o.modifiers.new("WN", "WEIGHTED_NORMAL")
    activate(o)
    bpy.ops.object.modifier_apply(modifier="WN")
    return [o]


def roca_plana(name, seed):
    """Piedra del sendero: huella ~2.3 × 1.6 m, cara superior casi plana en z = 0 (Unity y = 0), baja 1.4 m."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(2.3, 1.6, 1.4), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(0, 0, -0.7), verts=bm.verts)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=3, use_grid_fill=True)
    # chaflanes irregulares en las aristas superiores
    for i in range(6):
        a = rnd.uniform(0, 2 * math.pi)
        n = Vector((math.cos(a), math.sin(a), rnd.uniform(0.8, 1.4))).normalized()
        co = Vector((math.cos(a) * 1.0, math.sin(a) * 0.7, -0.08))
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=n, clear_outer=True)
        edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        if edges:
            bmesh.ops.holes_fill(bm, edges=edges)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    for v in bm.verts:
        amp = 0.015 if v.co.z > -0.02 else 0.06
        v.co += v.normal * noise.noise(v.co * 2.5 + Vector((seed, 1, 0))) * amp
    o = new_obj(name, bm, "M_Nat_Roca", smooth=False)
    return [o]


# ──────────────────────────────────────────────────────────────────────────────
# Pasto y juncos (detalles del terreno)
# ──────────────────────────────────────────────────────────────────────────────
def mata(name, seed, blades, h_range, spread, width, mat, bend=0.35):
    rnd = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    for i in range(blades):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0, spread)
        base = Vector((math.cos(a) * r, math.sin(a) * r, 0))
        h = rnd.uniform(*h_range)
        lean = Vector((math.cos(a), math.sin(a), 0)) * rnd.uniform(0.2, 1.0) * bend * h
        side = Vector((-math.sin(a + 1.2), math.cos(a + 1.2), 0))
        col = rnd.random()
        segs = 4
        prev = None
        for s in range(segs + 1):
            t = s / segs
            c = base + lean * t * t + Vector((0, 0, h * t))
            w = width * (1 - t) ** 0.8 * 0.5
            v1 = bm.verts.new(c - side * w)
            v2 = bm.verts.new(c + side * w)
            if prev:
                f = bm.faces.new((prev[0], prev[1], v2, v1))
                for l, uvv in zip(f.loops, ((col, (s - 1) / segs), (col + 0.01, (s - 1) / segs), (col + 0.01, t), (col, t))):
                    l[uv].uv = uvv
            prev = (v1, v2)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    # normales hacia arriba: el pasto se ilumina como una masa, no como cartas
    o = new_obj(name, bm, mat, smooth=True)
    me = o.data
    me.normals_split_custom_set_from_vertices([(0.0, 0.0, 1.0)] * len(me.vertices))
    return [o]


def textura_pasto():
    """T_PastoHoja: cada columna = una hoja (paja / verde seco), oscura en la base y clara en la punta."""
    W, H = 128, 256
    rnd = np.random.default_rng(5)
    hue = rnd.random(W)
    straw = np.array([0.78, 0.68, 0.42])
    green = np.array([0.45, 0.5, 0.26])
    brown = np.array([0.42, 0.33, 0.2])
    col = np.where(hue[:, None] < 0.55, straw, np.where(hue[:, None] < 0.85, green, brown))
    col = col * (0.85 + 0.3 * rnd.random(W))[:, None]
    v = np.linspace(0, 1, H)[:, None, None]
    shade = 0.35 + 0.65 * np.clip(v * 1.6, 0, 1)
    img = np.clip(col[None, :, :] * shade, 0, 1)
    rgba = np.concatenate([img, np.ones((H, W, 1))], axis=-1).astype(np.float32)
    os.makedirs(TEX_OUT, exist_ok=True)
    path = os.path.join(TEX_OUT, "T_PastoHoja.png")
    im = bpy.data.images.new("T_PastoHoja", W, H, alpha=True)
    im.colorspace_settings.name = "Non-Color"
    im.pixels[:] = rgba.ravel()
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    print("[NAT] T_PastoHoja.png")


# ──────────────────────────────────────────────────────────────────────────────
# Objetos humanos del paisaje
# ──────────────────────────────────────────────────────────────────────────────
def cerca_tramo(name, seed):
    """Cerca de troncos partidos, tramo de 2.5 m a lo largo de X (el poste del final lo pone el tramo siguiente)."""
    rnd = random.Random(seed)
    M = "M_Nat_MaderaCerca"
    p = [tube((-1.25, 0, -0.4), (-1.25 + rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), 1.25), 0.075, 0.065, M, verts=7)]
    p.append(tube((-1.25, 0, 1.25), (-1.25, 0, 1.3), 0.065, 0.03, M, verts=7))
    for z in (0.45, 0.9):
        dz = rnd.uniform(-0.08, 0.08)
        p.append(tube((-1.3, 0.05, z), (1.3, 0.05, z + dz), 0.055, 0.05, M, verts=6))
    return [join(name, p)]


def poste_luz(name):
    """Poste de madera de tendido eléctrico rural: 9 m, cruceta y aisladores."""
    P = "M_Nat_Poste"
    p = [tube((0, 0, -0.5), (0, 0, 9.0), 0.15, 0.11, P, verts=10)]
    p.append(tube((-1.1, 0, 8.5), (1.1, 0, 8.5), 0.06, 0.06, P, verts=4, smooth=False))
    for x in (-0.95, 0.0, 0.95):
        p.append(tube((x, 0, 8.56), (x, 0, 8.72), 0.035, 0.025, "M_Nat_Vidrio", verts=8))
    for s in (-1, 1):
        p.append(tube((0, 0, 8.0), (s * 0.6, 0, 8.48), 0.025, 0.025, P, verts=4, smooth=False))
    p.append(tube((0.12, 0, 7.4), (0.12, 0, 7.9), 0.12, 0.12, "M_Nat_Metal", verts=10))   # transformador
    return [join(name, p)]


def tronco_caido(name, seed):
    rnd = random.Random(seed)
    p = [tube((-2.6, 0, 0.25), (2.6, 0.2, 0.3), 0.32, 0.24, "M_Nat_Corteza", verts=10)]
    for i in range(3):
        x = rnd.uniform(-2, 2)
        a = rnd.uniform(0, math.pi)
        p.append(tube((x, 0, 0.35), (x + 0.3, math.cos(a) * 0.9, 0.35 + math.sin(a) * 0.8), 0.07, 0.02, "M_Nat_Corteza", verts=5))
    return [join(name, p)]


def tocon(name):
    p = [tube((0, 0, -0.2), (0, 0, 0.5), 0.45, 0.38, "M_Nat_Corteza", verts=10)]
    for i in range(4):
        a = i * math.pi / 2 + 0.4
        p.append(tube((0, 0, 0.1), (math.cos(a) * 0.8, math.sin(a) * 0.8, -0.15), 0.16, 0.05, "M_Nat_Corteza", verts=6))
    return [join(name, p)]


# ──────────────────────────────────────────────────────────────────────────────
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    textura_pasto()
    for n, seed, h in (("Pino_A", 1, 17.0), ("Pino_B", 2, 14.0), ("Pino_C", 3, 20.0)):
        publish(pino(n, seed, h), f"{n}.fbx")
    for n, seed, h in (("Alamo_A", 11, 15.0), ("Alamo_B", 12, 12.0)):
        publish(alamo(n, seed, h), f"{n}.fbx")
    publish(arbol_seco("ArbolSeco_A", 21, 11.0), "ArbolSeco_A.fbx")
    for n, seed in (("Artemisa_A", 31), ("Artemisa_B", 32)):
        publish(artemisa(n, seed), f"{n}.fbx")
    sizes = {"Roca_A": (1.6, 1.2, 0.9), "Roca_B": (2.6, 2.0, 1.4), "Roca_C": (0.8, 0.6, 0.5), "Roca_D": (1.2, 1.4, 1.1),
             "Roca_E": (3.5, 2.6, 1.8)}
    for i, (n, sz) in enumerate(sizes.items()):
        publish(roca(n, 40 + i, sz), f"{n}.fbx")
    publish(roca_plana("Roca_Plana", 50), "Roca_Plana.fbx")
    publish(mata("Pasto_A", 60, 34, (0.25, 0.5), 0.22, 0.035, "M_Nat_Pasto"), "Pasto_A.fbx")
    publish(mata("Pasto_B", 61, 46, (0.5, 0.85), 0.28, 0.03, "M_Nat_Pasto", bend=0.45), "Pasto_B.fbx")
    publish(mata("Juncos", 62, 26, (1.0, 1.6), 0.3, 0.025, "M_Nat_Juncos", bend=0.12), "Juncos.fbx")
    publish(cerca_tramo("Cerca_Tramo", 70), "Cerca_Tramo.fbx")
    publish(poste_luz("Poste_Luz"), "Poste_Luz.fbx")
    publish(tronco_caido("Tronco_Caido", 80), "Tronco_Caido.fbx")
    publish(tocon("Tocon"), "Tocon.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print(f"[NAT] {len(ASSETS)} assets. Guardado {BLEND}")


main()
