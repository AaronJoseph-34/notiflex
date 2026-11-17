using System.IO;
using System.Xml;
using UnityEditor.Android;
namespace Notiflex.VR.Editor
{
    // Permission prompts are issued by each sensor after XR focus, rather than
    // Unity eagerly asking for every permission before the user enters the app.
    public sealed class AndroidPermissionPolicy : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder=>100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest=Path.Combine(path,"src/main/AndroidManifest.xml");var doc=new XmlDocument();doc.Load(manifest);
            var application=doc.SelectSingleNode("/manifest/application");if(application==null)throw new InvalidDataException("Android application node missing");
            const string ns="http://schemas.android.com/apk/res/android";XmlElement target=null;
            foreach(XmlNode node in application.ChildNodes)if(node is XmlElement e&&e.Name=="meta-data"&&e.GetAttribute("name",ns)=="unityplayer.SkipPermissionsDialog")target=e;
            if(target==null){target=doc.CreateElement("meta-data");application.AppendChild(target);}target.SetAttribute("name",ns,"unityplayer.SkipPermissionsDialog");target.SetAttribute("value",ns,"true");doc.Save(manifest);
        }
    }
}
