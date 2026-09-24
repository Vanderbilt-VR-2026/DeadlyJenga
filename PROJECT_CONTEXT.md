# Deadly Jenga XL — project context

Source: the approximately 4:26 project proposal video, “DeadlyJenga,” in CS 4249 / CSET/CMA 3257 Virtual Reality Design (2026F). Reviewed on September 22, 2026 using the video's captions and presentation slides. This is a substantive summary, not a verbatim transcript or a statement of what the repository currently implements.

Course video: https://brightspace.vanderbilt.edu/d2l/le/content/673535/viewContent/4489049/View

## Concept and intended experience (0:00–0:46)

Deadly Jenga XL is a multiplayer VR survival minigame combining survival, destruction, collapsing towers, and escalating mayhem. Survivors start atop giant towers made from many smaller physics-driven blocks/crates. One randomly selected player begins as a giant flying hand and attacks the towers. Eliminated survivors immediately become additional hands. The last surviving player wins.

Fall damage can kill survivors. Contact with the ocean/floor is immediately lethal. The towers should withstand some disturbance but ultimately be capable of collapsing as their individual blocks move and fall.

## Home screen and session flow (0:47–1:12)

- Simple home/loading screen with options to host a private game, join by code, or join a random game/server.
- A user hosts their own game; others can join using its code.
- Everyone can see the joined-player count so they know who has loaded in.
- The host decides when to start.
- Minimum of two players, allowing one initial hand and at least one survivor.

## Survivor perspective, environment, and HUD (1:13–1:44)

- First-person perspective. The distant views in the proposal illustrate the overall arena rather than the survivor camera.
- Players are distinguished by randomly assigned colors.
- A diegetic HUD shows health, elapsed round time, remaining survivors, and hand count.
- Blocks/crates move individually under physics and collectively form massive, destructible towers.
- Only hands manipulate the blocks; survivors cannot pick up or move them as an interaction.
- Survivors take fall damage; ocean/floor contact is an instant kill.

## Survivor movement and climbing (1:45–2:13)

- Normal 3D movement and jumping, plus vaulting/climbing over crates.
- Edges/grapple points illuminate to communicate available climbing interactions.
- The presentation references Assassin's Creed and Arkham Knight as interaction examples.
- Players raise their hands toward illuminated points and press the control to climb/scale or grapple toward them.
- The slide mentions holding jump and raising hands to the grapple point while triggering both; the narration describes the action more generally. Exact controller mapping and the distinction between climbing, vaulting, and grappling remain to be resolved during implementation.

## Giant-hand role and interactions (2:14–3:07)

- A third-person-like perspective is proposed for ease of use and natural flight.
- Hands have no health bar but retain round information such as elapsed time and remaining players.
- Helicopter/UFO-like movement: unrestricted XYZ motion, hovering, backing up, and turning.
- The model is a floating hand with a fixed pointing finger. It is explicitly not synchronized to the user's real hand pose.
- Use the pointing finger as a physical pushing/shoving ram.
- Grab/pull specific nearby blocks slowly; interactable blocks illuminate.
- Rapid movement/velocity changes can deliver stronger pushes and suddenly fling blocks away.
- Blocks should offer some resistance to pushing.
- Hands grab/pull blocks, not players.
- One random player starts as a hand. Every eliminated survivor respawns as another hand, increasing pressure and destruction through the round.

## End of round and replay (3:08–3:22)

- Simple post-game summary congratulates the winner and shows how long each player survived, with recap information.
- Players can play again without rebooting the session.
- The intended format is quick rounds.

## Design goals (3:23–3:55)

- Constant urgency and exhilaration: survivors begin exposed at the top and must find safer positions while uncertain where hands will attack.
- Physics-driven collapse creates danger, spectacle, and changing terrain.
- Continuous participation: death immediately gives players a fun new objective instead of making them wait for the next round or their turn.
- Increasing numbers of hands escalate the stakes and mayhem.
- Keep lobby, loading, and results flows simple so the focus stays on the minigame.

## Optional additions, not baseline commitments (3:56–4:26)

- Friendly fire: survivors can push nearby players.
- Team-based play: last team standing wins.
- A competition between hands for the most kills (listed on the slide).
- Multiple map/tower heights, structures, and configurations.
- Natural disasters: rising ocean, earthquakes, and acid rain.
- The slide suggests acid rain could make climbing more costly when exposed to it.

## Boundaries and unresolved details

This video is a concept and scope proposal, not an implementation specification. It does not establish a headset target, networking solution, maximum player count, precise movement speeds, physics tuning, fall-damage formula, exact input bindings, round time limit, or detailed win/tie handling. Its illustrative UI numbers and concept-art geometry should not be treated as fixed requirements. Future project decisions and explicit user instructions supersede this proposal where they differ.

Use the core survival/hand gameplay, tower physics, movement/climbing, simple multiplayer session flow, HUD, and replay as the baseline context. Keep the explicitly optional additions separate when planning scope.
