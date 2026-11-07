"""
Someone's Home · Personajes low-poly estilizados (Blender 5.x, sin texturas externas).

Uso (desde la raíz del proyecto):
    blender -b --factory-startup --python Tools/Blender/build_characters.py

Genera con el modificador Skin (esqueleto de vértices → malla orgánica) + Subdivision:
  - Sam (hermano, 14 años):  poses Sitting / Standing / Running
  - Tammy (madre de Emily):  Standing
  - Emily (amiga):           Standing
  - LaCosa (criatura):       Body + Head (pivote en el cuello) + Jaw (pivote en la bisagra) + Eyes

Exporta FBX (girados 180°: en Blender miran a +Y, en Unity a +Z) a Assets/_Project/Art/Models/Characters/ y guarda Tools/Blender/Characters.blend
para abrirlo y retocar a mano. Convención: Z arriba, el personaje mira hacia -Y (vista Front de Blender).
Los nombres de material (M_Char_*) se remapean en Unity a materiales URP.
"""
import math
import os

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "Models", "Characters")
BLEND = os.path.join(ROOT, "Tools", "Blender", "Characters.blend")

# Colores de vista previa en Blender (en Unity se usan materiales URP propios).
PALETTE = {
    "M_Char_Skin":        (0.80, 0.60, 0.48),
    "M_Char_SkinTammy":   (0.72, 0.53, 0.42),
    "M_Char_HairDark":    (0.10, 0.07, 0.05),
    "M_Char_HairBrown":   (0.30, 0.18, 0.10),
    "M_Char_Eyes":        (0.04, 0.03, 0.03),
    "M_Sam_Hoodie":       (0.16, 0.24, 0.40),
    "M_Char_Jeans":       (0.18, 0.22, 0.32),
    "M_Char_Shoes":       (0.85, 0.85, 0.82),
    "M_Tammy_Sweater":    (0.45, 0.28, 0.42),
    "M_Tammy_Apron":      (0.85, 0.80, 0.68),
    "M_Char_Slacks":      (0.20, 0.18, 0.17),
    "M_Emily_Shirt":      (0.30, 0.50, 0.36),
    "M_Creature":         (0.25, 0.20, 0.16),
    "M_CreatureDark":     (0.08, 0.06, 0.05),
    "M_CreatureEyes":     (0.95, 0.90, 0.65),
}


# ──────────────────────────────────────────────────────────────────────────────
# Utilidades
# ──────────────────────────────────────────────────────────────────────────────
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*PALETTE.get(name, (0.5, 0.5, 0.5)), 1.0)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*PALETTE.get(name, (0.5, 0.5, 0.5)), 1.0)
        bsdf.inputs["Roughness"].default_value = 0.8
    return m


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_modifiers(obj):
    activate(obj)
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (a + ab * t - p).length


def skin_mesh(name, joints, bones, root, subdiv=1, smooth=True):
    """
    joints: {nombre: (Vector posición, (radio_x, radio_y))}
    bones:  [(joint_a, joint_b, material)]  → la malla se pinta según el hueso más cercano.
    """
    names = list(joints)
    idx = {n: i for i, n in enumerate(names)}
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([joints[n][0] for n in names], [(idx[a], idx[b]) for a, b, _ in bones], [])
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.modifiers.new("Skin", "SKIN")
    for i, n in enumerate(names):
        sv = obj.data.skin_vertices[0].data[i]
        sv.radius = joints[n][1]
        sv.use_root = n == root
    if subdiv:
        sub = obj.modifiers.new("Subsurf", "SUBSURF")
        sub.levels = subdiv
        sub.render_levels = subdiv
    apply_modifiers(obj)

    mats = []
    for _, _, m in bones:
        if m not in mats:
            mats.append(m)
    for m in mats:
        obj.data.materials.append(material(m))
    segs = [(joints[a][0], joints[b][0], mats.index(m)) for a, b, m in bones]
    for poly in obj.data.polygons:
        c = poly.center
        poly.material_index = min(segs, key=lambda s: seg_dist(c, s[0], s[1]))[2]
        poly.use_smooth = smooth
    return obj


def ellipsoid(name, center, scale, mat, segments=16, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=center)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material(mat))
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def box(name, center, size, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material(mat))
    bev = obj.modifiers.new("Bevel", "BEVEL")
    bev.width = min(size) * 0.25
    bev.segments = 2
    apply_modifiers(obj)
    return obj


def join(name, objs, origin=(0, 0, 0)):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name
    set_origin(obj, origin)
    return obj


def set_origin(obj, origin):
    bpy.context.scene.cursor.location = Vector(origin)
    activate(obj)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")


def unwrap(obj):
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")


def export(objs, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
        for c in o.children_recursive:
            c.select_set(True)
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, filename),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,       # rotación 0 en Unity (jerarquía plana, ver creature())
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        use_mesh_modifiers=True,
    )
    print(f"[CHAR] Exportado {filename}")


V = Vector


# ──────────────────────────────────────────────────────────────────────────────
# Humanos
# ──────────────────────────────────────────────────────────────────────────────
POSES = {
    # Coordenadas para una persona de ~1.75 m. Z arriba, mira a -Y.
    "Standing": {
        "pelvis": (0, 0, 0.95), "spine": (0, 0.01, 1.16), "chest": (0, 0.01, 1.34), "neck": (0, 0.01, 1.50),
        "shoulder": (0.19, 0.01, 1.43), "elbow": (0.23, 0.03, 1.16), "wrist": (0.25, 0.0, 0.92), "hand": (0.26, -0.01, 0.84),
        "hip": (0.10, 0, 0.93), "knee": (0.11, -0.02, 0.51), "ankle": (0.11, 0.02, 0.09), "toe": (0.11, -0.14, 0.04),
        "head": (0, -0.01, 1.64),
    },
    "Sitting": {
        "pelvis": (0, 0.05, 0.58), "spine": (0, 0.06, 0.78), "chest": (0, 0.05, 0.95), "neck": (0, 0.03, 1.10),
        "shoulder": (0.19, 0.05, 1.03), "elbow": (0.22, -0.06, 0.82), "wrist": (0.20, -0.26, 0.66), "hand": (0.19, -0.33, 0.63),
        "hip": (0.10, 0.03, 0.57), "knee": (0.11, -0.40, 0.58), "ankle": (0.11, -0.44, 0.10), "toe": (0.11, -0.58, 0.04),
        "head": (0, 0.0, 1.24),
    },
    "Running": {
        "pelvis": (0, 0, 0.93), "spine": (0, -0.04, 1.13), "chest": (0, -0.09, 1.29), "neck": (0, -0.13, 1.43),
        "shoulder": (0.19, -0.08, 1.37),
        "hip": (0.10, 0, 0.91), "head": (0, -0.17, 1.56),
    },
}
# Running: piernas y brazos alternados (L adelante / R atrás).
RUN_LIMBS = {
    "L": {"elbow": (0.23, 0.12, 1.12), "wrist": (0.24, 0.02, 0.94), "hand": (0.24, -0.03, 0.89),
          "knee": (0.11, -0.32, 0.62), "ankle": (0.11, -0.28, 0.22), "toe": (0.11, -0.41, 0.18)},
    "R": {"elbow": (-0.23, -0.24, 1.18), "wrist": (-0.22, -0.36, 1.32), "hand": (-0.21, -0.39, 1.38),
          "knee": (-0.11, 0.10, 0.50), "ankle": (-0.11, 0.38, 0.36), "toe": (-0.11, 0.40, 0.24)},
}


def human(name, pose, height, outfit, build=1.0, hair=None, extras=None):
    """
    outfit: dict con materiales de 'top', 'bottom', 'shoes', 'skin'.
    build:  ancho relativo (hombros/caderas).
    hair:   'short' | 'ponytail' | 'bun'
    """
    s = height / 1.75
    P = POSES[pose]

    def pos(key, side=None):
        if pose == "Running" and side and key in RUN_LIMBS[side]:
            x, y, z = RUN_LIMBS[side][key]
        else:
            x, y, z = P[key] if key in P else POSES["Standing"][key]
            if side == "R":
                x = -x
        if key in ("shoulder", "hip", "elbow", "wrist", "hand", "knee", "ankle", "toe"):
            x *= build
        return V((x * s, y * s, z * s))

    r = lambda a, b=None: (a * s * build, (b if b is not None else a) * s * build)
    joints = {
        "pelvis": (pos("pelvis"), r(0.15, 0.11)),
        "spine": (pos("spine"), r(0.14, 0.10)),
        "chest": (pos("chest"), r(0.17, 0.11)),
        "collar": (pos("chest").lerp(pos("neck"), 0.6), r(0.075)),
        "neck": (pos("neck"), r(0.05)),
    }
    bones = [("pelvis", "spine", outfit["top"]), ("spine", "chest", outfit["top"]),
             ("chest", "collar", outfit["top"]), ("collar", "neck", outfit["skin"])]
    for side in ("L", "R"):
        j = lambda k: f"{k}_{side}"
        joints.update({
            j("shoulder"): (pos("shoulder", side), r(0.065)),
            j("elbow"): (pos("elbow", side), r(0.05)),
            j("wrist"): (pos("wrist", side), r(0.038)),
            j("hand"): (pos("hand", side), r(0.045, 0.03)),
            j("hip"): (pos("hip", side), r(0.085)),
            j("knee"): (pos("knee", side), r(0.06)),
            j("ankle"): (pos("ankle", side), r(0.045)),
            j("toe"): (pos("toe", side), r(0.045, 0.035)),
        })
        bones += [
            ("chest", j("shoulder"), outfit["top"]),
            (j("shoulder"), j("elbow"), outfit["top"]),
            (j("elbow"), j("wrist"), outfit["top"]),
            (j("wrist"), j("hand"), outfit["skin"]),
            ("pelvis", j("hip"), outfit["bottom"]),
            (j("hip"), j("knee"), outfit["bottom"]),
            (j("knee"), j("ankle"), outfit["bottom"]),
            (j("ankle"), j("toe"), outfit["shoes"]),
        ]
    body = skin_mesh(f"{name}_{pose}_Body", joints, bones, root="pelvis")

    hp = pos("head")
    parts = [body, ellipsoid(f"{name}_Head", hp, (0.105 * s, 0.12 * s, 0.125 * s), outfit["skin"])]
    # Ojos (estilizados) y nariz.
    for sx in (-1, 1):
        parts.append(ellipsoid(f"{name}_Eye", hp + V((sx * 0.04 * s, -0.105 * s, 0.015 * s)), (0.014 * s,) * 3, "M_Char_Eyes", 8, 6))
    parts.append(ellipsoid(f"{name}_Nose", hp + V((0, -0.12 * s, -0.02 * s)), (0.016 * s, 0.02 * s, 0.025 * s), outfit["skin"], 8, 6))
    for sx in (-1, 1):
        parts.append(ellipsoid(f"{name}_Ear", hp + V((sx * 0.105 * s, 0, 0)), (0.018 * s, 0.03 * s, 0.04 * s), outfit["skin"], 8, 6))

    hair_mat = outfit.get("hair", "M_Char_HairDark")
    if hair:
        parts.append(ellipsoid(f"{name}_Hair", hp + V((0, 0.012 * s, 0.035 * s)), (0.115 * s, 0.13 * s, 0.11 * s), hair_mat))
    if hair == "ponytail":
        tail = skin_mesh(f"{name}_Ponytail", {
            "a": (hp + V((0, 0.11 * s, 0.05 * s)), (0.035 * s, 0.035 * s)),
            "b": (hp + V((0, 0.15 * s, -0.08 * s)), (0.03 * s, 0.03 * s)),
            "c": (hp + V((0, 0.14 * s, -0.2 * s)), (0.012 * s, 0.012 * s)),
        }, [("a", "b", hair_mat), ("b", "c", hair_mat)], root="a")
        parts.append(tail)
    if hair == "bun":
        parts.append(ellipsoid(f"{name}_Bun", hp + V((0, 0.1 * s, 0.08 * s)), (0.055 * s,) * 3, hair_mat))
    if hair == "short":
        # flequillo
        parts.append(ellipsoid(f"{name}_Fringe", hp + V((0, -0.07 * s, 0.07 * s)), (0.1 * s, 0.05 * s, 0.04 * s), hair_mat))

    if outfit.get("hood"):
        parts.append(ellipsoid(f"{name}_Hood", pos("neck") + V((0, 0.08 * s, 0.0)), (0.13 * s, 0.07 * s, 0.07 * s), outfit["top"]))
    for e in (extras or []):
        parts.append(e(s, pos))

    obj = join(f"{name}_{pose}", parts)
    unwrap(obj)
    return obj


def apron(s, pos):
    c = (pos("pelvis") + pos("chest")) * 0.5
    return box("Apron", c + V((0, -0.12 * s, -0.12 * s)), (0.3 * s, 0.02 * s, 0.6 * s), "M_Tammy_Apron")


# ──────────────────────────────────────────────────────────────────────────────
# La criatura: "algo parecido a un venado", de pie en dos patas, ~3 m
# ──────────────────────────────────────────────────────────────────────────────
def creature():
    skin, dark = "M_Creature", "M_CreatureDark"
    J = {
        "pelvis": (V((0, 0.05, 1.80)), (0.16, 0.12)),
        "belly": (V((0, 0.03, 2.08)), (0.13, 0.10)),     # cintura muy delgada
        "chest": (V((0, -0.02, 2.40)), (0.20, 0.14)),
        "neck0": (V((0, -0.06, 2.62)), (0.08, 0.07)),
        "neck1": (V((0, -0.16, 2.80)), (0.06, 0.06)),
        "neck2": (V((0, -0.17, 2.90)), (0.05, 0.05)),   # entra dentro del cráneo: sin hueco
    }
    B = [("pelvis", "belly", skin), ("belly", "chest", skin), ("chest", "neck0", skin), ("neck0", "neck1", skin),
         ("neck1", "neck2", skin)]
    for sx, side in ((1, "L"), (-1, "R")):
        j = lambda k: f"{k}_{side}"
        # Pata trasera digitígrada de venado: rodilla adelante, corvejón atrás, pezuña.
        J.update({
            j("hip"): (V((sx * 0.15, 0.05, 1.76)), (0.10, 0.10)),
            j("stifle"): (V((sx * 0.17, -0.20, 1.30)), (0.07, 0.07)),
            j("hock"): (V((sx * 0.16, 0.24, 0.62)), (0.045, 0.045)),
            j("fetlock"): (V((sx * 0.15, 0.06, 0.12)), (0.035, 0.035)),
            j("hoof"): (V((sx * 0.15, -0.04, 0.02)), (0.045, 0.035)),
            # Brazos demasiado largos, colgando hasta las rodillas, dedos largos.
            j("shoulder"): (V((sx * 0.25, -0.02, 2.48)), (0.06, 0.06)),
            j("elbow"): (V((sx * 0.31, 0.02, 1.95)), (0.04, 0.04)),
            j("wrist"): (V((sx * 0.30, -0.03, 1.42)), (0.03, 0.03)),
        })
        B += [("pelvis", j("hip"), skin), (j("hip"), j("stifle"), skin), (j("stifle"), j("hock"), skin),
              (j("hock"), j("fetlock"), dark), (j("fetlock"), j("hoof"), dark),
              ("chest", j("shoulder"), skin), (j("shoulder"), j("elbow"), skin), (j("elbow"), j("wrist"), dark)]
        for f, (dx, dy) in enumerate(((-0.03, -0.03), (0.0, -0.05), (0.03, -0.03))):
            tip = f"finger{f}_{side}"
            mid = f"fingerm{f}_{side}"
            J[mid] = (V((sx * (0.30 + dx * 0.6), -0.03 + dy * 0.5, 1.26)), (0.012, 0.012))
            J[tip] = (V((sx * (0.30 + dx), -0.03 + dy, 1.08)), (0.007, 0.007))
            B += [(j("wrist"), mid, dark), (mid, tip, dark)]
    body = skin_mesh("LaCosa_Body", J, B, root="pelvis")
    # Costillas marcadas: anillos delgados alrededor del pecho.
    ribs = []
    for k in range(4):
        z = 2.22 + k * 0.07
        ribs.append(ellipsoid(f"Rib{k}", V((0, -0.03, z)), (0.19 - k * 0.005, 0.135, 0.018), skin, 14, 6))
    body = join("LaCosa_Body", [body] + ribs)
    unwrap(body)

    # Cabeza: cráneo de venado, largo y estrecho. Pivote en la punta del cuello.
    neck_top = V((0, -0.16, 2.82))
    skull = skin_mesh("LaCosa_Skull", {
        "back": (V((0, -0.14, 2.90)), (0.085, 0.09)),
        "brow": (V((0, -0.27, 2.93)), (0.08, 0.075)),
        "snout": (V((0, -0.44, 2.87)), (0.05, 0.045)),
        "nose": (V((0, -0.56, 2.84)), (0.035, 0.03)),
    }, [("back", "brow", skin), ("brow", "snout", skin), ("snout", "nose", dark)], root="back")
    head_parts = [skull]
    for sx in (1, -1):
        ear = skin_mesh(f"Ear{sx}", {
            "a": (V((sx * 0.07, -0.18, 2.96)), (0.025, 0.012)),
            "b": (V((sx * 0.22, -0.16, 3.02)), (0.04, 0.012)),
            "c": (V((sx * 0.30, -0.15, 3.03)), (0.01, 0.006)),
        }, [("a", "b", skin), ("b", "c", skin)], root="a")
        antler = skin_mesh(f"Antler{sx}", {
            "base": (V((sx * 0.05, -0.22, 2.99)), (0.022, 0.022)),
            "mid": (V((sx * 0.11, -0.20, 3.18)), (0.016, 0.016)),
            "top": (V((sx * 0.16, -0.14, 3.38)), (0.01, 0.01)),
            "tine1": (V((sx * 0.06, -0.33, 3.30)), (0.008, 0.008)),
            "tine2": (V((sx * 0.25, -0.24, 3.30)), (0.008, 0.008)),
        }, [("base", "mid", dark), ("mid", "top", dark), ("mid", "tine1", dark), ("mid", "tine2", dark)], root="base")
        head_parts += [ear, antler]
    head = join("LaCosa_Head", head_parts, origin=neck_top)
    unwrap(head)

    eyes = join("LaCosa_Eyes", [ellipsoid(f"Eye{sx}", V((sx * 0.058, -0.36, 2.915)), (0.022, 0.018, 0.018), "M_CreatureEyes", 10, 8)
                                for sx in (1, -1)], origin=neck_top)
    hinge = V((0, -0.24, 2.84))
    jaw = skin_mesh("LaCosa_Jaw", {
        "h": (hinge, (0.05, 0.03)),
        "m": (V((0, -0.40, 2.81)), (0.04, 0.025)),
        "t": (V((0, -0.52, 2.80)), (0.028, 0.018)),
    }, [("h", "m", dark), ("m", "t", dark)], root="h")
    set_origin(jaw, hinge)
    unwrap(jaw)

    # Sin jerarquía en el FBX (los parent-inverse de Blender llegan torcidos a Unity):
    # CharacterSetup.SpawnCreature emparenta Jaw y Eyes a Head en Unity.
    return [body, head, eyes, jaw]


# ──────────────────────────────────────────────────────────────────────────────
def main():
    reset()
    sam_outfit = {"top": "M_Sam_Hoodie", "bottom": "M_Char_Jeans", "shoes": "M_Char_Shoes", "skin": "M_Char_Skin",
                  "hair": "M_Char_HairBrown", "hood": True}
    sam = [human("Sam", p, 1.62, sam_outfit, build=0.92, hair="short") for p in ("Sitting", "Standing", "Running")]
    face_unity_forward(sam)
    export(sam, "Sam.fbx")                    # las 3 poses superpuestas en el origen; Unity activa una

    tammy = human("Tammy", "Standing", 1.68, {"top": "M_Tammy_Sweater", "bottom": "M_Char_Slacks", "shoes": "M_Char_HairDark",
                                              "skin": "M_Char_SkinTammy", "hair": "M_Char_HairDark"},
                  build=1.0, hair="bun", extras=[apron])
    face_unity_forward([tammy])
    tammy.location.x = 2.0
    export_at_origin([tammy], "Tammy.fbx", 2.0)

    emily = human("Emily", "Standing", 1.58, {"top": "M_Emily_Shirt", "bottom": "M_Char_Jeans", "shoes": "M_Char_Shoes",
                                              "skin": "M_Char_Skin", "hair": "M_Char_HairBrown"},
                  build=0.88, hair="ponytail")
    face_unity_forward([emily])
    emily.location.x = 3.0
    export_at_origin([emily], "Emily.fbx", 3.0)

    lacosa = creature()
    face_unity_forward(lacosa)
    for o in lacosa:
        o.location.x += 5.0
    export_at_origin(lacosa, "LaCosa.fbx", 5.0)
    for i, o in enumerate(sam):
        o.location.x -= 1.0 - i                # en la .blend, separados para verlos

    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print(f"[CHAR] Guardado {BLEND}")


def face_unity_forward(objs):
    """Gira 180° en Z y lo aplica a la malla: en Unity el frente del personaje queda en +Z (transform.forward)."""
    rot = Matrix.Rotation(math.pi, 4, "Z")
    for o in objs:
        o.matrix_world = rot @ o.matrix_world
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


def export_at_origin(objs, filename, offset_x):
    """Los objetos están separados offset_x en la .blend; se exportan con los pies en el origen."""
    for o in objs:
        o.location.x -= offset_x
    export(objs, filename)
    for o in objs:
        o.location.x += offset_x


main()
