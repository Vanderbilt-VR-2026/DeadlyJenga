# Stage 2 music map, disaster-pvp-1

Source: the project's own `ArtDirection/HomeMenuMusic-Hype-20261026/02-collapse-protocol.wav` ("industrial arena groove:
crunchy low riffs, half-time impacts", 140 BPM, score sections: launch 0-4 bars, full 4-16, breakdown 16-20, build 20-24,
final 24-32). Neon Aftershock was used by hand-reveal-2, so it was not reused.

The track is flat-loud (about -17 to -19 dB RMS) except the breakdown, so it was edited on bar lines (bar = 1.714 s):

| Trailer s | Source s | What |
|---|---|---|
| 0.000-2.571 | 25.714-28.286 | last full bar, then the breakdown starts at 1.714 (a musical stop as the rival flies) |
| 2.571-9.886 | 33.429-40.743 | breakdown tail, build from 3.43 s rising to the drop |
| 9.886-10.286 | silence | a 0.4 s suck-out (stop-down) before the drop |
| 10.286-15.000 | 41.143-45.857 | the drop and the final section (ducked under the title in the mix) |

Measured on the edit (`tools/music_map.py`, `music-map/music.yaml`): 140.6 BPM; hits with rise >= 6 dB at
**0.84 s (f25.2, punch)**, **3.41 s (f102.3, build start)**, **10.26 s (f307.8, main: the drop)**, 14.55 s; stop-downs
2.0-3.42 s and **9.90-10.27 s**; beats every 12.86 frames through the build (f128, 141, 154, 167, 180, 193, 205, 218, 231, 244,
257, 270, 283, 296). Title hit candidates: 11.12 s (f333.6, strength 15.0) and **11.98 s (f359.4, strength 13.0)**.

Picture moved to the measured hits (never the reverse): punch f25, build/first rocks f100-102, aimed meteors on the
build's beats (f154 ... f283), planet killer contact f308, title f359.
