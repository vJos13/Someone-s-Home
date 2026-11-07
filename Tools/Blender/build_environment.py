"""
Someone's Home · Casa y columpio del Relato 1 (Blender 5.x, por código).

Uso (desde la raíz del proyecto):
    blender -b --factory-startup --python Tools/Blender/build_environment.py

Modela TODO en coordenadas de Unity (x este, y arriba, z norte/hacia el río) relativas a:
  - Casa:    centro del piso (c en Relato1Builder). Paredes 12 × 9 m, 3 m de alto, puerta trasera en x = +2.
  - Columpio: pie del columpio (swingPos). Pivote del asiento en y = 2.3.
La función U() convierte a Blender (Blender (x,y,z) = Unity (-x, z, -y) con el export FBX usado).

Salida: Assets/_Project/Art/Models/Environment/{Casa,Columpio}.fbx y Tools/Blender/Environment.blend.
Los colliders NO vienen de aquí: Unity conserva las cajas invisibles del greybox (EnvironmentSetup.cs).
UVs en metros / tamaño de tile del material (TILE) → en Unity los materiales usan tiling 1.
"""
import math
import os

import bmesh
import bpy
from mathutils import Euler, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "Models", "Environment")
BLEND = os.path.join(ROOT, "Tools", "Blender", "Environment.blend")

# Metros que cubre una repetición de la textura de cada material.
TILE = {
    "M_Casa_Revestimiento": 2.0, "M_Casa_Moldura": 1.0, "M_Casa_Yeso": 2.5, "M_Casa_Piso": 1.2,
    "M_Casa_TechoInt": 1.5, "M_Casa_Tejas": 2.0, "M_Casa_Concreto": 2.0, "M_Casa_Ladrillo": 1.0,
    "M_Casa_Azulejo": 0.6, "M_Casa_Gabinete": 1.0, "M_Casa_Encimera": 1.0, "M_Casa_Mueble": 1.0,
    "M_Mantel": 1.0, "M_Casa_PuertaPintada": 1.0, "M_Casa_Alfombra": 1.0, "M_Casa_Tela": 0.8,
    "M_Casa_Lena": 1.0, "M_Columpio_Metal": 1.0, "M_Columpio_Madera": 1.0,
}
PREVIEW = {  # solo para ver en Blender
    "M_Casa_Revestimiento": (0.8, 0.85, 0.85), "M_Casa_Moldura": (0.9, 0.9, 0.88), "M_Casa_Yeso": (0.85, 0.8, 0.7),
    "M_Casa_Piso": (0.45, 0.32, 0.2), "M_Casa_TechoInt": (0.6, 0.55, 0.45), "M_Casa_Tejas": (0.2, 0.19, 0.18),
    "M_Casa_Concreto": (0.5, 0.5, 0.48), "M_Casa_Ladrillo": (0.45, 0.25, 0.2), "M_Casa_Azulejo": (0.85, 0.83, 0.75),
    "M_Casa_Gabinete": (0.6, 0.48, 0.35), "M_Casa_Encimera": (0.8, 0.78, 0.7), "M_Casa_Metal": (0.6, 0.6, 0.6),
    "M_Casa_Esmalte": (0.85, 0.84, 0.8), "M_Casa_Negro": (0.05, 0.05, 0.05), "M_Casa_Mueble": (0.45, 0.3, 0.2),
    "M_Mantel": (0.7, 0.3, 0.3), "M_Casa_Vidrio": (0.6, 0.7, 0.75), "M_Foco": (1, 0.8, 0.5),
    "M_Casa_PuertaPintada": (0.4, 0.48, 0.45), "M_Casa_Alfombra": (0.45, 0.22, 0.18), "M_Casa_Tela": (0.85, 0.8, 0.65),
    "M_Casa_Lampara": (0.25, 0.38, 0.3), "M_Casa_Lena": (0.35, 0.25, 0.18), "M_Casa_Cuadro": (0.3, 0.32, 0.28),
    "M_Casa_Ceramica": (0.92, 0.9, 0.85), "M_Columpio_Metal": (0.3, 0.38, 0.3), "M_Columpio_Madera": (0.5, 0.4, 0.3),
}


# ──────────────────────────────────────────────────────────────────────────────
# Utilidades (todo en coordenadas Unity)
# ──────────────────────────────────────────────────────────────────────────────
def U(x, y, z):
    return Vector((-x, -z, y))


def S(sx, sy, sz):
    """Tamaño Unity → escala Blender."""
    return Vector((sx, sz, sy))


def material(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    col = PREVIEW.get(name, (0.5, 0.5, 0.5))
    m.diffuse_color = (*col, 1)
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*col, 1)
    return m


def _finish(obj, mat, smooth=False):
    obj.data.materials.clear()
    obj.data.materials.append(material(mat))
    for p in obj.data.polygons:
        p.use_smooth = smooth
    return obj


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_mods(obj):
    activate(obj)
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def box(center, size, mat, yaw=0.0, bevel=0.0, name="Box"):
    """Caja en coords Unity. yaw = grados alrededor de Y de Unity."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=U(*center), rotation=(0, 0, -math.radians(yaw)))
    o = bpy.context.active_object
    o.name = name
    o.scale = S(*size)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        b = o.modifiers.new("Bevel", "BEVEL")
        b.width = bevel
        b.segments = 2
        b.limit_method = "ANGLE"
        apply_mods(o)
    return _finish(o, mat)


def local_box(origin, yaw, offset, size, mat, bevel=0.0):
    """Caja en el espacio local de un objeto (origin + rotación yaw) — p. ej. piezas de una silla."""
    r = math.radians(yaw)
    ox, oy, oz = offset
    x = origin[0] + ox * math.cos(r) + oz * math.sin(r)
    z = origin[2] - ox * math.sin(r) + oz * math.cos(r)
    return box((x, origin[1] + oy, z), size, mat, yaw=yaw, bevel=bevel)


def beam(a, b, w, h, mat, up=(0, 1, 0)):
    """Viga entre dos puntos Unity: w = ancho, h = alto de la sección."""
    A, B = U(*a), U(*b)
    d = B - A
    bpy.ops.mesh.primitive_cube_add(size=1, location=(A + B) / 2)
    o = bpy.context.active_object
    o.scale = (w, h, d.length)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    upb = U(*up) if abs(d.normalized().dot(U(*up).normalized())) < 0.95 else U(1, 0, 0)
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    # Alinear el eje Y local con 'up' proyectado.
    q = d.to_track_quat("Z", "Y")
    cur_up = q @ Vector((0, 1, 0))
    want = (upb - upb.project(d)).normalized()
    ang = cur_up.angle(want) if want.length > 0 else 0
    if cur_up.cross(want).dot(d) < 0:
        ang = -ang
    from mathutils import Quaternion
    o.rotation_euler = (Quaternion(d.normalized(), ang) @ q).to_euler()
    return _finish(o, mat)


def cyl(a, b, r, mat, verts=12, cap=True):
    A, B = U(*a), U(*b)
    d = B - A
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d.length, location=(A + B) / 2,
                                        end_fill_type="NGON" if cap else "NOTHING")
    o = bpy.context.active_object
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return _finish(o, mat, smooth=True)


def cone(center, r_bottom, r_top, height, mat, verts=24, open_ends=False, thickness=0.0):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r_bottom, radius2=r_top, depth=height, location=U(*center),
                                    end_fill_type="NOTHING" if open_ends else "NGON")
    o = bpy.context.active_object
    if thickness:
        s = o.modifiers.new("Solid", "SOLIDIFY")
        s.thickness = thickness
        apply_mods(o)
    return _finish(o, mat, smooth=True)


def sphere(center, scale, mat, seg=16, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=U(*center))
    o = bpy.context.active_object
    o.scale = S(*scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(o, mat, smooth=True)


def prism(points_u, depth_axis_vec, mat):
    """Polígono plano (coords Unity) extruido 'depth_axis_vec' (Unity)."""
    pts = [U(*p) for p in points_u]
    off = U(*depth_axis_vec)
    verts = pts + [p + off for p in pts]
    n = len(pts)
    faces = [list(range(n))[::-1], list(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append([i, j, n + j, n + i])
    me = bpy.data.meshes.new("prism")
    me.from_pydata(verts, [], faces)
    me.validate()
    o = bpy.data.objects.new("Prism", me)
    bpy.context.collection.objects.link(o)
    activate(o)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return _finish(o, mat)


def cut(obj, center, size):
    bpy.ops.mesh.primitive_cube_add(size=1, location=U(*center))
    c = bpy.context.active_object
    c.scale = S(*size)
    mod = obj.modifiers.new("Cut", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.solver = "EXACT"
    mod.object = c
    apply_mods(obj)
    bpy.data.objects.remove(c, do_unlink=True)


def join(name, objs, origin=(0, 0, 0)):
    objs = [o for o in objs if o]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    bpy.context.scene.cursor.location = U(*origin)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    box_uv(o)
    return o


def box_uv(obj):
    """Proyección de caja en metros (mundo) / TILE del material de cada cara."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.verify()
    mw = obj.matrix_world
    names = [m.name if m else "" for m in me.materials]
    for f in bm.faces:
        tile = TILE.get(names[f.material_index] if f.material_index < len(names) else "", 1.0)
        n = (mw.to_3x3() @ f.normal)
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            p = mw @ loop.vert.co
            if ax == 0:
                u, v = p.y, p.z
            elif ax == 1:
                u, v = p.x, p.z
            else:
                u, v = p.x, p.y
            loop[uv].uv = (u / tile, v / tile)
    bm.to_mesh(me)
    bm.free()


def export(objs, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, filename), use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", use_mesh_modifiers=True)
    print(f"[ENV] Exportado {filename}")


# ──────────────────────────────────────────────────────────────────────────────
# Casa
# ──────────────────────────────────────────────────────────────────────────────
WX, WZ, WH, TH = 12.0, 9.0, 3.25, 0.2       # muros (alto hasta el alero; el cielo raso está a 3.0)
PITCH = math.radians(32)
EAVE_Z = 5.3                                # alero (vuelo 0.7 m)
ROOF_X = 6.6
ROOF_T = 0.14
RIDGE_UNDER = 3.2 + 4.6 * math.tan(PITCH)


def wall_xf(side, t, y, d):
    """Coordenadas locales de muro (t a lo largo, d hacia afuera) → Unity."""
    if side == "N":
        return (t, y, WZ / 2 + d)
    if side == "S":
        return (t, y, -WZ / 2 - d)
    if side == "E":
        return (WX / 2 + d, y, t)
    return (-WX / 2 - d, y, t)


def wall_size(side, st, sy, sd):
    return (st, sy, sd) if side in "NS" else (sd, sy, st)


def wbox(side, t, y, d, st, sy, sd, mat, bevel=0.0):
    return box(wall_xf(side, t, y, d), wall_size(side, st, sy, sd), mat, bevel=bevel)


OPENINGS = {
    # lado: [(t, alféizar, ancho, alto, tipo)]
    "N": [(2.0, 0.0, 1.1, 2.2, "door"), (-3.0, 0.95, 1.2, 1.25, "win"), (-0.2, 0.95, 1.0, 1.25, "win")],
    "S": [(-3.5, 0.95, 1.2, 1.25, "win"), (3.5, 0.95, 1.2, 1.25, "win")],
    "W": [(0.0, 1.15, 1.2, 1.0, "win")],
    "E": [(-1.8, 0.95, 1.2, 1.25, "win"), (2.0, 0.95, 1.2, 1.25, "win")],
}


def build_walls():
    parts = []
    for side in "NSEW":
        if side in "NS":
            w = box(wall_xf(side, 0, WH / 2, 0), (WX + 0.4, WH, TH), "M_Casa_Revestimiento")
        else:
            w = box(wall_xf(side, 0, WH / 2, 0), (TH, WH, WZ - 0.2), "M_Casa_Revestimiento")
        for t, sill, ow, oh, kind in OPENINGS[side]:
            cut(w, wall_xf(side, t, sill + oh / 2, 0), wall_size(side, ow, oh, 0.8))
        # Materiales por cara: exterior = revestimiento, interior = yeso, cantos = moldura.
        w.data.materials.clear()
        for m in ("M_Casa_Revestimiento", "M_Casa_Yeso", "M_Casa_Moldura"):
            w.data.materials.append(material(m))
        mw = w.matrix_world
        for p in w.data.polygons:
            c = mw @ p.center                  # en mundo (el origen del objeto es el centro del muro)
            n = mw.to_3x3() @ p.normal
            cu, nu = (-c.x, c.z, -c.y), (-n.x, n.z, -n.y)
            if side in "NS" and abs(nu[2]) > 0.9:
                p.material_index = 0 if abs(cu[2]) > WZ / 2 else 1
            elif side in "EW" and abs(nu[0]) > 0.9:
                p.material_index = 0 if abs(cu[0]) > WX / 2 else 1
            else:
                p.material_index = 2
        parts.append(w)
    return parts


def build_openings():
    """Marcos, contramarcos, alféizares, vidrios y cortinas."""
    frames, glass, cloth = [], [], []
    M = "M_Casa_Moldura"
    for side, items in OPENINGS.items():
        for t, sill, w, h, kind in items:
            # Jambas y dintel dentro del vano.
            frames.append(wbox(side, t, sill + h - 0.025, 0, w, 0.05, TH + 0.02, M))
            for sx in (-1, 1):
                frames.append(wbox(side, t + sx * (w / 2 - 0.025), sill + h / 2, 0, 0.05, h, TH + 0.02, M))
            # Contramarcos exterior e interior.
            for d in (TH / 2 + 0.015, -TH / 2 - 0.015):
                frames.append(wbox(side, t, sill + h + 0.06, d, w + 0.24, 0.12, 0.03, M, bevel=0.006))
                for sx in (-1, 1):
                    frames.append(wbox(side, t + sx * (w / 2 + 0.06), sill + h / 2 + (0 if kind == "win" else 0.0), d,
                                       0.12, h + (0.0 if kind == "door" else 0.0), 0.03, M, bevel=0.006))
            if kind == "door":
                frames.append(wbox(side, t, 0.01, 0, w, 0.02, TH + 0.04, "M_Casa_Mueble"))   # umbral
                continue
            # Alféizar (sobresale afuera) + repisa interior.
            frames.append(wbox(side, t, sill - 0.03, 0.06, w + 0.16, 0.05, TH + 0.14, M, bevel=0.008))
            frames.append(wbox(side, t, sill - 0.09, TH / 2 + 0.02, w + 0.1, 0.08, 0.03, M))
            # Ventana de guillotina: marco de hojas, travesaño y parteluz.
            frames.append(wbox(side, t, sill + h / 2, 0.02, w - 0.1, 0.05, 0.06, M))
            frames.append(wbox(side, t, sill + h / 2, 0.035, 0.03, h - 0.1, 0.03, M))
            frames.append(wbox(side, t, sill + h * 0.25, 0.035, w - 0.1, 0.025, 0.025, M))
            frames.append(wbox(side, t, sill + h * 0.75, 0.035, w - 0.1, 0.025, 0.025, M))
            glass.append(wbox(side, t, sill + h / 2, 0.02, w - 0.08, h - 0.08, 0.008, "M_Casa_Vidrio"))
            # Cortinas recogidas a los lados + barra.
            for sx in (-1, 1):
                cloth.append(wbox(side, t + sx * (w / 2 + 0.02), sill + h / 2 + 0.12, -TH / 2 - 0.08,
                                  w * 0.32, h + 0.35, 0.03, "M_Casa_Tela", bevel=0.01))
            a = wall_xf(side, t - w / 2 - 0.25, sill + h + 0.22, -TH / 2 - 0.08)
            b = wall_xf(side, t + w / 2 + 0.25, sill + h + 0.22, -TH / 2 - 0.08)
            cloth.append(cyl(a, b, 0.012, "M_Casa_Metal", verts=8))
    return frames, glass, cloth


def build_shell():
    parts = []
    # Piso, cielo raso, cimiento.
    parts.append(box((0, -0.025, 0), (WX - 0.2, 0.05, WZ - 0.2), "M_Casa_Piso"))
    parts.append(box((0, 3.025, 0), (WX - 0.2, 0.05, WZ - 0.2), "M_Casa_TechoInt"))
    for sz in (-1, 1):
        parts.append(box((0, -0.5, sz * (WZ / 2 + 0.05)), (WX + 0.5, 1.0, 0.32), "M_Casa_Concreto"))
    for sx in (-1, 1):
        parts.append(box((sx * (WX / 2 + 0.05), -0.5, 0), (0.32, 1.0, WZ + 0.1), "M_Casa_Concreto"))
    # Zócalos interiores (con hueco en la puerta trasera).
    for side in "NSEW":
        length = (WX if side in "NS" else WZ) - 0.4
        if side == "N":
            for a, b in ((-length / 2, 1.4), (2.6, length / 2)):
                parts.append(wbox(side, (a + b) / 2, 0.06, -TH / 2 - 0.01, b - a, 0.12, 0.02, "M_Casa_Moldura"))
        else:
            parts.append(wbox(side, 0, 0.06, -TH / 2 - 0.01, length, 0.12, 0.02, "M_Casa_Moldura"))
    # Esquineros exteriores.
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(box((sx * (WX / 2 + 0.13), WH / 2 - 0.05, sz * (WZ / 2 + 0.13)), (0.14, WH + 0.1, 0.14),
                             "M_Casa_Moldura", bevel=0.01))
    # Hastiales (triángulos de los extremos E/O).
    apex = RIDGE_UNDER
    for sx in (-1, 1):
        x = sx * (WX / 2 + 0.1)
        tri = [(x, WH, -WZ / 2 - 0.1), (x, WH, WZ / 2 + 0.1), (x, apex, 0)]
        parts.append(prism(tri, (-sx * TH, 0, 0), "M_Casa_Revestimiento"))
        # Ventila del ático.
        parts.append(box((x + sx * 0.01, apex - 1.1, 0), (0.04, 0.5, 0.35), "M_Casa_Moldura", bevel=0.01))
    # Sofito y frisos bajo el alero.
    for sz in (-1, 1):
        parts.append(box((0, 2.95, sz * (WZ / 2 + 0.45)), (2 * ROOF_X, 0.03, 0.7), "M_Casa_Moldura"))
        parts.append(box((0, WH - 0.08, sz * (WZ / 2 + 0.12)), (WX + 0.4, 0.16, 0.04), "M_Casa_Moldura"))
    return parts


def build_roof():
    parts = []
    t = math.tan(PITCH)
    cos = math.cos(PITCH)
    for sz in (-1, 1):
        y_e = RIDGE_UNDER - EAVE_Z * t
        nz, ny = sz * math.sin(PITCH) * ROOF_T, cos * ROOF_T
        pts = [(-ROOF_X, RIDGE_UNDER, 0), (ROOF_X, RIDGE_UNDER, 0), (ROOF_X, y_e, sz * EAVE_Z), (-ROOF_X, y_e, sz * EAVE_Z)]
        if sz < 0:
            pts = pts[::-1]
        parts.append(prism(pts, (0, ny, nz), "M_Casa_Tejas"))
        # Fascia y canaleta.
        parts.append(box((0, y_e - 0.06, sz * (EAVE_Z + 0.02)), (2 * ROOF_X, 0.2, 0.04), "M_Casa_Moldura"))
        parts.append(box((0, y_e - 0.1, sz * (EAVE_Z + 0.1)), (2 * ROOF_X, 0.1, 0.12), "M_Casa_Metal", bevel=0.015))
        # Tablas de remate en los hastiales.
        for sx in (-1, 1):
            parts.append(beam((sx * (ROOF_X + 0.02), RIDGE_UNDER + 0.1, 0), (sx * (ROOF_X + 0.02), y_e + 0.05, sz * (EAVE_Z + 0.03)),
                              0.04, 0.22, "M_Casa_Moldura"))
    parts.append(box((0, RIDGE_UNDER + ROOF_T / cos + 0.02, 0), (2 * ROOF_X + 0.05, 0.08, 0.32), "M_Casa_Tejas", bevel=0.02))
    # Alero sobre la puerta trasera.
    parts.append(beam((2.0, 2.68, WZ / 2 + 0.1), (2.0, 2.48, WZ / 2 + 1.05), 1.8, 0.07, "M_Casa_Tejas"))
    return parts


def build_exterior():
    parts = []
    # Chimenea de ladrillo (hastial oeste).
    top = RIDGE_UNDER + 1.0
    parts.append(box((-WX / 2 - 0.45, (top - 0.9) / 2, -1.2), (0.7, top + 0.9, 0.8), "M_Casa_Ladrillo"))
    parts.append(box((-WX / 2 - 0.45, top + 0.05, -1.2), (0.82, 0.1, 0.92), "M_Casa_Concreto", bevel=0.01))
    parts.append(cyl((-WX / 2 - 0.45, top + 0.1, -1.2), (-WX / 2 - 0.45, top + 0.35, -1.2), 0.1, "M_Casa_Metal"))
    # Bajantes en las esquinas.
    y_e = RIDGE_UNDER - EAVE_Z * math.tan(PITCH)
    for sx in (-1, 1):
        for sz in (-1, 1):
            x, z = sx * (WX / 2 + 0.3), sz * (WZ / 2 + 0.3)
            parts.append(box((x, (y_e - 0.15 - 0.45) / 2, z), (0.08, y_e - 0.15 + 0.45, 0.08), "M_Casa_Metal", bevel=0.01))
            parts.append(beam((x, y_e - 0.15, z), (x, y_e - 0.05, sz * (EAVE_Z + 0.1)), 0.08, 0.08, "M_Casa_Metal"))
    # Porche trasero: plataforma (encima del escalón collider del greybox), escalón y barandas.
    zb = WZ / 2
    parts.append(box((2.0, -0.33, zb + 0.6), (1.6, 0.58, 1.2), "M_Casa_Mueble"))
    for i in range(7):
        parts.append(box((2.0, -0.02, zb + 0.1 + i * 0.16 + 0.07), (1.64, 0.04, 0.15), "M_Casa_Mueble", bevel=0.005))
    parts.append(box((2.0, -0.26, zb + 1.38), (1.4, 0.06, 0.34), "M_Casa_Mueble", bevel=0.006))
    parts.append(box((2.0, -0.45, zb + 1.38), (1.3, 0.32, 0.3), "M_Casa_Mueble"))
    for sx in (1.22, 2.78):
        parts.append(box((sx, 0.45, zb + 1.12), (0.08, 0.95, 0.08), "M_Casa_Moldura", bevel=0.008))
        parts.append(beam((sx, 0.9, zb + 0.15), (sx, 0.9, zb + 1.16), 0.07, 0.05, "M_Casa_Moldura"))
        for k in range(4):
            parts.append(box((sx, 0.45, zb + 0.3 + k * 0.22), (0.03, 0.85, 0.03), "M_Casa_Moldura"))
        # Ménsulas del alero.
        parts.append(beam((sx, 2.0, zb + 0.12), (sx, 2.52, zb + 0.9), 0.06, 0.06, "M_Casa_Moldura"))
    # Puerta principal (sin vano, la casa no se recorre por ahí) + escalones.
    for d in (TH / 2 + 0.03, -TH / 2 - 0.03):
        parts.append(wbox("S", 0, 1.05, d, 1.0, 2.1, 0.05, "M_Casa_PuertaPintada", bevel=0.008))
        for py in (0.55, 1.5):
            parts.append(wbox("S", -0.22, py, d * 1.25, 0.32, 0.7, 0.02, "M_Casa_PuertaPintada", bevel=0.01))
            parts.append(wbox("S", 0.22, py, d * 1.25, 0.32, 0.7, 0.02, "M_Casa_PuertaPintada", bevel=0.01))
        parts.append(cyl(wall_xf("S", 0.38, 1.0, d), wall_xf("S", 0.38, 1.0, d * 1.9), 0.03, "M_Casa_Metal"))
    for sx in (-1, 1):
        parts.append(wbox("S", sx * 0.6, 1.12, TH / 2 + 0.02, 0.12, 2.24, 0.04, "M_Casa_Moldura", bevel=0.006))
    parts.append(wbox("S", 0, 2.28, TH / 2 + 0.02, 1.32, 0.14, 0.05, "M_Casa_Moldura", bevel=0.006))
    parts.append(wbox("S", 0, -0.22, 0.55, 1.6, 0.44, 0.9, "M_Casa_Concreto", bevel=0.01))
    parts.append(wbox("S", 0, -0.42, 1.15, 1.6, 0.3, 0.4, "M_Casa_Concreto", bevel=0.01))
    # Leña apilada junto a la pared este.
    k = 0
    for row in range(4):
        for i in range(7 - row):
            z = 1.0 + i * 0.2 + row * 0.1
            y = -0.25 + row * 0.17
            parts.append(cyl((WX / 2 + 0.35, y, z), (WX / 2 + 0.95, y, z), 0.085 + 0.01 * ((k * 7) % 3), "M_Casa_Lena", verts=9))
            k += 1
    # Botes de basura metálicos junto al porche.
    for i, x in enumerate((4.2, 4.8)):
        parts.append(cyl((x, -0.4, WZ / 2 + 0.55), (x, 0.42, WZ / 2 + 0.55), 0.26, "M_Columpio_Metal", verts=16))
        parts.append(cyl((x, 0.42, WZ / 2 + 0.55), (x, 0.47 + 0.05 * i, WZ / 2 + 0.55 + 0.04 * i), 0.29, "M_Columpio_Metal", verts=16))
    return parts


def build_back_door():
    """Puerta trasera con ventana de 6 vidrios. Origen = bisagra (x = 1.45, z = 4.5) para que gire."""
    hx, zb = 1.45, WZ / 2
    cx = hx + 0.53
    parts, door_glass = [], []
    slab = box((cx, 1.08, zb), (1.04, 2.16, 0.05), "M_Casa_PuertaPintada")
    cut(slab, (cx, 1.55, zb), (0.66, 0.78, 0.2))
    parts.append(slab)
    for dz in (0.03, -0.03):
        parts.append(box((cx, 0.62, zb + dz), (0.72, 0.62, 0.012), "M_Casa_PuertaPintada", bevel=0.008))
        parts.append(box((cx, 1.55, zb + dz * 0.6), (0.7, 0.04, 0.02), "M_Casa_Moldura"))
        for gx in (-0.11, 0.11):
            parts.append(box((cx + gx, 1.55, zb + dz * 0.6), (0.025, 0.78, 0.02), "M_Casa_Moldura"))
        parts.append(cyl((cx + 0.4, 1.0, zb + dz), (cx + 0.4, 1.0, zb + dz * 2.6), 0.032, "M_Casa_Metal"))
    door_glass.append(box((cx, 1.55, zb), (0.68, 0.8, 0.008), "M_Casa_Vidrio"))
    return join("Casa_Puerta", parts + door_glass, origin=(hx, 0, zb))


def build_kitchen():
    k = []
    X0 = -WX / 2 + TH / 2        # cara interior de la pared oeste (-5.9)
    # Gabinetes bajos (hueco para la estufa en z -2.0..-1.3).
    for z0, z1 in ((-2.5, -2.0), (-1.3, 2.5)):
        zc, zl = (z0 + z1) / 2, z1 - z0
        k.append(box((X0 + 0.3, 0.48, zc), (0.6, 0.76, zl), "M_Casa_Gabinete"))
        k.append(box((X0 + 0.27, 0.05, zc), (0.54, 0.1, zl), "M_Casa_Negro"))
        n = max(1, round(zl / 0.5))
        for i in range(n):
            dz = z0 + (i + 0.5) * zl / n
            k.append(box((X0 + 0.61, 0.4, dz), (0.02, 0.5, zl / n - 0.03), "M_Casa_Gabinete", bevel=0.006))
            k.append(box((X0 + 0.61, 0.76, dz), (0.02, 0.14, zl / n - 0.03), "M_Casa_Gabinete", bevel=0.006))
            k.append(cyl((X0 + 0.62, 0.76, dz - 0.06), (X0 + 0.62, 0.76, dz + 0.06), 0.008, "M_Casa_Metal", verts=6))
            k.append(cyl((X0 + 0.62, 0.55, dz + zl / n / 2 - 0.08), (X0 + 0.62, 0.65, dz + zl / n / 2 - 0.08), 0.008, "M_Casa_Metal", verts=6))
        top = box((X0 + 0.33, 0.88, zc), (0.66, 0.04, zl), "M_Casa_Encimera", bevel=0.006)
        if z0 < 0 < z1:
            cut(top, (X0 + 0.32, 0.88, 0), (0.42, 0.2, 0.7))
        k.append(top)
    # Fregadero + grifo.
    k.append(box((X0 + 0.32, 0.8, 0), (0.42, 0.02, 0.7), "M_Casa_Metal"))
    for sx in (-1, 1):
        k.append(box((X0 + 0.32 + sx * 0.2, 0.84, 0), (0.02, 0.1, 0.7), "M_Casa_Metal"))
        k.append(box((X0 + 0.32, 0.84, sx * 0.34), (0.42, 0.1, 0.02), "M_Casa_Metal"))
    k.append(cyl((X0 + 0.08, 0.9, 0), (X0 + 0.08, 1.15, 0), 0.018, "M_Casa_Metal"))
    k.append(cyl((X0 + 0.08, 1.15, 0), (X0 + 0.3, 1.12, 0), 0.015, "M_Casa_Metal"))
    for sz in (-1, 1):
        k.append(cyl((X0 + 0.08, 0.92, sz * 0.12), (X0 + 0.08, 0.98, sz * 0.12), 0.02, "M_Casa_Metal"))
    # Estufa esmaltada.
    k.append(box((X0 + 0.31, 0.45, -1.65), (0.62, 0.9, 0.7), "M_Casa_Esmalte", bevel=0.015))
    k.append(box((X0 + 0.63, 0.45, -1.65), (0.02, 0.5, 0.56), "M_Casa_Negro", bevel=0.01))
    k.append(cyl((X0 + 0.66, 0.74, -1.88), (X0 + 0.66, 0.74, -1.42), 0.012, "M_Casa_Metal", verts=8))
    for bx in (0.18, 0.44):
        for bz in (-1.82, -1.48):
            k.append(cyl((X0 + bx, 0.9, bz), (X0 + bx, 0.92, bz), 0.085, "M_Casa_Negro", verts=16))
    k.append(box((X0 + 0.04, 1.0, -1.65), (0.06, 0.2, 0.7), "M_Casa_Esmalte", bevel=0.01))
    for i in range(4):
        k.append(cyl((X0 + 0.07, 1.0, -1.9 + i * 0.16), (X0 + 0.1, 1.0, -1.9 + i * 0.16), 0.022, "M_Casa_Negro", verts=10))
    k.append(cyl((X0 + 0.44, 0.92, -1.48), (X0 + 0.44, 1.05, -1.48), 0.1, "M_Casa_Metal", verts=16))   # olla
    # Azulejo (salpicadero).
    k.append(box((X0 + 0.005, 1.0, 0), (0.01, 0.22, 5.0), "M_Casa_Azulejo"))
    # Gabinetes altos.
    for z0, z1 in ((-2.5, -0.75), (0.75, 2.5)):
        zc, zl = (z0 + z1) / 2, z1 - z0
        k.append(box((X0 + 0.17, 1.88, zc), (0.34, 0.74, zl), "M_Casa_Gabinete"))
        n = max(1, round(zl / 0.45))
        for i in range(n):
            dz = z0 + (i + 0.5) * zl / n
            k.append(box((X0 + 0.35, 1.88, dz), (0.02, 0.7, zl / n - 0.03), "M_Casa_Gabinete", bevel=0.006))
            k.append(cyl((X0 + 0.37, 1.58, dz + zl / n / 2 - 0.07), (X0 + 0.37, 1.68, dz + zl / n / 2 - 0.07), 0.008, "M_Casa_Metal", verts=6))
    # Repisa con frascos (pared sur).
    k.append(box((-4.0, 1.6, -WZ / 2 + TH / 2 + 0.11), (1.0, 0.03, 0.22), "M_Casa_Mueble", bevel=0.005))
    for i, (x, h, r) in enumerate(((-4.35, 0.16, 0.045), (-4.18, 0.12, 0.04), (-4.0, 0.2, 0.05), (-3.78, 0.14, 0.045), (-3.6, 0.1, 0.04))):
        z = -WZ / 2 + TH / 2 + 0.11
        k.append(cyl((x, 1.615, z), (x, 1.615 + h, z), r, "M_Casa_Vidrio", verts=12))
        k.append(cyl((x, 1.615 + h, z), (x, 1.635 + h, z), r * 0.85, "M_Casa_Metal", verts=12))
        k.append(cyl((x, 1.62, z), (x, 1.62 + h * 0.6, z), r * 0.8, ("M_Casa_Lena", "M_Casa_Alfombra", "M_Casa_Encimera")[i % 3], verts=10))
    # Mesa (centro = c + (-1.5, 0, 0.5) en el greybox).
    T = (-1.5, 0.0, 0.5)
    k.append(box((T[0], 0.725, T[2]), (1.8, 0.05, 1.0), "M_Casa_Mueble", bevel=0.008))
    for sx in (-1, 1):
        for sz in (-1, 1):
            k.append(box((T[0] + sx * 0.8, 0.35, T[2] + sz * 0.42), (0.06, 0.7, 0.06), "M_Casa_Mueble", bevel=0.006))
        k.append(box((T[0] + sx * 0.8, 0.64, T[2]), (0.025, 0.08, 0.8), "M_Casa_Mueble"))
    for sz in (-1, 1):
        k.append(box((T[0], 0.64, T[2] + sz * 0.42), (1.55, 0.08, 0.025), "M_Casa_Mueble"))
    # Mantel con caída.
    k.append(box((T[0], 0.755, T[2]), (1.92, 0.012, 1.12), "M_Mantel"))
    for sz in (-1, 1):
        k.append(box((T[0], 0.64, T[2] + sz * 0.56), (1.92, 0.24, 0.008), "M_Mantel"))
    for sx in (-1, 1):
        k.append(box((T[0] + sx * 0.96, 0.64, T[2]), (0.008, 0.24, 1.12), "M_Mantel"))
    # Platos, vasos y olla de comida (la cena está servida).
    for i in range(4):
        px, pz = T[0] + (-0.5 if i % 2 == 0 else 0.5), T[2] + (-0.28 if i < 2 else 0.28)
        k.append(cone((px, 0.772, pz), 0.08, 0.125, 0.022, "M_Casa_Ceramica", verts=24))
        k.append(cyl((px + 0.2, 0.762, pz + (0.08 if i < 2 else -0.08)), (px + 0.2, 0.86, pz + (0.08 if i < 2 else -0.08)), 0.035, "M_Casa_Vidrio", verts=12))
        for f in (-1, 1):
            k.append(box((px + f * 0.17, 0.763, pz), (0.02, 0.004, 0.18), "M_Casa_Metal"))
    k.append(cyl((T[0], 0.762, T[2]), (T[0], 0.9, T[2]), 0.13, "M_Casa_Esmalte", verts=20))
    k.append(cyl((T[0], 0.9, T[2]), (T[0], 0.92, T[2]), 0.135, "M_Casa_Esmalte", verts=20))
    k.append(sphere((T[0], 0.94, T[2]), (0.03, 0.03, 0.03), "M_Casa_Negro", 8, 6))
    # Alfombra.
    k.append(box((T[0], 0.004, T[2]), (2.7, 0.008, 2.0), "M_Casa_Alfombra"))
    # Sillas (mismo lugar que Silla_0..3 del greybox; el giro se copia a sus colliders).
    for i, yaw in enumerate(CHAIR_YAW):
        ox, oz = (-0.6 if i % 2 == 0 else 0.6), (-0.8 if i < 2 else 0.8)
        facing = 0 if i < 2 else 180
        o = (T[0] + ox, 0, T[2] + oz)
        y = facing + yaw
        k.append(local_box(o, y, (0, 0.45, 0), (0.42, 0.04, 0.42), "M_Casa_Mueble", bevel=0.006))
        for lx in (-0.18, 0.18):
            for lz in (-0.18, 0.18):
                k.append(local_box(o, y, (lx, 0.215, lz), (0.035, 0.43, 0.035), "M_Casa_Mueble"))
            k.append(local_box(o, y, (lx, 0.7, -0.19), (0.04, 0.5, 0.04), "M_Casa_Mueble"))
        for sy in (0.62, 0.74, 0.86):
            k.append(local_box(o, y, (0, sy, -0.19), (0.36, 0.05, 0.02), "M_Casa_Mueble", bevel=0.004))
        k.append(local_box(o, y, (0, 0.95, -0.19), (0.42, 0.06, 0.035), "M_Casa_Mueble", bevel=0.008))
        k.append(local_box(o, y, (0, 0.12, 0), (0.36, 0.025, 0.025), "M_Casa_Mueble"))
    # Decoración: reloj, cuadros, perchero con chaqueta, calendario.
    zN = WZ / 2 - TH / 2
    k.append(cyl((-1.5, 2.2, zN), (-1.5, 2.2, zN - 0.045), 0.17, "M_Casa_Mueble", verts=24))
    k.append(cyl((-1.5, 2.2, zN - 0.045), (-1.5, 2.2, zN - 0.05), 0.15, "M_Casa_Ceramica", verts=24))
    k.append(box((-1.5, 2.25, zN - 0.055), (0.012, 0.1, 0.004), "M_Casa_Negro"))
    k.append(box((-1.46, 2.2, zN - 0.055), (0.08, 0.01, 0.004), "M_Casa_Negro"))
    xE = WX / 2 - TH / 2
    for z, y, w, h in ((-1.0, 1.75, 0.5, 0.38), (0.5, 1.65, 0.36, 0.46), (3.0, 1.7, 0.6, 0.42)):
        k.append(box((xE - 0.015, y, z), (0.03, h, w), "M_Casa_Mueble", bevel=0.006))
        k.append(box((xE - 0.032, y, z), (0.005, h - 0.08, w - 0.08), "M_Casa_Cuadro"))
    k.append(box((3.6, 1.75, zN - 0.02), (0.7, 0.09, 0.03), "M_Casa_Mueble", bevel=0.006))
    for i in range(4):
        k.append(cyl((3.33 + i * 0.18, 1.75, zN - 0.035), (3.33 + i * 0.18, 1.78, zN - 0.11), 0.01, "M_Casa_Metal", verts=6))
    k.append(sphere((3.45, 1.4, zN - 0.12), (0.22, 0.42, 0.08), "M_Casa_Alfombra", 12, 8))
    k.append(box((X0 + 0.005, 1.55, -3.5), (0.005, 0.4, 0.3), "M_Casa_Ceramica"))
    return k


CHAIR_YAW = [-9.0, 6.0, 11.0, -5.0]


def build_fridge():
    X0 = -WX / 2 + TH / 2
    p = [box((X0 + 0.36, 0.88, -3.4), (0.7, 1.76, 0.76), "M_Casa_Esmalte", bevel=0.05)]
    p.append(box((X0 + 0.72, 1.25, -3.4), (0.01, 0.006, 0.7), "M_Casa_Negro"))
    for y0, y1 in ((0.55, 1.1), (1.35, 1.6)):
        p.append(cyl((X0 + 0.76, y0, -3.1), (X0 + 0.76, y1, -3.1), 0.015, "M_Casa_Metal", verts=8))
    return join("Casa_Refri_Solid", p)


def build_lamp():
    T = (-1.5, 0.5)
    p = [cyl((T[0], 3.0, T[1]), (T[0], 2.72, T[1]), 0.006, "M_Casa_Negro", verts=6),
         cyl((T[0], 3.0, T[1]), (T[0], 2.99, T[1]), 0.05, "M_Casa_Lampara", verts=12),
         cone((T[0], 2.61, T[1]), 0.24, 0.05, 0.22, "M_Casa_Lampara", verts=28, open_ends=True, thickness=0.008),
         sphere((T[0], 2.56, T[1]), (0.055, 0.07, 0.055), "M_Foco", 12, 8)]
    return join("Casa_Lampara", p)


def build_house():
    shell = build_walls() + build_shell()
    frames, glass, cloth = build_openings()
    objs = [
        join("Casa_Estructura", shell + frames + cloth),
        join("Casa_Techo", build_roof()),
        join("Casa_Exterior", build_exterior()),
        join("Casa_Vidrio", glass),
        join("Casa_Cocina", build_kitchen()),
        build_fridge(),
        build_lamp(),
        build_back_door(),
    ]
    return objs


# ──────────────────────────────────────────────────────────────────────────────
# Columpio (glider de 3 asientos, metal pintado oxidado + tablillas de madera)
# ──────────────────────────────────────────────────────────────────────────────
def build_swing():
    M, Wd = "M_Columpio_Metal", "M_Columpio_Madera"
    top, half, spread = 2.3, 1.4, 0.57
    frame = [cyl((-half - 0.15, top, 0), (half + 0.15, top, 0), 0.035, M)]
    for sx in (-half, half):
        for sz in (-1, 1):
            frame.append(cyl((sx, top + 0.03, 0), (sx, 0.0, sz * spread), 0.03, M))
            frame.append(box((sx, 0.02, sz * spread), (0.08, 0.04, 0.12), M, bevel=0.01))
        frame.append(cyl((sx, 0.55, -spread * 0.76), (sx, 0.55, spread * 0.76), 0.02, M))
        frame.append(sphere((sx, top, 0), (0.05, 0.05, 0.05), M, 10, 6))
    for sz in (-1, 1):
        frame.append(cyl((-half, 0.12, sz * spread * 0.95), (half, 0.12, sz * spread * 0.95), 0.022, M))
    marco = join("Columpio_Marco", frame)

    seat = []
    for hx in (-1.1, 1.1):
        seat.append(cyl((hx, top, 0), (hx, 0.62, 0.0), 0.012, M, verts=8))
        seat.append(cyl((hx - 0.03, top - 0.02, 0), (hx + 0.03, top - 0.02, 0), 0.04, M, verts=10))   # abrazadera
    for z in (-0.27, 0.27):
        seat.append(cyl((-1.18, 0.55, z), (1.18, 0.55, z), 0.018, M))
    for x in (-1.18, 0.0, 1.18):
        seat.append(cyl((x, 0.55, -0.27), (x, 0.55, 0.27), 0.016, M))
    for i in range(6):
        z = -0.24 + i * 0.096
        seat.append(box((0, 0.58, z), (2.3, 0.025, 0.085), Wd, bevel=0.006))
    for x in (-1.18, -0.4, 0.4, 1.18):
        seat.append(cyl((x, 0.55, -0.27), (x, 1.1, -0.42), 0.015, M))
    for y in (0.68, 0.82, 0.96):
        seat.append(box((0, y, -0.3 - (y - 0.55) * 0.27), (2.3, 0.09, 0.025), Wd, bevel=0.006))
    for sx in (-1, 1):
        x = sx * 1.2
        seat.append(cyl((x, 0.55, 0.27), (x, 0.8, 0.22), 0.015, M))
        seat.append(cyl((x, 0.8, 0.24), (x, 0.8, -0.32), 0.016, M))
        seat.append(box((x, 0.82, -0.04), (0.07, 0.02, 0.56), Wd, bevel=0.006))
    asiento = join("Columpio_Asiento", seat, origin=(0, top, 0))
    return [marco, asiento]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    house = build_house()
    export(house, "Casa.fbx")
    swing = build_swing()
    for o in swing:
        o.location.x -= 20       # separado de la casa en la .blend (Blender x = -Unity x)
    export_offset(swing, "Columpio.fbx", 20)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print(f"[ENV] Guardado {BLEND}")


def export_offset(objs, filename, dx):
    for o in objs:
        o.location.x += dx
    export(objs, filename)
    for o in objs:
        o.location.x -= dx


main()
