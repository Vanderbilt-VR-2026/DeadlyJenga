# Red vs Blue / random disasters — 20-second concept trailer

`DeadlyJenga_RedBlue_Disaster_Concept_20s.mp4`: exactly 20 seconds, 600 frames, 30 fps, 1280×720, H.264 video with stereo AAC audio. This is an art-driven animated concept edit, not recorded Unity gameplay or fully AI-animated character footage. Source game scripts and scenes were not changed.

## Edit

- 0–6.5s: the same white/red/blue tower; hold-to-claim progress, capture confirmation and changing team scores. Random-event roll followed by a 3-second meteor warning begins at 3.5s.
- 6.5–10.5s: meteor impact and escape paths, falling blocks and camera shake.
- 10.5–15.5s: roaming small AI gloves plus a much larger water spirit whose splashes/nudges are localized. Another event roll and 3-second flood warning begin at 12.5s.
- 15.5–18.2s: flood consumes low routes while teams keep climbing and capturing.
- 18.2–20s: title and concept-trailer end card.

Camera motion, dissolves, warning countdowns, changing score, capture bar, animated spark/droplet/wave accents and sound are composited locally. Poses and base environment are generated keyframe artwork. The proposed meteor/flood events are editorially chosen examples of the random-disaster system, not a simulated random match.

## Artwork / prompts

Four clean wide keyframes were generated with the built-in image tool using the supplied red-vs-blue castle as the main visual reference. Prompts preserve the same multilevel linked tower, glossy white neutral blocks and red/blue team ownership, with only red/blue players and no baked UI:

1. Contested foreground bridge; blue player holds a white block to capture it, red opponents approach.
2. Varied-size meteor shower strikes an outside tower while survivors escape across intact bridges.
3. Small white AI gloves patrol; one large turquoise water spirit uses gently cupped splashes and modest nudges, leaving the main tower intact.
4. Rising flood submerges lower tower levels; red and blue teams climb onto dry upper routes and keep claiming blocks.

Audio uses the existing project music, surf, flight, block/contact and splash assets plus original synthesized warning/capture/end-card tones. `trailer-mix.wav` is the editable stereo mix. No external commercial song was added.

`render_trailer.py` reproduces the local composition from the four PNGs, Pillow/numpy, the existing game audio folder and the installed File Converter ffmpeg. `trailer-manifest.json` contains timings and camera settings. `review-contact-sheet.png` and `poster.png` are review renders; `validation.log` confirms a complete 600-frame decode and 20.00-second duration; `audio-validation.log` records decoded audio levels.

Magnific account setup succeeded but the account had zero credits and no available video models. The user explicitly selected the local animated concept-trailer route.
