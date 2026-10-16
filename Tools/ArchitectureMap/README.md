# Architecture Map

![The architecture workspace](docs/dashboard.png)

Double-click **Open Architecture Map.cmd** at the repository root. A browser opens after the first scan. Install the free [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) if prompted. There are no NuGet, npm, AI, MCP, account, or paid-service dependencies during normal use. Git enables change comparisons; the map can still analyze assets without it.

In Unity, use **Tools → Architecture Map → Open**. The small editor assembly is excluded from player builds. It exports direct AssetDatabase dependencies while the dashboard is open and pauses work while Unity is compiling, updating, or entering Play mode. Exporting never opens or saves scenes. On macOS/Linux, build and run the CLI below instead of the Windows launcher.

## Explore

Start with the folder-based systems. Double-click to explore scripts or Unity objects; double-click a leaf to read local source. Search finds nodes at any level. Select an arrow to inspect its direction, evidence, source line, and verification status. Group arrows aggregate underlying connections; the inspector explicitly identifies the representative record.

Switch File Structure and Code Relationships to keep separate arrangements. Edit nodes directly in either view. Use the Node list for keyboard-accessible selection. Drag empty canvas to pan, wheel to zoom, and Fit to frame visible nodes. Visible graphs are capped at 240 cards; use search or drill-down to narrow them.

The workspace menu offers light/dark themes, connection routing, relationship filters, descriptions, layout import/export, team presets, refresh, and diagnostics. Node inspectors offer labels, colors, shapes, notes, and position locking. Shift-click selects several nodes; drag them together. Ctrl+Z / Ctrl+Y undo and redo presentation edits. `/` focuses search; Escape closes the inspector. Team presets are checked in; personal overrides are ignored by Git.

### Views versus arrangements

The two tabs show different aspects of the same project, not three versions of the project:

| View | Question it answers | Default automatic arrangement |
|---|---|---|
| File Structure | What contains what? Scenes/prefabs, GameObjects, components | Hierarchy tree; containment links only |
| Code Relationships | What uses what? Scripts, types, methods, dependencies | Dependency flow, left to right |

Use the **layout menu beside Fit** to choose a visualization. Changes take effect immediately as a draft. Save view makes them the saved presentation; Undo restores the previous arrangement. The left and right canvas arrows cycle only through the checked menu items. Checkbox choices are included in exported and shared presentation JSON. Rearrange recalculates the current layout. Locked nodes stay in place in existing layouts; ELK requires unlocking them.

Use **1 level / 2 levels / 3 levels** to reveal more hierarchy without opening each node. Large expanded views may need zooming or drilling down to keep labels readable. Tree layouts hide unrelated dependency arrows; their root/context connector represents navigation, not a method call. Dependency flow preserves directions and groups cyclic relationships at one rank; it is not a guaranteed execution order and cannot turn cyclic code into a strict tree.

## Declare scope and rules

Select a node or group and choose Editable, Protected, or Watch. Child nodes inherit scope unless explicitly overridden; Inherit removes the override. These are warnings, not file permissions. Changed protected paths produce errors; watched paths produce informational findings. Scope is stored in `.architecture/intent.json`. Git changes are checked by path; method-only scope does not prove a specific method was edited.

Select an observed arrow to add a required, prohibited, expected-direction, or acyclic rule. The issue inspector shows the configured rule and exact detected evidence. Remove an acknowledged rule or revise scope intentionally; no changes are reverted automatically. Required relationships missing from partial analysis are warnings, never proof of an error. Cycles and reverse arrows are only errors when explicitly prohibited. You can also edit the intent JSON to describe a relationship that does not currently exist.

Incoming/outgoing impact traversal is configurable from one to five levels. It shows structural reachability, not guaranteed runtime behavior. Impact highlights only currently visible matching nodes; search or drill down to inspect other results.

## Git and pull requests

The Git panel separates index/worktree status, displays upstream ahead/behind counts, accepts a comparison base, and overlays locally available branches using merge-base comparisons. Each branch has its own color and B-number. Choose File changes only or Relationship changes only. Source diffs compare tracked working content with HEAD. Untracked files have no baseline diff.

Refresh GitHub PRs fetches ordinary REST metadata. Public repositories need no token, subject to GitHub limits. For authenticated access set `GH_TOKEN` in the launching process; it is never sent to the browser or written to reports. The tool does not fetch or push branches. Fetch PR commits using your normal Git client, then select the PR overlay. Unavailable commits and network failures are explicit. Remote data is refreshed on request, not continuously synchronized.

Combined-change assessment uses `git merge-tree --write-tree`: it may add Git objects, but never updates HEAD, the index, or working files. A confirmed textual conflict is distinct from same-file overlap and possible dependency impact. Dependency estimates use the **current local graph**, not reanalyzed merged content. Method-level hunk overlap, authorship history, and merged-content architecture analysis remain unimplemented. Historical relationship comparisons are separate from the merge-conflict preview. Export a named folder to retain a snapshot for manual comparison.

## Automatic updates and repair

While the dashboard is open, a filesystem watcher debounces changes for 750 ms. Asset fragments are content-hash cached. C# or assembly context changes conservatively rebind **all C# scripts**, which is slower than symbol-level invalidation but avoids retaining obsolete bindings. Deleted files are removed and incoming unresolved references are reclassified. A 60-second file-inventory reconciliation checks missed events and Unity exports, with a deeper content audit after five minutes. Identical file timestamps and lengths can defer detection until that audit. Cached source fragments are persistent; semantic compilation is rebuilt on launch. Compilation-project changes are picked up on reconciliation. ProjectSettings/Packages configuration graphs and asmdef reference-rule validation are not comprehensive.

The inspector shows scan time, changed files and cache state. An interrupted/failed scan leaves the previous graph with a stale banner. Refresh retries; Full rebuild discards cached analysis while preserving rules and layouts. Invalid rules are preserved and reported for repair. Personal settings live in `.architecture/local/presentation.json`; if this JSON is damaged, rename it to a backup and relaunch. The process stops after 90 seconds without a browser heartbeat. Stop local server shuts it down immediately. Browser background throttling can also cause shutdown; relaunch if disconnected.

## What is authoritative—and what is not

* Unity GUIDs identify assets across moves. Serialized YAML document IDs identify saved objects. Explicit serialized references and component ownership are parsed from Unity's text format; these are never treated as method calls. Nested prefab source links are detected, but effective prefab overrides and unsaved scene hierarchy are not reconstructed.
* When Unity is available, direct AssetDatabase dependencies are imported only when their source content hash still matches. Package/built-in subobjects absent from the local asset catalog remain unknown, rather than being declared broken.
* Roslyn parses declarations and binds calls, construction, inheritance, event subscriptions/unsubscriptions, declared field/parameter/property/return types, qualified member references, and derived type dependencies. Unity-generated `.csproj` files supply compilation references and defines. Their imports, analyzers, generators and build commands are **never executed**. Cross-assembly source symbols are linked by assembly and documentation identity when available.
* Without Unity project metadata (including ordinary CI), fallback semantic analysis is labeled inferred. Compiler errors affecting a binding also downgrade it. Generated code, package source, inactive conditional compilation branches, and external symbols may be unavailable. Virtual/interface calls identify compile-time targets, not the runtime implementation.
* Reflection, runtime registration, dynamically constructed resource names, networking behavior, UnityEvent method-name resolution, general property/field dependency analysis, and removed-interface diagnostics are not comprehensive. No runtime tracing is installed. Unity object references are not behavioral guarantees.
* Type and member IDs combine the asset GUID, assembly, and Roslyn documentation identity. Moving a script with its `.meta` preserves identity. Changing a type name or method signature changes symbol identity. Renaming without retaining `.meta` cannot preserve Unity identity. Duplicate GUIDs produce explicit errors.

The graph is deliberately partial. No unsupported relationship is invented to fill gaps. The UI does not yet provide arbitrary control-point routing, custom fonts/icons/sizes, minimaps, saved named layouts, or per-branch colors. All existing controls act on real data.

## Local data and privacy

| Layer | File | Shared? |
|---|---|---|
| Actual graph and fragment cache | `.architecture/local/actual.json`, `cache.json` | No |
| Intended architecture and scope | `.architecture/intent.json` | Yes, through normal Git review |
| Team visual preset | `.architecture/presentation.json` | Yes |
| Personal overrides, Unity export, logs | `.architecture/local/` | No |

The local server binds only `127.0.0.1`, rejects unexpected Host/Origin headers and cross-site requests, requires a custom mutation header, and uses a restrictive Content Security Policy. It can write only the intent and presentation files. Source previews are limited to text assets inside Assets, with traversal/symlink checks. Source and annotations are inserted as text, never HTML. Like other localhost developer tools, it trusts processes running as your OS user.

Exports contain architecture names, paths, rule/presentation notes, Git status, and evidence metadata. Review them before sharing. They contain no source-file text or authentication tokens. No data is uploaded during launch. GitHub metadata is requested only when you press Refresh GitHub PRs.

## Shared GitHub version

Repository visibility was checked during implementation and was public. Neither workflow has been pushed or executed by this task.

After merging the tool into the default branch, **Actions → Architecture Map** runs on pushes and PR open/update/close events. Download its `architecture-map` artifact, extract it, and serve the directory with a local static server (for example `python -m http.server 8000`). Open `http://localhost:8000`. This uses the same renderer and graph schema. Snapshot architecture and policy are read-only; presentation edits stay in browser storage. Local source, Git comparison and scope mutations require the live local server.

PR analysis uses tooling from the trusted default branch and treats the checked-out repository as data. No PR scripts or Unity-generated builds run. The check has read-only repository permissions and no secrets. Initial workflow bootstrapping requires the tooling on the default branch; the first PR cannot run trusted tooling that has not yet been merged. Policy checking compares paths against the base commit and also checks graph constraints.

Optional public Pages hosting requires deliberate configuration: enable GitHub Pages with Actions, set repository variable `PUBLISH_ARCHITECTURE_PUBLIC=true`, then manually run **Publish Architecture Map (explicit opt-in)** on the default branch. Its guard rejects private repositories. Review the exported data and any existing Pages site before enabling it: deployment replaces that site's content. The expected URL is `https://vanderbilt-vr-2026.github.io/DeadlyJenga/`; it is not enabled by this implementation. Pages publishes a default-branch snapshot, not a continuously synchronized PR dashboard. Private repositories should retain authenticated GitHub artifacts instead.

## Developer commands

From the repository root:

```powershell
dotnet restore Tools/ArchitectureMap/ArchitectureMap.csproj --configfile Tools/ArchitectureMap/NuGet.Config
dotnet build Tools/ArchitectureMap/ArchitectureMap.csproj --no-restore
dotnet Tools/ArchitectureMap/bin/Debug/net10.0/ArchitectureMap.dll --test
dotnet Tools/ArchitectureMap/bin/Debug/net10.0/ArchitectureMap.dll --root . --benchmark
dotnet Tools/ArchitectureMap/bin/Debug/net10.0/ArchitectureMap.dll --root . --benchmark-fixtures
dotnet Tools/ArchitectureMap/bin/Debug/net10.0/ArchitectureMap.dll --root . --export .architecture/local/snapshot
dotnet Tools/ArchitectureMap/bin/Debug/net10.0/ArchitectureMap.dll --root . --check --base main
```

For a local pre-commit check, explicitly add `Check-Architecture.ps1` to your existing hook manager. No hooks are installed automatically. This checks the current worktree against the base, not a staged-only snapshot. Exit 1 indicates deterministic policy violations; exit 2 indicates an unavailable comparison baseline.

Optional browser tests use Playwright as a **development-only** dependency. Start the server, install Playwright in your testing environment, then run `node Tools/ArchitectureMap/tests/browser.cjs`. Set `ARCHITECTURE_BROWSER=msedge` to use installed Edge or install Playwright Chromium. Tests restore rules/personal presentation after exercising the UI. `ARCHITECTURE_SCREENSHOT` optionally captures the final view.

On Windows, `Tools/ArchitectureMap/tests/Run-BrowserTests.ps1` starts and stops an isolated test server automatically. After exporting to `.architecture/local/export`, `node Tools/ArchitectureMap/tests/snapshot.cjs` verifies the static shared renderer with no live API.

See [verification results](VERIFICATION.md) for actual measurements, tested coverage and remaining gaps. The automated suite is meaningful coverage, not a claim that every item in the implementation brief is complete.

API references: [Roslyn semantic model](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.semanticmodel), [Unity AssetDatabase dependencies](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.GetDependencies.html), [GitHub untrusted-workflow guidance](https://docs.github.com/en/actions/reference/security/secure-use).

### Editing, themes, and sharing

There is no separate Custom Architecture tab. Node positions, labels, shapes, colors, and notes are edited in File Structure or Code Relationships. Changes remain a draft until **Save view**; the small toolbar indicator shows unsaved changes. Saving also remembers the active view, drill-down and camera. Undo/Redo works for presentation edits. Scope/rule changes are separate shared architecture policy and are not staged by Save view.

The arrows immediately beside the layout dropdown cycle through its checked entries. The list and six visual themes are part of the shareable presentation JSON. Style Studio has **Visual presets** (Studio, Retro Desk, Neon Lab, Terminal, Cozy, Blueprint Paper), **Color representations**, and an **Edit…** menu for Nodes, Arrows, and Sharing. Visual themes preserve semantic colors while changing textures, corners, contrast, fonts and shadows.

**Share** exports the current diagram as SVG or PNG (up to 4096 pixels on its longest side), including draft styling and branch legends. Editable layout JSON is for reopening on the same repository; it does not embed source code or an entire historical graph. Set as team default writes shared presentation JSON; commit and push it to distribute it. Personal overrides take precedence. Temporary historical comparison maps can be shared as images; clear the comparison before saving a normal view.

### Relationship changes in Git

**File changes only** compares paths from the common ancestor to each branch. **Relationship changes only** reconstructs each revision from Git blobs, runs static analysis separately and compares source/target/type counts. It does not use changed-file highlighting as a substitute. Comment-only or line-number edits are ignored. Added arrows are solid with + labels, removed arrows dashed with − labels; B-numbers and colors identify branches. Removed endpoints are retained. Hovering a branch isolates its arrows. At most 240 endpoint cards are displayed; the expandable results include every detected change.

Historical analysis reads committed source and serialized Assets only. It never checks out branches, executes scripts, builds projects, or loads historical binaries. Compiled Unity/package references, Unity AssetDatabase exports and runtime-only relationships are unavailable, so some calls cannot be resolved. No detected changes is not proof of identical runtime behavior. The common-ancestor and head commit IDs and coverage caveat are shown with each result. Missing commits and unsupported source LFS pointers report an unavailable comparison, never a successful empty diff. Baked LightingData is omitted as opaque binary data. Extraction is bounded to 60,000 entries, 32 MB per analyzed file and 256 MB total; four graph snapshots are cached in memory. Initial scans may take minutes. Fetch commits with your Git client first.

### Layouts and navigation

ELK runs locally in a worker and controls both node placement and connection routing. It uses visible nodes only, with a 30-second layout timeout. Unlock displayed nodes before choosing ELK. Switch to another layout for manual dragging. Dense graphs may still need depth limits and hover/selection filtering. The indented hierarchy remains useful for many siblings; wide ELK hierarchies keep readable zoom and may require panning. Vendor notices are in THIRD-PARTY.md and ELK-LICENSE.md.

### Branding and status

The header reads Digital Architecture Map and uses the repository root folder name. Green animation means connected/current with no reported issues; yellow means checking/refreshing, warnings, or an offline snapshot; red means detected changes pending refresh, errors or disconnection. Click the icon for details. Reduced-motion pauses the supplied local MP4 animation. Status does not certify game correctness.

### Arrowheads and directional glow
Open **Style studio → Edit… → Arrows → Connections** to choose head shape, base size (2–24 graph units), fixed or record-count sizing, and Dreamchase travel time. Shapes are independent of line thickness; zoom scales both. Auto maps inheritance to a hollow triangle, containment to a diamond, references to an open chevron, subscriptions to a circle, and other relationships to a filled triangle. These are diagram conventions, not strict UML. Expand **By relationship type** to override shapes or effects for a kind.

Dreamchase sends a bright pulse from source to target. Auto applies it to calls. It illustrates static relationship direction, not observed execution or traffic. Inferred evidence remains dashed underneath. Reduced-motion hides moving pulses; indented hierarchy and Git comparisons use plain connectors. Settings participate in Undo, Save view, and shared layout exports.

Relationship comparison responsiveness: requests now have a two-minute cancellation budget including queue time, propagated through historical extraction and Roslyn analysis. Identical Assets trees return immediately without scans. The UI compares branches sequentially, shows branch progress and elapsed time, provides Cancel, aborts superseded/closed comparisons, and imposes a 135-second per-request fallback deadline. Cancellation preserves the displayed map; timeouts never display partial results as a successful comparison.

## Get only the visualizer on another game branch

To install the standalone visualizer for the first time without switching or merging game branches, run from the repository root:

```sh
git fetch origin Evan_Sprint_3
git restore --source origin/Evan_Sprint_3 -- Tools/ArchitectureMap "Open Architecture Map.cmd"
```

These paths contain the tool and launcher only. This replaces existing files at those tool paths, so preserve any local visualizer edits first. It does not copy scenes, game scripts, packages or ProjectSettings. The tool creates default architecture settings if absent. Add `.architecture/local/`, `Tools/ArchitectureMap/bin/`, and `Tools/ArchitectureMap/obj/` to your local `.git/info/exclude` if your branch does not already ignore them. The .NET 10 SDK is required; Git enables branch comparisons. Open the launcher to build and run the map. Unity can remain closed; without Unity-generated metadata, some relationships are labeled inferred.

The optional `Assets/ArchitectureMap` editor integration adds the Unity menu and dependency export. It is already included on Evan_Sprint_3, but is not required for the standalone launcher. Importing only the tool does not install that integration. To update an existing Evan_Sprint_3 checkout, use your normal pull instead; pulling a whole branch includes everything on that branch, not a selectable subset of files.
