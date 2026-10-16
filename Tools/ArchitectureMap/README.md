# Architecture Map

![The architecture workspace](docs/dashboard.png)

Double-click **Open Architecture Map.cmd** at the repository root. A browser opens after the first scan. Install the free [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) if prompted. There are no NuGet, npm, AI, MCP, account, or paid-service dependencies during normal use. Git enables change comparisons; the map can still analyze assets without it.

In Unity, use **Tools → Architecture Map → Open**. The small editor assembly is excluded from player builds. It exports direct AssetDatabase dependencies while the dashboard is open and pauses work while Unity is compiling, updating, or entering Play mode. Exporting never opens or saves scenes. On macOS/Linux, build and run the CLI below instead of the Windows launcher.

## Explore

Start with the folder-based systems. Double-click to explore scripts or Unity objects; double-click a leaf to read local source. Search finds nodes at any level. Select an arrow to inspect its direction, evidence, source line, and verification status. Group arrows aggregate underlying connections; the inspector explicitly identifies the representative record.

Switch Unity Structure, Code Relationships, or Custom Architecture to keep separate arrangements of the same facts. Select nodes and choose **Create visual module from selection** to group them conceptually in Custom Architecture. This changes presentation only. Scope actions on a visual module apply to its members explicitly; actual hierarchy groups use inherited scope. Use the Node list for keyboard-accessible selection. Drag empty canvas to pan, wheel to zoom, and Fit to frame visible nodes. Search results are capped at 240 visible nodes, with the total shown; narrower searches expose the remaining nodes.

The workspace menu offers light/dark themes, connection routing, relationship filters, descriptions, layout import/export, team presets, refresh, and diagnostics. Node inspectors offer labels, colors, shapes, notes, and position locking. Shift-click selects several nodes; drag them together. Ctrl+Z / Ctrl+Y undo and redo presentation edits. `/` focuses search; Escape closes the inspector. Team presets are checked in; personal overrides are ignored by Git.

### Views versus arrangements

The three tabs show different aspects of the same project, not three versions of the project:

| View | Question it answers | Default automatic arrangement |
|---|---|---|
| Unity Structure | What contains what? Scenes/prefabs, GameObjects, components | Hierarchy tree; containment links only |
| Code Relationships | What uses what? Scripts, types, methods, dependencies | Dependency flow, left to right |
| Custom Architecture | How do we want to explain or group this project? | Grouped columns for your modules |

Use the **layout menu beside Fit** to choose a visualization. Changes take effect and save immediately; Undo restores the previous arrangement. The left and right canvas arrows cycle only through the checked menu items. Checkbox choices are included in exported and shared presentation JSON. Rearrange recalculates the current layout. Locked nodes stay in place in existing layouts; ELK requires unlocking them.

Use **1 level / 2 levels / 3 levels** to reveal more hierarchy without opening each node. Large expanded views may need zooming or drilling down to keep labels readable. Tree layouts hide unrelated dependency arrows; their root/context connector represents navigation, not a method call. Dependency flow preserves directions and groups cyclic relationships at one rank; it is not a guaranteed execution order and cannot turn cyclic code into a strict tree.

## Declare scope and rules

Select a node or group and choose Editable, Protected, or Watch. Child nodes inherit scope unless explicitly overridden; Inherit removes the override. These are warnings, not file permissions. Changed protected paths produce errors; watched paths produce informational findings. Scope is stored in `.architecture/intent.json`. Git changes are checked by path; method-only scope does not prove a specific method was edited.

Select an observed arrow to add a required, prohibited, expected-direction, or acyclic rule. The issue inspector shows the configured rule and exact detected evidence. Remove an acknowledged rule or revise scope intentionally; no changes are reverted automatically. Required relationships missing from partial analysis are warnings, never proof of an error. Cycles and reverse arrows are only errors when explicitly prohibited. You can also edit the intent JSON to describe a relationship that does not currently exist.

Incoming/outgoing impact traversal is configurable from one to five levels. It shows structural reachability, not guaranteed runtime behavior. Impact highlights only currently visible matching nodes; search or drill down to inspect other results.

## Git and pull requests

The Git panel separates index/worktree status, displays upstream ahead/behind counts, accepts a comparison base, and overlays locally available branches using merge-base comparisons. Multiple overlays are retained, currently using one gold outline style. Source diffs compare tracked working content with HEAD. Untracked files have no baseline diff.

Refresh GitHub PRs fetches ordinary REST metadata. Public repositories need no token, subject to GitHub limits. For authenticated access set `GH_TOKEN` in the launching process; it is never sent to the browser or written to reports. The tool does not fetch or push branches. Fetch PR commits using your normal Git client, then select the PR overlay. Unavailable commits and network failures are explicit. Remote data is refreshed on request, not continuously synchronized.

Combined-change assessment uses `git merge-tree --write-tree`: it may add Git objects, but never updates HEAD, the index, or working files. A confirmed textual conflict is distinct from same-file overlap and possible dependency impact. Dependency estimates use the **current local graph**, not reanalyzed merged content. Method-level hunk overlap, authorship history, historical graph diffs, and merged-content architecture analysis remain unimplemented. Export a named folder to retain a snapshot for manual comparison.

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

### Style studio and navigation

Use **Style studio** in the bottom toolbar to apply a visual language to the entire diagram: node types, hierarchy, system families, connection hubs, change scopes, or quiet monochrome. Color, shape and size mappings are independent. Hierarchy size represents containment depth; hub size represents the number of visible aggregated connections, not execution order or architectural authority. The legend explains the current encoding. Presets retain explicit node overrides; use **Clear node appearance overrides** to return those nodes to automatic styling.

The studio also controls typography, node size, border weight, line routing, relationship colors, line thickness/opacity, labels and arrowheads. Bulk color/shape edits target all nodes, visible nodes, selected nodes or a node type. Type overrides also apply to subsequently discovered nodes of that type; all/visible/selected edits apply to current nodes. Undo/redo and personal persistence cover styling. Save as team preset explicitly to share via Git; personal overrides still take precedence.

**Back** is above the diagram beside clickable **Project › parent › current** breadcrumbs. It returns one containment level; Alt+Left does the same. Project returns to the root. Back is disabled at the root.

### Adaptive styles and relationship readability

**Adaptive overview** assigns distinct system colors at the folder level and type colors below it, with size based on visible connectivity. System colors follow original ancestry, ignoring the generic Unassigned / Review classification. Presets clear explicit node fill/shape overrides; Undo restores them. Kind/depth encodings intentionally show identical colors for identical values.

Connections support plain, dashed, flowing, glowing and tapered effects globally or per relationship kind. Auto maps calls to directional flow, subscriptions to glow and inheritance to taper. Inferred evidence stays dashed, and reduced-motion disables animation. Count-based thickness uses logarithmic scaling of aggregated static relationship records; it does not measure runtime traffic or architectural importance.

**Focus connections** isolates the selected neighborhood; click a node to show its incident connections, or click the button again to restore all. The studio also offers strongest 30 and minimum-record filters, with shown/total counts. Expand the small About this control notes for explanations. Failed saves remain pending in the current tab and retry after reconnection; keep that tab open until saving succeeds.

### Face snapping, auto gain, and guided Git

Arrows recompute attachment sides whenever nodes move. Incoming and outgoing connections share distributed face ports, including reciprocal links. Rounded/pill boundaries are respected. Arrowheads stay fixed at 8 graph units regardless of weighted line thickness. Dense diagrams can still cross; use Focus connections when many links share a small node.

Node size gain and Arrow thickness gain default to auto. Node gain applies to connectivity sizing; arrow gain applies to count weighting. Both normalize logarithmically against the current projected graph’s 90th percentile. Nodes remain within their layout cell and arrow thickness is capped at 6 graph units. Navigation and graph updates recalculate gain. Clutter filters retain the same scale so selecting a neighborhood does not suddenly amplify its links. Fixed gain remains available.

The Git button is labeled explicitly. The guided panel provides a starting branch, branch with changes, Compare and highlight, clear overlay, and an interactive illustrative example. Comparisons highlight file paths on the current graph, not a historical branch graph. Local changes, overlap checks, and GitHub PRs have separate expandable sections. Remote-tracking branches reflect the last fetch.

### Connection-aware arrangements

Hierarchy layout now allocates contiguous subtree spans and never wraps siblings into multiple rows. Dependency flow uses alternating neighbor-order sweeps and wider rank spacing to reduce crossings. Face ports are sorted by the other node’s position. Tree links use orthogonal routing; orthogonal routes search nearby corridors around intervening nodes. Labels follow the route midpoint. This is a readability heuristic, not a guarantee of zero crossings; dense graphs and locked placements can still need focus filtering. Wide trees deliberately use more canvas space. Choose a layout or click Rearrange to replace the current positions; Undo restores the prior arrangement.

### Recommended section overview

Unity structure at one level now uses responsive section cards when Recommended is selected. The heading and breadcrumbs identify the shared parent, so redundant parent arrows and the synthetic context node are omitted. Cards show direct child counts, use consistent readable sizing, and automatically fit above the toolbar. Single-click opens details; double-click explores. This mode arranges cards automatically; choose an explicit layout to drag positions or show hierarchy links. Two/three-level views still show the hierarchy. The diagram legend is collapsed by default in graph views and hidden in the section overview.

### Hover isolation

Hover a connection to hide all other connections, including their labels, arrowheads, glow and taper segments. Hover a node to retain only its incoming/outgoing connections. Leaving the hovered object or canvas restores the currently filtered set. Thin arrows have a wider invisible pointer target; clicking still opens relationship evidence. Hovering does not change saved styling or graph data.

### Current Unity hierarchy behavior

Recommended and Hierarchy tree now use an indented vertical outline with fixed readable node dimensions. The former automatic card overview is disabled. Original auto positions are replaced on entering this hierarchy; explicit position locks remain honored. Parent rails run beside children, containment labels/arrowheads are omitted, and relationship hover isolation still works. Scroll browses the hierarchy; Ctrl+wheel zooms. Fit returns to the readable top instead of shrinking the whole tree. Other diagram layouts retain their existing zoom/fit behavior.

### Multi-branch visual comparison

The Git panel accepts up to eight branches against one starting branch. **Use open pull requests** populates those inputs from currently open GitHub PRs (up to eight); it does not fetch missing commits. **Compare and highlight** opens Unity structure in grouped columns, initially showing changed sections. **Show unchanged sections** in the floating legend restores context.

Changed descendants highlight their containing sections. Each branch has a distinct color and B-number. Nodes changed by multiple branches retain separate labeled outlines. Nearby nodes share an enclosing glow only when their complete branch membership matches and the region contains no unrelated nodes. Hover a colored outline or a legend entry to hide other branch highlights and isolate related arrows. Leaving restores them. Local uncommitted-change outlines are suppressed during branch comparison to avoid mixing meanings.

Per-branch file lists are collapsed by default. They also report changed paths with no node in the current graph, including deleted/non-Unity files. Comparison highlights the current graph, not a reconstructed historical branch graph. Missing local PR commits are reported per branch, while successful comparisons still display. Comparisons do not switch, fetch, push, or merge branches.

### Help and panel refresh (current behavior)

Inline help now describes actual scope: visual Undo does not revert architecture rules, scope declarations, Git selections, or a shared preset write. Hierarchy-specific fixed card sizing, text size, plain rails, hidden labels/heads, scrolling and Back to top are explained explicitly. Gain is in graph units and scales on zoom; dashed styling alone is not proof of inferred evidence. All-node and type bulk defaults also apply to newly discovered nodes. Import/export, reset, scan actions and team sharing have separate explanations.

Style studio uses collapsible sections for presets, node appearance, connections, per-relationship overrides, bulk edits, and reset/sharing. Each relationship type has a nested disclosure. Node inspectors group scope, impact, presentation and relationships. Workspace settings group display/navigation, relationship visibility, personal/team layout, scan controls and diagnostics. Open/closed sections are remembered during the current page session.

The Git panel now separates setup, actions, results and advanced tools. Compact branch rows show the same B-number/color as displayed results; unavailable branches have no active color. Compare remains in a sticky action strip. PR population and Add branch are adjacent. Example, local changes and overlap checks are below the primary workflow. Changed file lists remain collapsed and partial comparison failures retain the successful branch highlights.

### ELK layouts

Use the bottom layout menu → **ELK · Dependency flow** or **ELK · Hierarchy**, and it takes effect immediately. Undo restores the prior arrangement. Existing layout choices remain available. ELK runs locally in a worker with a 30-second timeout and uses the visible graph (up to 240 cards), not the entire project graph.

ELK controls both positions and orthogonal connection paths, with separate connection ports and label space. It recalculates when visible nodes, relationships, or node dimensions change. Hover isolation, multi-selection, relationship inspection, styling and Git highlights remain available. ELK layouts cannot be manually dragged; choose another layout for manual placement. Unlock visible nodes before using ELK. Dense graphs still benefit from Focus connections, reduced depth and drill-down; no engine guarantees crossing-free diagrams. A wide hierarchy may require zooming/panning; the existing indented hierarchy remains available for reading many siblings.

Vendor attribution and license: THIRD-PARTY.md and ELK-LICENSE.md.


### Branding, status, and Style Studio

The header reads Digital Architecture Map and uses the repository root folder name beneath it, also stored in new static exports. Supplied green animation loops locally as MP4; yellow/red use the supplied static icons. Green means connected/current with no reported issues; yellow means checking/refreshing, warnings to review, or an offline snapshot; red means detected changes awaiting refresh, errors, or a disconnected service. Click the icon for the reason and Refresh now. Reduced-motion preference pauses the animation. The status does not certify game correctness.

Style Studio separates Presets, Nodes, Arrows, and Sharing. Changes save automatically for you; use Undo to reverse them. Sharing a team preset writes presentation JSON, including layout-cycle choices; commit/push that file to distribute it. Personal overrides continue to take precedence.
