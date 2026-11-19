# Larger portraits and dancing scoreboard podium

The elimination feed uses 76-unit portraits. Live standings use 70-unit portraits and round results use 74-unit portraits. The shared portrait renderer captures 144-pixel stills.

Live standings and results show the first three players from the full ranking beside the list. The podium copies the available avatar meshes, skin and accessories, then animates the copies. It renders at up to 20 frames per second while visible. Infection uses the primary survival ranking. Murder Mystery keeps its unranked live roster and omits the podium. Results show five rows per page so the portraits have room.

The checkpoint is on `codex/scoreboard-dancing-podium`, based on `Evan_Sprint_4` at `fe7b42ca070acc103c8f00742ce4645bbffb34ff`. The Presentation code uses existing match and avatar interfaces.

Unity's bundled C# compiler passed for Core and Presentation using current source and cached Unity references. Reference canvas geometry checks passed for regular and split standings, the feed and paging. Play Mode is pending. Check three distinct avatars, a late avatar, closing and reopening the board, page changes, long names, and desktop and VR framing. Headset rendering and performance require a Quest run.
