using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    public static class NearbyBluetoothChecks
    {
        public static void RunApkBatch()
        {
            try{Validate();TycoonLocalQuestBuild.RunBatch();}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
        public static void ValidateBatch()
        {
            try{Validate();EditorApplication.Exit(0);}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
        public static void Validate()
        {
            int checks=0;
            void Check(bool valid,string label){if(!valid)throw new Exception(label);checks++;}
            Check(NearbyPlayerProfile.Clean("  Evan  ")=="Evan","Trim player name");
            Check(NearbyPlayerProfile.Clean("Evan\n\t<>")=="Evan","Control characters and UI markup excluded");
            Check(NearbyPlayerProfile.Clean(new string('a',80)).Length==24,"Name bound");
            Check(NearbyPlayerProfile.Clean("Renée-42")=="Renée-42","Unicode name retained");
            Check(NearbyPlayerProfile.Clean(null)=="","Missing name handled");
            Check(NearbyPlayerProfile.Clean("e\u0301")=="é","Canonical Unicode");
            string file=ProjectPath.Find("NearbyBluetooth.java");
            var importer=AssetImporter.GetAtPath(file) as PluginImporter;
            Check(importer&&importer.GetCompatibleWithPlatform(BuildTarget.Android),"Native source included in Android");
            Check(!importer.GetCompatibleWithEditor(),"Native source excluded from Editor");
            string source=File.ReadAllText(file);
            Check(!source.Contains("com.oculus")&&!source.Contains("OVR"),"No Meta account discovery API dependency");
            var roomFields=typeof(DeadlyJenga.Core.NearbyRoomInfo).GetFields().Select(f=>f.Name).ToArray();
            Check(!roomFields.Any(f=>f.IndexOf("code",StringComparison.OrdinalIgnoreCase)>=0),"Public discovered room rows contain no join code");
            string report=ProjectPath.Report("NearbyBluetoothChecks.txt");
            File.WriteAllText(report,"PASS "+checks+" profile/import/contract checks. Not a Bluetooth radio or Quest runtime test.\n");
            Debug.Log("Nearby Bluetooth: "+checks+" profile/import/contract checks passed; hardware pending.");
        }
    }
}
