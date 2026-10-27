using System.IO;
using UnityEditor;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit terminal request lets an unfocused editor import authored changes without taking focus.
    [InitializeOnLoad] public static class EditorRefreshRequest
    {
        static EditorRefreshRequest()=>EditorApplication.update+=()=>
        {
            var request=ProjectPath.Report("EditorRefreshMain.request");
            if(!File.Exists(request)||File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString()||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(request);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        };
    }
}
