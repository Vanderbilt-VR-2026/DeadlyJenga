# Trailer Studio: research notes

How other teams make scripted gameplay moments, and what Trailer Studio takes
from each. Gathered 2026-11-18 from public docs and write-ups; some sources
are community guides, so treat feature details as approximate.

## The common pattern

Studios record gameplay once, make the recording deterministic, and then film
it as many times as they like. Physics is cached rather than re-simulated,
because physics engines rarely give the same result twice. Cameras are placed
on the replay afterward.

## Tools and what we borrow

| Source | What it does | What we take |
|--------|--------------|--------------|
| **Unreal Take Recorder** | Records gameplay actors and live performances into Sequencer as takes, keeps a take history, records the player in Play-in-Editor. | Takes per track, take history, "record into the current sequence", choosing which actors are sources. Avoid its flaw: new takes overwriting old data. |
| **Marvel Rivals trailer pipeline (NetEase, Unreal Fest 2024)** | Combined the game's replay system with Take Recorder to film gameplay trailers efficiently. | Proof that replay + take recording is how a real studio ships gameplay trailers. |
| **Unreal Chaos Cache Manager** | Records a physics simulation once and plays it back in Sequencer; record and playback are explicit modes. | The world track: record mode, playback mode, and caches per branch. Watch disk size. |
| **Rockstar Editor (GTA V)** | Clip markers that change camera, speed, filters and depth of field from that point on; slow-motion speed ramps between markers; blend easing between cameras; Director Mode for cast, weather and time. | Markers as the main editing gesture, speed ramps, blend easing, a Set page for cast/weather/time. |
| **Halo Theater / game replay modes** | Saved matches you can scrub and film from any angle. | Free scrub of any tape, film any time later. |
| **Source Filmmaker / s&box Movie Maker** | Record gameplay into clips, then edit motion over time; smoothing and jitter presets on motion paths; IK puppeting; property tracks you record during Play Mode. | Motion editing on recorded tracks (smooth, retime), IK overrides, "record any bound property". |
| **Dreams (Media Molecule)** | Puppeteered performance recording layered part by part, with a timeline showing animation, audio and effects; redo any part of a take. | Layered performance with punch-in, timeline that shows everything together, friendliness. |
| **Virtual cameras (Unreal VCam, Kert Gartner's VR camera, God of War handheld capture)** | A tracked controller or phone drives the CG camera so an operator films by hand. | VR handheld camera mode in Phase 5. |
| **Microsoft Flight Simulator capture tool** | Forces a fixed frame rate and renders frame by frame from the replay; shows camera paths. | Fixed capture clock (Theatre already has this) and visible camera paths. |
| **EA game capture team** | Capture artists plan shots in advance with leads, then run focused capture sessions. | Shot list and notes per Set, so sessions are planned. |
| **Racing game ghosts (e.g. Trackmania)** | Earlier runs replay as ghosts while you drive. | Ghosts of earlier passes while performing. |

## Sources

- [Take Recorder (Unreal docs)](https://dev.epicgames.com/documentation/en-us/unreal-engine/take-recorder-in-unreal-engine)
- [Record gameplay in Unreal](https://dev.epicgames.com/documentation/unreal-engine/record-gameplay-in-unreal-engine)
- [Take Recorder overwrite note (forum)](https://forums.unrealengine.com/t/is-it-possible-to-record-an-entire-gameplay-with-all-the-actors-to-sequencer/459456)
- [Marvel Rivals trailer pipeline talk](https://forums.unrealengine.com/t/talks-and-demos-marvel-rivals-efficient-cinematic-gameplay-trailer-production-pipeline-using-take-recorder-unreal-fest-2024/2152666)
- [Chaos Cache Manager API](https://dev.epicgames.com/documentation/unreal-engine/API/Plugins/ChaosCaching/AChaosCacheManager)
- [Using fracture with Sequencer (Versluis)](https://www.versluis.com/2025/06/using-fracture-with-sequencer-in-unreal-engine/)
- [Inconsistent Chaos simulations (DEV)](https://dev.to/dinesh_04/how-to-fix-inconsistent-chaos-or-niagara-simulations-in-unreal-engine-2nhm)
- [Rockstar Editor guide (PC Gamer)](https://www.pcgamer.com/get-the-most-out-of-gta-5s-video-editor)
- [Rockstar Editor (GTABase)](https://www.gtabase.com/grand-theft-auto-v/rockstar-editor/)
- [Director Mode guide (Prima)](https://primagames.com/featured/gta-5-director-mode-beginners-guide-to-filmmaking)
- [Source Filmmaker (Valve wiki)](https://developer.valvesoftware.com/wiki/Source_Filmmaker)
- [s&box Movie Maker recording](https://sbox.game/dev/doc/movie-maker/recording)
- [Dreams cutscenes and puppeteering at GDC](https://www.impspace.com/?p=3114)
- [Virtual cameras in gamedev (80.lv)](https://80.lv/articles/using-virtual-cameras-in-gamedev-filmmaking)
- [VR camera legacy tools (Unreal)](https://dev.epicgames.com/documentation/unreal-engine/vr-camera-and-light-legacy-tools)
- [MSFS video capture tool](https://docs.flightsimulator.com/msfs2024/html/2_DevMode/Menus/Tools/The_Video_Capture_Tool.htm)
- [EA game capture team (LBB)](https://lbbonline.com/news/how-electronic-arts-game-capture-team-strives-for-visceral-authenticity)
- [Creating a replay system (Game Developer)](https://www.gamedeveloper.com/programming/what-just-happened-creating-a-replay-system)
- [Fortnite trailer layout (CGW)](https://www.cgw.com/Publications/CGW/2018/Volume-41-Issue-2-Edition-2-2018-/In-a-Fortnite.aspx)
