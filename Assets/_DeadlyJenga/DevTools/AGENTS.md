# DevTools (side box)

Testing and editor tools. May use every box. Nothing may use DevTools.
Never included in gameplay logic.

## Contents
- Scripts/CastleTestControls: runtime test controls.
- Scripts/CandyHandLab + Scenes/CandyHandContactLab: one-block bench for the physical hand.
- Editor/: validation suites (input, traversal, climbing, stamina, VR view...),
  CastlePrototypeBuilder and CastlePlayBuilder.
- Editor/Social/: AI social layer tests and the population window (moved out of GameSession).
- Editor/SessionTestAccess: the only way tests reach real classes behind Core interfaces
  (session.Survivor(), Bots(), Hands(), Generator(), lobby.View()). Gameplay code never does this.

## Rules
- Reports go to Logs/Validation via ProjectPath.Report. Assets are found by name
  (ProjectPath.Find / FolderOf), never by hard-coded path.
- Never run "Build Test Assets", "Install Player and Hand Prefabs" or
  "Apply Confirmed Structure" on the real project; they regenerate assets.
- No tool may create or overwrite assets automatically when Unity loads.
