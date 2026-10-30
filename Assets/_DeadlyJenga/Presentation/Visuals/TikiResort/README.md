# Waterfront Tiki Resort

Select **Demo → Waterfront Tiki Resort** in the lobby, then launch. Amber Cantina remains separately available. The island lobby is still the entry scene.

This is an independent reimagining of the cantina layout: an open teak deck, coral/turquoise/cream painted timber, six thatched resort cabanas, an open tiki bar, sunset music stage, lagoon drink dispensers, palms, festoon lanterns, beach loungers, parasols and a waterfront pier. The original bar, seven seating groups and twin-tank landmark informed the arrangement.

The map uses the same 0.5 root scale as Amber Cantina to match the existing avatar. The central deck is approximately 14 × 10.5 m; the sand island is approximately 32 × 27 m, with a roughly 12 m waterfront pier. Walking collision belongs to World; visual scenery and lighting belong to Presentation. Major posts, furniture, stairs, shelter roofs, rail guards, sand and pier surfaces have static collision.

The variant owns its editable materials and new assets. Unchanged base meshes are reused read-only; original cantina prefabs, materials, textures and code are preserved. No Core, package, XR or global project setting changes are required.

The resort sky, sun and lagoon are scoped to the map lifetime. Outside island art is hidden while this environment is active and restored when it is removed. Ocean placement follows the map transform. VR limits local accent lights to four, in addition to one sun. Opaque static scenery is combined by material, while clear surfaces and signs remain separate. These are budget precautions, not measured Quest acceptance.

The separate `Waterfront Tiki Resort Preview` scene is for review. Explicit authoring and traversal-polish tools live under Tools → Deadly Jenga → Maps. Actual Game-scene validation and six game-camera captures are produced by the Tiki Runtime check under Validation; reports and captures go to Logs/Validation. It preserves saved scene setup and match choices and refuses to replace unsaved scenes.

Demo only: cabana signs are scenery, with no host/join or level-selection actions yet. This is a basis for a later resort lobby, not a replacement lobby. Quest performance, stereo appearance and physical controller acceptance still need an actual device test.
