"""
Slime (Lizards to the Rescue) — rig, pesos y animaciones a partir de SLIME.glb.

Uso (desde la carpeta del proyecto):
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --python Tools/Blender/slime_build.py -- <ruta a SLIME.glb>

Salida:
  ArtSource/Slime/Slime.blend            (editable, con textura empaquetada)
  Assets/Lizard/Art/Slime/Slime.fbx      (rig Generic para Unity, 7 clips a 30 FPS)
  Assets/Lizard/Art/Slime/Slime_BaseColor.jpg

Huesos (todos en vertical, Y local = arriba):
  Root  → base fija en el suelo (el clip nunca la mueve: el código de Unity desplaza al enemigo)
  Body  → hijo de Root, pivote en el suelo: aplastar / estirar todo el cuerpo sin despegar la base
  Mid   → hijo de Body: inclinación y bamboleo del tronco
  Top   → hijo de Mid: cabeza / parte alta
  Lid   → hijo de Root: la tapa rígida; cada fotograma se recoloca sobre Top sin heredar su escala
  Skirt_F/B/L/R → hijos de Root: el borde de la base se desparrama (aterrizajes, muerte)
"""
import bpy, bmesh, math, sys, os
import numpy as np
from mathutils import Vector, Matrix, Quaternion

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
GLB = argv[0] if argv else r"C:\Users\samir\Downloads\SLIME.glb"
BLEND = os.path.join(ROOT, "ArtSource", "Slime", "Slime.blend")
UNITY_DIR = os.path.join(ROOT, "Assets", "Lizard", "Art", "Slime")
FBX = os.path.join(UNITY_DIR, "Slime.fbx")
TARGET_TRIS = 32000
FPS = 30

os.makedirs(os.path.dirname(BLEND), exist_ok=True)
os.makedirs(UNITY_DIR, exist_ok=True)

# ---------------------------------------------------------------- escena y modelo
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = FPS
bpy.ops.import_scene.gltf(filepath=GLB)
mesh_obj = [o for o in bpy.data.objects if o.type == 'MESH'][0]
mesh_obj.name = "Slime"; mesh_obj.data.name = "SlimeMesh"
for o in list(bpy.data.objects):
    if o is not mesh_obj and o.type != 'MESH': bpy.data.objects.remove(o, do_unlink=True)
bpy.context.view_layer.objects.active = mesh_obj
mesh_obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Reducción de polígonos conservando silueta y UV (la textura guarda los detalles).
tris = sum(len(p.vertices) - 2 for p in mesh_obj.data.polygons)
if tris > TARGET_TRIS:
    mod = mesh_obj.modifiers.new("Reducir", 'DECIMATE')
    mod.ratio = TARGET_TRIS / tris
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
# Une las costuras partidas (mismas posiciones) para que la piel no se abra al deformar.
bm = bmesh.new(); bm.from_mesh(mesh_obj.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0004)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.to_mesh(mesh_obj.data); bm.free()
for p in mesh_obj.data.polygons: p.use_smooth = True
me = mesh_obj.data
print("TRIS", sum(len(p.vertices) - 2 for p in me.polygons), "VERTS", len(me.vertices))

# ---------------------------------------------------------------- textura
mat = me.materials[0]; mat.name = "Slime_Material"
img = None
for n in mat.node_tree.nodes:
    if n.type == 'TEX_IMAGE' and n.image: img = n.image
img.name = "Slime_BaseColor"
tex_path = os.path.join(UNITY_DIR, "Slime_BaseColor.jpg")
img.filepath_raw = tex_path; img.file_format = 'JPEG'; img.save()
W, H = img.size
px = np.empty(W * H * 4, dtype=np.float32); img.pixels.foreach_get(px); px = px.reshape(H, W, 4)

# ---------------------------------------------------------------- tapa: color lila + altura, suavizado entre vecinos
uv = me.uv_layers.active.data
vert_uv = [None] * len(me.vertices)
for loop in me.loops:
    if vert_uv[loop.vertex_index] is None: vert_uv[loop.vertex_index] = uv[loop.index].uv.copy()
def sample(vi):
    u = vert_uv[vi] or Vector((0, 0))
    return px[min(H - 1, max(0, int(u.y * H))), min(W - 1, max(0, int(u.x * W)))]
lavender = [False] * len(me.vertices)
for v in me.vertices:
    c = sample(v.index); lavender[v.index] = c[2] > c[1] + 0.02 and c[0] > c[1] - 0.05
# La tapa está inclinada sobre la cabeza: se ajusta un plano (PCA) a los puntos lila seguros y se clasifica
# por distancia a ese plano, no por altura.
# Anillo exterior de la tapa (lila y lejos del eje): plano z = ax + by + c por mínimos cuadrados.
pts = np.array([tuple(v.co) for v in me.vertices if lavender[v.index] and math.hypot(v.co.x, v.co.y) > 0.33 and v.co.z > 0.40])
A = np.c_[pts[:, 0], pts[:, 1], np.ones(len(pts))]; (pa, pb_, pc), *_ = np.linalg.lstsq(A, pts[:, 2], rcond=None)
nrm = np.array([-pa, -pb_, 1.0]); nrm /= np.linalg.norm(nrm)
cx, cy = pts[:, 0].mean(), pts[:, 1].mean(); cen = np.array([cx, cy, pa * cx + pb_ * cy + pc])
LID_N = Vector(nrm.tolist()); LID_C = Vector(cen.tolist())
def plane_d(co): return (co - LID_C).dot(LID_N)
def axis_r(co): q = co - LID_C; return (q - LID_N * q.dot(LID_N)).length
rim = sorted(plane_d(v.co) for v in me.vertices if lavender[v.index] and axis_r(v.co) > 0.3 and plane_d(v.co) > -0.1)
THR = rim[int(len(rim) * 0.03)] - 0.008
in_lid_zone = [plane_d(v.co) > THR and axis_r(v.co) < 0.58 for v in me.vertices]
# Tapa = lila, o manchas de baba sobre la tapa reconocidas por la geometría (cara inferior, borde o por encima de la cúpula).
# La cúpula verde que queda bajo la tapa (normales hacia arriba y por debajo de ella) sigue siendo cuerpo:
# así la tapa se separa limpia en el ragdoll y no arrastra el cuerpo.
def lid_vertex(v):
    if lavender[v.index] and plane_d(v.co) > THR - 0.045 and 0.3 < axis_r(v.co) < 0.58: return True  # borde que baja más por la inclinación
    if not in_lid_zone[v.index]: return False
    if lavender[v.index]: return True
    if axis_r(v.co) > 0.27 and plane_d(v.co) > THR + 0.012: return True  # borde de la tapa (y el filo de la cúpula que asoma), aunque tenga baba verde
    d = plane_d(v.co); nd = v.normal.dot(LID_N)
    if nd < -0.5 and d > -0.02: return True            # cara inferior de la tapa
    if nd > 0.5: return d >= 0.035                     # encima: baba sobre la tapa; debajo: cúpula del cuerpo
    return d > 0.03 or (axis_r(v.co) > 0.32 and d > -0.05)  # paredes: borde exterior de la tapa y asa
is_lid = [lid_vertex(v) for v in me.vertices]
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
for _ in range(3):
    new = list(is_lid)
    for v in bm.verts:
        nb = [e.other_vert(v).index for e in v.link_edges]
        if not nb: continue
        votes = sum(1 for i in nb if is_lid[i]) / len(nb)
        if votes > 0.6 and (in_lid_zone[v.index] or (plane_d(v.co) > THR - 0.045 and axis_r(v.co) > 0.3)): new[v.index] = True
        elif votes < 0.3: new[v.index] = False
    is_lid = new
bm.free()
LID_BOTTOM = LID_C.z + THR  # altura del borde de la tapa en el centro: escala de alturas del cuerpo
# El cuerpo que asoma por encima de la base de la tapa (la cúpula de debajo, por la inclinación) se hunde bajo ella,
# con una transición suave para que no se note el escalón.
def push_under_lid(skip=None):
    n = 0
    for v in me.vertices:
        if is_lid[v.index] or axis_r(v.co) > 0.58 or (skip and skip(v)): continue
        d = plane_d(v.co); lim = THR - 0.012
        if d > lim - 0.03:
            excess = d - (lim - 0.03)
            newd = (lim - 0.03) + 0.03 * (1 - math.exp(-excess / 0.03))  # se acerca al límite sin cruzarlo
            v.co -= LID_N * (d - newd); q = v.co - LID_C; radial = q - LID_N * q.dot(LID_N)
            v.co -= radial * min(0.08, excess * 0.6)  # y un poco hacia dentro, bajo el ala de la tapa
            n += 1
    me.update(); return n
pushed = push_under_lid()
print("BODY bajo la tapa", pushed, "siguen altos", sum(1 for v in me.vertices if not is_lid[v.index] and plane_d(v.co) > -0.06))
print("LID verts", sum(is_lid), "normal", tuple(round(x, 3) for x in LID_N), "centro", tuple(round(x, 3) for x in LID_C), "umbral", round(THR, 3),
      "verdes dentro", sum(1 for v in me.vertices if is_lid[v.index] and not lavender[v.index]), "lilas fuera", sum(1 for v in me.vertices if lavender[v.index] and not is_lid[v.index]))

# ---------------------------------------------------------------- cara: lienzo liso para la cara animada de Unity
# Los ojos y la boca venían esculpidos y pintados (no se podían mover). Se alisa esa zona de la malla y se rellena
# con el verde del cuerpo; en Unity se pone una cara procedimental con ojos y boca que gesticulan.
def clamp01(x): return max(0.0, min(1.0, x))
FACE_C = Vector((0.0, -0.36, 0.25))
def face_w(co):
    if co.y > 0.05: return 0.0  # incluye el interior de la boca y de los ojos, que se hunde en la cabeza
    dx = co.x / 0.30; dz = (co.z - FACE_C.z) / 0.20
    return clamp01(1.25 - math.sqrt(dx * dx + dz * dz)) ** 0.7 if co.z > 0.04 else 0.0
fw = [0.0 if is_lid[v.index] else min(1.0, face_w(v.co)) for v in me.vertices]
print("FACE verts", sum(1 for x in fw if x > 0), "max", round(max(fw), 3))
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
for it in range(70):
    cos = [v.co.copy() for v in bm.verts]
    for v in bm.verts:
        w = fw[v.index]
        if w <= 0: continue
        nb = [e.other_vert(v).index for e in v.link_edges]
        if not nb: continue
        avg = sum((cos[i] for i in nb), Vector()) / len(nb)
        v.co = cos[v.index].lerp(avg, 0.6 * w)
# Recupera algo de volumen (el alisado encoge): empuja la zona hacia fuera según su peso.
for v in bm.verts:
    w = fw[v.index]
    if w > 0: v.co.y -= 0.012 * w
# Agujeros de la malla original en la zona de la cara (se veían como motas bajo la boca): se tapan.
# Solo se añaden caras, no vértices, así que los índices de vértice siguen valiendo.
uvl = bm.loops.layers.uv.active
holes_edges = [e for e in bm.edges if e.is_boundary and all(fw[v.index] > 0 or v.co.y < -0.2 for v in e.verts) and all(v.co.z < 0.42 for v in e.verts)]
res = bmesh.ops.holes_fill(bm, edges=holes_edges, sides=60)
new_faces = res.get("faces", [])
for f in new_faces:
    f.smooth = True
    # Todo el parche toma el color de un solo punto verde vecino: sus UV no pueden abarcar trozos de textura distintos.
    src = next((ol for l in f.loops for ol in l.vert.link_loops if ol.face not in new_faces), None)
    for l in f.loops:
        if src: l[uvl].uv = src[uvl].uv.copy()
print("HOLES tapados", len(new_faces), "de", len(holes_edges), "bordes")
bm.to_mesh(me); bm.free(); me.update()
uv = me.uv_layers.active.data  # la referencia anterior queda obsoleta tras reescribir la malla
# El alisado de la cara vuelve a subir algo de cuerpo junto a la tapa: se hunde otra vez, salvo delante (la cara).
print("BODY hundido otra vez fuera de la cara", push_under_lid(lambda v: v.co.y < -0.15))

def raster_mask(faces):
    """Máscara (H×W) de los píxeles de textura que cubren las caras dadas."""
    mask = np.zeros((H, W), dtype=bool)
    for p in faces:
        uvs = [uv[li].uv for li in p.loop_indices]
        for k in range(1, len(uvs) - 1):
            a, b, c = uvs[0], uvs[k], uvs[k + 1]
            xs = np.array([a.x, b.x, c.x]) * W; ys = np.array([a.y, b.y, c.y]) * H
            x0, x1 = max(0, int(xs.min()) - 1), min(W - 1, int(xs.max()) + 1); y0, y1 = max(0, int(ys.min()) - 1), min(H - 1, int(ys.max()) + 1)
            if x1 < x0 or y1 < y0: continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
            if abs(d) < 1e-9: continue
            l1 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / d
            l2 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / d
            inside = (l1 >= -0.02) & (l2 >= -0.02) & (1 - l1 - l2 >= -0.02)
            mask[y0:y1 + 1, x0:x1 + 1] |= inside
    return mask

rgb = px[:, :, :3].copy()
greenness = rgb[:, :, 1] - np.maximum(rgb[:, :, 0], rgb[:, :, 2])
lum = rgb @ np.array([0.3, 0.59, 0.11], dtype=np.float32)

# 1) Tapa sin manchas verdes: lo verde dentro de la tapa pasa a lila conservando el sombreado.
lid_faces = [p for p in me.polygons if all(is_lid[i] for i in p.vertices)
             or (sum(1 for i in p.vertices if is_lid[i]) >= 1 and min(plane_d(me.vertices[i].co) for i in p.vertices) > THR + 0.03)]
lid_mask = raster_mask(lid_faces)
lav_px = rgb[lid_mask & (greenness < 0.0)]
LID_COL = np.median(lav_px, axis=0) if len(lav_px) else np.array([0.62, 0.58, 0.68], dtype=np.float32)
LID_LUM = float(LID_COL @ np.array([0.3, 0.59, 0.11]))
yellowish = np.minimum(rgb[:, :, 0], rgb[:, :, 1]) - rgb[:, :, 2]  # verde amarillento de la baba
k = np.clip(np.maximum((greenness + 0.02) / 0.08, (yellowish - 0.12) / 0.1), 0, 1) * lid_mask
shade = np.clip(lum / max(0.05, LID_LUM), 0.6, 1.25)[..., None]
rgb = rgb * (1 - k[..., None]) + (LID_COL[None, None, :] * shade) * k[..., None]

# 2) Cara: ojos blancos, pupilas y boca oscuras → relleno con el verde de alrededor (difusión).
face_faces = [p for p in me.polygons if any(fw[i] > 0.02 for i in p.vertices)]
face_mask = raster_mask(face_faces)
for _ in range(4):  # margen de la textura alrededor de cada trozo de UV
    m2 = face_mask.copy(); m2[1:] |= face_mask[:-1]; m2[:-1] |= face_mask[1:]; m2[:, 1:] |= face_mask[:, :-1]; m2[:, :-1] |= face_mask[:, 1:]; face_mask = m2
core_mask = raster_mask([p for p in me.polygons if all(fw[i] > 0.3 for i in p.vertices)])
face_mask &= ~lid_mask  # la cara nunca pinta sobre la tapa
hole = face_mask & ((greenness < 0.08) | (core_mask & ((greenness < 0.12) | (lum < 0.33))))  # contornos oscuros solo en el centro de la cara
grow = hole.copy()
for _ in range(6):  # incluye el halo oscuro de los bordes
    g2 = grow.copy(); g2[1:] |= grow[:-1]; g2[:-1] |= grow[1:]; g2[:, 1:] |= grow[:, :-1]; g2[:, :-1] |= grow[:, 1:]; grow = g2
hole = grow & face_mask
ys_, xs_ = np.where(hole)
if len(ys_):
    y0, y1, x0, x1 = max(0, ys_.min() - 40), min(H, ys_.max() + 41), max(0, xs_.min() - 40), min(W, xs_.max() + 41)
    sub = rgb[y0:y1, x0:x1].copy()
    # Solo sirve de fuente el verde que está dentro de la cara (no los márgenes vacíos entre trozos de UV).
    valid = (face_mask[y0:y1, x0:x1] & ~hole[y0:y1, x0:x1] & (greenness[y0:y1, x0:x1] > 0.12)).astype(np.float32)
    fill_target = hole[y0:y1, x0:x1] | ~face_mask[y0:y1, x0:x1]
    acc = sub * valid[..., None]
    for _ in range(400):
        if valid.min() > 0: break
        def blur(a):
            b = a.copy(); b[1:] += a[:-1]; b[:-1] += a[1:]; b[:, 1:] += a[:, :-1]; b[:, :-1] += a[:, 1:]; return b
        s = blur(acc); w = blur(valid)
        newly = (valid == 0) & (w > 0)
        acc[newly] = s[newly] / w[newly][:, None]; valid[newly] = 1
    rng = np.random.default_rng(3)
    noise = (rng.random(acc.shape[:2]) - 0.5)[..., None] * 0.01
    left = valid == 0
    if left.any(): acc[left] = acc[valid > 0].mean(axis=0)  # lo que no alcanzó la difusión: verde medio
    acc = np.where(hole[y0:y1, x0:x1][..., None], np.clip(acc + noise, 0, 1), sub)
    rgb[y0:y1, x0:x1] = acc

# 3) Suavizado de la piel en la cara y bajo el borde de la tapa: quita manchas, motas y la baba dentada pintada.
def box_blur(a, r):
    c = np.cumsum(np.cumsum(np.pad(a, ((r + 1, r), (r + 1, r)) + ((0, 0),) * (a.ndim - 2), mode="edge"), 0), 1)
    return (c[2 * r + 1:, 2 * r + 1:] - c[:-2 * r - 1, 2 * r + 1:] - c[2 * r + 1:, :-2 * r - 1] + c[:-2 * r - 1, :-2 * r - 1]) / float((2 * r + 1) ** 2)
band_faces = [p for p in me.polygons if not any(is_lid[i] for i in p.vertices)
              and all(THR - 0.11 < plane_d(me.vertices[i].co) < THR + 0.05 and axis_r(me.vertices[i].co) < 0.5 for i in p.vertices)]
smooth_mask = (raster_mask(band_faces) | face_mask) & ~lid_mask
gn = rgb[:, :, 1] - np.maximum(rgb[:, :, 0], rgb[:, :, 2])
src = (smooth_mask & (gn > 0.1)).astype(np.float32)
ys_, xs_ = np.where(smooth_mask)
if len(ys_):
    for (y0, y1, x0, x1) in [(max(0, ys_.min() - 24), min(H, ys_.max() + 25), max(0, xs_.min() - 24), min(W, xs_.max() + 25))]:
        s = src[y0:y1, x0:x1]; col = rgb[y0:y1, x0:x1]
        for r in (14, 8):
            wsum = box_blur(s, r); csum = box_blur(col * s[..., None], r)
            sm = np.where(wsum[..., None] > 1e-3, csum / np.maximum(wsum, 1e-3)[..., None], col)
            col = np.where(smooth_mask[y0:y1, x0:x1][..., None], sm, col)
        rgb[y0:y1, x0:x1] = col
print("TEX suavizado px", int(smooth_mask.sum()))
# Repaso final de la tapa: nada de lo anterior puede haberle devuelto el verde.
green2 = rgb[:, :, 1] - np.maximum(rgb[:, :, 0], rgb[:, :, 2]); lum2 = rgb @ np.array([0.3, 0.59, 0.11], dtype=np.float32)
yellow2 = np.minimum(rgb[:, :, 0], rgb[:, :, 1]) - rgb[:, :, 2]
k2 = np.clip(np.maximum((green2 + 0.02) / 0.08, (yellow2 - 0.12) / 0.1), 0, 1) * lid_mask
rgb = rgb * (1 - k2[..., None]) + (LID_COL[None, None, :] * np.clip(lum2 / max(0.05, LID_LUM), 0.6, 1.25)[..., None]) * k2[..., None]
px[:, :, :3] = rgb
img.pixels.foreach_set(px.ravel()); img.update(); img.save()
print("TEX tapa px", int(lid_mask.sum()), "verde corregido", int((k > 0.5).sum()), "cara px", int(face_mask.sum()), "rellenados", len(ys_))

# Puntos de anclaje de la cara sobre la superficie ya alisada (en Unity se coloca ahí la cara animada).
from mathutils.bvhtree import BVHTree
bvh = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
def surface(x, z):
    hit = bvh.ray_cast(Vector((x, -2.0, z)), Vector((0, 1, 0)))
    return (hit[0], hit[1]) if hit[0] else (Vector((x, -0.38, z)), Vector((0, -1, 0)))
FACE_ANCHORS = {"Face_EyeL": surface(0.13, 0.33), "Face_EyeR": surface(-0.13, 0.33), "Face_Mouth": surface(0.0, 0.18)}
for n, (p, nn) in FACE_ANCHORS.items(): print("ANCHOR", n, tuple(round(x, 3) for x in p), tuple(round(x, 3) for x in nn))

# ---------------------------------------------------------------- esqueleto
arm_data = bpy.data.armatures.new("SlimeRig")
arm = bpy.data.objects.new("SlimeRig", arm_data)
scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
for o in bpy.context.selected_objects: o.select_set(False)
arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
def bone(name, head, tail, parent=None):
    b = eb.new(name); b.head = head; b.tail = tail; b.roll = 0
    if parent: b.parent = eb[parent]; b.use_connect = False
    return b
LID_Z = max(0.46, min(0.6, LID_BOTTOM))
bone("Root", (0, 0, 0), (0, 0, 0.08))
bone("Body", (0, 0, 0), (0, 0, 0.26), "Root")
bone("Mid", (0, 0, 0.26), (0, 0, 0.44), "Body")
bone("Top", (0, 0, 0.44), (0, 0, LID_Z), "Mid")
bone("Lid", (0, 0, LID_Z), (0, 0, 0.75), "Root")
SKIRTS = {"Skirt_F": (0, -1), "Skirt_B": (0, 1), "Skirt_L": (1, 0), "Skirt_R": (-1, 0)}  # el frente del slime mira a -Y
for n, (dx, dy) in SKIRTS.items(): bone(n, (dx * 0.36, dy * 0.36, 0.0), (dx * 0.36, dy * 0.36, 0.14), "Root")
# Anclajes de la cara (sin pesos): cabeza en la superficie, cola hacia fuera según la normal.
for n, (p, nn) in FACE_ANCHORS.items(): bone(n, tuple(p), tuple(p + nn * 0.06), "Root")
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- pesos
groups = {n: mesh_obj.vertex_groups.new(name=n) for n in ["Body", "Mid", "Top", "Lid"] + list(SKIRTS)}
def clamp01(x): return max(0.0, min(1.0, x))
def smooth(a, b, x):
    t = clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t)
for v in me.vertices:
    if is_lid[v.index]: groups["Lid"].add([v.index], 1.0, "REPLACE"); continue
    # Verde junto al borde de la tapa: transición suave Tapa/Cabeza para que no haya costura rígida.
    # El cuerpo no se pesa a la tapa (para que se separe limpia en el ragdoll), salvo la franja justo bajo el borde,
    # que pasa en rampa de cuerpo a tapa para que el borde no forme picos al aplastarse.
    ar = axis_r(v.co); dd = plane_d(v.co)
    wl = smooth(THR - 0.09, THR + 0.04, dd) * smooth(0.28, 0.34, ar) if ar < 0.58 else 0.0
    if wl > 0.98: groups["Lid"].add([v.index], 1.0, "REPLACE"); continue
    z = v.co.z; r = math.hypot(v.co.x, v.co.y)
    h = z / LID_BOTTOM
    w = {"Body": 1 - smooth(0.0, 0.55, h), "Mid": 1 - abs(h - 0.55) / 0.45, "Top": smooth(0.55, 0.95, h)}
    w["Mid"] = clamp01(w["Mid"])
    s = sum(w.values()) or 1; w = {k: x / s for k, x in w.items()}
    if wl > 0.001:
        w = {k: x * (1 - wl) for k, x in w.items()}; w["Lid"] = wl
    # Borde de la base: parte del peso pasa a las faldas (se abren al aplastarse sin despegar del suelo).
    skirt = 0.65 * (1 - smooth(0.0, 0.16, z)) * smooth(0.18, 0.42, r)
    if skirt > 0.001:
        ang = math.atan2(v.co.y, v.co.x)
        lobes = {n: max(0.0, math.cos(ang - math.atan2(dy, dx))) ** 2 for n, (dx, dy) in SKIRTS.items()}
        ls = sum(lobes.values()) or 1
        for n, lw in lobes.items():
            if lw > 0: groups[n].add([v.index], skirt * lw / ls, 'REPLACE')
        w = {k: x * (1 - skirt) for k, x in w.items()}
    for k, x in w.items():
        if x > 0.001: groups[k].add([v.index], x, 'REPLACE')
mesh_obj.parent = arm
mod = mesh_obj.modifiers.new("Armature", 'ARMATURE'); mod.object = arm

# ---------------------------------------------------------------- utilidades de pose
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
pbs = arm.pose.bones
for pb in pbs: pb.rotation_mode = 'QUATERNION'
REST_INV = {pb.name: arm.data.bones[pb.name].matrix_local.to_3x3().inverted() for pb in pbs}
FWD = Vector((0, -1, 0)); RIGHT = Vector((-1, 0, 0))  # frente del slime = -Y

def lean_q(name, fwd_deg, side_deg):
    """Inclinación hacia delante/lado expresada en el espacio local del hueso."""
    q = Quaternion(REST_INV[name] @ RIGHT.cross(Vector((0, 0, 1))).normalized(), 0)  # identidad
    qa = Quaternion(REST_INV[name] @ RIGHT, math.radians(fwd_deg))      # gira alrededor del eje lateral → hacia delante
    qb = Quaternion(REST_INV[name] @ FWD, math.radians(-side_deg))
    return qb @ qa

def ease(t): t = clamp01(t); return t * t * (3 - 2 * t)
def lerp(a, b, t): return a + (b - a) * t
def keys_at(keys, f):
    """Interpolación suave entre fotogramas clave {frame: dict}."""
    fs = sorted(keys)
    if f <= fs[0]: return dict(keys[fs[0]])
    for a, b in zip(fs, fs[1:]):
        if a <= f <= b:
            t = ease((f - a) / (b - a)); ka, kb = keys[a], keys[b]
            return {k: lerp(ka.get(k, DEF[k]), kb.get(k, DEF[k]), t) for k in DEF}
    return dict(keys[fs[-1]])
DEF = dict(sy=1.0, sxz=1.0, fwd=0.0, side=0.0, top_fwd=0.0, top_sy=1.0, skirt=0.0, lid_tilt=0.0, lid_lift=0.0, twist=0.0)
def full(d): x = dict(DEF); x.update(d); return x

def tremor(f, start, amp, freq=7.0, decay=5.0, end=None):
    if f < start: return 0.0
    t = (f - start) / FPS
    env = math.exp(-decay * t)
    if end is not None: env *= clamp01((end - f) / max(1, end - start)) ** 0.5
    return amp * env * math.sin(2 * math.pi * freq * t)

def apply_pose(p, lid_override=None):
    vol = 1 / math.sqrt(max(0.05, p["sy"]))  # conserva volumen aproximado (gelatina)
    sxz = p["sxz"]
    pbs["Root"].location = (0, 0, 0); pbs["Root"].rotation_quaternion = (1, 0, 0, 0); pbs["Root"].scale = (1, 1, 1)
    b = pbs["Body"]; b.scale = (sxz, p["sy"], sxz); b.rotation_quaternion = Quaternion((0, 1, 0), math.radians(p["twist"])) ; b.location = (0, 0, 0)
    m = pbs["Mid"]; m.rotation_quaternion = lean_q("Mid", p["fwd"], p["side"]); m.scale = (1, 1, 1); m.location = (0, 0, 0)
    t = pbs["Top"]; t.rotation_quaternion = lean_q("Top", p["top_fwd"], p["side"] * 0.6); t.scale = (1 / math.sqrt(p["top_sy"]), p["top_sy"], 1 / math.sqrt(p["top_sy"])); t.location = (0, 0, 0)
    for n, (dx, dy) in SKIRTS.items():
        s = pbs[n]; s.location = REST_INV[n] @ (Vector((dx, dy, 0)) * p["skirt"]); s.rotation_quaternion = (1, 0, 0, 0)
        s.scale = (1 + p["skirt"] * 2, 1, 1 + p["skirt"] * 2)
    bpy.context.view_layer.update()
    # La tapa sigue a la cabeza sin heredar su escala (queda rígida).
    tm = pbs["Top"].matrix
    loc, rot, sc = tm.decompose()
    top_len = arm.data.bones["Top"].length
    lid_pos = tm @ Vector((0, top_len, 0))
    q = rot @ Quaternion(REST_INV["Lid"] @ RIGHT, math.radians(p["lid_tilt"])) @ Quaternion(Vector((0, 1, 0)), 0)
    lid_pos = lid_pos + Vector((0, 0, p["lid_lift"]))
    if lid_override is not None:
        k, pos, qq = lid_override
        lid_pos = lid_pos.lerp(pos, k); q = q.slerp(qq, k)
    pbs["Lid"].matrix = Matrix.LocRotScale(lid_pos, q, (1, 1, 1))
    bpy.context.view_layer.update()

def make_clip(name, frames, pose_fn, events):
    act = bpy.data.actions.new(name); act.use_fake_user = True
    arm.animation_data_create(); arm.animation_data.action = act
    for f in range(frames + 1):
        scene.frame_set(f)
        res = pose_fn(f)
        if isinstance(res, tuple): apply_pose(*res)
        else: apply_pose(res)
        for pb in pbs:
            pb.keyframe_insert("location", frame=f); pb.keyframe_insert("rotation_quaternion", frame=f); pb.keyframe_insert("scale", frame=f)
    act["events"] = str(events)
    act.frame_range = (0, frames)
    for m in pose_markers(act, events): pass
    CLIPS.append((name, frames, events))
    return act

def pose_markers(act, events):
    for n, f in events.items():
        m = act.pose_markers.new(n); m.frame = f; yield m

CLIPS = []

# ---------------------------------------------------------------- clips
# Idle — 2 s en bucle: respiración lenta y oscilación suave.
def idle(f):
    t = f / 60.0; b = math.sin(2 * math.pi * t)
    return full(dict(sy=1 + 0.035 * b, sxz=1 - 0.018 * b, side=2.2 * math.sin(2 * math.pi * t + 0.6),
                     fwd=1.2 * math.sin(2 * math.pi * t + 1.7), top_fwd=1.5 * math.sin(2 * math.pi * 2 * t),
                     lid_tilt=1.8 * math.sin(2 * math.pi * t + 2.2), skirt=0.006 * b))
make_clip("Idle", 60, idle, {"loop": 0})

# HopMove — 0,8 s en bucle: comprime, se estira al despegar, rebota al aterrizar.
HOP = {0: full({}), 5: full(dict(sy=0.78, sxz=1.14, fwd=5, skirt=0.025)), 7: full(dict(sy=1.22, sxz=0.88, fwd=-4, top_fwd=-3, skirt=-0.03)),
       12: full(dict(sy=1.08, sxz=0.95, fwd=-2)), 16: full(dict(sy=1.02, sxz=0.98, fwd=2)), 17: full(dict(sy=0.74, sxz=1.2, fwd=6, top_fwd=4, skirt=0.05, lid_tilt=-4)),
       20: full(dict(sy=1.08, sxz=0.95, fwd=-3, top_fwd=-2, lid_tilt=3)), 24: full({})}
def hop(f):
    p = keys_at(HOP, f)
    w = tremor(f, 17, 0.05, 9, 7, end=24); p["sy"] += w; p["side"] += tremor(f, 17, 2.5, 7, 6, end=24)
    p["lid_lift"] = 0.02 * math.sin(math.pi * clamp01((f - 6) / 11)) if 6 <= f <= 17 else 0
    return p
make_clip("HopMove", 24, hop, {"takeoff": 6, "land": 17})

# Alert — 0,4 s una vez: se endereza (la orientación al jugador la hace el código).
ALERT = {0: full({}), 3: full(dict(sy=0.9, sxz=1.06, fwd=3)), 8: full(dict(sy=1.16, sxz=0.92, top_fwd=-7, lid_tilt=-6, lid_lift=0.02)), 12: full(dict(sy=1.06, sxz=0.97, top_fwd=-2))}
def alert(f):
    p = keys_at(ALERT, f); p["sy"] += tremor(f, 8, 0.03, 10, 9); return p
make_clip("Alert", 12, alert, {"upright": 8})

# AttackLeap — aplastado y bajo 0,5 s, despega en 15, impacta en 27, expuesto hasta 48.
LEAP = {0: full({}), 15: full(dict(sy=0.56, sxz=1.32, fwd=13, top_fwd=9, skirt=0.06, lid_tilt=6)),
        17: full(dict(sy=1.34, sxz=0.8, fwd=16, top_fwd=6, skirt=-0.04, lid_tilt=-8)), 24: full(dict(sy=1.04, sxz=1.0, fwd=4)),
        27: full(dict(sy=0.42, sxz=1.55, fwd=-2, skirt=0.13, lid_tilt=10)), 33: full(dict(sy=0.56, sxz=1.38, skirt=0.1, lid_tilt=4)),
        48: full(dict(sy=0.6, sxz=1.34, skirt=0.08, lid_tilt=2)), 54: full({})}
def leap(f):
    p = keys_at(LEAP, f)
    if f < 15: p["side"] += 1.8 * math.sin(f * 2.1) * (f / 15)   # tiembla mientras carga
    p["sy"] += tremor(f, 27, 0.07, 6, 3.5, end=48); p["side"] += tremor(f, 27, 4, 5, 3, end=48); p["fwd"] += tremor(f + 3, 27, 3, 5, 3, end=48)
    return p
make_clip("AttackLeap", 54, leap, {"takeoff": 15, "impact": 27, "vulnerable_start": 27, "vulnerable_end": 48})

# AttackSplash — se hincha alto 0,6 s y expulsa tres gotas en 19.
SPL = {0: full({}), 18: full(dict(sy=1.3, sxz=1.22, top_fwd=-10, top_sy=1.12, lid_tilt=-8, lid_lift=0.03)),
       21: full(dict(sy=0.82, sxz=1.06, fwd=12, top_fwd=10, skirt=0.04, lid_tilt=8)), 30: full({})}
def splash(f):
    p = keys_at(SPL, f)
    if f < 18: p["sxz"] += 0.025 * math.sin(f * 1.7) * (f / 18)
    p["sy"] += tremor(f, 21, 0.05, 8, 6, end=30); p["side"] += tremor(f, 21, 3, 6, 6, end=30)
    return p
make_clip("AttackSplash", 30, splash, {"expel": 19})

# HitReact — 0,3 s: se hunde y recupera (el código añade la inclinación hacia el lado del golpe).
HIT = {0: full({}), 2: full(dict(sy=0.8, sxz=1.12, skirt=0.03, lid_tilt=-5)), 5: full(dict(sy=1.07, sxz=0.96, lid_tilt=3)), 9: full({})}
def hit(f):
    p = keys_at(HIT, f); p["sy"] += tremor(f, 5, 0.02, 10, 10, end=9); return p
make_clip("HitReact", 9, hit, {"impact": 0})

# Death — 0,9 s: pierde volumen, se desparrama y queda como charco; la tapa resbala al suelo.
DIE = {0: full({}), 4: full(dict(sy=0.9, sxz=1.05, lid_lift=0.05)), 14: full(dict(sy=0.4, sxz=1.45, skirt=0.1, fwd=-4)),
       27: full(dict(sy=0.12, sxz=1.85, skirt=0.22))}
LID_REST = None
def death(f):
    p = keys_at(DIE, f); p["side"] += 2 * math.sin(f * 1.9) * clamp01(1 - f / 14)
    k = ease(clamp01((f - 6) / 18))
    pos = Vector((0.16, 0.06, 0.13)); qq = Quaternion((0, 0, 1), math.radians(20)) @ Quaternion((0, 1, 0), math.radians(14)) @ arm.data.bones["Lid"].matrix_local.to_quaternion()
    return (p, (k, pos, qq))
make_clip("Death", 27, death, {"puddle": 27})

# Pose de descanso y NLA limpio.
arm.animation_data.action = bpy.data.actions["Idle"]
scene.frame_set(0)
bpy.ops.object.mode_set(mode='OBJECT')
for a in bpy.data.actions:
    for fc in getattr(a, "fcurves", []):
        for kp in fc.keyframe_points: kp.interpolation = 'LINEAR'

# ---------------------------------------------------------------- guardar
if img.packed_file: img.unpack(method="REMOVE")  # la del GLB venía empaquetada: se cambia por la textura corregida
img.filepath = tex_path; img.reload(); img.pack()
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
for o in bpy.context.selected_objects: o.select_set(False)
arm.select_set(True); mesh_obj.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={'ARMATURE', 'MESH'}, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
                         primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL', use_armature_deform_only=False,
                         bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                         bake_anim_step=1.0, bake_anim_simplify_factor=0.0, mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
for n, f, e in CLIPS: print("CLIP", n, "frames", f, "seconds", round(f / FPS, 2), "events", e)
print("SAVED", BLEND, FBX)
