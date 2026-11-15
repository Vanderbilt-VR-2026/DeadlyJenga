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
