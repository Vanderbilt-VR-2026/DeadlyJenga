# Trailer Studio Phase4 acceptance run

Date: 2026-11-18 13:09  
Unity: 6000.3.23f1  
Map: sparse-viaduct-study

| Test | Result | Detail |
|------|--------|--------|
| Setup: three performed takes | PASS | Ada, Ben and Cy each performed a 6 s take. Ada walked 5.92 m. |
| 4. A 20-person crowd from 3 performed takes, with no two people in step | PASS | 20 people made from Ada's, Ben's and Cy's takes (20 different outfits). Closest two start 0.94 m apart (asked for 0.90). Comparing every pair's speed frame by frame: 0 pairs move in step; the most alike pair still differs by 0.10 m/s on average. 23 actors on screen at once in playback. People placed with nothing under their feet: 0. Notes: none Pictures: crowd/. |
| 1. Retime 1 s of a walk to half speed; feet do not slide | PASS | Frames 24 to 54 of Ada's take now last 60 frames (180 frames in the new take; the old take is still on the track). Every slowed pose matches the original performance (largest difference 0.0 mm); body speed went from 1.61 to 0.80 m/s. Planted-foot slide along the ground, measured on the avatar's own foot bones (Left foot, Rounded candy foot) during playback: 1.66 m/s before the edit (body 1.61 m/s), 0.80 m/s after it (body 0.79 m/s). After "Lock feet", the feet are within 0.0 mm of the surface on 10 checked frames. Pictures: retime/. |
| 2. A recorded block hits an actor and triggers a knockback that blends back into the take | PASS | Block #59 was pushed at 1.0 s; Ben stands in its path. Ben: block #59 reaches them at 1.40 s moving 6.0 m/s. (The block is at Ben's spot at 1.50 s.) Just before the hit Ben is on his take (0.0 mm off). Half a second after it he has been thrown 1.25 m, in the direction the block was moving (alignment 0.96, 1 = exactly). At the end of the reaction he is back on his take (0.0 mm off). The take itself was not changed. Pictures: knockback/. |
| 3. A look-at override on 5 actors toward the incoming tornado | PASS | Tornado "Tornado" from heading 90, recorded on the hazard track. At 3.0 s, angle between each head and the tornado, before -> with the layer: Ada 110° -> 25°, Ben 34° -> 0°, Cy 111° -> 26°, Cy clone 1 105° -> 20°, Cy clone 2 136° -> 51°. 5 of 5 turned toward it; 5 of 5 look straight at it or as far as a neck turns (85°). The takes underneath are unchanged. Pictures: look-at/. |
| Runtime errors during the Studio tests | PASS | None. |

Failures: 0
