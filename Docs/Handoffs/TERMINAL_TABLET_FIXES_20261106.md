# Terminal and tablet UI fixes

Owner: DJenga6. Branch: `codex/ui-terminal-tablet-fixes-20261106`.
Base and requested APK-fix baseline: `8c03e7be22f89ff5bc4f5cf73317dabe818930b4`.
Integration target: `origin/Evan_Sprint_4`, through the designated integration owner.
Status: checked source checkpoint; current-source runtime and visual QA pending.

## What changed

Terminal focus now grows the whole control (1.12x, Marina 1.10x), changes its color and adds cached outline/glow feedback. Existing mode-card focus keeps its separate 1.45x treatment. Physical keys and sliders keep their interaction geometry. Animated flat UI controls use their actual transformed rectangle for pointing, avoiding stale physics-transform queries.

Map hover previews include an uncropped picture, name and catalogue description. Marina destination cards provide descriptions and grouped captions. Mode cards already selected the mode immediately: the extra `USE THIS MODE` button only opened maps. It is removed; an accepted mode-card click now immediately proceeds to compatible map choices. Existing provider permissions, compatibility filtering and leave confirmations remain authoritative.

The offhand tablet keeps dominant-controller aim and click on the same device. It is placed at 1.05m for the floating fallback; held placement follows the other hand with depth constrained to 0.95Ã¢â‚¬â€œ1.35m. A 960x1120 surface contains a smaller 0.8x page body, four persistent tabs, an always-available section rail, current-path text and existing Back/Home controls. Existing utilities and specialised pages remain. Dynamically created disaster sliders join the page body. The shared placement also moves existing modal panels farther away; their own Back/Resume paths remain.

The small follow-up exposes the existing dominant-grip / RMB Aim binding in the existing three-slot armed HUD (drawn ranged weapons only), preserving Fire and mode/holster hints. Controls text explains automatic gun/sight raising and release to lower. No extra HUD slot or control binding is added. Standings help also reflects the HUD owner’s automatic VR page cycle and bare look-stick hold/pin behavior; it no longer claims trigger/pinch paging.

No Core, gameplay, package, scene, camera or render-texture changes. Existing Unity assets/metas stay in place. No caller-ring or historical test overlay is included.

## Validation

All 77 Core and 180 Presentation runtime C# sources compile against Unity 6000.3.23f1 references (Roslyn exit 0 for both). Source whitespace checks pass. These are static checks, not a Unity import or Play Mode pass. The cold source checkout did not launch Unity. Existing warnings are retained in the local compile report.

The earlier 298/298 XR-simulator pointer pass on source `72aa920683a520df0d32b7f5acdaaff53a70b545` predates these changes and is not validation of this checkpoint. Its intermittent first-press limitation remains separate. No headset, APK, stereo comfort or performance acceptance is claimed.

## Current-source QA and capture plan

Use the existing single QA-operated warm editor after serial integration. Keep current Marina and normal providers. No screenshot-only editor.

- Marina main: focused mode/map destination, entire control scale and glow, readable description.
- Mode browser: square uncropped images and hover description; one click selects and opens maps without a confirmation step.
- Map browser: hover different map, verify its name/image/description; one click selects it.
- JOIN and HOST: conspicuous hover and unchanged permitted actions/leave confirmation.
- Tablet overview: offhand anchoring and distance, dominant pointer, readable whole surface and four tabs.
- Personal equipment: preview and Give remain separate; tabs/sections remain accessible.
- Bots: population and equipment paths through existing commands.
- Match disaster detail: sliders fit, navigation remains visible, return via Back/Home or another tab.
- Settings: current path, accessible controls and modal Back/Resume return.

Capture about five terminal states and five tablet states, plus a short hover/pointer/navigation motion sample. Inspect hand/weapon occlusion, Marina caption spacing, enlarged-control overlap, text size and layout after leaving specialised pages. Verify keyboard/mouse and XR-simulator pointing, then physical Quest separately. Reuse existing scenario adapters and preserve their assertions; do not manufacture a pass with forced modal opening or authority changes.

## Actual Marina detail-flow follow-up

Actual integrated-source `8f0602fad12148a9cb670c82e055568c9fb034e5` Game View captures exposed a production navigation discontinuity: selecting JOIN/HOST on the central Marina hid that screen and enabled a side panel outside the unchanged player's view. Map/mode details were centered but too small. The shared Back control was behind the natural arrival position. Public command assertions did not establish visual acceptance or physical pointer interaction.

The bounded follow-up reuses those existing panels. On open it records their original local transforms, establishes a fixed world pose in the actual view, and fits the detail plus Back header to both camera axes. It does not move or lock the camera/player. The JOIN keyboard is shown on code focus, uses a closer lower pose and matching apparent width, and keeps its existing keys/solid case and commands; the case is placed in front of and below the head, above the player's feet. Back stays above the panel while typing. Closing restores the saved station, keyboard and Back transforms. No bot removal, new blocker API, Core, camera or rendering framework is introduced.

Static Core77/Presentation180 Unity-reference compilation and diff checks pass for this follow-up. Actual same-camera runtime readability, physical/ray activation, JOIN code entry, keyboard/body/floor clearance, Back/close restoration and bot occlusion still require QA. The original six captures remain historical failed visual evidence, not a pass. Retest only the changed Marina flow on the combined successor; unchanged Session/network evidence remains separate.
