# Menu flow review — November 5, 2026

Branch: `ui-menus-cleanup`. All flows await user review.

This inventory describes source behavior and the revised navigation. It is not a headset layout sign-off. The interactive review is in the workspace canvas `UI-menu-review.canvas.tsx`.

## Changes in this pass

- Home bypasses its 2D screen and enters the island. The island makes one automatic Fusion host attempt, retaining local play and manual retry if it fails.
- In-room join already leaves the prior connection before connecting to the entered room. An occupied old room retains the existing migration policy.
- Active VR menus share a tracked-controller ray and its own trigger, with a visible local beam. Missing controllers do not fall back to head gaze.
- Room-code keyboard is a private, local popup. Fingertip typing and its solid piano cabinet are removed from the active path.
- Pause menus start with personal actions; development pages are behind host ownership checks. Personal voice preferences remain available to guests.

## Validation

- C# compilation: passed in Unity 6000.3.23f1, including the final wording/navigation refresh.
- Play Mode: first run was blocked before scene startup by the installed Meta XR Simulator failing Vulkan initialization.
- A desktop-only test fixture uses an unsaved XR settings instance to bypass that native runtime during the test. It does not change XR settings assets or Quest build configuration.
- MenuFlowChecks: 31 checks passed with zero runtime errors. Includes solo Game initialization, controller switching/tracking loss, automatic host, private keyboard, room replacement, guest menu restrictions and guest leave preserving the remote host.
- Two real Fusion runners connected through Photon in one editor. Separate Multiplayer Play Mode virtual players and physical Quest testing remain required.
- Main branch check: current HEAD matches latest fetched origin/main; changes remain uncommitted.
- No Core contracts changed. Presentation/Visuals and Presentation/Audio own menu changes; GameSession/Session owns startup; DevTools contains the test.

## Screen inventory

### 01. Launch & automatic hosting

Group: Start. Review: **pending**.

Before: Home shows Join, Host, Join Random Session and Demo before creating a player. Opening Lobby directly is a separate route.

Revised: Home now goes straight to the island. The player body initializes locally and the island attempts to host one Fusion room. Failure keeps the island usable and offers room creation to retry.

Joined player: Same launch behavior. Becoming a guest happens only after joining another room.

Actions, in flow order:

- Enter island → marina
- Connection failed: create room → room

Review note: The legacy Home screen is retained behind showLegacyHomeMenu for a future decision. No endless automatic retries. Offline use remains possible.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/HomeMenuView.cs`.

### 02. Marina play screen

Group: Lobby. Review: **pending**.

Before: A large play screen mixes mode/map cards, Game/Demo choices, Play, Round Info and four detail shortcuts. It uses head aim plus several select inputs.

Revised: The existing marina layout stays as the lobby hub. Controller laser and its own trigger select the screen. Mode/map configuration stays owned by the room leader.

Joined player: Can join another room, customize their avatar, inspect room information and set personal readiness. Shared choices are read-only.

Actions, in flow order:

- Join → join
- Room → room
- Avatar → wardrobe
- Settings → setup
- Map → maps (host only)
- Mode → modes (host only)
- Round info → round
- Start / cancel → round (host only)

Review note: Current shortcut captions are JOIN / HOST / STYLE / SETTINGS. Suggested future wording: Join / Room / Avatar / Match settings. Your approval does not apply new wording automatically.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/MayhemMarina/MarinaScreen.cs`.

### 03. Join another room

Group: Lobby. Review: **pending**.

Before: JOIN terminal has code, Join, Search Local, nearby pages, invitations and a guest leave action. A giant keyboard is always in the environment.

Revised: Selecting the code field opens a private keyboard. Join uses the existing leave-then-connect path. Success moves the local connection into the requested room. A failed attempt uses the existing recovery path.

Joined player: Hosts and guests can switch rooms from the island. In-match visitors must leave/return to the island before switching.

Actions, in flow order:

- Enter room code → keyboard
- Join room → room
- Find nearby rooms → nearby
- Back to marina → marina

Review note: Joining your current code does nothing. An empty old room closes. If other players remain, existing host migration can preserve their room. Recovery after a failed join still needs two-player verification.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/IslandLobby/IslandLobbyView.Stations.cs`.

### 04. Private room-code keyboard

Group: Lobby. Review: **pending**.

Before: A permanent giant piano/cabinet accepts pointing and fingertip touches beside JOIN.

Revised: A local keyboard opens about one meter in front of the player when code input is focused. It closes on Close, losing input eligibility, or opening another modal. It has no network object and sends only the final join request.

Joined player: Identical private keyboard for every player. Other machines do not receive the keyboard or partial typed code.

Actions, in flow order:

- Characters / numbers / hyphen → keyboard
- Backspace → keyboard
- Clear → keyboard
- Join room → room
- Close → join

Review note: Controller laser + trigger only in VR. Desktop hardware typing remains supported. The popup stays at its opening position rather than following every head movement. Quest size and reach need device review.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/IslandLobby/IslandLobbyView.Keyboard.cs`.

### 05. Nearby rooms & invitations

Group: Lobby. Review: **pending**.

Before: Search Local lists three nearby rooms per page, More Rooms, Hide Results and an invitation action. Discovery advertises Fusion room codes on Wi-Fi.

Revised: The same optional discovery route stays. Joining still uses Fusion and replaces the local room connection.

Joined player: Guests can discover and switch to another room from the island.

Actions, in flow order:

- Select nearby room → room
- More rooms → nearby
- Use a code → keyboard
- Back → join

Review note: Wi-Fi is discovery only, not a second gameplay networking system. Discovery failure should direct users to a code.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/IslandLobby/IslandLobbyView.Stations.cs`.

### 06. Room information & ownership

Group: Lobby. Review: **pending**.

Before: HOST terminal offers Create Join Code, Region, Invite Nearby, Request Host, shared Demo permissions, leave/close, and host-request decisions.

Revised: Keep shared code and player count visible. Creation becomes the connection-failure retry route. The host leave label now says Leave / Transfer Host so it matches migration behavior.

Joined player: Can read code/count, request host and leave their own connection. Cannot approve host requests or change shared permissions.

Actions, in flow order:

- Create join code / retry → room
- Invite nearby → nearby (host only)
- Request host → room
- Accept / deny host request → room (host only)
- Leave / transfer host → marina (host only)
- Leave room → marina
- Back → marina

Review note: Leaving and closing for everyone are different actions. This patch preserves migration instead of adding a destructive close-for-everyone button.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/IslandLobby/IslandLobbyView.Room.cs`.

### 07. Map browser

Group: Lobby. Review: **pending**.

Before: Map picker includes category headers, map rows, preview/status, previous/next pages and Back. Experimental map categories are separated by the existing browser.

Revised: Preserve catalog-driven maps and availability. Controller laser selects and pages. Host chooses; guests can inspect the shared result.

Joined player: Shared map selection is read-only. A category header may still expand locally.

Actions, in flow order:

- Select available map → marina (host only)
- Expand category / next page → maps
- Back → marina

Review note: No maps or compatibility rules were removed.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/IslandLobby/IslandLobbyView.Game.cs`.

### 08. Mode browser

Group: Lobby. Review: **pending**.

Before: Mode cards are grouped into PvP and survival with descriptions, previews, availability and paging. Curated Game profiles restrict raw choices.

Revised: Keep catalog and curated-profile restrictions. Use the same controller laser as the rest of the lobby.

Joined player: Can read mode details; shared choice remains with host.

Actions, in flow order:

- Select available mode → marina (host only)
- Next / previous page → modes
- Back → marina

Review note: The Game / Demo split remains for this review. Decide whether Demo should be renamed Sandbox or moved entirely under Host Tools.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/IslandLobby/IslandLobbyView.Game.cs`.

### 09. Match settings

Group: Lobby. Review: **pending**.

Before: MODE + MAP has mode/map cycling, island effects, settings/demo tuning, team rules, population, Start and nested setup pages.

Revised: Existing match-rule commands remain host-owned. Guests see shared values. Personal control preferences remain in Controls & Comfort.

Joined player: Shared rules are read-only, with the existing personal secret-role preference exception.

Actions, in flow order:

- Mode → modes (host only)
- Map → maps (host only)
- Rules / population / teams → setup (host only)
- Round & readiness → round
- Back → marina

Review note: Settings subpages include mode-specific rules, survivor/stamina rules, bot/hand choices and other existing catalog options. Wording and grouping still await your review.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/IslandLobby/IslandLobbyView.Settings.cs`.

### 10. Round & readiness

Group: Shared. Review: **pending**.

Before: Game / Demo panel shows profile/status, map, readiness count, population and countdown. It offers profile selection, automatic rounds, ready/unready, cancel, retry, recenter and Back.

Revised: Keep the existing round lifecycle and readiness rules; adopt the common laser. Pause-menu entry is now Round & Readiness.

Joined player: Controls their own readiness. Cannot select the shared profile, change automatic rounds, cancel a host start or retry the room transition.

Actions, in flow order:

- Game / Demo → round (host only)
- Automatic rounds → round (host only)
- Ready / unready → round
- Cancel start / retry → round (host only)
- Recenter panel → round
- Back → marina

Review note: The technical prototype/unvalidated profile status is retained for now because it describes current mode limitations. It is a wording-review item.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/GameExperiencePanel.cs`.

### 11. Avatar wardrobe

Group: Personal. Review: **pending**.

Before: Wardrobe can auto-open when walking near the pod. It has categories, twelve items/page, mirror, layer-conflict notices and Done. Desktop has its own panel.

Revised: Open wardrobe explicitly from a menu/portrait. Proximity alone no longer opens it. VR choices use the controller laser; appearance and mirror remain personal.

Joined player: Everyone can customize their own avatar. The private chooser is local; the resulting appearance can replicate.

Actions, in flow order:

- Category / previous / next → wardrobe
- Toggle appearance item → wardrobe
- Done → marina

Review note: Keep outfit layering, notices and camera ownership intact. Physical avatar alignment still needs a headset pass.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/IslandLobby/IslandLobbyView.Wardrobe.cs`.

### 12. Controls & comfort

Group: Personal. Review: **pending**.

Before: Dedicated panel includes handedness, movement/turning and seated/standing controls. It accepts controller aim with head fallback and pinch bindings.

Revised: Selection uses the shared tracked-controller laser and trigger. Personal preferences stay available to hosts and guests.

Joined player: Full control over their own comfort and handedness.

Actions, in flow order:

- Left / right preferred hand → comfort
- Movement / turn / seated / standing → comfort
- Done → pause

Review note: Hand tracking without physical controllers does not select menus in this revision. Menu opening/closing gestures may still exist; pointing and selection use controllers.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/CandyComfortPanel.cs`.

### 13. Flying-hand appearance

Group: Personal. Review: **pending**.

Before: Hand appearance has a separate local picker with previews, paging/recenter and Back.

Revised: Preserve the picker and use the common controller laser. Both roles can review their hand appearance where currently supported.

Joined player: Changes only their own appearance preference.

Actions, in flow order:

- Previous / next appearance → appearance
- Recenter → appearance
- Back → pause

Review note: Confirm labels, order, and headset readability.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/CharacterPicker.cs`.

### 14. Pause menu

Group: In game. Review: **pending**.

Before: VR root directly exposes rebuilding, disasters, guns, bot counts, role switching, duplicate control links and four development tabs. Desktop opens the development dashboard directly.

Revised: VR root is Resume, Controls & Comfort, Hand Appearance, Room & Players, Audio & Voice, Round & Readiness, contextual Drop Weapon, and Host Tools when allowed. Desktop starts with personal/room controls and an explicit Host Tools entry.

Joined player: No Host Tools entry, even if shared Demo permissions are enabled. Their own controls, voice, appearance and readiness remain available.

Actions, in flow order:

- Resume → hud
- Controls & comfort → comfort
- Hand appearance → appearance
- Room & players → ingame-room
- Audio & voice → voice
- Round & readiness → round
- Host tools → tools (host only)

Review note: The desktop menu keeps a different layout suitable for mouse use. This is a flow review, not a promise that desktop and VR look identical.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/CastleWorldMenu.Sections.cs`.

### 15. In-game room & players

Group: In game. Review: **pending**.

Before: Fusion room page shows status/code, shared Demo controls, nearby invites, invitation notices and Leave / Transfer Host.

Revised: Room information and local leave remain available. Shared-permission and nearby-invite host controls are hidden from the guest player-facing page. Switching by code happens in the island lobby.

Joined player: May visit the island, leave their connection, and read/copy room code on desktop. Returning the entire room to the island is a host action.

Actions, in flow order:

- Resume → hud
- Visit / return to lobby → marina
- Leave room → marina
- Back → pause

Review note: Guest leave has an existing confirmation step. Guests visiting the lobby must return to the current match before room-wide launch controls are available.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/MultiplayerPanel.cs`.

### 16. Audio & voice

Group: Personal. Review: **pending**.

Before: Voice preferences live among legacy movement settings in VR and a desktop Social page. Desktop diagnostics include a text conversation test.

Revised: Personal microphone, bot voices, volumes and per-bot mute remain available. Desktop player-facing voice controls hide conversation diagnostics and its text-test field.

Joined player: Controls their own microphone and listening preferences. They do not need host diagnostics.

Actions, in flow order:

- Mute / enable microphone → voice
- Bot voice volume / mute → voice
- Back → pause

Review note: VR audio/voice still shares the legacy local movement/voice page; a dedicated visual redesign is an approval item. Microphone permission prompts remain platform-controlled.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/SocialPanel.cs`.

### 17. Host Tools hub

Group: Host tools. Review: **pending**.

Before: Development tabs are mixed into the normal root. Shared Demo permissions can make guests eligible for commands.

Revised: An explicit Host Tools entry reveals Personal, Bots, Match and Settings tabs, plus rebuild and bot-population shortcuts. Visibility requires actual host ownership and development eligibility.

Joined player: Hidden. A stale open Host Tools page resets when eligibility is lost.

Actions, in flow order:

- Personal → tools-personal (host only)
- Bots → bots (host only)
- Match → tools-match (host only)
- Settings → tools-settings (host only)
- Bot population → bots (host only)
- Back to pause menu → pause (host only)

Review note: Curated Game mode may intentionally disable development tools even for a host. This UI gate supplements existing command authorization.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/CastleWorldMenu.Sections.cs`.

### 18. Host Tools / Personal

Group: Host tools. Review: **pending**.

Before: Character, Equipment and Spectate sections include role switching, respawn, third-person inspection, draw/holster, dominant hand, blaster testing and weapon drops.

Revised: Keep the existing development actions inside Host Tools. Keep ordinary comfort/appearance and contextual dropping outside it.

Joined player: Development pages are hidden; normal gameplay actions retain their existing inputs.

Actions, in flow order:

- Character / role / respawn → tools-personal (host only)
- Equipment grants → tools-personal (host only)
- Spectate → tools-personal (host only)
- Third-person inspection → inspection (host only)
- Back → tools (host only)

Review note: No gameplay roles or loadout rules were changed.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/CandyDemoHud.Sections.cs`.

### 19. Host Tools / Bots

Group: Host tools. Review: **pending**.

Before: Survivors, Giant Hands, Bot Equipment and Enemies expose spawn/fill/personality/clear, loadouts and wildlife/enemy test spawns.

Revised: Keep these under host tools. Wildlife test panel now also refuses guest use and closes on ownership loss.

Joined player: Hidden.

Actions, in flow order:

- Survivors / population preview → bots (host only)
- Giant hands → bots (host only)
- Bot equipment → bots (host only)
- Enemies / wildlife → wildlife (host only)
- Back → tools (host only)

Review note: Population fill still has an explicit preview/apply path in VR. No bot behavior changes.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/DemoMenuSections.cs`.

### 20. Wildlife testing panel

Group: Host tools. Review: **pending**.

Before: F6 or the enemy page opens wild/companion selection, clear, spawn-all and species buttons. It used head aim.

Revised: Host-only test panel with the shared controller laser. Preserve species and wild/companion actions.

Joined player: Cannot open it via the menu or F6.

Actions, in flow order:

- Wild / companion → wildlife (host only)
- Spawn species / all → wildlife (host only)
- Clear wildlife → wildlife (host only)
- Close → bots (host only)

Review note: Confirm labels, order, and headset readability.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/CandyWildlifePanel.cs`.

### 21. Host Tools / Match

Group: Host tools. Review: **pending**.

Before: Modes, Maps, Disasters, World, Ground Pickups and Powerup Spawning expose live world/round commands.

Revised: Keep existing pages behind Host Tools. Shared map changes and world actions still use their existing authority checks.

Joined player: Hidden. Shared values are shown in lobby/round screens instead.

Actions, in flow order:

- Modes / maps → tools-match (host only)
- Disasters → disasters (host only)
- World / rebuild / end round → tools-match (host only)
- Ground pickups / powerups → powerups (host only)
- Back → tools (host only)

Review note: Tabletop, scaffold delivery and HUD/readability subpages remain nested development options where the existing mode exposes them.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/DemoMenuSections.cs`.

### 22. Host Tools / Disasters

Group: Host tools. Review: **pending**.

Before: Disaster catalog previews, launch/stop, stop all, selection and parameter sliders. Wave-specific settings use hold-to-adjust.

Revised: Keep catalog controls, use controller laser/trigger for clicks and slider dragging, and hide the page from the ordinary player menu.

Joined player: Hidden. Disaster effects themselves remain visible to players.

Actions, in flow order:

- Select disaster / preview → disasters (host only)
- Adjust parameters → disasters (host only)
- Launch / stop / stop all → disasters (host only)
- Back → tools-match (host only)

Review note: Gameplay/network synchronization was not changed.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/CastleWorldMenu.Disasters.cs`.

### 23. Host Tools / Powerups & equipment

Group: Host tools. Review: **pending**.

Before: Equipment grants, ground spawns, random block powerups and weighted spawn settings exist across separate pages. Desktop also has a floating Random Block Powerups panel.

Revised: Keep those pages under Host Tools; the floating desktop editor now also checks host eligibility. VR uses existing laser-compatible world pages.

Joined player: Hidden. Players can still use or drop equipment through normal gameplay.

Actions, in flow order:

- Equipment / ground pickups → powerups (host only)
- Random spawn settings → powerups (host only)
- Back → tools-match (host only)

Review note: The floating panel is desktop-only; no new headset-only IMGUI is introduced.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/CandyPowerupSpawnPanel.cs`.

### 24. Host Tools / Settings

Group: Host tools. Review: **pending**.

Before: Controls, Settings, Friends and Voice mix personal settings with stamina, curves, hand physics, scenery, HUD and diagnostic options.

Revised: Preserve development tuning under Host Tools while exposing personal comfort, room and voice controls from the pause menu.

Joined player: Shared development tuning and diagnostic text fields are hidden. Personal settings remain available elsewhere.

Actions, in flow order:

- Controls / HUD / scenery / stamina → tools-settings (host only)
- Friends / room → ingame-room (host only)
- Voice diagnostics → tools-settings (host only)
- Sound studio (desktop F8) → studio (host only)
- Back → tools (host only)

Review note: A second design pass can split this large legacy Settings page into cleaner sections after you approve the top-level flow.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/SurvivorSettingsPanel.cs`.

### 25. Round results

Group: In game. Review: **pending**.

Before: Standings, split infection scores, pages, room code, Stay / Next Round and lobby return. VR selects with head gaze.

Revised: Preserve scores and next-round permission rules. Select buttons with the shared controller laser and trigger.

Joined player: Can read/page standings and visit/leave as currently allowed. Starting the next round remains a host/leader command.

Actions, in flow order:

- Stay / next round → round (host only)
- Return / visit lobby → marina
- Standings pages → results

Review note: Automatic-round participation is managed in Round & Readiness. Host migration and separate headsets still need a pass.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Scripts/CandyResultsHud.cs`.

### 26. Third-person inspection

Group: Host tools. Review: **pending**.

Before: Local mirror has Left, Right, Up, Down, Closer, Further and Done. It uses head aim and a select hold.

Revised: Use the common controller laser. Hold its trigger over movement controls and click Done to close.

Joined player: Inspection remains a host development entry in the revised pause flow.

Actions, in flow order:

- Orbit / zoom → inspection (host only)
- Done → tools-personal (host only)

Review note: This camera is local and keeps the original tracked eye available.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/CandyThirdPersonInspection.cs`.

### 27. Gameplay HUD & contextual overlays

Group: In game. Review: **pending**.

Before: Match HUD, stamina, equipment/powerup status, spectator labels, mystery prompts/choices, captions, room notices and spectator TVs.

Revised: Status displays remain visible and do not become menus. This pass changes results and menu pointing; gameplay-specific mystery shortcuts retain their own gameplay inputs.

Joined player: Sees personal status and shared match results. Spectator TVs remain shared-world information displays.

Actions, in flow order:

- Open pause menu → pause
- Round ends → results

Review note: Mystery role/meeting/disaster-choice overlays are a separate gameplay-interface review item. TutorialCoach is retained but existing validation expects no automatic legacy tutorial. No claim of a full gameplay-HUD redesign.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Scripts/CandyMatchHud.cs`.

### 28. Desktop sound studio

Group: Development. Review: **pending**.

Before: F8 opens an authoring overlay: categories, sound list, clip search/library, audition, cue tuning, mix and local presets.

Revised: Retain the desktop authoring tool. Gate it to eligible hosts during gameplay, hide it for guests/VR, and keep it out of normal player menus.

Joined player: Hidden, including the F8 shortcut. Personal listening preferences remain on the voice controls.

Actions, in flow order:

- Categories / sound / clip library → studio (host only)
- Audition / mix / local preset → studio (host only)
- Close → tools-settings (host only)

Review note: Its search/text fields remain desktop development controls. It is not converted into a headset keyboard in this pass.

Source: `Assets/_DeadlyJenga/Presentation/Audio/CandyAudio/CandyAudioStudio.cs`.

### 29. Legacy Home screen (disabled)

Group: Legacy. Review: **pending**.

Before: 2D Home has Join, Host, Join Random Session, Demo, controls/handedness; Join has code/region/nearby lists and an embedded keyboard.

Revised: Keep the implementation for reversibility, bypass it by default. It is outside the active player route.

Joined player: Everyone bypasses it.

Actions, in flow order:

- Current launch route → entry

Review note: If re-enabled later, its own legacy gaze/input code must be reviewed before shipping. This patch does not present it to players.

Source: `Assets/_DeadlyJenga/Presentation/Visuals/Menus/HomeMenuView.cs`.
