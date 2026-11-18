using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Holds the Set as text so Unity's own Undo (Ctrl+Z, Edit menu) can step through Set edits.
    public sealed class StudioUndoState : ScriptableObject
    {
        public string setId = "";
        [TextArea] public string json = "";
    }
}
