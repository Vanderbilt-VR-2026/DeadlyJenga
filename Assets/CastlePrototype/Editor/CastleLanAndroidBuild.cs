#if UNITY_ANDROID
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;

namespace DeadlyJenga.CastlePrototype.Editor
{
    // Merge permissions into Unity's generated manifest without replacing its
    // OpenXR/activity configuration or requiring a networking package/service.
    public sealed class CastleLanAndroidBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            var document = XDocument.Load(manifest);
            XNamespace android = "http://schemas.android.com/apk/res/android";
            foreach (string permission in new[] { "android.permission.INTERNET", "android.permission.ACCESS_WIFI_STATE", "android.permission.CHANGE_WIFI_MULTICAST_STATE" })
                if (!document.Root.Elements("uses-permission").Any(e => (string)e.Attribute(android + "name") == permission))
                    document.Root.Add(new XElement("uses-permission", new XAttribute(android + "name", permission)));
            document.Save(manifest);
        }
    }
}
#endif
