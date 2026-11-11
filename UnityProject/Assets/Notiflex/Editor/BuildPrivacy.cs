using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Notiflex.VR.Editor
{
    // Meta's default build hook injects a local agent credential even when unused.
    // Run after that hook so this research app contains no remote agent connection.
    public sealed class BuildPrivacy : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 10;
        public static void Sanitize()
        {
            var asset=AssetDatabase.LoadMainAssetAtPath("Assets/Resources/DevAgentSettings.asset");
            if(asset==null)return;
            var settings=new SerializedObject(asset);
            var enabled=settings.FindProperty("enabled");if(enabled!=null)enabled.boolValue=false;
            foreach(string field in new[]{"accessToken","witClientAccessToken"})
            {var property=settings.FindProperty(field);if(property!=null)property.stringValue="";}
            var address=settings.FindProperty("serverAddress");if(address!=null)address.stringValue="127.0.0.1";
            settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();
        }
        public void OnPreprocessBuild(BuildReport report){Sanitize();}
        public void OnPostprocessBuild(BuildReport report){Sanitize();}
    }
}
