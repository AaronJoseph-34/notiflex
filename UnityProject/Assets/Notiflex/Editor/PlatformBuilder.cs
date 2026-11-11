using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.SceneManagement;

namespace Notiflex.VR.Editor
{
    public static class PlatformBuilder
    {
        static readonly string[] Names={"Desk","Kitchen","LivingRoom","Passthrough"};
        const string SceneDir="Assets/Notiflex/Scenes";
        static string Root {get{return Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));}}
        static Material Material(string name,Color color)
        {
            string dir="Assets/Notiflex/Materials";Directory.CreateDirectory(dir);
            string path=dir+"/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Glossiness",.15f);return m;
        }
        static GameObject Shape(string name,Vector3 p,Vector3 scale,Color color,PrimitiveType type=PrimitiveType.Cube)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.position=p;g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=Material(name.Replace(" ",""),color);return g;
        }
        static ProtectedRegion Region(string name,Vector3 p,Vector2 size,RegionKind kind)
        {
            var g=new GameObject(name);g.transform.position=p;g.transform.rotation=Quaternion.Euler(0,180,0);
            var r=g.AddComponent<ProtectedRegion>();r.kind=kind;r.size=size;return r;
        }
        [MenuItem("Notiflex/Build authored environments")]
        public static void AuthorScenes()
        {
            Directory.CreateDirectory(SceneDir);
            for(int i=0;i<Names.Length;i++)
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var env=new GameObject("Environment "+Names[i]).AddComponent<EnvironmentBindings>();env.environmentName=Names[i];env.passthrough=i==3;
                if(i<3)Furnish(i,env);
                EditorSceneManager.SaveScene(scene,SceneDir+"/"+Names[i]+".unity");
            }
            EditorBuildSettings.scenes=Array.ConvertAll(Names,n=>new EditorBuildSettingsScene(SceneDir+"/"+n+".unity",true));
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(SceneDir+"/Desk.unity");
        }
        static void Furnish(int room,EnvironmentBindings env)
        {
            Color wood=new Color(.46f,.29f,.16f), dark=new Color(.12f,.18f,.21f), cream=new Color(.75f,.72f,.64f);
            Shape("Floor",new Vector3(0,-.08f,0),new Vector3(9,.15f,10),new Color(.27f,.29f,.28f));
            Shape("Back wall",new Vector3(0,1.6f,4.5f),new Vector3(9,3.2f,.15f),cream);
            Shape("Left wall",new Vector3(-4.5f,1.6f,0),new Vector3(.15f,3.2f,9),cream);
            Shape("Right wall",new Vector3(4.5f,1.6f,0),new Vector3(.15f,3.2f,9),cream*.9f);
            var light=new GameObject("Daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(45,-35,0);
            RenderSettings.ambientLight=new Color(.45f,.48f,.5f);RenderSettings.skybox=null;
            var fill=new GameObject("Room lamp").AddComponent<Light>();fill.type=LightType.Point;fill.transform.position=new Vector3(2,2.6f,0);fill.range=10;fill.intensity=1.5f;
            Shape("Window frame",new Vector3(2.3f,2,4.38f),new Vector3(1.5f,1.3f,.08f),Color.white);
            Shape("Window sky",new Vector3(2.3f,2,4.31f),new Vector3(1.36f,1.16f,.04f),new Color(.38f,.67f,.82f));
            if(room==0)
            {
                Shape("Desk surface",new Vector3(0,.78f,1.6f),new Vector3(2.6f,.12f,1.2f),wood);
                for(int x=-1;x<=1;x+=2)Shape("Desk leg "+x,new Vector3(x,.36f,1.6f),new Vector3(.1f,.72f,.7f),dark);
                Shape("Monitor bezel",new Vector3(0,1.35f,1.72f),new Vector3(1.02f,.68f,.08f),dark);
                Shape("Monitor display",new Vector3(0,1.35f,1.66f),new Vector3(.92f,.58f,.03f),new Color(.12f,.38f,.4f));
                Shape("Keyboard",new Vector3(0,.87f,1.15f),new Vector3(.7f,.05f,.22f),dark);
                for(int k=0;k<4;k++)Shape("Screen line "+k,new Vector3(-.1f,1.53f-k*.10f,1.635f),new Vector3(.55f,.018f,.01f),new Color(.55f,.8f,.75f));
                env.task=Region("Task monitor",new Vector3(0,1.35f,1.62f),new Vector2(.94f,.6f),RegionKind.Task).transform;
                Shape("Bookshelf",new Vector3(2.8f,1,3.8f),new Vector3(1.2f,2,.35f),wood);
                for(int k=0;k<7;k++)Shape("Book "+k,new Vector3(2.35f+k*.14f,1.3f,3.53f),new Vector3(.08f,.4f,.18f),new Color(.16f+k*.05f,.28f,.38f));
            }
            if(room==1)
            {
                Shape("Kitchen counter",new Vector3(0,.86f,1.7f),new Vector3(3.6f,.12f,1.2f),new Color(.5f,.54f,.54f));
                Shape("Cabinets",new Vector3(0,.4f,1.9f),new Vector3(3.4f,.8f,.8f),wood);
                for(int k=-1;k<=1;k++)Shape("Cabinet handle "+k,new Vector3(k,.6f,1.47f),new Vector3(.35f,.035f,.06f),dark);
                Shape("Cutting board",new Vector3(-.55f,.95f,1.25f),new Vector3(.65f,.035f,.4f),new Color(.68f,.46f,.23f));
                Shape("Bowl",new Vector3(-.55f,1.04f,1.3f),new Vector3(.25f,.18f,.25f),new Color(.68f,.24f,.1f),PrimitiveType.Sphere);
                Shape("Stove",new Vector3(.85f,.96f,1.6f),new Vector3(.85f,.04f,.75f),dark);
                Shape("Pot",new Vector3(.85f,1.12f,1.55f),new Vector3(.32f,.15f,.32f),new Color(.35f,.36f,.38f),PrimitiveType.Cylinder);
                Region("Protected hot surface",new Vector3(.85f,1.12f,1.29f),new Vector2(.8f,.45f),RegionKind.Hazard);
                env.task=Region("Task preparation area",new Vector3(-.55f,1.05f,1.02f),new Vector2(.7f,.45f),RegionKind.Task).transform;
                Shape("Refrigerator",new Vector3(2.8f,1.05f,3.7f),new Vector3(.95f,2.1f,.9f),new Color(.73f,.77f,.79f));
            }
            if(room==2)
            {
                Shape("Sofa seat",new Vector3(0,.4f,2.8f),new Vector3(2.6f,.4f,.85f),new Color(.25f,.4f,.43f));
                Shape("Sofa back",new Vector3(0,.9f,3.15f),new Vector3(2.6f,.75f,.2f),new Color(.22f,.36f,.39f));
                for(int x=-1;x<=1;x+=2)Shape("Sofa arm "+x,new Vector3(x*1.25f,.65f,2.8f),new Vector3(.22f,.6f,.9f),new Color(.22f,.36f,.39f));
                Shape("Coffee table",new Vector3(0,.48f,1.2f),new Vector3(1.3f,.12f,.7f),wood);
                Shape("Reading book",new Vector3(0,.57f,1.15f),new Vector3(.35f,.04f,.25f),new Color(.65f,.3f,.18f));
                env.task=Region("Task reading area",new Vector3(0,.65f,.9f),new Vector2(.6f,.4f),RegionKind.Task).transform;
                Shape("Rug",new Vector3(0,.005f,1.6f),new Vector3(3.5f,.015f,3),new Color(.5f,.46f,.37f));
            }
            Vector3 face=new Vector3(-1.5f,1.67f,1.6f);
            Shape("Conversation partner torso",face+Vector3.down*.55f,new Vector3(.5f,.75f,.30f),new Color(.25f,.38f,.55f));
            Shape("Conversation partner head",face,new Vector3(.3f,.40f,.28f),new Color(.7f,.59f,.48f),PrimitiveType.Sphere);
            env.partner=Region("Protected partner face",face-new Vector3(0,0,.16f),new Vector2(.34f,.42f),RegionKind.Face).transform;
            Shape("Plant pot",new Vector3(-3,.25f,3.4f),new Vector3(.45f,.5f,.45f),wood,PrimitiveType.Cylinder);
            Shape("Plant leaves",new Vector3(-3,.9f,3.4f),new Vector3(.8f,1.1f,.65f),new Color(.18f,.36f,.2f),PrimitiveType.Sphere);
        }
        [MenuItem("Notiflex/Configure Quest build")]
        public static void ConfigureQuest()
        {
            PlayerSettings.companyName="Notiflex Research";PlayerSettings.productName="Notiflex VR";PlayerSettings.bundleVersion="0.3.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"org.notiflex.prototype");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,"org.notiflex.prototype");
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel35;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            var meta=OVRProjectConfig.CachedProjectConfig;
            meta.targetDeviceTypes=new List<OVRProjectConfig.DeviceType>{OVRProjectConfig.DeviceType.Quest3S,OVRProjectConfig.DeviceType.Quest3};
            meta.insightPassthroughSupport=OVRProjectConfig.FeatureSupport.Supported;
            meta.handTrackingSupport=OVRProjectConfig.HandTrackingSupport.ControllersOnly;
            OVRProjectConfig.CommitProjectConfig(meta);
            PlayerSettings.runInBackground=true;PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.MTRendering=false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3});
            var serialized=new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var input=serialized.FindProperty("activeInputHandler");if(input!=null)input.intValue=2;
            // Meta Core references Unity's microphone API even though this app does not capture it.
            // Unity's Mac build scanner requires this description; it does not grant permission.
            var microphone=serialized.FindProperty("microphoneUsageDescription");
            if(microphone!=null)microphone.stringValue="The bundled XR SDK includes optional audio features. Notiflex does not record microphone input.";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var general=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if(general==null)
            {
                var all=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();Directory.CreateDirectory("Assets/XR");
                AssetDatabase.CreateAsset(all,"Assets/XR/XRGeneralSettingsPerBuildTarget.asset");EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,all,true);
                general=ScriptableObject.CreateInstance<XRGeneralSettings>();AssetDatabase.AddObjectToAsset(general,all);all.SetSettingsForBuildTarget(BuildTargetGroup.Android,general);
            }
            if(general.Manager==null){var manager=ScriptableObject.CreateInstance<XRManagerSettings>();AssetDatabase.AddObjectToAsset(manager,general);general.Manager=manager;}
            XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",BuildTargetGroup.Android);
            general.InitManagerOnStart=true;
            var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var evidence=new List<string>();
            foreach(var f in xr.GetFeatures())
            {
                string type=f.GetType().Name;
                if(type=="MetaXRFeature" || type=="MetaQuestFeature" || type.Contains("OculusTouchControllerProfile") || type.Contains("TouchPlusControllerProfile"))f.enabled=true;
                evidence.Add(type+"="+f.enabled);
            }
            Directory.CreateDirectory(Root+"/evidence");File.WriteAllLines(Root+"/evidence/openxr-features.txt",evidence);
            EditorUtility.SetDirty(general);EditorUtility.SetDirty(general.Manager);EditorUtility.SetDirty(xr);AssetDatabase.SaveAssets();
        }
        public static void BuildMac()
        {
            AuthorScenes();ConfigureQuest();Checks.Run();
            Directory.CreateDirectory(Root+"/Builds");
            var r=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,Root+"/Builds/NotiflexVR.app",BuildTarget.StandaloneOSX,BuildOptions.Development);
            if(r.summary.result!=BuildResult.Succeeded)throw new Exception("Mac build failed");
        }
        public static void BuildAndroid()
        {
            ConfigureQuest();Directory.CreateDirectory(Root+"/Builds");
            var scenes=new[]{"Passthrough","Desk","Kitchen","LivingRoom"};
            var paths=Array.ConvertAll(scenes,n=>SceneDir+"/"+n+".unity");
            var r=BuildPipeline.BuildPlayer(paths,Root+"/Builds/NotiflexVR.apk",BuildTarget.Android,BuildOptions.Development);
            if(r.summary.result!=BuildResult.Succeeded)throw new Exception("Android build failed");
        }
        public static void BuildMacAndRender()
        {
            BuildMac();Checks.RenderEvidence();
        }
    }
}
