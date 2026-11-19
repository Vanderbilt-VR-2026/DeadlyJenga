# Crown Courtyards and Harbor Exchange

This checkpoint combines the MapLab source candidate `44932d37136d55ba12a3ea8ccd3602e124b5ba1d` with native Unity-authored match layout assets. The geometry and matching tokens must travel together.

- Crown Courtyards (`mode-ctf`) has 55 loose physics bodies on an 88 by 64 footprint, with ground routes and an upper level at 4.4 metres.
- Harbor Exchange (`epic-double-loop`) has 14 compound bodies and six pavilion sites on a 120 by 104 footprint. Two upper crossings supplement the ground loops.
- Each map has ten free spawns, two teams of five spawns, flag homes and a hill anchor. Existing Christmas Candy Town layout data is unchanged.
- The room content discriminator changes so older geometry or equipment protocol builds cannot silently join this version.

## Native authoring evidence

Run `DeadlyJenga.DevTools.Editor.CuratedMapRouteChecks.Inspect` explicitly in the Unity editor to reproduce the structural inspection. The November 5 native report passed with zero failed assertions, the expected 55/14 body counts and both complete match-layout records. A guarded authoring tool used the actual imported prefab SaveLayout strings to create exactly the two new assets; their readback tokens and GUIDs matched.

Crown layout GUID: `f424358e4c46e744db95bb13c7e0fa10`. Harbor layout GUID: `735d3ebd2a68dbb419b90078555501b3`. The native World assembly MVID was `113cbc6b-347a-4d53-9152-0931383393ed`. Inspection report SHA256: `f525a924020ef056841dacf171889d6726411db22f82bdd104019a8ab077c010`; pair-authoring receipt SHA256: `4de116999092a5a5d793c41435e838d93b55aba7472408a601680d321ca315c6`. Only empty importer-value trailing spaces were normalized during integration; asset payloads and GUIDs were preserved.

## Acceptance still required

This is an integrated source checkpoint, not shipping-map acceptance. Actual player routes, capsule/spawn clearance, grounded carrier trips, bot execution, settling, support removal, objective recovery and map reset still need gameplay tests. The upper-ramp seams need direct movement testing. Matched current screenshots and same-version two-peer checks remain pending. No physical Quest test is claimed. Other experimental designs are not promoted by this checkpoint.

The first native low-route tests on source `60b0c2b5dcf46665eeaf582985e3e38e25ca3fec` passed structural, spawn and disaster-setup checks, but neither completed pickup/capture/reset. Crown reached all eight route points and stood within 0.18 metres horizontally of the enemy ground home without picking up its flag. The support probe searched downward from above the entire world, selecting the roof over that home. It now probes below the current home, preserving its intended floor and existing downward/water recovery. A 113-source compile and 16 extracted-method assertions passed; actual pickup, raised-home and destruction regressions remain pending. The old native trace did not record the flag's exact height.

Harbor's first test drifted away from its authored street before stalling; its steering and the large pale visual strips seen in the actual overview need diagnosis. Those failures are retained, and the map is not promoted to shipping-ready status.
