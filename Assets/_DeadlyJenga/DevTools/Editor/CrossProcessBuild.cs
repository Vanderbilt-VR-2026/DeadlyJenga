using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class CrossProcessBuild
    {
        public static void RunBatch()
        {
            var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName="Builds/IntegrationSweep/DeadlyJenga.exe",target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development});
            if(build.summary.result!=BuildResult.Succeeded)throw new Exception("Independent player build failed: "+build.summary.result);
            EditorApplication.Exit(0);
        }
    }
}
