using System.IO;
using UnityEditor;

public class ProjectPath
{
    public static string Find(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);
        foreach (var guid in AssetDatabase.FindAssets(name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path) == fileName) return path;
        }
        throw new FileNotFoundException("Could not find " + fileName + " in the project.");
    }

    public static string FolderOf(string fileName) =>
        Path.GetDirectoryName(Find(fileName)).Replace('\\', '/') + "/";

    public static string Report(string fileName)
    {
        Directory.CreateDirectory("Logs/Validation");
        return "Logs/Validation/" + fileName;
    }
}
