using System.IO;
using UnityEditor;
namespace DeadlyJenga.CastleSocial.Editor
{
    public static class SocialIntegrationMigration
    {
        public static void RunBatch()
        {
            string source=Path.GetDirectoryName(ProjectPath.FolderOf("SocialSettings.cs").TrimEnd('/')).Replace('\\','/');
            string target=Path.GetDirectoryName(ProjectPath.FolderOf("CastlePlaySession.cs").TrimEnd('/')).Replace('\\','/')+"/CastleSocial";
            if(source!=target)
            {
                string result=AssetDatabase.MoveAsset(source,target);
                if(!string.IsNullOrEmpty(result))throw new IOException(result);
                AssetDatabase.DeleteAsset(target+"/CastleSocial.Runtime.asmref");
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            File.WriteAllText(ProjectPath.Report("SocialAssetMigration.txt"),"Social assets moved through AssetDatabase, with GUIDs preserved: "+target);
        }
    }
}
