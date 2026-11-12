"""Hoja de contacto: renderiza fotogramas clave de cada clip del Slime.blend para revisar la deformación."""
import bpy, math, os, sys
import numpy as np
from mathutils import Vector
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "ArtSource", "Slime", "Slime.blend"))
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x = 260; sc.render.resolution_y = 240
sc.render.film_transparent = False
w = bpy.data.worlds.new("W"); sc.world = w
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.2
cam.location = (1.6, -2.6, 1.3)
cam.rotation_euler = (Vector((0.1, 0, 0.35)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
# suelo de referencia
bpy.ops.mesh.primitive_plane_add(size=3, location=(0, 0, 0))
arm = bpy.data.objects["SlimeRig"]
SHEETS = {"Idle": [0, 15, 30, 45], "HopMove": [0, 5, 7, 12, 17, 20], "Alert": [0, 3, 8, 12],
          "AttackLeap": [0, 15, 17, 24, 27, 36, 48, 54], "AttackSplash": [0, 10, 18, 21, 25, 30],
          "HitReact": [0, 2, 5, 9], "Death": [0, 6, 14, 20, 27]}
out_dir = os.path.join(ROOT, "ArtSource", "Slime", "Revision"); os.makedirs(out_dir, exist_ok=True)
tmp = os.path.join(out_dir, "_tmp.png")
for clip, frames in SHEETS.items():
    arm.animation_data.action = bpy.data.actions[clip]
    tiles = []
    for f in frames:
        sc.frame_set(f); sc.render.filepath = tmp; bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(tmp); a = np.array(im.pixels[:], dtype=np.float32).reshape(im.size[1], im.size[0], 4); bpy.data.images.remove(im)
        tiles.append(a)
    sheet = np.concatenate(tiles, axis=1)
    img = bpy.data.images.new(clip + "_sheet", sheet.shape[1], sheet.shape[0])
    img.pixels = sheet.ravel(); img.filepath_raw = os.path.join(out_dir, clip + ".png"); img.file_format = 'PNG'; img.save()
    print("SHEET", clip, frames)
os.remove(tmp)
