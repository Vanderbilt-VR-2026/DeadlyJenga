using System.IO;
using UnityEditor;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit terminal request lets an unfocused editor import authored changes without taking focus.
    [InitializeOnLoad] public static class EditorRefreshRequest
    {
        static EditorRefreshRequest()=>EditorApplication.update+=()=>
        {
            var request=ProjectPath.Report("EditorRefresh.request");
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(request);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        };
    }
}
