using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using DeadlyJenga.CastleSocial;

namespace DeadlyJenga.Session.Editor
{
    public sealed class OnDeviceConversationBuild : BuildPlayerProcessor
    {
        public override void PrepareForBuild(BuildPlayerContext context)
        {
            string pack=Path.GetFullPath(Path.Combine(Application.dataPath,"../Tools/OnDeviceConversation/.models/pack"));
            if(File.Exists(Path.Combine(pack,"manifest.json")))context.AddAdditionalPathToStreamingAssets(pack,"CastleConversation");
            else Debug.LogWarning("On-device conversations omitted: run Tools/OnDeviceConversation/prepare.py before building to include the model pack. All other game features remain available.");
        }
        [InitializeOnLoadMethod] static void Hook()
        {
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode)Locate();};
        }
        static void Locate()
        {
            string dll;try{dll=ProjectPath.Find("deadlyjenga_conversation.dll");}catch(FileNotFoundException){return;}
            if(!string.IsNullOrEmpty(dll))OnDeviceConversation.SetEditorLibraries(Path.GetDirectoryName(Path.GetFullPath(dll)));
        }
    }
}
