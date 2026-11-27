"""
Someone's Home · Personajes con esqueleto y animaciones (Blender 5.x).

Uso (desde la raíz del proyecto):
    blender -b --factory-startup --python Tools/Blender/build_rigged_characters.py

A diferencia de build_characters.py (poses fijas), aquí cada personaje es UNA malla con esqueleto y clips:
  - Sam:    Idle, Walk, Run, Sit, Shiver, Scream
  - Tammy:  Idle, Walk, Talk
  - Emily:  Idle, Walk, Talk
  - LaCosa: Idle, Walk, Run (+ hueso Jaw y Head para que el juego mueva la boca y la cabeza)

Más detalle que la versión anterior: subdivisión 2, cejas, boca, párpados, pulgar, suelas, cordones de la
capucha, cinturón y pelo en capas. Los pesos se calculan por distancia a cada hueso (robusto con piezas sueltas).

Exporta a Assets/_Project/Resources/Characters/<Nombre>_Rig.fbx (Unity: animación Legacy, la configura
Editor/RiggedCharacterImport.cs). En Blender miran a -Y; FBX con -Z adelante y Y arriba → en Unity miran a +Z.
"""
import math
import os

import bpy
from mathutils import Euler, Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
_src = open(os.path.join(HERE, "build_characters.py"), encoding="utf-8").read().rsplit("\nmain()", 1)[0]
exec(compile(_src, "build_characters.py", "exec"))   # reutiliza skin_mesh, ellipsoid, box, join, material, POSES…

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Characters")
BLEND_RIG = os.path.join(HERE, "CharactersRigged.blend")
FPS = 30


# ──────────────────────────────────────────────────────────────────────────────
# Esqueleto y pesos
# ──────────────────────────────────────────────────────────────────────────────
def make_armature(name, bones):
    """bones: [(nombre, cabeza, cola, padre)] en coordenadas de mundo (Z arriba, mira a -Y)."""
    arm_data = bpy.data.armatures.new(name + "_Armature")
    arm = bpy.data.objects.new(name + "_Armature", arm_data)
    bpy.context.collection.objects.link(arm)
    activate(arm)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    for bname, head, tail, parent in bones:
        b = eb.new(bname)
        b.head = Vector(head)
        b.tail = Vector(tail)
        b.roll = 0.0
        if parent:
            b.parent = eb[parent]
            b.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def weight_mesh(mesh_obj, arm, segments, rigid_groups=()):
    """
    segments: [(hueso, a, b)] segmentos que deforman (un hueso puede tener varios).
    rigid_groups: grupos de vértices ya creados (p. ej. "Head") que quedan 100 % en ese hueso.
    Cada vértice libre va a los 2 huesos más cercanos, con peso ∝ 1/d⁴.
    """
    vg = {}
    for bname in {s[0] for s in segments} | set(rigid_groups):
        vg[bname] = mesh_obj.vertex_groups.get(bname) or mesh_obj.vertex_groups.new(name=bname)
    rigid_idx = {mesh_obj.vertex_groups[g].index for g in rigid_groups if g in mesh_obj.vertex_groups}
    mw = mesh_obj.matrix_world
    for v in mesh_obj.data.vertices:
        if any(g.group in rigid_idx and g.weight > 0.5 for g in v.groups):
            continue
        p = mw @ v.co
        best = {}
        for bname, a, b in segments:
            d = seg_dist(p, Vector(a), Vector(b))
            if bname not in best or d < best[bname]:
                best[bname] = d
        ranked = sorted(best.items(), key=lambda kv: kv[1])
        (b1, d1), (b2, d2) = ranked[0], ranked[1]
        w1 = 1.0 / (d1 ** 4 + 1e-7)
        w2 = 1.0 / (d2 ** 4 + 1e-7) if d2 < d1 * 1.5 + 0.01 else 0.0
        t = w1 + w2
        vg[b1].add([v.index], w1 / t, "REPLACE")
        if w2 > 0:
            vg[b2].add([v.index], w2 / t, "REPLACE")
    mesh_obj.parent = arm
    mod = mesh_obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm


def tag_group(obj, group):
    g = obj.vertex_groups.new(name=group)
    g.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")
    return obj


# ──────────────────────────────────────────────────────────────────────────────
# Animación: rotaciones expresadas en ejes del mundo (X = izquierda del personaje, -Y = adelante, Z = arriba)
#   pierna/brazo hacia adelante: rx < 0 · rodilla: rx > 0 · codo: rx < 0 · inclinar torso adelante: rx > 0
# ──────────────────────────────────────────────────────────────────────────────
def to_local(arm, bname, rot_deg=(0, 0, 0)):
    m = arm.data.bones[bname].matrix_local.to_3x3()
    q = Euler([math.radians(a) for a in rot_deg], "XYZ").to_matrix()
    return (m.inverted() @ q @ m).to_quaternion()


def loc_local(arm, bname, loc):
    m = arm.data.bones[bname].matrix_local.to_3x3()
    return m.inverted() @ Vector(loc)


def bake_action(arm, name, frames, pose_fn, step=2):
    """pose_fn(f) → {hueso: (rx, ry, rz)} y opcional '__loc__': {hueso: (x, y, z)}."""
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    if not arm.animation_data:
        arm.animation_data_create()
    arm.animation_data.action = act
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = frames
    keys = list(range(0, frames + 1, step))
    if keys[-1] != frames:
        keys.append(frames)
    for f in keys:
        pose = pose_fn(f)
        locs = pose.pop("__loc__", {})
        for pb in arm.pose.bones:
            pb.rotation_mode = "QUATERNION"
            pb.rotation_quaternion = to_local(arm, pb.name, pose.get(pb.name, (0, 0, 0)))
            pb.location = loc_local(arm, pb.name, locs.get(pb.name, (0, 0, 0)))
            pb.keyframe_insert("rotation_quaternion", frame=f)
            pb.keyframe_insert("location", frame=f)
    arm.animation_data.action = None
    return act


def ph(f, period):
    return 2 * math.pi * (f % period) / period


# ──────────────────────────────────────────────────────────────────────────────
# Humanos
# ──────────────────────────────────────────────────────────────────────────────
def human_rigged(name, height, outfit, build=1.0, hair=None, extras=None, belt=False):
    s = height / 1.75
    P = POSES["Standing"]

    def pos(key, side=None):
        x, y, z = P[key]
        if side == "R":
            x = -x
        if key in ("shoulder", "hip", "elbow", "wrist", "hand", "knee", "ankle", "toe"):
            x *= build
        if key in ("elbow", "wrist", "hand"):           # brazos un poco abiertos (mejores pesos)
            x += (0.02 if side == "L" else -0.02) * (1 if key == "elbow" else 1.6)
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
        sx = 1 if side == "L" else -1
        hand = pos("hand", side)
        joints.update({
            j("shoulder"): (pos("shoulder", side), r(0.065)),
            j("elbow"): (pos("elbow", side), r(0.05)),
            j("wrist"): (pos("wrist", side), r(0.036)),
            j("hand"): (hand, r(0.045, 0.026)),
            j("fingers"): (hand + V((0, 0, -0.06 * s)), r(0.032, 0.018)),
            j("thumb"): (pos("wrist", side).lerp(hand, 0.6) + V((-sx * 0.0, -0.035 * s, -0.01 * s)), r(0.014)),
            j("hip"): (pos("hip", side), r(0.085)),
            j("knee"): (pos("knee", side), r(0.06)),
            j("ankle"): (pos("ankle", side), r(0.045)),
            j("toe"): (pos("toe", side), r(0.048, 0.036)),
        })
        bones += [
            ("chest", j("shoulder"), outfit["top"]),
            (j("shoulder"), j("elbow"), outfit["top"]),
            (j("elbow"), j("wrist"), outfit["top"]),
            (j("wrist"), j("hand"), outfit["skin"]),
            (j("hand"), j("fingers"), outfit["skin"]),
            (j("wrist"), j("thumb"), outfit["skin"]),
            ("pelvis", j("hip"), outfit["bottom"]),
            (j("hip"), j("knee"), outfit["bottom"]),
            (j("knee"), j("ankle"), outfit["bottom"]),
            (j("ankle"), j("toe"), outfit["shoes"]),
        ]
    body = skin_mesh(f"{name}_Body", joints, bones, root="pelvis", subdiv=2)
    parts = [body]

    # Suelas (más oscuras) y cinturón.
    for side in ("L", "R"):
        a, t = pos("ankle", side), pos("toe", side)
        parts.append(box(f"{name}_Sole{side}", (a + t) * 0.5 + V((0, -0.01 * s, -0.045 * s)),
                         (0.085 * s * build, (t - a).length + 0.09 * s, 0.025 * s), "M_Char_HairDark"))
    if belt:
        parts.append(ellipsoid(f"{name}_Belt", pos("pelvis") + V((0, 0, 0.04 * s)),
                               (0.158 * s * build, 0.118 * s * build, 0.022 * s), "M_Char_HairDark", 20, 6))

    # ── Cabeza (todo rígido al hueso Head) ──
    hp = pos("head")
    skin = outfit["skin"]
    head = [ellipsoid(f"{name}_Head", hp, (0.103 * s, 0.118 * s, 0.128 * s), skin, 24, 16),
            ellipsoid(f"{name}_Jaw", hp + V((0, -0.035 * s, -0.07 * s)), (0.075 * s, 0.075 * s, 0.06 * s), skin, 16, 10)]
    for sx in (-1, 1):
        e = hp + V((sx * 0.04 * s, -0.1 * s, 0.015 * s))
        head.append(ellipsoid(f"{name}_EyeWhite", e, (0.017 * s, 0.01 * s, 0.012 * s), "M_Char_Shoes", 10, 8))
        head.append(ellipsoid(f"{name}_Eye", e + V((0, -0.008 * s, 0)), (0.009 * s,) * 3, "M_Char_Eyes", 8, 6))
        head.append(ellipsoid(f"{name}_Lid", e + V((0, -0.003 * s, 0.009 * s)), (0.02 * s, 0.011 * s, 0.006 * s), skin, 10, 6))
        head.append(box(f"{name}_Brow", e + V((0, -0.004 * s, 0.03 * s)), (0.034 * s, 0.01 * s, 0.008 * s),
                        outfit.get("hair", "M_Char_HairDark"), rot=(0, sx * 0.12, 0)))
        head.append(ellipsoid(f"{name}_Ear", hp + V((sx * 0.102 * s, 0.005 * s, 0)), (0.016 * s, 0.03 * s, 0.04 * s), skin, 10, 8))
    head.append(ellipsoid(f"{name}_Nose", hp + V((0, -0.118 * s, -0.018 * s)), (0.015 * s, 0.022 * s, 0.026 * s), skin, 10, 8))
    head.append(ellipsoid(f"{name}_Mouth", hp + V((0, -0.103 * s, -0.062 * s)), (0.026 * s, 0.006 * s, 0.005 * s), "M_Char_Eyes", 10, 6))

    hair_mat = outfit.get("hair", "M_Char_HairDark")
    if hair:
        head.append(ellipsoid(f"{name}_Hair", hp + V((0, 0.014 * s, 0.035 * s)), (0.112 * s, 0.128 * s, 0.112 * s), hair_mat, 24, 14))
        head.append(ellipsoid(f"{name}_HairBack", hp + V((0, 0.05 * s, -0.035 * s)), (0.1 * s, 0.09 * s, 0.1 * s), hair_mat, 18, 12))
    if hair == "ponytail":
        head.append(skin_mesh(f"{name}_Ponytail", {
            "a": (hp + V((0, 0.11 * s, 0.05 * s)), (0.035 * s, 0.035 * s)),
            "b": (hp + V((0, 0.15 * s, -0.08 * s)), (0.03 * s, 0.03 * s)),
            "c": (hp + V((0, 0.14 * s, -0.2 * s)), (0.012 * s, 0.012 * s)),
        }, [("a", "b", hair_mat), ("b", "c", hair_mat)], root="a"))
    if hair == "bun":
        head.append(ellipsoid(f"{name}_Bun", hp + V((0, 0.1 * s, 0.08 * s)), (0.055 * s,) * 3, hair_mat))
    if hair == "short":
        head.append(ellipsoid(f"{name}_Fringe", hp + V((0, -0.068 * s, 0.075 * s)), (0.1 * s, 0.05 * s, 0.04 * s), hair_mat))
        for sx in (-1, 1):
            head.append(ellipsoid(f"{name}_Side", hp + V((sx * 0.085 * s, 0.0, 0.03 * s)), (0.035 * s, 0.09 * s, 0.07 * s), hair_mat))
    for o in head:
        tag_group(o, "Head")
    parts += head

    if outfit.get("hood"):
        hood = ellipsoid(f"{name}_Hood", pos("neck") + V((0, 0.07 * s, -0.01 * s)), (0.13 * s, 0.075 * s, 0.07 * s), outfit["top"])
        parts.append(hood)
        for sx in (-1, 1):   # cordones
            parts.append(skin_mesh(f"{name}_String{sx}", {
                "a": (pos("neck") + V((sx * 0.035 * s, -0.06 * s, -0.02 * s)), (0.006 * s, 0.006 * s)),
                "b": (pos("chest") + V((sx * 0.04 * s, -0.115 * s, -0.02 * s)), (0.005 * s, 0.005 * s)),
            }, [("a", "b", "M_Char_Shoes")], root="a", subdiv=0))
        parts.append(box(f"{name}_Pocket", pos("spine") + V((0, -0.105 * s, -0.04 * s)), (0.2 * s, 0.02 * s, 0.1 * s), outfit["top"]))
    for e in (extras or []):
        parts.append(e(s, pos))

    mesh = join(f"{name}_Mesh", parts)
    unwrap(mesh)

    # ── Esqueleto ──
    up = V((0, 0, 0.12 * s))
    B = [("Root", (0, 0, 0), (0, 0, 0.2 * s), None),
         ("Hips", pos("pelvis"), pos("spine"), "Root"),
         ("Spine", pos("spine"), pos("chest"), "Hips"),
         ("Chest", pos("chest"), pos("neck"), "Spine"),
         ("Neck", pos("neck"), hp - V((0, 0, 0.05 * s)), "Chest"),
         ("Head", hp - V((0, 0, 0.05 * s)), hp + up, "Neck")]
    for side in ("L", "R"):
        j = lambda k: f"{k}_{side}"
        B += [(f"Clavicle_{side}", pos("chest") + V((0, 0, 0.06 * s)), pos("shoulder", side), "Chest"),
              (f"UpperArm_{side}", pos("shoulder", side), pos("elbow", side), f"Clavicle_{side}"),
              (f"Forearm_{side}", pos("elbow", side), pos("wrist", side), f"UpperArm_{side}"),
              (f"Hand_{side}", pos("wrist", side), joints[j("fingers")][0], f"Forearm_{side}"),
              (f"Thigh_{side}", pos("hip", side), pos("knee", side), "Hips"),
              (f"Shin_{side}", pos("knee", side), pos("ankle", side), f"Thigh_{side}"),
              (f"Foot_{side}", pos("ankle", side), pos("toe", side), f"Shin_{side}")]
    arm = make_armature(name, B)

    seg = [("Hips", pos("pelvis"), pos("spine")), ("Spine", pos("spine"), pos("chest")),
           ("Chest", pos("chest"), joints["collar"][0]), ("Neck", joints["collar"][0], pos("neck"))]
    for side in ("L", "R"):
        j = lambda k: f"{k}_{side}"
        seg += [("Hips", pos("pelvis"), pos("hip", side)),
                ("Chest", pos("chest"), pos("shoulder", side).lerp(pos("chest"), 0.45)),
                (f"UpperArm_{side}", pos("shoulder", side), pos("elbow", side)),
                (f"Forearm_{side}", pos("elbow", side), pos("wrist", side)),
                (f"Hand_{side}", pos("wrist", side), joints[j("fingers")][0]),
                (f"Thigh_{side}", pos("hip", side), pos("knee", side)),
                (f"Shin_{side}", pos("knee", side), pos("ankle", side)),
                (f"Foot_{side}", pos("ankle", side), pos("toe", side) + V((0, -0.05 * s, 0)))]
    weight_mesh(mesh, arm, seg, rigid_groups=("Head",))
    return arm, mesh, s


# Poses de los humanos ---------------------------------------------------------
def h_idle(s, breathe=1.0):
    def f(fr):
        p = ph(fr, 90)
        return {"Spine": (1.2 * breathe * math.sin(p), 0, 0), "Chest": (1.5 * breathe * math.sin(p), 0, 0.8 * math.sin(p * 0.5)),
                "Head": (1.5 * math.sin(p + 1), 0, 4 * math.sin(p * 0.5 + 0.5)),
                "UpperArm_L": (2 * math.sin(p), -3, 0), "UpperArm_R": (-2 * math.sin(p), 3, 0),
                "Forearm_L": (-8, 0, 0), "Forearm_R": (-8, 0, 0),
                "__loc__": {"Root": (0, 0, 0.003 * s * math.sin(p))}}
    return f


def h_walk(s):
    def f(fr):
        p = ph(fr, 32)
        out = {"__loc__": {"Root": (0, 0, -0.025 * s * math.sin(p) ** 2)}}
        for side, off in (("L", 0.0), ("R", math.pi)):
            q = p + off
            thigh = -24 * math.sin(q)
            shin = 4 + 42 * max(0.0, math.cos(q)) ** 1.5
            out[f"Thigh_{side}"] = (thigh, 0, 0)
            out[f"Shin_{side}"] = (shin, 0, 0)
            out[f"Foot_{side}"] = (-0.4 * (thigh + shin) + 4, 0, 0)
            out[f"UpperArm_{side}"] = (18 * math.sin(q), 3 if side == "R" else -3, 0)
            out[f"Forearm_{side}"] = (-14 - 8 * max(0.0, -math.sin(q)), 0, 0)
        out["Chest"] = (2, 0, 5 * math.sin(p))
        out["Hips"] = (0, 0, -4 * math.sin(p))
        out["Head"] = (-1, 0, -3 * math.sin(p))
        return out
    return f


def h_run(s):
    def f(fr):
        p = ph(fr, 20)
        out = {"__loc__": {"Root": (0, 0, 0.045 * s * abs(math.cos(p)) - 0.03 * s)}}
        for side, off in (("L", 0.0), ("R", math.pi)):
            q = p + off
            thigh = -10 - 42 * math.sin(q)
            shin = 25 + 75 * max(0.0, math.cos(q)) ** 1.2
            out[f"Thigh_{side}"] = (thigh, 0, 0)
            out[f"Shin_{side}"] = (shin, 0, 0)
            out[f"Foot_{side}"] = (-0.3 * (thigh + shin), 0, 0)
            out[f"UpperArm_{side}"] = (38 * math.sin(q), 6 if side == "R" else -6, 0)
            out[f"Forearm_{side}"] = (-85, 0, 0)
        out["Spine"] = (10, 0, 0)
        out["Chest"] = (6, 0, 8 * math.sin(p))
        out["Hips"] = (0, 0, -7 * math.sin(p))
        out["Head"] = (-10, 0, -5 * math.sin(p))
        return out
    return f


def h_sit(s):
    def f(fr):
        p = ph(fr, 90)
        out = {"__loc__": {"Root": (0, 0.02 * s, -0.4 * s)}}
        for side in ("L", "R"):
            out[f"Thigh_{side}"] = (-88, 0, 3 if side == "L" else -3)
            out[f"Shin_{side}"] = (88 + 6 * (1 if side == "L" else 0), 0, 0)
            out[f"Foot_{side}"] = (-4, 0, 0)
            out[f"UpperArm_{side}"] = (-22, 0, 0)
            out[f"Forearm_{side}"] = (-55, 0, 0)
        out["Spine"] = (6 + 1.2 * math.sin(p), 0, 0)
        out["Chest"] = (4 + 1.4 * math.sin(p), 0, 0)
        out["Head"] = (6 + 2 * math.sin(p + 1), 0, 5 * math.sin(p * 0.5))
        return out
    return f


def h_shiver(s):
    rnd = [math.sin(i * 12.9898) * 43758.5453 % 1 for i in range(400)]

    def f(fr):
        j = lambda k, a: a * (rnd[(fr * 7 + k) % 400] - 0.5) * 2
        out = {"Spine": (4 + j(1, 1.2), j(2, 0.8), 0), "Chest": (5 + j(3, 1.5), j(4, 1.0), j(5, 1.2)),
               "Head": (8 + j(6, 2.0), 0, j(7, 2.5)),
               "UpperArm_L": (-28, -12, 0), "UpperArm_R": (-28, 12, 0),
               "Forearm_L": (-105 + j(8, 3), 0, -25), "Forearm_R": (-105 + j(9, 3), 0, 25),
               "Thigh_L": (j(10, 1.0), 0, 0), "Thigh_R": (j(11, 1.0), 0, 0)}
        return out
    return f


def h_scream(s):
    def f(fr):
        p = ph(fr, 30)
        t = min(1.0, fr / 8.0)
        return {"Spine": (-6 * t, 0, 0), "Chest": (-8 * t, 0, 0), "Head": (-18 * t + 2 * math.sin(p * 3), 0, 0),
                "UpperArm_L": (-55 * t, -25 * t, 0), "UpperArm_R": (-55 * t, 25 * t, 0),
                "Forearm_L": (-70 * t, 0, 0), "Forearm_R": (-70 * t, 0, 0),
                "Thigh_L": (-8 * t, 0, 0), "Shin_L": (10 * t, 0, 0),
                "__loc__": {"Root": (0, 0.05 * s * t, -0.02 * s * t)}}
    return f


def h_talk(s):
    idle = h_idle(s)

    def f(fr):
        out = idle(fr)
        p = ph(fr, 60)
        out["UpperArm_R"] = (-25 - 8 * math.sin(p), 10, 0)
        out["Forearm_R"] = (-70 - 15 * math.sin(p * 2), 0, 10 * math.sin(p))
        out["Head"] = (3 * math.sin(p * 2), 0, 6 * math.sin(p))
        return out
    return f


# ──────────────────────────────────────────────────────────────────────────────
# La criatura
# ──────────────────────────────────────────────────────────────────────────────
def creature_rigged():
    parts = creature()   # [body, head, eyes, jaw] de build_characters.py
    body, head, eyes, jaw = parts
    tag_group(head, "Head")
    tag_group(eyes, "Head")
    tag_group(jaw, "Jaw")
    mesh = join("LaCosa_Mesh", [body, head, eyes, jaw])

    def J(k, side=None):
        sx = 1 if side != "R" else -1
        table = {
            "pelvis": (0, 0.05, 1.80), "belly": (0, 0.03, 2.08), "chest": (0, -0.02, 2.40),
            "neck0": (0, -0.06, 2.62), "neck1": (0, -0.16, 2.80), "neck2": (0, -0.17, 2.90),
            "hip": (0.15, 0.05, 1.76), "stifle": (0.17, -0.20, 1.30), "hock": (0.16, 0.24, 0.62),
            "fetlock": (0.15, 0.06, 0.12), "hoof": (0.15, -0.04, 0.02),
            "shoulder": (0.25, -0.02, 2.48), "elbow": (0.31, 0.02, 1.95), "wrist": (0.30, -0.03, 1.42),
            "fingers": (0.30, -0.06, 1.08),
        }
        x, y, z = table[k]
        return V((x * sx, y, z))

    hinge = V((0, -0.24, 2.84))
    B = [("Root", (0, 0, 0), (0, 0, 0.3), None),
         ("Hips", J("pelvis"), J("belly"), "Root"),
         ("Spine", J("belly"), J("chest"), "Hips"),
         ("Chest", J("chest"), J("neck0"), "Spine"),
         ("Neck", J("neck0"), J("neck1"), "Chest"),
         ("Neck2", J("neck1"), V((0, -0.16, 2.82)), "Neck"),
         ("Head", V((0, -0.16, 2.82)), V((0, -0.5, 2.88)), "Neck2"),
         ("Jaw", hinge, V((0, -0.52, 2.80)), "Head")]
    seg = [("Hips", J("pelvis"), J("belly")), ("Spine", J("belly"), J("chest")), ("Chest", J("chest"), J("neck0")),
           ("Neck", J("neck0"), J("neck1")), ("Neck2", J("neck1"), J("neck2"))]
    for side in ("L", "R"):
        B += [(f"Clavicle_{side}", J("chest"), J("shoulder", side), "Chest"),
              (f"UpperArm_{side}", J("shoulder", side), J("elbow", side), f"Clavicle_{side}"),
              (f"Forearm_{side}", J("elbow", side), J("wrist", side), f"UpperArm_{side}"),
              (f"Hand_{side}", J("wrist", side), J("fingers", side), f"Forearm_{side}"),
              (f"Thigh_{side}", J("hip", side), J("stifle", side), "Hips"),
              (f"Shin_{side}", J("stifle", side), J("hock", side), f"Thigh_{side}"),
              (f"Cannon_{side}", J("hock", side), J("fetlock", side), f"Shin_{side}"),
              (f"Hoof_{side}", J("fetlock", side), J("hoof", side), f"Cannon_{side}")]
        seg += [("Hips", J("pelvis"), J("hip", side)), ("Chest", J("chest"), J("shoulder", side).lerp(J("chest"), 0.4)),
                (f"UpperArm_{side}", J("shoulder", side), J("elbow", side)),
                (f"Forearm_{side}", J("elbow", side), J("wrist", side)),
                (f"Hand_{side}", J("wrist", side), J("fingers", side)),
                (f"Thigh_{side}", J("hip", side), J("stifle", side)),
                (f"Shin_{side}", J("stifle", side), J("hock", side)),
                (f"Cannon_{side}", J("hock", side), J("fetlock", side)),
                (f"Hoof_{side}", J("fetlock", side), J("hoof", side))]
    arm = make_armature("LaCosa", B)
    weight_mesh(mesh, arm, seg, rigid_groups=("Head", "Jaw"))
    return arm, mesh


def c_idle(fr):
    p = ph(fr, 120)
    return {"Spine": (1.5 * math.sin(p), 0, 0), "Chest": (2.5 * math.sin(p), 0, 1.5 * math.sin(p * 0.5)),
            "Neck": (2 * math.sin(p + 0.7), 0, 0), "Neck2": (0, 0, 6 * math.sin(p * 0.5)),
            "UpperArm_L": (3 * math.sin(p + 0.3), -2, 0), "UpperArm_R": (3 * math.sin(p + 1.3), 2, 0),
            "Forearm_L": (2 * math.sin(p), 0, 0), "Forearm_R": (2 * math.sin(p + 2), 0, 0)}


def c_walk(fr):
    p = ph(fr, 44)
    out = {"__loc__": {"Root": (0, 0, -0.04 * math.sin(p) ** 2)}}
    for side, off in (("L", 0.0), ("R", math.pi)):
        q = p + off
        thigh = -22 * math.sin(q)
        out[f"Thigh_{side}"] = (thigh, 0, 0)
        out[f"Shin_{side}"] = (-10 * max(0.0, math.cos(q)), 0, 0)
        out[f"Cannon_{side}"] = (22 * max(0.0, math.cos(q)), 0, 0)
        out[f"Hoof_{side}"] = (-0.5 * thigh, 0, 0)
        out[f"UpperArm_{side}"] = (-14 * math.sin(q + math.pi), 0, 0)   # braceo lento, al revés que la pierna
        out[f"Forearm_{side}"] = (-6, 0, 0)
    out["Spine"] = (4, 0, 0)
    out["Chest"] = (3, 0, 4 * math.sin(p))
    out["Neck"] = (-4, 0, 0)
    out["Neck2"] = (0, 0, -3 * math.sin(p))
    return out


def c_run(fr):
    p = ph(fr, 18)
    out = {"__loc__": {"Root": (0, 0, 0.08 * abs(math.cos(p)) - 0.12)}}
    for side, off in (("L", 0.0), ("R", math.pi)):
        q = p + off
        thigh = -8 - 40 * math.sin(q)
        out[f"Thigh_{side}"] = (thigh, 0, 0)
        out[f"Shin_{side}"] = (-25 * max(0.0, math.cos(q)), 0, 0)
        out[f"Cannon_{side}"] = (45 * max(0.0, math.cos(q)), 0, 0)
        out[f"Hoof_{side}"] = (-0.5 * thigh, 0, 0)
        out[f"UpperArm_{side}"] = (40 * math.sin(q), -8 if side == "L" else 8, 0)   # los brazos largos se sacuden
        out[f"Forearm_{side}"] = (-25 - 20 * max(0.0, math.sin(q)), 0, 0)
    out["Spine"] = (16, 0, 0)
    out["Chest"] = (12, 0, 7 * math.sin(p))
    out["Neck"] = (-14, 0, 0)
    out["Neck2"] = (-8, 0, -4 * math.sin(p))
    return out


# ──────────────────────────────────────────────────────────────────────────────
def export_rig(arm, mesh, filename):
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, filename),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=True,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
    )
    print(f"[RIG] Exportado {filename}")


def build_human(name, height, outfit, clips, **kw):
    reset()
    bpy.context.scene.render.fps = FPS
    arm, mesh, s = human_rigged(name, height, outfit, **kw)
    lengths = {"Idle": 90, "Walk": 32, "Run": 20, "Sit": 90, "Shiver": 60, "Scream": 30, "Talk": 120}
    makers = {"Idle": h_idle, "Walk": h_walk, "Run": h_run, "Sit": h_sit, "Shiver": h_shiver, "Scream": h_scream, "Talk": h_talk}
    for c in clips:
        bake_action(arm, c, lengths[c], makers[c](s))
    export_rig(arm, mesh, f"{name}_Rig.fbx")
    return arm, mesh


def main():
    build_human("Sam", 1.62, {"top": "M_Sam_Hoodie", "bottom": "M_Char_Jeans", "shoes": "M_Char_Shoes", "skin": "M_Char_Skin",
                              "hair": "M_Char_HairBrown", "hood": True},
                ["Idle", "Walk", "Run", "Sit", "Shiver", "Scream"], build=0.92, hair="short")
    build_human("Tammy", 1.68, {"top": "M_Tammy_Sweater", "bottom": "M_Char_Slacks", "shoes": "M_Char_HairDark",
                                "skin": "M_Char_SkinTammy", "hair": "M_Char_HairDark"},
                ["Idle", "Walk", "Talk"], build=1.0, hair="bun", extras=[apron], belt=True)
    build_human("Emily", 1.58, {"top": "M_Emily_Shirt", "bottom": "M_Char_Jeans", "shoes": "M_Char_Shoes",
                                "skin": "M_Char_Skin", "hair": "M_Char_HairBrown"},
                ["Idle", "Walk", "Talk"], build=0.88, hair="ponytail", belt=True)

    reset()
    bpy.context.scene.render.fps = FPS
    arm, mesh = creature_rigged()
    bake_action(arm, "Idle", 120, c_idle)
    bake_action(arm, "Walk", 44, c_walk)
    bake_action(arm, "Run", 18, c_run)
    export_rig(arm, mesh, "LaCosa_Rig.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_RIG)
    print("[RIG] Listo")


main()
