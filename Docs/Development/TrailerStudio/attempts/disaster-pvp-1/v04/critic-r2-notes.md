# Critic round 2 (fresh Opus critic, fine cut before the S08 retime)

Planted-defect check: passed (named the defective pack; 3 of 3: S05 key swapped for an S03 frame, rock_purple sync
removed, title +5 frames). Weighted (strict /5): real 3.13, planted 2.48 (gap 0.65). Round 1 was 2.98.

Round 1 problems: green ellipse fixed; payoff out-scales the build (fixed; S09 one rock about 0.6 of frame, shower
clutter remains); hero continuity partly fixed.

Remaining top three (critic):
1. S08: the hero's body turns lavender-white within the shot (f286 on). Response: S08 now uses only its clean render
   frames 265-285, stretched 21 -> 24 frames (0.875x, three repeated frames). The cause is NOT found; it looks like
   the motion-blur ghosting again, starting when the scream layer moves the head, despite the skinned-vector fix.
   Logged as TOOL-BROKEN.
2. Rival continuity: the punched rival respawns on the summit (the game's own respawn, deaths off on the stage) and is
   beside the hero in S05/S06. Not fixed (needs a re-render with his track muted after f60, or a re-record).
3. Mid-section readability: S05/S07 victims are 0.07-0.08 of frame height and the rocks read as smudges; S01's rival
   leaves frame within 3-6 frames. Not fixed (needs closer cameras and a re-render).
Also noted: hero reads lime under the red castle light (S08, S10); no geography bridge summit -> castle wall; the grade
jumps when the title darkening starts; title drop -4.6 LU (anchor -6).

Stopped: critic cap (2 rounds) spent and the Unity job budget exceeded (18 against about 15). Score rose 0.15.
