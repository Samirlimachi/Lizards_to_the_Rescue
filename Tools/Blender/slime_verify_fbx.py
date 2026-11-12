import bpy, os
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, "Assets", "Lizard", "Art", "Slime", "Slime.fbx"), use_anim=True)
for o in bpy.data.objects:
    print("OBJ", o.type, o.name, [b.name for b in o.data.bones] if o.type == 'ARMATURE' else "", len(o.data.vertices) if o.type == 'MESH' else "", [g.name for g in o.vertex_groups] if o.type == 'MESH' else "")
for a in bpy.data.actions:
    print("ACTION", a.name, "range", tuple(a.frame_range))
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
# Comprueba que la animación mueve de verdad los huesos: altura de la cabeza en el impacto del salto.
for a in bpy.data.actions:
    if a.name.endswith("AttackLeap"):
        arm.animation_data.action = a
        for f in (0, 15, 27):
            bpy.context.scene.frame_set(f)
            print("LEAP f", f, "Body scale", tuple(round(x, 2) for x in arm.pose.bones["Body"].matrix.to_scale()), "Lid z", round(arm.pose.bones["Lid"].head.z, 3))
