# Fine Crystals A avatar coating

Fine crystal surface relief and sparse raised, skinned facets replace the rejected brown coating. Each exposure varies coverage and grain coordinates. Timed erosion releases lit mesh grains from posed surfaces, including wrists raised above their original coating height. First-person overlays follow the camera-specific body mesh. Original skin materials are preserved.

Presentation/Visuals owns coating, material and particles. Characters/Player transports forearm roll through the elbow and sorts spine influences for reduced-influence skinning. No Core, package, project-setting, scene, input or gameplay-rule changes. Texture generation is reproducible with Docs/Tools/generate_sugar_surface.py: a mipmapped 2048-square map representing 64 mm of surface, plus sparse geometry. Capture resolution is not a claim that every grain is geometry.

## Identity and integration

- Owner/task: Marina / Fine Crystals A, approved direction and elbow repair November 15, 2026.
- Source branch: codex/marina-sticky-sugar; remote: https://github.com/Vanderbilt-VR-2026/DeadlyJenga.git.
- Production candidate, pushed and read back: c46b7d7335c73d90f3b37591b9b67cac63b7ac1c.
- Target: origin/Evan_Sprint_4 through MAIN; last verified target 1359ca474fb8a0bc86cdd72dca7dab5e3e631a71.
- Intentional water dependency: 3d6db3d1ceb4cfea9049f970a5defd73859d8d98.
- Tested fixture: 79251e8bfe181daac04958c64e998a0edd831778. Production Characters and Presentation files match the source candidate; local capture-only DevTools changes are excluded from the production branch.
- Capture job: 61dc2b3ce9f54ac3b9ddbe78e701dff0, completed; slot 2 returned.
- Supersedes the rejected brown/plate coating candidates, including 9b429470c25e8ba750c51ed36aa4645d604f13b4.
- Status: ready for integration review, not integrated or shipped. MAIN must verify current ancestry/equivalence and validate combined changes. This report commit adds evidence only.

## Actual runtime evidence

[Runtime report](STICKY_SUGAR_COATING_RUNTIME.txt): **45 PASS, zero failures**. [Manifest](STICKY_SUGAR_COATING_CAPTURE.json) records exact revision identities, dimensions and SHA-256 hashes. Unity 6000.3.23f1, D3D11 desktop Play Mode, Castle Game.unity with staged cameras/placement and actual sand contact/timers. Explicit TwoBones skinning passed. Tests are not a headset session.

Short contact stays clean, sustained contact coats, and the 20-second lifetime ends with erosion. Default, Penguin and CyberSamurai cases passed; these do not exhaust all skins. Three exposures used different seeds. Six elbow checks across three production-rig poses and both arms retained a minimum 0.0428 m joint-ring width. Isolated raised facets changed 175 actual rendered pixels. Dust-off emitted actual mesh particles (peak 122); the first-person dust still was captured inside its erosion phase. Underwater survivor/flying-hand regression checks also passed.

| Coated | Dusting off |
|---|---|
| ![Actual coated avatar](FineCrystalsA/avatar-fully-coated.png) | ![Actual erosion](FineCrystalsA/avatar-dusting-01.png) |

[Surface close-up](FineCrystalsA/crystal-closeup.png), [first-person coated](FineCrystalsA/first-person-coated.png), [first-person dusting](FineCrystalsA/first-person-dusting.png), [sampled dust-off video](FineCrystalsA/dust-off.mp4). The video preserves sampled simulation timestamps; it is not a native high-frame-rate recording. Full-body images are 3840x2880; first-person images are 3840x2160. These are runtime captures, not concept renders. The implementation interprets the reference; pixel-identical concept matching is not claimed.

## Measured cost and remaining limits

Twenty warmed paired 1280x720 camera.Render samples with synchronous one-pixel readback: one avatar bare median 10.397 ms / coated 10.252 ms (negative delta is measurement noise); four frozen visual replicas bare 9.620 ms / coated 10.508 ms, added 0.888 ms. This is an editor rendering sample, not isolated GPU timing, bot-AI/network load or Quest performance acceptance.

No headset, stereo/comfort, APK inclusion or exhaustive skin compatibility is verified. Unreadable imported meshes retain an object-coordinate fallback and need asset-specific review. MAIN integration acceptance remains pending. Visual review can compare the preserved actual evidence with the approved concept without confusing functional checks with subjective fidelity.
