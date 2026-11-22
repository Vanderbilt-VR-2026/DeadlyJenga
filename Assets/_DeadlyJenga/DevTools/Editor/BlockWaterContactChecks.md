# Castle water and sand contact handoff

The source branch is `codex/marina-block-contact`. The integration target is
`origin/Evan_Sprint_4` at base `cdac1fd7fbb801c615808593588d25469c26e1ba`.
The latest tested source is `d674e5afd3ac7f0ab92f60c5c574d1937ad72685`.
MAIN owns integration.

World/Maps slows Castle blocks as the bottom reaches open water, preserves
shallow static support, and retires a block after its top is 3 m below the
surface. Presentation/Visuals deforms the generated sand mesh, sizes observed
splashes, and applies water filtering and deep concealment to the active view.
The sand-coating art and sand-engulf HUD are owned separately. There are no Core,
network wire, package, scene, or settings changes.

## Reproduce in Unity 6000.3.23f1

Run the chosen static entry point in an isolated batch editor with graphics on.
All methods are on `DeadlyJenga.DevTools.Editor.BlockWaterContactChecks`. The
fixture owns batch exit. Results are written under `Logs/Validation`.

| Entry point | Tested revision | Result |
| --- | --- | --- |
| `Run` against the original physics | `9398461dc236675a199ad054aa96de773de9faa5` | 16 expected failures across two rounds. The 40 m/s drops were fully underwater before drag began. |
| `Run` with fixed-step entry | `1327ba3dd5d61d4cd11bbc92f86e2ae166475a79` | 27 checks passed. Both sizes entered at about 0.98 m/s, straddled the surface for 0.26 or 0.80 s, and retired near 3 m. The shoreline shelf stayed dry. |
| `RunSand` | `306ac9031d239d8d7136c5946d269607ff5fdc3b` | 20 checks passed in isolated Play Mode. Single bursts of 36 and 102 droplets, overlapping terrain depressions and ripples, restored terrain after hazard-state interruption and retirement, and rendered depth/surface checks with a nearby red marker. |
| Initial `RunGameViews` | `ba6c3aabe786ccb30485f191db4f824038d80568` | Real bot contact, coating threshold and fade passed. The normal match camera handoff invalidated the injected survivor and hand view checks. The run also recorded a separate Unity assistant relay timeout. |
| Revised `RunGameViews` | `bcf8a4de10b53e8d2cca3c7fadfaa1fe11ccfd98` | Normal above-water fallback, a dry 12 m landing, and bot contact and fade passed. The death view from the first scenario persisted into the camera staging and invalidated both role checks. |
| Ordered `RunGameViews` | `d674e5afd3ac7f0ab92f60c5c574d1937ad72685` | 19 checks passed on Castle with 568 blocks. Both real role cameras passed controlled dry, shallow, under-map and resurfaced render samples. The normal match camera guard passed. The survivor landed from 12 m with the eye at least 0.73 m above water. The 0.4 s bot contact stayed clean. A 1.15 s contact added the lower-body coat, which dusted off and cleared around 20 s. |

Matched gameplay captures and physical Quest checks remain open. The render
assertions use a color marker to detect how near scenery is filtered or hidden.
Splash timing is sampled from local poses. An
optional `IBlockSurfaceContactSource.TryGetLastContact(Rigidbody block, out BlockSurfaceContact contact)`
could provide exact contact type, sequence, position, footprint and incident
speed. Guest bodies would also need a protocol for that contact data.

## Log-flume whitewater and dry sand

Follow-up branch: `codex/marina-whitewater`, based on `ed687f49a3dd96b0c3c839cae046df41ed84838e`.
Production source: `4d84b914787eea78d0b056c031fa89f19ccfbaf3`. Integration target
and owner are unchanged.

Presentation/Visuals adds pooled 3D spray sheets, droplets and lingering foam.
The base ocean clips beneath the original dry island surface, while the deforming
sand uses more mesh resolution around the play area. The follow-up adds no Core
contract, gameplay physics, or scene changes.

Desktop Castle Demo in Unity 6000.3.23f1 completed with the capture-only fixture
`94cbabb8c547cbfad418bfd7ba32fd4c32e1500a` on top of the production source. It
used real blocks, controlled releases, a fixed camera and temporary support for
the sand setup. The ordinary and fast water entries showed 74 and 112 live
droplets and active whitewater meshes. The sand run observed two sinking blocks
and five live ripple particles. Frame review confirmed a dry dent with the ocean
still visible offshore. Fresh role-camera, multiplayer and Quest checks remain
open for this follow-up.

## Breaking whitewater follow-up

Production source: `996b777f8ec6000645bc114ff7014d0d448b9a59` on
`codex/marina-whitewater`. The desktop capture loaded that source plus the
Editor-only fixture at `bdd47ed29b8b2ff11d7e1118358e4ea3d9afd5fe`.

The same Castle Local Demo cameras and controlled releases verified the low
outward skirt, the taller breaking crests, spray and mist. The ordinary drop
produced 106 live droplets and 27 mist particles. The fast drop produced 158
droplets and 36 mist particles. The previous source produced 74 and 112 live
droplets in the same setups. The fast crest extended past the top of the
matched view, and the ordinary view captured the full crest. The batch exited
successfully with both sink sequences recorded.

The capture fixture printed a stale gameplay SHA in its banner. Checked
admission and the slot Git HEAD verified the exact fixture before launch.
The change was limited to splash presentation. Quest appearance and
performance remain unverified.

## Combined Sprint 4 desktop acceptance

On base `1359ca474fb8a0bc86cdd72dca7dab5e3e631a71`, desktop Castle Local
Demo passed `RunGameViews` at `4acb59d81880d02a953102e54cb608a38ff20b37`
with zero failed assertions. Production water source is
`9027fb1228bf73926146e9fd09682b86c15acbea`. Subsequent changes through
the tested revision update the Editor fixture alone.

The fixture holds the two real active cameras away from moving parents for
the controlled dry, shallow, deep and resurfaced renders. It restores their
local transforms before the 12 m shore drop. The survivor landed on the
chosen support with 0.77 m water clearance and a 0.80 m eye-to-feet offset.
The normal match view handed off to the above-water camera.

The bot used the real shore. It settled on dry support after both contacts.
A sampled 0.358 s contact left no coat. A sampled 1.363 s contact applied a
full lower-body coat. It faded to 0.594 coverage by 18.52 s and was removed
by 20.51 s. These are controlled desktop checks with a staged transfer to
dry support. Quest and guest replication remain unverified.
