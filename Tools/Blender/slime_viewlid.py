import bpy, os, math
from mathutils import Vector
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "ArtSource", "Slime", "Slime.blend"))
sc = bpy.context.scene; o = bpy.data.objects["Slime"]; me = o.data
g = o.vertex_groups["Lid"].index
ca = me.color_attributes.new("lidviz", 'FLOAT_COLOR', 'POINT')
for v in me.vertices:
    w = 0
    for gg in v.groups:
        if gg.group == g: w = gg.weight
    ca.data[v.index].color = (w, 1 - w, 0.2, 1)
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.color_type = 'VERTEX'; sc.display.shading.light='STUDIO'
sc.render.resolution_x = 400; sc.render.resolution_y = 300
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type='ORTHO'; cam.data.ortho_scale=1.3
for name, pos in [("lid_front",(0,-3,0.4)),("lid_side",(3,0,0.4)),("lid_tex_side",(3,0,0.4))]:
    if name=="lid_tex_side": sc.display.shading.color_type='TEXTURE'
    cam.location = pos; cam.rotation_euler = (Vector((0,0,0.4))-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath = os.path.join(ROOT,"ArtSource","Slime","Revision",name+".png"); bpy.ops.render.render(write_still=True)
