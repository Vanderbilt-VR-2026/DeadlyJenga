# Building physics on Underneath the Tree

The map builds the wizard tower, superhero base and mansion from the existing Blender Piece sections and Hull collision meshes. Existing modes start with independent sections. To start all three as compound bodies, enable **Start Buildings Together** on the UnderneathTreeMap prefab before loading the map. In Play Mode the UnderneathTreeBuilding component on each placed landmark has commands to group a pristine building, enable breakaway sections, or retire and demolish it. A rebuild restores the starting setup.

Each shell adds one stable slot to the map block list. The host switches the active body slots. Clients use those slots to bind the same section visuals, lights and colliders. No new bodies are added to the network list during a collapse. Sections inherit the moving shell's point velocity and spin. Switching a stationary intact shell to breakaway arms existing support and impact behavior. Demolish releases all remaining sections immediately. Reassembly of displaced pieces requires a map reset.

The wizard terrace has four storeys, the mansion has four, and the superhero base has six. The capture section and exact walkable hull are designated on each toy prefab. Keep their names through Blender export or update the prefab fields. Furniture baked into a storey stays with it. Independent furniture needs its own physics piece.

Only the authoritative map can change a building's physics mode or report CanScore. A scripted wipe calls Demolish at the end of its warning. For ordinary damage, a deleted capture floor immediately retires the objective. Two continuous seconds beyond 45 degrees of tilt, outside the actual rug footprint, or more than half a world unit below the rug surface also retire it. Contact by other floors or the roof has no effect. The geometric test does not determine whether a route into the room has been blocked by rubble.

Retirement lasts for this map instance. A future control mode should save the three retired bits in its authoritative round state for late join and host migration, and reapply them to the map on takeover. Scoring, the timed round transitions and the tickets phase belong to that mode. The current setup is World-side and can be exercised without a scoring mode.

Desktop fixture: DeadlyJenga.CastlePrototype.Editor.UnderneathTreePlayChecks.CheckBuildingModes.
