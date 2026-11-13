"""Build the Tycoon art pilot and export it as an FBX for Unity."""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy


def ribbon(name, sections, low, high, color):
    vertices = [
        (x, y, z + offset)
        for x, y, z in sections
        for offset in (low, high)
    ]
    faces = [(0, 2, 3, 1)]
    for index in range(0, len(vertices) - 4, 4):
        a, b = index, index + 4
        faces.extend([
            (a, b, b + 2, a + 2),
            (a + 1, a + 3, b + 3, b + 1),
            (a, a + 1, b + 1, b),
            (a + 2, b + 2, b + 3, a + 3),
        ])
    end = len(vertices) - 4
    faces.append((end, end + 1, end + 3, end + 2))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    obj.data.materials.append(material)
    bevel = obj.modifiers.new("Rounded candy edges", "BEVEL")
    bevel.width = min(.08, (high - low) * .28)
    bevel.segments = 3
    obj.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")
    return obj


def sections_at(points, width):
    for index, (x, y, z) in enumerate(points):
        previous = points[max(0, index - 1)]
        following = points[min(len(points) - 1, index + 1)]
        dx, dy = following[0] - previous[0], following[1] - previous[1]
        length = math.hypot(dx, dy)
        yield [
            (x + sign * width * dy / length / 2,
              y - sign * width * dx / length / 2, z)
            for sign in (-1, 1)
        ]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    args.out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)

    points = [(1.35 * math.sin(math.tau * t), 12 * t, 4 * (1 - t))
              for t in (i / 24 for i in range(25))]
    green = (.36, .83, .043)
    aqua = (.019, .76, .89)
    strip = list(sections_at(points, 3.6))
    ribbon("Flume shell", [edge for pair in strip for edge in pair], -.30, 0, green)
    liner = list(sections_at(points, 3.02))
    ribbon("Aqua liner", [edge for pair in liner for edge in pair], -.025, .025, aqua)
    for index, side in enumerate(("Left", "Right")):
        ribbon(side + " flume lip", [edge for outer, inner in zip(strip, liner)
                                    for edge in (outer[index], inner[index])], -.05, .29, green)
    terrace = list(sections_at([(0, 12, 0), (0, 18, 0)], 7.4))
    ribbon("Red splash terrace", [edge for pair in terrace for edge in pair], -.4, 0, (.9, .045, .09))

    bpy.context.preferences.filepaths.save_version = 0
    # Store a source file alongside a portable interchange model.
    bpy.ops.wm.save_as_mainfile(filepath=str(args.out / "TycoonPilot.blend"))
    bpy.ops.export_scene.fbx(filepath=str(args.out / "TycoonPilot.fbx"), axis_forward="-Z", axis_up="Y",
                             object_types={"MESH"}, add_leaf_bones=False)
    (args.out / "mesh-report.json").write_text(json.dumps({
        "meshes": {obj.name: {"sourceVertices": len(obj.data.vertices),
                              "sourcePolygons": len(obj.data.polygons)}
                   for obj in bpy.context.scene.objects if obj.type == "MESH"},
        "lengthMetres": 18, "fallMetres": 4,
    }, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
