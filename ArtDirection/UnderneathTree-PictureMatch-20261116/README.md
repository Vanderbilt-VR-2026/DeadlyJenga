# Underneath the Tree: matching the approved picture

`approved-reference.png` is the picture Evan approved. `concepts/` holds four companion
pictures made from it (fireplace wall, left wall, player height, top-down plan). They are
proposals for the sides the approved picture never shows and are not approved yet.

For the approved in-game RGBY lighting, see the [bulb pool tuning guide](LIGHTING.md).

## Layout lab

The gift layout is data, not hand-typed C#.

1. Edit `layout-lab/design.py` (every gift: position, size, turn, colour, what it rests on).
2. `python design.py` prints the checks: overlaps, unsupported gifts, gifts off the rug,
   gifts in the train's way. All four lists must be empty.
3. `bash go.sh` renders a grey-box from the approved picture's camera with Blender and
   writes `out/side.jpg` next to the picture.
4. `python export.py <repo>/Assets/_DeadlyJenga/World/Maps/Scripts/UnderneathTreeMap.LayoutData.cs`
   writes the table the map builds from.

Course frame: x = right (east), z = back (north), origin = middle of the train loop.
The map places that origin at room position (-74, -110) and enlarges the course 1.9 times.

## Seeing it in Unity

`Tools > Deadly Jenga > Underneath the Tree > Open Lookdev` loads the map in a solo demo.
`UnderneathTreeLookdev.ShootAll("Logs/TreeLookdev/name")` saves twelve fixed views: the
approved picture's camera, the room from three other sides, and seven player-height views.
