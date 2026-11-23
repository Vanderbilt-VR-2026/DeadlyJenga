"""Builds the Underneath the Tree toy set and exports one FBX per toy.
Run: blender -b -P toys.py -- <output folder> [preview.png]
Every toy stands on z = 0 and faces -Y. Material names are a shared paint list that Unity maps
to its own materials, so the colours can be tuned in one place. Generic toys only, no brands."""
import bpy, math, sys, os
from mathutils import Vector

out = sys.argv[sys.argv.index("--") + 1]
preview = sys.argv[sys.argv.index("--") + 2] if len(sys.argv) > sys.argv.index("--") + 2 else None
os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

PAINT = {
    "Chestnut resin": (1,1,1), "Honey scale lip": (.42,.165,.047),
    "Lemon ABS": (.99,.76,.001), "Safety orange ABS": (1,.075,.001), "Lime ABS": (.085,.95,.008),
    "Stamped brass": (.62,.36,.085), "Strawberry lacquer": (.45,.008,.022),
    "Printed dough tub": (1,1,1), "Bear pouch print": (1,1,1), "Rope sleeve print": (1,1,1),
    "Clear candy film": (.92,.96,1),
    "Cone heart": (.095,.041,.018), "Cone bark": (.22,.104,.048), "Cone rim": (.37,.212,.107),
    "Cocoa": (.19,.066,.031), "Cocoa wrapper": (.14,.031,.043),
    "Gummy cherry": (.73,.016,.026), "Gummy lime": (.11,.58,.035),
    "Gummy amber": (.97,.36,.015), "Gummy berry": (.49,.042,.20),
    "Stocking velvet": (.63,.026,.037), "Stocking fleece": (.93,.69,.42), "Cellophane": (.78,.91,.98),
    "Toy Red": (.75, .03, .04), "Toy Blue": (.04, .18, .75), "Toy Navy": (.03, .06, .30),
    "Toy Yellow": (1., .78, .08), "Toy Green": (.05, .55, .12), "Toy Teal": (.0, .55, .55),
    "Toy White": (.92, .92, .9), "Toy Black": (.02, .02, .025), "Toy Gold": (1., .68, .2),
    "Toy Silver": (.75, .77, .8), "Toy Brown": (.35, .18, .08), "Toy Pink": (1., .45, .65),
    "Toy Orange": (1., .4, .05), "Toy Purple": (.35, .1, .6), "Toy Skin": (.95, .75, .6),
    "Toy Cream": (.96, .9, .78), "Toy Plush": (.85, .62, .42), "Toy Frost": (.95, .97, 1.),
    "Toy Gray": (.3, .31, .33), "Toy Wood": (.6, .38, .2), "Toy Glass": (.6, .85, 1.),
}

def paint(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*PAINT[name], 1)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*PAINT[name], 1)
    if name.startswith("Gummy "):
        bsdf.inputs["Roughness"].default_value = .19
        bsdf.inputs["Coat Weight"].default_value = .4
    print_files={"Printed dough tub":"Knead-O print", "Bear pouch print":"Bruin pouch print",
                 "Rope sleeve print":"Loop Ropes sleeve print", "Chestnut resin":"Pine scale grain"}
    if name in print_files:
        texture_dir = Path(__file__).resolve().parents[3] / "Assets/_DeadlyJenga/World/Maps/Models/UnderneathTheTree/Toys"
        node=m.node_tree.nodes.new("ShaderNodeTexImage")
        node.image=bpy.data.images.load(str(texture_dir/(print_files[name]+".png")),check_existing=True)
        m.node_tree.links.new(node.outputs["Color"],bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value=.37
    if name.endswith(" ABS") or name in ("Chestnut resin","Honey scale lip","Strawberry lacquer"):
        bsdf.inputs["Roughness"].default_value=.20 if name.endswith(" ABS") else .31
        bsdf.inputs["Coat Weight"].default_value=.38
    if name=="Stamped brass":
        bsdf.inputs["Metallic"].default_value=.80
        bsdf.inputs["Roughness"].default_value=.29
    if name=="Clear candy film":
        bsdf.inputs["Alpha"].default_value=.049
        bsdf.inputs["Roughness"].default_value=.15
        m.diffuse_color=(*PAINT[name],.049)
    if name == "Cellophane":
        bsdf.inputs["Alpha"].default_value = .12
        bsdf.inputs["Roughness"].default_value = .2
        m.diffuse_color = (*PAINT[name], .12)
    elif name in ("Stocking velvet", "Stocking fleece"):
        bsdf.inputs["Roughness"].default_value = .94
        bsdf.inputs["Sheen Weight"].default_value = .4
    return m

from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from small_toys_export import export_small_toys
from shelf_toys import export_shelf

for color in PAINT:
    paint(color)
built = export_shelf(out)
built.extend(export_small_toys(out))

print("EXPORTED", len(built))

if preview:
    for o in bpy.context.scene.objects:
        o.hide_render = True
    cols = 5
    for i, o in enumerate(built):
        o.hide_render = False
        o.location = ((i % cols) * 18, -(i // cols) * 16 + 30, 0)
    bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(30)))
    bpy.context.active_object.data.energy = 3
    bpy.ops.object.camera_add(location=(36, -110, 75), rotation=(math.radians(55), 0, 0))
    cam = bpy.context.active_object
    cam.data.lens = 32
    bpy.context.scene.camera = cam
    w = bpy.context.scene.world or bpy.data.worlds.new("w")
    bpy.context.scene.world = w
    w.color = (.15, .15, .2)
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            bpy.context.scene.render.engine = engine
            break
        except TypeError:
            pass
    bpy.context.scene.render.resolution_x = 1600
    bpy.context.scene.render.resolution_y = 1000
    bpy.context.scene.render.filepath = preview
    bpy.ops.render.render(write_still=True)
