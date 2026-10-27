Completed in slot 05, Unity 6000.3.23f1, desktop Editor.

Final run: 82 passing assertions, zero reported errors.
Checks include map reachability and classification, lobby/desktop/VR picker collections, real lobby-to-Game departure, cancellation and arrival cleanup. Quest headset rendering was not tested.

Gallery: index.html (8 actual Unity images). Beacon phases were controlled for review; actual departure and cleanup were separately exercised. World renders are 2000x1250; overlay/menu screenshots retain their native Game-view capture resolution.

Presentation/Visuals: desktop beacon now draws over IMGUI hints; host/map decorative stands removed while JOIN stand and keyboard supports remain. DevTools/Editor: captures and safe scene/view restoration. No Core changes.

OwnedChanges.patch contains only the three owned files changed after the snapshot. TestedSourceHashes.json identifies exact tested bytes. This is a test-only dirty-source overlay, not the old baseline commit. Nothing was merged or pushed. Earlier diagnostic reports are retained separately; use Checks.txt and Evidence.json for the final result.
