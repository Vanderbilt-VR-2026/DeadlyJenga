# Verification record — October 15, 2026 (America/Chicago)

This is a working implementation with documented gaps, not a claim that the entire original specification is complete.

## Repository inspection

* Worked on `EP_Sprint_2`; did not switch, commit, push, reset, or reorganize game content.
* After verification, at the user's request, transferred the uncommitted implementation to `Evan_Sprint_3`, created from freshly fetched `origin/main` at `7c688d492`. Preserved main's Sprint 2 README additions and existing local files; nothing was committed or pushed during the transfer.
* Unity `6000.3.23f1`; URP 17.3, OpenXR/XR Interaction Toolkit, XR Hands, and Unity Test Framework are installed.
* Multiplayer includes Photon Fusion assemblies and the separate Castle LAN transport/session implementation. Existing Android build tooling is editor-only.
* The currently enabled build scene is `Assets/CastlePrototype/Scenes/CastlePhysicsTest.unity`; Lobby/Main are present but disabled in build settings.
* Existing generated Unity C# projects provide assembly references/defines. Existing tests include the standalone LAN transport suite. No previous architecture tool or GitHub workflow was found.
* GitHub reported `Vanderbilt-VR-2026/DeadlyJenga` as public. Public deployment remains disabled without explicit repository configuration and a manual workflow run.

## Executed checks

| Check | Result |
|---|---|
| .NET 10 build, nullable analysis, warnings-as-errors | Passed, zero warnings/errors |
| JavaScript syntax (`node --check`) | Passed |
| Analyzer/policy/Git regression suite | **19 passed, 0 failed** |
| Recorded random seed 731 | 30 edit/delete sequences; incremental and independent full graphs equal after each change |
| Browser/API security suite on installed Edge, headless | **24 passed** |
| Exported static snapshot on a separate local web server | **4 passed**: real snapshot load, offline Git labeling, local visual persistence, no JavaScript errors |
| Existing Castle LAN transport suite | **15 passed** |
| Unity 6000.3.23f1 isolated batch project | Editor assembly compiled; material reference, nested prefab dependency and content-hash assertions passed; editor exited 0 |
| Git working tree review | Existing tracked game files unchanged; only `.gitignore` and README changed among previously tracked files |
| Source diff whitespace check | Passed |

The 19 regression cases cover semantic overload selection in an explicit compilation context; inferred calls when Unity metadata is absent; static calls, construction, inheritance and events; serialized ownership and nested-prefab references; GUID-preserving moves; signature/inheritance/assembly changes; malformed caches; duplicate GUIDs; scope inheritance/overrides and deleted protected nodes; explicit prohibited/direction/cycle rules; bounded impact traversal; path safety; independent presentation; and actual temporary Git repositories with staged/unstaged/untracked/renamed content, merge bases and confirmed textual conflicts. Tests verify that merge preview does not change the working tree.

The browser suite checks actual repository data, view navigation, search, selection, scope persistence, text-only rendering of malicious annotations, dragging/undo, custom modules, themes, panel behavior, keyboard search, branch comparison and narrow screens. API checks cover cross-origin rejection, mutation headers, traversal and invalid configuration. It creates and deletes a uniquely named temporary Unity asset and waits for automatic watcher updates without using Refresh. The test restores its policy/presentation changes and confirms actual graph contents return to the initial state.

Unity validation used an isolated project under `.architecture/local/unity-validation`, never a second editor instance of the game's working project. The initial sandboxed launch could not reach Unity's licensing client; the permitted licensed launch succeeded. The fixture source is `Tests/ArchitectureMap/Editor/ArchitectureMapUnityChecks.cs`. Logs are local/ignored at `.architecture/local/unity-checks-final.log`. This was batch API validation, **not** a full-game build or NUnit EditMode/PlayMode suite. In-game performance, domain reload while a dashboard is connected, and player-device behavior remain unverified.

## Measured performance

Saved machine-readable reports are in `benchmarks/`. Timings are single local measurements on Windows/.NET 10.0.12, not guarantees or statistical distributions.

| Workload | Result |
|---|---:|
| Current repository graph | 26,304 nodes; 142,207 edges |
| Initial process scan, with persistent source fragments available | 26,345 ms |
| Unchanged inventory/context reconciliation | 612 ms |
| Managed bytes reported after the project benchmark | 828,264,472 bytes (~790 MiB); not peak or retained-memory measurement |
| 100-object Unity YAML fixture: initial / changed-file scan | 70.5 / 8.1 ms |
| 1,000-object fixture: initial / changed-file scan | 24.9 / 18.0 ms |
| 5,000-object fixture: initial / changed-file scan | 264.9 / 205.1 ms |
| 5,000-object fixture: 100 backend searches | 22.4 ms |

All three fixture incremental graphs matched independent full scans. The small first fixture includes cold runtime/JIT overhead; its timings should not be compared as a scaling curve. The project scan is dominated by semantic binding. Changes to C# conservatively rebind the entire code graph; low-latency symbol-level invalidation is not implemented. A five-minute content audit is more expensive than the metadata fast path.

The renderer limits visible nodes, caches graph indexes and aggregated relationships during dragging, and preserves manual positions. Browser interactions were exercised against the full graph, but sustained FPS, accessibility conformance, multi-hour endurance, and memory-leak slopes have **not** been benchmarked. Memory use and whole-code rebinding are significant optimization opportunities.

## Remaining coverage and features

* Effective nested-prefab override expansion, unsaved object state, resolved UnityEvent callbacks, runtime reflection/registration, and full assembly/configuration dependency modeling remain incomplete.
* PR metadata uses normal GitHub REST, but automated mocked pagination/rate-limit/malformed-response tests are not yet provided. GitHub workflows and Pages deployment have not been run remotely; no site was published.
* PR overlap is file-level plus current-graph dependency reachability. Method/hunk/serialized-field overlap, commit authorship history, branch snapshot diff UI and merged-content semantic analysis are not implemented. Simultaneous overlays share a single color.
* Custom modules, independent layouts, notes, colors, shapes, routing styles and undo/redo work. Fonts/icons/sizes, minimap, control-point routing and named layout libraries remain absent. Automatic layout choices were added in the follow-up below.
* Required missing relationships are review warnings under partial analysis. Unexpected dependencies need explicit rules; no architectural expectations are guessed.
* The optional local policy check analyzes worktree state, not an isolated staged-only snapshot. Concurrent edits from multiple dashboard clients use last-write-wins persistence.
* Exact filesystem reconciliation timing, remote authentication, background-tab throttling, private repository workflows, full cross-platform launch, complete accessibility and hostile file-volume limits need broader testing.

See the [guide](README.md) for setup, privacy boundaries, operational limitations, and repeatable commands.

## Layout follow-up

Added explicit view explanations and relevant-node filtering, hierarchy/dependency/grouped/radial/grid layouts, per-view recommendations, depth expansion, and Apply/Cancel preview with undo and locked-position preservation. No actual graph edges are changed by layout. Ten pure layout tests cover determinism, finite coordinates, non-overlap, unchanged input graphs, cyclic dependencies, cyclic malformed containment, direction/rank invariants and locked positions. Seventeen browser checks exercise all layout previews, cancellation, application, undo, persisted preferences, view-specific links, expansion and narrow-screen controls. Commands: `node --test Tools/ArchitectureMap/tests/layout.test.mjs` and `node Tools/ArchitectureMap/tests/layout-browser.cjs` (the latter uses Playwright and the existing local export).

ELK integration validation (2026-10-16): .NET build passed without warnings/errors; 14 existing pure-layout tests passed; 77 browser checks passed using Edge and the production CSP, including ELK flow/hierarchy Apply, worker cancellation, node overlap checks, edge endpoint attachment, containment, and hover isolation. Git comparison regression checks passed. Local server returned HTTP 200 for the bundled worker and adapter. ELK is optional; existing recommended layouts are retained. Wide ELK hierarchy uses a readable minimum zoom and requires panning.

UI simplification validation (2026-10-16): removed the Apply/Cancel layout flow; layout selection and side-arrow cycling now autosave and support Undo. Cycle inclusion is stored in presentation.layoutCycle and verified in the shared-preset payload. Style Studio has four category tabs with preset cards. The header name comes from repositoryName in live state/new exports. Original supplied MP4/PNG assets are bundled locally; video range requests return 206 and static icons return 200. Production build passed with zero warnings/errors. Updated 77-check layout/Git regression suite, 14 pure layout tests, and four snapshot checks passed. Additional UI tests cover branding, playback, autosave, cycling/reload/sharing, tab visibility, pending ELK work, and status transitions.

2026-10-16 design and comparison update: File structure replaces the former label; Custom Architecture navigation is removed. Layout arrows flank the picker. Git has a larger outlined logo button. Edits are explicit drafts with Save view and SVG/PNG/layout-JSON sharing. Six visual themes preserve semantic colors; color representations are separate from visual presets. Historical relationship comparison reads committed asset blobs in isolated temporary directories, compares endpoint/kind counts and retains deleted endpoints. Same-line call occurrences now retain source columns so repeated calls are not collapsed. Build passed without warnings; 20 analyzer tests (including real Git history), 77 layout/Git browser checks, 14 pure layout tests and snapshot checks passed. New UI checks cover draft saving, sharing exports, themes, arrow placement and file/relationship mode separation. Actual repository comparison 7c688d492 → 27c761c44 returned 110 changed relationship groups and 80 endpoints without errors. Coverage is static and does not include compiled Unity/package or runtime-only bindings.

Arrowhead/Dreamchase update: browser checks passed for shape switching, size bounds, arrowhead visibility, Dreamchase animation and reduced-motion suppression, and inclusion of settings in shared preferences. SVG/PNG export and existing UI checks passed. Visually inspected the rendered glow screenshot. Web assets copied to the local Debug server output.

Comparison hang fix verified: build succeeded with zero warnings/errors; all 20 analyzer tests passed, including cancelled-request recovery and identical-revision scan bypass. Browser suite passed, including cancelling an in-flight request without replacing the existing relationship map. Live repository comparison Evan_Sprint_3 vs 7c688d492 completed in 94.7 seconds and returned 110 changed relationship groups without errors; identical revisions returned in 555 ms. Updated server running on port 47831.
