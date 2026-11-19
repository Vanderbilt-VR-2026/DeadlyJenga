# Trailer Studio Sets

Saved Sets live in this folder, one `<id>.set.json` file each. The Studio
finds the folder by this file's name, so the folder can be moved in Unity.

- A Set is small: map, marks, cues, notes. It is safe to commit.
- Tapes (the recordings) are large and live outside `Assets`, in
  `Recordings/TrailerStudio/<set id>/`. They are not committed.
- Edit Sets in the Studio (**Tools → Deadly Jenga → Trailer Studio**), not by
  hand.
