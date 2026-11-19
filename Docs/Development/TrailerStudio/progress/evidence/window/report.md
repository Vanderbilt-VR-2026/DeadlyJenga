# Trailer Studio Window acceptance run

Date: 2026-11-18 19:36  
Unity: 6000.3.23f1  
Map: sparse-viaduct-study

| Test | Result | Detail |
|------|--------|--------|
| One audio listener on the stage | PASS | View Camera: enabled True, active False; View Camera: enabled False, active False; Trailer Studio ears: enabled True, active True |
| Window tour: every tab used in a real editor window | PASS | Tabs: Set, Perform, Polish, Direct, Shoot, Cut, Notes. 12 of 12 screenshots show real content (01-set-stage-open, 02-set-mark-selected, 03-recording, 04-recorded-timeline, 05-perform, 06-polish-layer, 07-direct-push, 08-shoot-cameras, 09-shoot-through-the-camera, 10-cut, 11-command-palette, 12-shortcuts). The pictures are the window as drawn on screen, read back from the display. |
| Undo and redo for Set edits | PASS | Marks: 3, after adding one 4, after undo 3, after redo 4 (Unity's own undo stack). |
| Runtime errors during the tour | PASS | None. |

Failures: 0
