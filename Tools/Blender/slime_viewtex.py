import bpy, os
from mathutils import Vector
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "ArtSource", "Slime", "Slime.blend"))
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.color_type = 'TEXTURE'; sc.display.shading.light='STUDIO'
sc.render.resolution_x = 420; sc.render.resolution_y = 340
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type='ORTHO'; cam.data.ortho_scale=1.25
tiles=[]
import numpy as np
tmp = os.path.join(ROOT,"ArtSource","Slime","Revision","_t.png")
for pos in [(0,-3,0.4),(0.01,-0.4,3),(2.2,-2.2,1.6)]:
    cam.location = pos; cam.rotation_euler = (Vector((0,0,0.35))-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath = tmp; bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(tmp); tiles.append(np.array(im.pixels[:],dtype=np.float32).reshape(im.size[1],im.size[0],4)); bpy.data.images.remove(im)
sheet = np.concatenate(tiles, axis=1); out = bpy.data.images.new("s", sheet.shape[1], sheet.shape[0]); out.pixels = sheet.ravel()
out.filepath_raw = os.path.join(ROOT,"ArtSource","Slime","Revision","Textura.png"); out.file_format='PNG'; out.save(); os.remove(tmp)
