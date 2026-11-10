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
    "M_Prop_Galvanizado": 0.8, "M_Prop_SacoVerde": 0.6, "M_Prop_SacoRojo": 0.6, "M_Prop_Almohada": 0.5,
    "M_Prop_Mochila": 0.5, "M_Cobertizo_Madera": 1.2,
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
    "M_Prop_Plastico": (0.03, 0.03, 0.035), "M_Prop_Galvanizado": (0.55, 0.56, 0.55), "M_Prop_SacoVerde": (0.2, 0.32, 0.22),
    "M_Prop_SacoRojo": (0.45, 0.12, 0.1), "M_Prop_Almohada": (0.85, 0.85, 0.8), "M_Prop_Pelo": (0.12, 0.08, 0.05),
    "M_Prop_Celular": (0.02, 0.02, 0.02), "M_Prop_Pantalla": (0.6, 0.75, 1.0),
    "M_Auto_Pintura": (0.85, 0.82, 0.74), "M_Auto_Rojo": (0.45, 0.08, 0.07), "M_Auto_Vidrio": (0.05, 0.06, 0.07),
    "M_Auto_Llanta": (0.05, 0.05, 0.05), "M_Auto_Luz": (0.6, 0.05, 0.04), "M_Prop_Papel": (0.92, 0.9, 0.8),
    "M_Prop_Mochila": (0.25, 0.35, 0.6), "M_Prop_Linterna": (0.85, 0.6, 0.1), "M_Prop_Collar": (0.5, 0.1, 0.08),
    "M_Cobertizo_Madera": (0.5, 0.42, 0.33), "M_Prop_Bidon": (0.6, 0.08, 0.05),
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


def torus(center, axis, r_major, r_minor, mat, seg=24, minor_seg=8):
    """Anillo en coords Unity; axis = eje del anillo (Unity)."""
    bpy.ops.mesh.primitive_torus_add(major_segments=seg, minor_segments=minor_seg, major_radius=r_major,
                                     minor_radius=r_minor, location=U(*center))
    o = bpy.context.active_object
    o.rotation_euler = U(*axis).to_track_quat("Z", "Y").to_euler()
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
    # (El bote de basura ahora es Casa_Tacho, un objeto aparte: se usa en una tarea.)
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


VAJILLA = []


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
    # (Separados en VAJILLA: el jugador pone la mesa.)
    for i in range(4):
        px, pz = T[0] + (-0.5 if i % 2 == 0 else 0.5), T[2] + (-0.28 if i < 2 else 0.28)
        VAJILLA.append(cone((px, 0.772, pz), 0.08, 0.125, 0.022, "M_Casa_Ceramica", verts=24))
        VAJILLA.append(cyl((px + 0.2, 0.762, pz + (0.08 if i < 2 else -0.08)), (px + 0.2, 0.86, pz + (0.08 if i < 2 else -0.08)), 0.035, "M_Casa_Vidrio", verts=12))
        for f in (-1, 1):
            VAJILLA.append(box((px + f * 0.17, 0.763, pz), (0.02, 0.004, 0.18), "M_Casa_Metal"))
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


# ──────────────────────────────────────────────────────────────────────────────
# Props de las tareas y de la noche (se activan/mueven desde Unity)
# ──────────────────────────────────────────────────────────────────────────────
def lumpy(center, scale, mat, seed, amount=0.08, seg=14, rings=10):
    """Esfera deformada (bolsa, almohada): ruido suave en los vértices."""
    import random
    o = sphere(center, scale, mat, seg, rings)
    rnd = random.Random(seed)
    for v in o.data.vertices:
        k = 1.0 + rnd.uniform(-amount, amount)
        v.co = v.co * k
    return o


def build_props():
    X0 = -WX / 2 + TH / 2
    objs = [join("Casa_Vajilla", VAJILLA)]

    # Pila de platos y vasos en la mesada.
    pila = []
    for i in range(4):
        pila.append(cone((X0 + 0.34, 0.912 + i * 0.024, 1.75), 0.08, 0.125, 0.022, "M_Casa_Ceramica", verts=24))
    for i in range(4):
        pila.append(cyl((X0 + 0.22 + (i % 2) * 0.09, 0.9, 2.08 + (i // 2) * 0.09),
                        (X0 + 0.22 + (i % 2) * 0.09, 1.0, 2.08 + (i // 2) * 0.09), 0.035, "M_Casa_Vidrio", verts=12))
    for i in range(8):
        pila.append(box((X0 + 0.5, 0.905 + i * 0.004, 1.98 + (i % 2) * 0.012), (0.02, 0.004, 0.18), "M_Casa_Metal"))
    objs.append(join("Casa_PilaPlatos", pila))

    # Bolsa de basura (origen en la base: se mueve al tacho).
    bx, bz = -5.2, 3.7
    bolsa = [lumpy((bx, 0.3, bz), (0.27, 0.32, 0.25), "M_Prop_Plastico", 3),
             lumpy((bx, 0.62, bz), (0.07, 0.06, 0.07), "M_Prop_Plastico", 4, 0.15, 8, 6)]
    for sx in (-1, 1):
        bolsa.append(lumpy((bx + sx * 0.06, 0.7, bz), (0.05, 0.035, 0.03), "M_Prop_Plastico", 5 + sx, 0.2, 8, 6))
    objs.append(join("Casa_Bolsa", bolsa, origin=(bx, 0, bz)))

    # Tacho de basura galvanizado (afuera, esquina NE; origen en la base para apoyarlo en el terreno).
    tx, tz = 4.5, WZ / 2 + 0.6
    tacho = [cone((tx, 0.39, tz), 0.27, 0.31, 0.78, "M_Prop_Galvanizado", verts=28, open_ends=True, thickness=0.01),
             cyl((tx, 0.0, tz), (tx, 0.012, tz), 0.27, "M_Prop_Galvanizado", verts=28)]
    for y in (0.12, 0.4, 0.66):
        r = 0.27 + 0.04 * y / 0.78
        tacho.append(cyl((tx, y, tz), (tx, y + 0.025, tz), r + 0.012, "M_Prop_Galvanizado", verts=28))
    for sx in (-1, 1):
        tacho.append(box((tx + sx * 0.32, 0.6, tz), (0.03, 0.03, 0.14), "M_Prop_Galvanizado"))
    tacho.append(cone((tx, 0.81, tz), 0.335, 0.3, 0.06, "M_Prop_Galvanizado", verts=28))
    tacho.append(cyl((tx, 0.86, tz - 0.06), (tx, 0.86, tz + 0.06), 0.015, "M_Prop_Galvanizado", verts=8))
    objs.append(join("Casa_Tacho", tacho, origin=(tx, 0, tz)))

    # Cortina cerrada (ventana este junto a los sacos de dormir).
    t, sill, w, h = -1.8, 0.95, 1.2, 1.25
    folds = []
    n = 9
    for i in range(n):
        tt = t - (w + 0.3) / 2 + (i + 0.5) * (w + 0.3) / n
        d = -TH / 2 - (0.1 if i % 2 == 0 else 0.14)
        folds.append(wbox("E", tt, sill + h / 2 + 0.1, d, (w + 0.3) / n + 0.03, h + 0.4, 0.025, "M_Casa_Tela"))
    objs.append(join("Casa_CortinaCerrada", folds))

    # Puerta interior (al pasillo de los cuartos; no se abre).
    pi = [wbox("S", 0.0, 1.03, -TH / 2 - 0.025, 0.86, 2.04, 0.045, "M_Casa_PuertaPintada", bevel=0.006)]
    for y0, hh in ((0.25, 0.75), (1.15, 0.75)):
        for tt in (-0.2, 0.2):
            pi.append(wbox("S", tt, y0 + hh / 2, -TH / 2 - 0.05, 0.3, hh, 0.012, "M_Casa_PuertaPintada", bevel=0.006))
    for tt in (-0.48, 0.48):
        pi.append(wbox("S", tt, 1.07, -TH / 2 - 0.03, 0.08, 2.14, 0.05, "M_Casa_Moldura"))
    pi.append(wbox("S", 0.0, 2.17, -TH / 2 - 0.03, 1.04, 0.08, 0.05, "M_Casa_Moldura"))
    pi.append(sphere(wall_xf("S", 0.33, 1.0, -TH / 2 - 0.09), (0.03, 0.03, 0.03), "M_Casa_Metal", 10, 6))
    objs.append(join("Casa_PuertaInterior", pi))

    # Interruptor de luz junto a la puerta trasera.
    sw = [wbox("N", 2.85, 1.3, -TH / 2 - 0.005, 0.08, 0.12, 0.01, "M_Casa_Esmalte", bevel=0.003),
          wbox("N", 2.85, 1.31, -TH / 2 - 0.015, 0.015, 0.03, 0.012, "M_Casa_Esmalte")]
    objs.append(join("Casa_Interruptor", sw))

    # Cerrojo (perilla en la cara interior de la puerta; se cuelga de la bisagra en Unity).
    hx, zb = 1.45, WZ / 2
    cx, cy, cz = hx + 0.53 + 0.4, 1.3, zb - 0.03
    cer = [cyl((cx, cy, cz), (cx, cy, cz - 0.01), 0.032, "M_Casa_Metal", verts=16),
           box((cx, cy, cz - 0.02), (0.014, 0.055, 0.02), "M_Casa_Metal", bevel=0.003)]
    objs.append(join("Casa_Cerrojo", cer, origin=(cx, cy, cz)))

    # Sacos de dormir (mitad este de la casa; solo de noche).
    def saco(name, cx, cz, mat, occupied):
        p = [box((cx, 0.05, cz), (1.95, 0.1 if not occupied else 0.08, 0.78), mat, bevel=0.04)]
        p.append(lumpy((cx + 1.0, 0.09, cz), (0.16, 0.07, 0.3), "M_Prop_Almohada", 11 + int(cz * 10), 0.05))
        if occupied:
            p.append(lumpy((cx - 0.05, 0.2, cz), (0.8, 0.16, 0.3), mat, 21, 0.06, 18, 10))      # cuerpo
            p.append(lumpy((cx - 0.25, 0.32, cz + 0.08), (0.28, 0.1, 0.18), mat, 22, 0.08))        # rodillas
            p.append(lumpy((cx + 0.9, 0.24, cz - 0.02), (0.11, 0.1, 0.1), "M_Prop_Pelo", 23, 0.12))  # cabeza (de espaldas)
        else:
            p.append(box((cx - 0.2, 0.12, cz), (1.5, 0.03, 0.78), mat, bevel=0.03))               # abierto
        return join(name, p)
    objs.append(saco("Casa_SacoAlex", 3.25, -0.55, "M_Prop_SacoVerde", False))
    objs.append(saco("Casa_SacoSam", 3.25, -3.0, "M_Prop_SacoRojo", True))

    # Celular de Sam junto a su almohada (la pantalla se enciende cuando te escribe).
    px, pz = 4.05, -2.42
    objs.append(join("Casa_CelularSam", [box((px, 0.012, pz), (0.075, 0.009, 0.155), "M_Prop_Celular", bevel=0.004)]))
    objs.append(join("Casa_PantallaSam", [box((px, 0.0172, pz), (0.066, 0.001, 0.142), "M_Prop_Pantalla")]))
    return objs



# ──────────────────────────────────────────────────────────────────────────────
# Llegada, apagón y mañana: camionetas, mochila, linterna, nota, cobertizo, collar
# ──────────────────────────────────────────────────────────────────────────────
def pickup(name, cx, cz, south, paint):
    """Camioneta pickup (cabina adelante, caja atrás). Origen = centro en el suelo. south: la trompa mira a -z."""
    f = -1 if south else 1                         # signo de "adelante" en z
    def P(x, y, z):
        return (cx + x, y, cz + f * z)
    def B(c, size, mat, bevel=0.0):
        return box(P(*c), size, mat, bevel=bevel)
    parts = []
    parts.append(B((0, 0.7, 0), (1.8, 0.55, 5.0), paint, 0.05))                       # carrocería baja
    parts.append(B((0, 1.04, 1.75), (1.74, 0.16, 1.5), paint, 0.04))                  # capó
    # Cabina (perfil con parabrisas inclinado) + vidrios.
    prof = [(1.0, 0.97), (0.45, 1.7), (-0.6, 1.7), (-0.6, 0.97)]
    pts = [P(-0.85, y, z) for z, y in prof]
    if not south:
        pts = pts[::-1]
    parts.append(prism(pts, (1.7, 0, 0), paint))
    gprof = [(0.93, 1.05), (0.5, 1.62), (-0.62, 1.62), (-0.62, 1.05)]
    gpts = [P(-0.86, y, z) for z, y in gprof]
    if not south:
        gpts = gpts[::-1]
    parts.append(prism(gpts, (1.72, 0, 0), "M_Auto_Vidrio"))
    for z in (0.15,):                                                                  # parante central
        parts.append(B((0, 1.33, z), (1.74, 0.6, 0.08), paint))
    # Caja de carga (paredes bajas).
    for sx in (-1, 1):
        parts.append(B((sx * 0.87, 1.2, -1.55), (0.06, 0.45, 1.85), paint, 0.01))
    parts.append(B((0, 1.2, -2.47), (1.8, 0.45, 0.06), paint, 0.01))                  # compuerta
    parts.append(B((0, 0.99, -1.55), (1.68, 0.02, 1.8), "M_Casa_Negro"))              # piso de la caja
    # Ruedas.
    for sx in (-1, 1):
        for z in (1.6, -1.55):
            parts.append(cyl(P(sx * 0.8, 0.38, z), P(sx * 1.0, 0.38, z), 0.38, "M_Auto_Llanta", verts=18))
            parts.append(cyl(P(sx * 0.98, 0.38, z), P(sx * 1.01, 0.38, z), 0.2, "M_Casa_Metal", verts=14))
    # Paragolpes, parrilla, faros, calaveras, espejos, manijas.
    for z in (2.55, -2.55):
        parts.append(B((0, 0.55, z), (1.86, 0.18, 0.15), "M_Casa_Metal", 0.02))
    parts.append(B((0, 0.82, 2.51), (1.0, 0.25, 0.02), "M_Casa_Negro"))
    for sx in (-1, 1):
        parts.append(B((sx * 0.62, 0.85, 2.505), (0.3, 0.15, 0.03), "M_Casa_Esmalte"))
        parts.append(B((sx * 0.8, 1.15, -2.505), (0.12, 0.25, 0.03), "M_Auto_Luz"))
        parts.append(B((sx * 0.98, 1.3, 0.85), (0.14, 0.12, 0.05), "M_Casa_Negro"))
        parts.append(B((sx * 0.905, 1.0, -0.2), (0.02, 0.03, 0.14), "M_Casa_Metal"))
        parts.append(B((sx * 0.902, 0.75, 0.97), (0.01, 0.5, 0.01), "M_Casa_Negro"))   # línea de puerta
    return join(name, parts, origin=(cx, 0, cz))


def build_arrival_props():
    objs = []
    objs.append(pickup("Casa_Camioneta", 11.5, -3.5, True, "M_Auto_Pintura"))
    objs.append(pickup("Casa_CamionetaPapa", 7.5, -12.5, False, "M_Auto_Rojo"))

    # Mochila (origen en la base; empieza en la caja de la camioneta de Tammy).
    mx, my, mz = 11.75, 1.0, -3.5 + 1.75
    m = [lumpy((mx, my + 0.24, mz), (0.17, 0.24, 0.11), "M_Prop_Mochila", 41, 0.05),
         lumpy((mx, my + 0.16, mz + 0.1), (0.13, 0.1, 0.06), "M_Prop_Mochila", 42, 0.06),
         lumpy((mx, my + 0.47, mz - 0.02), (0.06, 0.03, 0.05), "M_Prop_Mochila", 43, 0.1, 8, 6)]
    for sx in (-1, 1):
        m.append(box((mx + sx * 0.08, my + 0.26, mz - 0.11), (0.035, 0.4, 0.015), "M_Casa_Negro"))
    objs.append(join("Casa_Mochila", m, origin=(mx, my, mz)))

    # Linterna en la mesada (junto al fregadero).
    X0 = -WX / 2 + TH / 2
    lx, lz = X0 + 0.3, 1.05
    lin = [cyl((lx, 0.925, lz - 0.1), (lx, 0.925, lz + 0.07), 0.022, "M_Prop_Linterna", verts=12),
           cyl((lx, 0.925, lz + 0.07), (lx, 0.925, lz + 0.13), 0.035, "M_Prop_Linterna", verts=14),
           cyl((lx, 0.925, lz + 0.13), (lx, 0.925, lz + 0.135), 0.03, "M_Casa_Esmalte", verts=14),
           box((lx, 0.95, lz - 0.02), (0.012, 0.01, 0.03), "M_Casa_Negro")]
    objs.append(join("Casa_Linterna", lin, origin=(lx, 0.925, lz)))

    # Nota pegada en la puerta del refri.
    objs.append(join("Casa_NotaRefri", [box((X0 + 0.745, 1.38, -3.25), (0.004, 0.2, 0.15), "M_Prop_Papel"),
                                        box((X0 + 0.75, 1.47, -3.25), (0.01, 0.025, 0.025), "M_Auto_Luz")]))

    # Saco de Sam vacío (la mañana siguiente).
    p = [box((3.25, 0.05, -3.0), (1.95, 0.08, 0.78), "M_Prop_SacoRojo", bevel=0.04),
         lumpy((3.25 + 1.0, 0.09, -3.0), (0.16, 0.07, 0.3), "M_Prop_Almohada", 12, 0.05),
         box((3.0, 0.11, -2.85), (1.2, 0.04, 0.55), "M_Prop_SacoRojo", yaw=14, bevel=0.03)]
    objs.append(join("Casa_SacoSamVacio", p))

    # Collar del perro de los vecinos (en el campo, al final de las huellas).
    kx, kz = -8.0, 26.0
    col = [torus((kx, 0.03, kz), (0, 1, 0.25), 0.11, 0.014, "M_Prop_Collar"),
           cyl((kx + 0.12, 0.02, kz + 0.03), (kx + 0.125, 0.02, kz + 0.03), 0.025, "M_Casa_Metal", verts=12)]
    objs.append(join("Casa_Collar", col, origin=(kx, 0, kz)))

    objs += build_shed()
    return objs


SHED = (-11.5, 8.0)      # centro del piso del cobertizo (local de la casa)
SHED_W, SHED_D, SHED_H = 2.6, 2.2, 2.1


def build_shed():
    sx, sz = SHED
    W, D, H, T = SHED_W, SHED_D, SHED_H, 0.08
    Mw = "M_Cobertizo_Madera"
    parts = []
    parts.append(box((sx, 0.04, sz), (W, 0.08, D), Mw))                                           # piso
    parts.append(box((sx - W / 2 + T / 2, H / 2, sz), (T, H, D), Mw))                              # oeste
    for k in (-1, 1):
        parts.append(box((sx, H / 2, sz + k * (D / 2 - T / 2)), (W, H, T), Mw))                    # norte / sur
    east = box((sx + W / 2 - T / 2, H / 2, sz), (T, H, D), Mw)
    cut(east, (sx + W / 2, 0.95, sz), (0.4, 1.9, 0.95))                                           # vano de la puerta
    parts.append(east)
    # Tablas verticales (relieve) por fuera.
    for i in range(9):
        t = -D / 2 + 0.12 + i * (D - 0.24) / 8
        parts.append(box((sx - W / 2 - 0.005, H / 2, sz + t), (0.015, H, 0.05), Mw))
    # Techo de un agua (más alto hacia la puerta).
    parts.append(beam((sx - W / 2 - 0.25, H + 0.02, sz), (sx + W / 2 + 0.3, H + 0.2, sz), D + 0.4, 0.06, "M_Casa_Tejas"))
    # Paredes norte/sur suben hasta el techo inclinado (sin hueco).
    for k in (-1, 1):
        z = sz + k * (D / 2 - T / 2)
        tri = [(sx - W / 2, H, z - T / 2), (sx + W / 2, H, z - T / 2), (sx + W / 2, H + 0.17, z - T / 2), (sx - W / 2, H + 0.02, z - T / 2)]
        parts.append(prism(tri, (0, 0, T), Mw))
    parts.append(box((sx + W / 2 - T / 2, H + 0.09, sz), (T, 0.18, D), Mw))
    # Puerta abierta contra la pared.
    parts.append(box((sx + W / 2 + 0.47, 0.97, sz - 0.5), (0.9, 1.86, 0.04), Mw, bevel=0.005))
    # Repisas con latas, bidón, pala.
    for y in (0.9, 1.45):
        parts.append(box((sx - 0.2, y, sz + D / 2 - 0.22), (1.6, 0.03, 0.3), Mw))
    for i in range(5):
        x = sx - 0.85 + i * 0.32
        parts.append(cyl((x, 0.915, sz + D / 2 - 0.2), (x, 0.915 + 0.12 + 0.03 * (i % 2), sz + D / 2 - 0.2), 0.055,
                         ("M_Casa_Metal", "M_Prop_Bidon", "M_Prop_Galvanizado")[i % 3], verts=12))
    parts.append(box((sx + 0.6, 0.22, sz + D / 2 - 0.3), (0.3, 0.36, 0.18), "M_Prop_Bidon", bevel=0.02))
    parts.append(cyl((sx + 0.95, 0.08, sz - D / 2 + 0.15), (sx + 1.0, 1.3, sz - D / 2 + 0.12), 0.015, "M_Casa_Mueble"))
    parts.append(box((sx + 0.94, 0.15, sz - D / 2 + 0.15), (0.2, 0.25, 0.03), "M_Casa_Metal"))
    shed = join("Casa_Cobertizo", parts, origin=(sx, 0, sz))

    # Caja de fusibles en la pared del fondo (oeste) + palanca aparte (gira).
    bx = sx - W / 2 + T + 0.05
    caja = join("Casa_CajaFusibles", [box((bx, 1.4, sz + 0.3), (0.1, 0.45, 0.35), "M_Casa_Metal", bevel=0.01),
                                      box((bx + 0.051, 1.4, sz + 0.3), (0.004, 0.36, 0.26), "M_Casa_Negro")],
                origin=(bx, 1.4, sz + 0.3))
    pal = join("Casa_Palanca", [box((bx + 0.09, 1.4 - 0.06, sz + 0.3), (0.04, 0.12, 0.04), "M_Prop_Bidon"),
                                box((bx + 0.12, 1.4 - 0.12, sz + 0.3), (0.06, 0.03, 0.1), "M_Prop_Bidon")],
               origin=(bx + 0.07, 1.4, sz + 0.3))
    return [shed, caja, pal]


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
    ] + build_props() + build_arrival_props()
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
