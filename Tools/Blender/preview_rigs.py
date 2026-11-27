"""Hoja de control: importa los *_Rig.fbx y renderiza cada clip en 2 fotogramas (Workbench).
blender -b --factory-startup --python Tools/Blender/preview_rigs.py -- <carpeta_salida>"""
import os, sys, math, bpy
from mathutils import Vector
out = sys.argv[sys.argv.index("--") + 1]
root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
src = os.path.join(root, "Assets", "_Project", "Resources", "Characters")
for name in ("Sam", "Tammy", "Emily", "LaCosa"):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(src, f"{name}_Rig.fbx"))
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "MATERIAL"
    sc.render.resolution_x, sc.render.resolution_y = 360, 480
    h = 3.6 if name == "LaCosa" else 1.9
    cam_data = bpy.data.cameras.new("c"); cam_data.type = "ORTHO"; cam_data.ortho_scale = h * 1.15
    cam = bpy.data.objects.new("c", cam_data); sc.collection.objects.link(cam); sc.camera = cam
    for act in bpy.data.actions:
        clip = act.name.split("|")[-1]
        if not arm.animation_data: arm.animation_data_create()
        arm.animation_data.action = act
        if hasattr(arm.animation_data, "action_slot") and act.slots:
            arm.animation_data.action_slot = act.slots[0]
        n = int(act.frame_range[1])
        for k, fr in enumerate((0, n // 2)):
            sc.frame_set(fr)
            for view, (loc, rot) in {"front": ((0, -8, h / 2), (math.radians(90), 0, 0)),
                                    "side": ((8, 0, h / 2), (math.radians(90), 0, math.radians(90)))}.items():
                cam.location = loc; cam.rotation_euler = rot
                sc.render.filepath = os.path.join(out, f"{name}_{clip}_{k}_{view}.png")
                bpy.ops.render.render(write_still=True)
print("PREVIEW OK")
