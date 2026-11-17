# #3DBenchy toy boats

Source: official #3DBenchy by Creative Tools (https://www.3dbenchy.com), CC0 1.0 public domain.
Files: CreativeTools/3DBenchy on GitHub, Multi-part/Dualprint STLs (white: hull, box, bridge walls, rod holder, chimney; red: gunwale, deck, plate, wheel, frames, roof, chimney top).

benchy_export.py (Blender 5.3, background): voxel remesh 0.45 mm, decimate, convert to Unity axes, write Presentation/Resources/CandyScenery/BenchyWhite.bytes and BenchyRed.bytes.
Run: blender -b --factory-startup --python benchy_export.py -- <folder with white.stl and red.stl> <output folder>
