# DeadlyJenga


| Team Member | Major(s) | Skills / Interests | Responsibilities |
|-------------|----------|--------------------|------------------|
| Evan | Neuroscience & CS | Brain-Visual Symmetry / Cohesion | User Controls<br>Movement<br>Appearance |
| Kaleb | CS | Architecture | Physics<br>Runtime Latency |
| Luke | CS | Online Social Cohesion | Networking<br>Map State |
| Arsema | Cognitive Studies | Graphic Design | Textures<br>Diegetic Elements |


A multiplayer VR project targeting Meta Quest.

## Sprint 3

You can find the Sprint 3 folder [here](https://drive.google.com/drive/folders/1_7nJXGaH6qDPWy1Jsvq3VEvNlQzg7mHt?usp=drive_link).
It contains working APK, demo videos, as well as more details regarding the information below.

## Design:
- We decided that the startup combination of a professional homescreen followed by immedietly loading into a lobby gave the ease of a minigame while still feeling like an official platform.  Instantly loading into the lobby made the game feel unserious and overbearing, but we did not want to much "slowdown" (many menus with choices etc.) from getting into the main experience which is geared towards quick fun.  By using HD photos on the homescreen we instill that "real game" feeling, and by next loading into a disney-resort like lobby we get right into that upbeat fun type of atmosphere, which we feel is important since loading right into such a hectic game can be overwhelming.
- As for controls, we tried to find a way to universally use hand tracking of the dominant hand and a joystick for the other.  This is mainly because playing as a flying hand requires this (otherwise flying around and hand sync could not occur altogether) and we did not want players to have to change how their setup throughout gameplay.  This has had some consequences for more odd scenarios like using weapons and rapidly switching from climbing/vaulting/jumping and walking as a play.
- We have made bright blue glowing edges around nearby ledges that can be vaulted.  They appear from far away but give that indication that makes a player feel more confident that the game will recognize their vault attempt.  The only objects players might be interacting with to get them in their hands are weapons, but we felt it would be smoother to have them just work on the usual mechanic where walking into them grants them to you.  This is because our game is very fast paced and so stopping to manually grab a powerup/gadget felt out of place.
- While consistently debugging and testing VR controls remains a big weakspot relative to our humongous content creation, we suspect significant work is going to be put into making this experience less nauseating for users given the amount of jumps and 360 turns the game requires.  The entire control setup may need to change as a result.

##  These are the significant implemented additions and changes in Sprint 3:
- Home screen: Added a separate starting menu before the lobby, with local play, hosting, joining and demo entry.
- Home music: Added the trimmed, looping Shorebreak track, which stops when leaving Home.
- New default lobby: Built Mayhem Marina, with a coastal clubhouse, social spaces and integrated lobby functions; preserved the previous lobby as a backup.
- Lobby music: Added Ready Set Splash as the looping lobby track.
- Lobby population: Added offline bot autofill, with bots removed when entering hosted multiplayer.
- Lobby terminals: Redesigned the join, host and game-setup terminals, including larger controls, revised screen placement and decorative arcade controls.
- Lobby flow: Added lobby visits, match resuming and persistent lobby/session handling; fixed problems starting another game after returning.
- Character viewing: Added smooth third-person orbit and zoom, including wardrobe inspection, idle poses and occasional emotes.
- Demo menu: Reorganized controls into Personal, Bot, Game and Misc, with subcategories, equipment filters and improved readability.
- Demo permissions: Expanded in-game testing controls for equipment, bots, disasters, maps and other settings.
- Bulk equipment grants: Added giving equipment to everyone for the current life or immediately plus every respawn.
- Map browsing: Added categorized map selection, more linked screenshots and previews.
- Mode browsing: Added illustrated mode choices, descriptions, PvP/Survival grouping and two-column presentation.
- Mode-to-map filtering: Added the compatibility framework so map choices can respond to the selected mode.
- Game modes: Added playable rules for Disaster Survival, King of the Hill, Color/Block Control, Capture the Flag, Infection, Last Team Standing and Murder Mystery.
- Mode configuration: Added per-mode combat, movement, scoring, population, respawn and disaster settings.
- Scoring and results: Reworked live statistics and post-game criteria to emphasize meaningful objectives rather than displaying survival time everywhere.
- HUD: Expanded mode-specific information, including clearer CTF flag states, objectives, standings and Murder Mystery information.
- Murder Mystery evidence: Added or expanded body discovery, reporting, footprints, sheriff succession and role-specific interactions.
- Murder Mystery reactions: Bots can respond to seeing a drawn murder weapon, rather than needing to witness a kill first.
- Murder Mystery replays: Added bounded ten-second murderer-perspective history for kill-cams and round-end playback, including sheriff/murderer split views when applicable.
- Body persistence: Fixed investigated cases of corpses disappearing or passing through map floors, and improved footprint compatibility.
- Bot behavior framework: Separated shared movement/survival execution from mode-specific objectives.
- Modular objectives: Added data-driven objectives, queries, conditions, assignment checks and execution contracts to reduce bot rewrites for new modes.
- Bot navigation: Expanded walking, jumping, climbing, vaulting, route planning, stuck detection and recovery.
- Bot combat: Added broader pursuit, melee, ranged aiming, cover checks, weapon switching and pickup behavior.
- Bot equipment handling: Added support for the expanded arsenal, including ballistic grenades, charged attacks and temporary powers.
- Bot optimization: Added geometry caching, navigation reuse, reduced duplicate checks and combined avatar-pose work.
- Bot expression: Expanded to approximately 70 contextual emotes, including suspicion, fear, investigation, celebration and social reactions.
- Bot appearance: Added randomized cosmetic customization.
- Enemy NPC framework: Added enemy identities, configurable spawning, perimeter placement and separate damage settings.
- Jaguar enemies: Added big-cat appearance, claws, quadrupedal movement, attack posing, movement tuning and sound reactions.
- Mushroom enemies: Added short mushroom enemies with enhanced jumping, no climbing and projectile attacks.
- Additional enemies: Added Longhand and Gum Brute enemy types.
- Enemy health/death presentation: Added specialized damage-fill and death effects, including the big-cat hairball effect.
- Wildlife framework: Added spawnable wildlife and tame/companion controls.
- Avatar customization: Added a wardrobe system that modifies the existing avatar rather than replacing its gameplay body.
- Skins: Added dozens of animal, monster, holiday, food and fantasy styles—including penguin, snowman, fox, panda, dragon, astronaut and candy-themed characters.
- Team-aware appearances: Added palettes that adapt skins to team colors while retaining recognizable secondary details.
- Accessories: Added hats, horns, ears, wings, capes, noses, glasses and other attachments.
- Hair and eyewear: Added multiple dedicated hairstyle and glasses selections.
- Clothing: Added robes, sports equipment, skirts, tuxedos, jackets and other clothing overlays.
- Customizer navigation: Expanded category navigation to accommodate the larger wardrobe.
- Cosmetic corrections: Improved clothing fit, skeleton-head behavior and the enclosed jack-o’-lantern appearance.
- Powered armor: Added an armor overlay with palm and foot emitters, flight and hand-fired plasma.
- Initial weapon system: Added the alien plasma blaster, murderer sword, sheriff revolver, convertible rifle/shotgun and giant boxing glove.
- Expanded firearms: Added rocket launcher, dual alien spike guns, chain gun, electric grenade launcher, wrist laser, needle drill and stocked automatic/burst pistol.
- Expanded melee: Added saw-wheel staff, barbed bat, chainsaw, laser claws, flaming marshmallow, laser axe and dagger.
- Weapon mechanics: Added magazines, reloads, cooldowns, spin-up, charging, continuous fire, burst fire, splash damage and different melee swing timings.
- Weapon aiming: Added aim assistance, enemy-sensitive reticles, more generous firearm hit areas and revised rifle/shotgun optics.
- Convertible rifle: Added the rotating two-barrel carriage, alternate optics, animated switching and two-handed aiming.
- Weapon presentation: Added first-person and external models, thigh holsters, back slings, visible projectiles and expanded materials/effects.
- Inventory rules: Added one-weapon limits, swap prompts, displaced-item drops and equipment drops on death.
- Pickups: Added weighted random spawning, lifetimes, limits and translucent pickup clouds containing actual item previews.
- Default PvP dagger: Added a mode-controlled fallback melee weapon.
- Temporary flying hand: Added a timed transformation powerup with player identity/color and safe return placement.
- Weapon audio: Added firing, handling, mode-switch, impact, reload and explosion sound suites.
- Player feedback: Added revised candy/plastic pain, death and healing sounds, plus peripheral damage and recovery effects.
- Death animation: Reworked death into full-size ragdolling followed by gradual melting, puddling and drying.
- First-person visibility: Improved downward viewing and body-obstruction handling.
- Traversal fixes: Addressed jumping, vaulting, island sinking and map-specific movement restrictions.
- Powered flight controls: Added more stable hover-style flight and keyboard height controls.
- Map library: Added numerous independently selectable maps beyond the original castle.
- Castle variants: Added longer-block castles, larger tower gaps, catwalk layouts and alternate central support structures.
- Social/environment maps: Added Amber Cantina, the waterfront tiki resort, Breakwater Village and a blank island.
- Sparse structure maps: Added crown, terrace, viaduct and hollow-keep designs, their larger variants and Festival of Giants.
- Festival City: Added interconnected landmark structures and several experimental geometry/collision variants.
- Structure exhibitions: Added large-island collections of experimental structures and twenty tower designs.
- Epic interconnected maps: Added ten large layouts, then woven-in lower structures and routes to create more varied heights and spaces.
- Megablock Tower: Added the large rectangular tower with navigable interiors.
- Fantasy maps: Added Honeycomb Hive, Cloud Carnival and Candy Glass Above Clouds.
- Fantasy environment art: Added dedicated sky panoramas, themed materials, textures, lighting, rounded visual edges and baked reflections.
- Map documentation: Added actual Unity screenshots, cinematic galleries, destruction captures and design comparisons.
- Disasters: Expanded the map-aware disaster framework and its registered hazard collection.
- Low gravity: Changed the anti-gravity behavior toward slow falling rather than simply floating upward.
- Water disasters: Expanded tsunami and water-kaiju visuals, splashes and wet/shiny surface effects.
- Storm presentation: Added revised tornado, storm, mist and tsunami effects and audio.
- Shared collision optimization: Reduced overhead from survivor collision copies through shared bodies, fewer transform updates and batched synchronization.
- Debris cleanup: Added qualifying impact shattering, displaced-block sinking/burial and faster submerged cleanup.
- Fast debris: Added a guarded cheaper visual-motion path for suitable fast-moving debris.
- Bot performance testing: optimized fps cost for bot counts, tsunami stress, navigation state, block activity and continuous memory logging.
- Project architecture: Reorganized code into Core, World, Characters, Presentation, GameSession and DevTools assemblies, with shared contracts replacing direct cross-system dependencies.
- Networking: Expanded replicated state for modes, equipment, cosmetics, enemies, pickups, replays and persistent lobby/session behavior.
- Match recording: Added scripted match setup, repeated runs, four-view spectating, video capture, diagnostic exports and completion notifications.
