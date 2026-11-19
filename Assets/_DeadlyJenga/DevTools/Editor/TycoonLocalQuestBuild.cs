using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.DevTools.Editor
{
    public static class TycoonLocalQuestBuild
    {
        public static void RunBatch()
        {
            string report=ProjectPath.Report("TycoonLocalQuestBuild.txt");
            try
            {
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)throw new Exception("Launch with -buildTarget Android");
                var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-questOutput");
                if(index<0||index+1>=args.Length)throw new Exception("Explicit -questOutput APK path required");
                string output=Path.GetFullPath(args[index+1]);if(File.Exists(output))throw new Exception("Refusing to replace existing APK");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))throw new Exception("Android support is not installed");
                PlayerSettings.productName="Deadly Jenga";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.deadlyjenga.quest3");
                PlayerSettings.bundleVersion="0.4.0";PlayerSettings.Android.bundleVersionCode=20261112;
                PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
                PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel35;
                PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
                PlayerSettings.Android.useCustomKeystore=false;
                PlayerSettings.Android.splitApplicationBinary=false;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android,Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android,ManagedStrippingLevel.Low);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
                EditorUserBuildSettings.buildAppBundle=false;EditorUserBuildSettings.exportAsGoogleAndroidProject=false;
                EditorUserBuildSettings.androidBuildSubtarget=MobileTextureSubtarget.ASTC;
                EditorUserBuildSettings.development=false;
                var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                if(!xr||!xr.GetFeature<HybridHandsFeature>()||!xr.GetFeature<HybridHandsFeature>().enabled)throw new Exception("Hybrid OpenXR feature is missing or disabled");
                var eyeGaze=xr.GetFeature<UnityEngine.XR.OpenXR.Features.Interactions.EyeGazeInteraction>();
                if(eyeGaze){eyeGaze.enabled=false;EditorUtility.SetDirty(eyeGaze);}
                var quest=xr.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>().FirstOrDefault(f=>f.GetType().FullName=="UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature");
                if(!quest||!quest.enabled)throw new Exception("Meta Quest OpenXR support is required");
                var questSettings=new SerializedObject(quest);
                var targets=questSettings.FindProperty("targetDevices");
                if(targets==null||!targets.isArray)throw new Exception("Quest device settings were not found");
                bool quest3Found=false;
                for(int targetIndex=0;targetIndex<targets.arraySize;targetIndex++)
                {
                    var target=targets.GetArrayElementAtIndex(targetIndex);
                    string name=target.FindPropertyRelative("manifestName").stringValue;
                    if(name=="cambria")target.FindPropertyRelative("enabled").boolValue=false;
                    if(name=="eureka"){target.FindPropertyRelative("enabled").boolValue=true;quest3Found=true;}
                    if(name=="quest3s")target.FindPropertyRelative("enabled").boolValue=true;
                }
                if(!quest3Found)throw new Exception("Quest 3 target is unavailable");
                questSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(quest);
                var composition=xr.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>().FirstOrDefault(f=>f.GetType().Name=="OpenXRCompositionLayersFeature");
                if(!composition)throw new Exception("Composition Layers Support feature is unavailable");
                composition.enabled=true;EditorUtility.SetDirty(composition);
                var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
                if(scenes.Length<3||!scenes[0].EndsWith("/Home.unity"))throw new Exception("Home must be first in complete build scene list");
                File.WriteAllText(report,"Quest 3 standalone APK build\nARM64 IL2CPP Release; Vulkan; ASTC; min API32 / target API35; self-contained APK; debug signing; Home first.\n"+string.Join("\n",scenes)+"\n");
                AssetDatabase.SaveAssets();
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=output,target=BuildTarget.Android,options=BuildOptions.None});
                File.AppendAllText(report,$"RESULT {result.summary.result}; bytes={result.summary.totalSize}; duration={result.summary.totalTime}; errors={result.summary.totalErrors}; warnings={result.summary.totalWarnings}\n");
                foreach(var step in result.steps)foreach(var message in step.messages)
                    if(message.type==LogType.Error||message.type==LogType.Exception)File.AppendAllText(report,step.name+": "+message.content+"\n");
                if(result.summary.result!=BuildResult.Succeeded||!File.Exists(output))throw new Exception("Quest APK build failed: "+result.summary.result);
                File.WriteAllText(ProjectPath.Report("TycoonLocalQuestBuild.json"),JsonUtility.ToJson(new Receipt{project=Directory.GetParent(Application.dataPath).FullName,apk=output,bytes=new FileInfo(output).Length,result="Succeeded",errors=result.summary.totalErrors,warnings=result.summary.totalWarnings,unity=Application.unityVersion,package="com.deadlyjenga.quest3"},true));
                EditorApplication.Exit(0);
            }
            catch(Exception error){File.AppendAllText(report,"FAIL "+error+"\n");Debug.LogException(error);EditorApplication.Exit(2);}
        }
        [Serializable] sealed class Receipt
        {public string project,apk,result,unity,package;public long bytes;public int errors,warnings;}
    }
}
