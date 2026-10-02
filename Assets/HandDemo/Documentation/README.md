# Flying Hand

Open the tracked ROOT Unity project at C:/Users/evanp/Downloads/DeadlyJenga.
See IMPORT_GUIDE.md for controls and validation, ROOT_TRANSFER.md for transfer and root-validation results, and QUEST_CONFIGURATION.md for the precise host settings changes.

The demo is a 650 kg force-driven flying hand with compound primitive colliders, a third-person XR camera rig, a platform and dynamic test crates. The complete FlyingHandQuest scene includes its XR rig; FlyingHandPlayer.prefab contains the player only. Visuals can be replaced independently of colliders. Camera obstacle avoidance is not implemented.

The feature is in Assets/HandDemo. Its materials use the root project's existing URP pipeline; runtime code has no URP dependency. Older validation files record source/nested-project runs.
