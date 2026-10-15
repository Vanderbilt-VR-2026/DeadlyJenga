# Candy demo HUD

Presentation lives in `CandyDemoHud.cs` and `CandyHudVisuals.cs`; the existing headset menu is themed in `CastleWorldMenu.cs`. `CastlePlaySession` installs the HUD at runtime, so no scene or prefab rebake is required. It delegates menu actions to the existing session, bot, network and sound-studio APIs. No movement, hand physics, terrain or block properties are changed.

## Desktop

- Esc opens / resumes. Click a section, then an illustrated action card. Wheel scrolls.
- Left / right arrows change sections; up / down choose an action card; Enter activates it. Settings sliders and existing Friends / stamina controls use mouse interaction.
- Tab changes survivor / giant-hand role during gameplay, and no longer changes roles while browsing a menu.
- F1 hides / restores gameplay hints. Settings offers Adaptive / Always / Hidden plus opacity. Adaptive fades after nine seconds and brightens on role, input-mode or traversal-state changes.
- Sound Studio is available in Play. Runtime changes / settings apply to the current session.

## Headset

The existing gaze + right-trigger menu remains, with rounded candy panels, icon rows, clear hover feedback and short explanations. Left Y opens / closes. With complete tracked skeletons, right pinch selects and left thumb + middle toggles the menu, matching the actual tracking source. Gameplay hints use a world-space canvas, since IMGUI does not render a stereo headset overlay. Their noninteractive panels add no colliders.

VR prompt labels reflect the supported XR bindings rather than assuming that a connected desktop gamepad drives gameplay. Bare-hand flight still needs the existing flight input bindings; the HUD does not invent gesture flight. Hand gestures and headset comfort must be evaluated on device.

## Style / extension

Generated rounded textures, mint/lavender/peach accents and outlined icons are shared between desktop and headset. Edit `CandyHudVisuals` for the palette / icons, `Hints()` for control descriptions and the page methods for menu content. Gameplay cards pair an icon, binding label and action text; translucency affects the backing as well as the overlay, with stronger opacity during climbing. The separate stamina meter stays intact.

The first pass uses the project's current Input System bindings. If remapping is introduced later, replace those labels with action-binding display strings and device-specific glyphs together; never change glyphs merely because an unrelated controller connects.

## Validation

Tools → Castle Prototype → Check Candy HUD starts the demo if necessary and checks visibility, input suspension, mode-specific binding prompts, stereo canvas mode, and absence of new physics / listeners. It saves a report and desktop gameplay / menu screenshots here, then leaves the demo menu open. It is explicit, not an automatic startup test. No scene save is required.

## Design sources

- [Microsoft Xbox Accessibility Guideline 101: Text display](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/101): pair readable text with glyphs; account for display size and distance.
- [Microsoft Xbox Accessibility Guideline 102: Contrast](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/102): retain text/background separation over changing scenery. This first pass is not a certification of all accessibility guidelines.
- [Unity Learn: Best UI Practices for VR](https://learn.unity.com/tutorial/unit-6-best-ui-practices-for-vr?uv=2018.4): world-space interfaces and comfortable VR interaction.

Implementation choice: use the existing IMGUI demo flow on desktop and the existing world-space headset interaction, rather than adding another input framework or changing player controls. Gameplay hints share one uGUI canvas. No per-frame mesh or texture generation is used.
