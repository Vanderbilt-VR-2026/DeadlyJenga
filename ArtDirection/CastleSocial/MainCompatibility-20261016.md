# Main compatibility assessment — October 16, 2026

Current branch: Evan_Sprint_3, HEAD 05c2a8669.
Fetched main: a1b2362da. HEAD is an ancestor of main, 18 commits behind.
This is an assessment, not an executed merge or a successful Unity compatibility run.

## Result

Do not directly pull main and assume the current AI/voice integration will remain
functional. The changes can be integrated, but require a deliberate migration and
runtime validation. Git conflicts are only part of the work.

## Isolated merge evidence

A separate temporary Git index captured the nine modified tracked files and the
new CastleSocial module, speech-service sources, tests and local virtual-project
configuration. Ignored model caches/virtual environment were not added. A
temporary unreferenced snapshot was merged with main using git merge-tree; no
branch, real index, existing working file or Unity session was changed.

Two content conflicts were reported, after rename detection:

- Assets/_DeadlyJenga/GameSession/Session/Scripts/CastlePlaySession.cs
- Assets/_DeadlyJenga/GameSession/Session/Scripts/CandyDemoHud.cs

The other modified tracked files were carried through Git's rename/merge process
without reported textual conflicts. That does not establish compilation or
behavioral compatibility. Temp/MainCompatibility/audit.json and merge-tree.txt
contain the raw assessment data until Unity clears Temp.

ProjectSettings/VirtualProjectsConfig.json is currently untracked but tracked on
main. Preserve/compare it before checkout or merge rather than overwriting it.

## Integration risks

1. **Assemblies:** main separates Core, World, Characters, Presentation and
   GameSession into named assemblies. Current CastleSocial code is compiled in
   Assembly-CSharp. The session/HUD/world-menu hooks reference CastleSocial;
   named Unity assemblies cannot directly reference that predefined assembly.
   Resolve module placement, assembly references and dependency boundaries, not
   just the two conflict markers.
2. **Startup:** main initializes bots through BotWorld and conditionally creates
   a LAN or Fusion session. The current SocialWorld.Initialize assumes a present
   initialized Network component and subscribes immediately. Direct Game-scene
   play can leave Network null under main's MatchSetup defaults.
3. **Voice networking:** current SendSocial, human positions and leave events
   are implemented against LAN socket peers. Main's new CastleFusionSession
   handles readiness/welcome/world/pose/ack messages, with no equivalent social
   audio/identity transport. A clean textual merge of CastleLanSession would not
   make multiplayer voices work through the Fusion lobby. Add an explicit bridge
   with the same host attribution and bounded audio behavior.
4. **UI/flow:** main enables Lobby as the first build scene and renames/moves the
   castle scene to Game.unity, preserving its GUID. Match setup moves into the
   lobby; the current in-game social settings need a reachable home in that flow.
5. **Tools/tests:** the social studio/test runner currently use old asset/scene
   paths. Update those for the new structure and editor assembly boundaries.
   Main also deletes several earlier one-time validation/installer tools.
6. **Configuration/packages:** main adds Unity AI Assistant and inference packages,
   changes Android defines, and removes two preloaded XR settings objects. These
   changes are not proof of a broken headset build, but require import/build/XR
   regression checks. Existing XR package versions are not directly upgraded by
   the manifest diff examined here.

## What is relatively isolated

Tools/SocialVoiceBridge source and the ignored local .venv, .models and .cache
have no incoming same-path replacement on main. Much of the scene/art/prefab
reorganization preserves asset GUIDs. The portable social simulation can be
retained; it needs integration into the new assembly architecture.

## Safe next sequence

1. Save intended Unity asset edits and checkpoint/backup the complete current
   AI/voice work, including new untracked source/assets. Keep local model caches
   and the Python environment available separately; they are intentionally ignored.
2. Perform integration in a separate branch/checkout with the new main layout.
3. Port the social startup, UI, assembly boundaries and LAN/Fusion voice routing.
   Confirm the intended lobby/default-build experience before finalizing that flow.
4. Compile/import, run core and socket tests, then rerun real Unity bot movement,
   audio synthesis/recognition, late join, invitations, authority and hand checks.
   Add equivalent Fusion multi-peer voice checks; previous LAN successes do not
   certify the new Fusion path.
5. Test the resulting APK and headset input/audio before replacing this working
   laptop demo or merging the integration.

No pull, actual branch merge, source migration or push was performed by this audit.
