using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace DeadlyJenga.CastlePrototype.Editor
{
    // Adds only the direct Bluetooth permissions to generated Gradle output.
    public sealed class NearbyBluetoothManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder=>100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string file=Path.Combine(path,"src/main/AndroidManifest.xml");
            var doc=new XmlDocument();doc.Load(file);var root=doc.DocumentElement;
            const string ns="http://schemas.android.com/apk/res/android";
            void Permission(string name,string maximum=null,bool neverLocation=false)
            {
                XmlElement element=null;
                foreach(XmlElement child in root.SelectNodes("uses-permission"))if(child.GetAttribute("name",ns)==name){element=child;break;}
                if(element==null){element=doc.CreateElement("uses-permission");element.SetAttribute("name",ns,name);root.AppendChild(element);}
                if(maximum!=null)element.SetAttribute("maxSdkVersion",ns,maximum);
                if(neverLocation)element.SetAttribute("usesPermissionFlags",ns,"neverForLocation");
            }
            Permission("android.permission.BLUETOOTH","30");Permission("android.permission.BLUETOOTH_ADMIN","30");
            Permission("android.permission.ACCESS_FINE_LOCATION","30");
            Permission("android.permission.BLUETOOTH_SCAN",neverLocation:true);
            Permission("android.permission.BLUETOOTH_ADVERTISE");Permission("android.permission.BLUETOOTH_CONNECT");
            var feature=doc.CreateElement("uses-feature");feature.SetAttribute("name",ns,"android.hardware.bluetooth_le");feature.SetAttribute("required",ns,"false");root.AppendChild(feature);
            doc.Save(file);
        }
    }
}
