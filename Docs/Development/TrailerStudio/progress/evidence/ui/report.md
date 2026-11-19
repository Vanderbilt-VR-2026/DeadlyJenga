# Trailer Studio Ui acceptance run

Date: 2026-11-18 19:06  
Unity: 6000.3.23f1  
Map: sparse-viaduct-study

| Test | Result | Detail |
|------|--------|--------|
| U1. Timeline frame <-> x | PASS | Every frame of 180 survives frame -> x -> frame on a 900 px lane: True. Off the ends clamps: True. A lane with NaN or 0 width gives numbers, not NaN: True. |
| U2. Repeated names in a popup can each be picked | PASS | Mia, Ben, Mia, (null), Mia (2) became: Mia / Ben / Mia (2) /  / Mia (2) (2) |
| U3. Chain reactions: order, delay, loops, missing links | PASS | WaitsFor follows chains and stops in loops: True. Log (4 lines, wanted root, a, b at +0.2 s, c): 0.500s slowTime to 0.9× for 0 s (applies while performing in real time) / 0.500s slowTime to 0.2× for 0 s (applies while performing in real time) (chained) / 0.700s slowTime to 0.3× for 0 s (applies while performing in real time) (chained) / 0.700s slowTime to 0.4× for 0 s (applies while performing in real time) (chained). A loop, a missing link and a self-link never ran. Action scheduled before End ran later: False. Slow-time ranges right and cleared by End: True. |
| U4. Deleting a cue frees the cues that waited for it | PASS | Cues left: 1; the waiting cue's link "", on hit False. |
| U5. Slow time while performing, and the clock afterwards | PASS | Real-time pass: time scale 1 before the range, 0.5 inside it, 0 when the stage holds again. Stopped inside the range: True, time scale then 0 (the stage holds time still; closing the stage puts the game's own value back). |
| U6. Time of day and weather are put back | PASS | 1 directional light(s). Fog applied: True. Weather back to the map's own while the hour stays set removes the fog: True. Hour off while the weather stays set turns the sun back: True. Everything back after Restore: True. |
| U7. Set files: odd names, German number format, damaged file, trash | PASS | Ids "café-ü-名字-quoted" and "café-ü-名字-quoted-2". Saved and loaded back with a German number format in a folder with spaces and non-ASCII letters: True. Same title twice gives two Sets: True. A damaged file loads as missing instead of throwing: True. Trash keeps a copy beside the tapes: True. |
| U8. Damaged Theatre take folders are reported, not half imported | PASS | Tape cut off inside a frame: reported: The Theatre motion tape is cut short (damaged, or it was still being written).. Tape with no frames: reported: The Theatre motion tape has no frames.. Recipe only, with an actor without id and a cue without action: imported, mark "Actor 1", fine: True. Folder without recipe: reported. |
| U9. Window: camera rows, cue delete, look-through, playhead times | PASS | Clicking the camera's row in the left list selects it: True. Deleting a cue frees the cue that waited for it: True. Look-through ends in another tab: True. Key button before scrubbing "Key here  (00:00.00)", after scrubbing to frame 90: "Key here  (00:03.00)". |
| Runtime errors during the UI checks | PASS | None. |

Failures: 0
