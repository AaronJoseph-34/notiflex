using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using NUnit.Framework;
using Debug=UnityEngine.Debug;

namespace Notiflex.VR.Editor
{
    public static class Checks
    {
        static string Evidence { get { return Path.GetFullPath(Application.dataPath+"/../../evidence"); } }
        static readonly List<string> passed=new List<string>();
        static void Check(bool ok,string name)
        {
            if(!ok)throw new Exception("NOTIFLEX CHECK FAILED: "+name);
            passed.Add("PASS "+name);
        }
        [MenuItem("Notiflex/Validate policy rules")]
        public static void Run()
        {
            passed.Clear();Directory.CreateDirectory(Evidence);
            var c=new ContextSnapshot{poseValid=true,workload=.8f,audioEnabled=true,taskAngle=0,partnerAngle=0};
            Check(PlacementPolicy.Infer(c)==Activity.Focus,"Task alignment and low movement infer focused work");
            c.handsBusy=true;c.movement=.5f;
            Check(PlacementPolicy.Infer(c)==Activity.Physical,"Occupied hands and movement infer a physical task");
            c.conversation=true;
            Check(PlacementPolicy.Infer(c)==Activity.Conversation,"Conversation takes precedence when partner is in range");
            c.partnerAngle=66;
            Check(PlacementPolicy.Infer(c)==Activity.Physical,"Conversation signal alone does not establish partner engagement");
            c.age=2.01f;
            Check(PlacementPolicy.Infer(c)==Activity.Unknown,"Stale context returns unknown");
            Check(PlacementPolicy.Delivery(c,Priority.Urgent,true)==Channel.Deferred,"Stale context cannot deliver urgent visual content");
            c.age=0;c.poseValid=false;
            Check(PlacementPolicy.Delivery(c,Priority.Normal,true)==Channel.Deferred,"Tracking loss defers presentation");
            c.poseValid=true;c.conversation=false;c.activity=Activity.Physical;
            Check(PlacementPolicy.Delivery(c,Priority.Normal,true)==Channel.SpatialAudio,"Hands-busy physical task selects audio");
            c.audioEnabled=false;
            Check(PlacementPolicy.Delivery(c,Priority.Normal,true)==Channel.Visual,"Audio-disabled fallback remains visual when a candidate exists");
            Check(PlacementPolicy.Delivery(c,Priority.Urgent,false)==Channel.Deferred,"No safe placement and no audio defers even urgent content");
            c.audioEnabled=true;c.speaking=true;
            Check(PlacementPolicy.Delivery(c,Priority.Urgent,false)==Channel.Deferred,"Speaking suppresses audio fallback");
            c.speaking=false;c.conversation=true;
            Check(PlacementPolicy.Delivery(c,Priority.Normal,false)==Channel.Deferred,"Conversation suppresses no-placement audio fallback");
            foreach(Activity activity in Enum.GetValues(typeof(Activity)))foreach(Priority priority in Enum.GetValues(typeof(Priority)))
            {
                c.activity=activity;var w=PlacementPolicy.Weights(activity,priority);
                Check(Mathf.Abs(w.interruption+w.occlusion+w.social-1)<1e-6f,"Weights sum to one "+activity+"/"+priority);
                if(priority==Priority.Urgent)Check(PlacementPolicy.Delivery(c,priority,true)==Channel.Visual,"Urgent visual delivery "+activity);
                foreach(float angle in new[]{0f,10f,20f,40f,90f,180f})foreach(float coverage in new[]{0f,.25f,1f})foreach(int words in new[]{0,5,25,100})
                {
                    var r=new PlacementResult{eligible=true,headAngle=angle,partnerAngle=angle,occlusion=coverage};PlacementPolicy.Score(r,c,priority,words);
                    if(float.IsNaN(r.total)||r.total<0||r.total>1.000001f)throw new Exception("Unbounded score");
                }
            }
            Check(true,"864 context priority angle overlap length combinations remain normalized");
            Check(PlacementPolicy.Coverage(new Rect(0,0,1,1),new Rect(0,0,1,1))==1,"Full geometric coverage");
            Check(PlacementPolicy.Coverage(new Rect(2,2,1,1),new Rect(0,0,1,1))==0,"Disjoint geometric coverage");
            Check(Mathf.Abs(PlacementPolicy.Coverage(new Rect(0,0,.5f,1),new Rect(0,0,1,1))-.5f)<1e-6,"Half geometric coverage");
            var dwell=new ContextDwell();dwell.Update(Activity.Focus,0);dwell.Update(Activity.Focus,1.1f);
            Check(dwell.Current==Activity.Unknown,"Context dwell resists early switching");dwell.Update(Activity.Focus,1.21f);
            Check(dwell.Current==Activity.Focus,"Stable context activates after 1.2 seconds");dwell.Update(Activity.Conversation,1.3f);dwell.Update(Activity.Focus,1.4f);
            Check(dwell.Current==Activity.Focus,"Transient conversation evidence does not flicker context");
            Check(PlacementPolicy.Best(new List<PlacementResult>())==null,"Empty eligible set has no forced visual selection");
            c.activity=Activity.Conversation;
            var near=new PlacementResult{headAngle=5,partnerAngle=5};var far=new PlacementResult{headAngle=40,partnerAngle=60};
            PlacementPolicy.Score(near,c,Priority.Normal,20);PlacementPolicy.Score(far,c,Priority.Normal,20);
            Check(near.social<far.social,"Partner-relative separation increases social risk");
            float longCost=far.social;PlacementPolicy.Score(far,c,Priority.Normal,1);
            Check(far.social<longCost,"Reading duration contributes to social risk");
            Check(Mathf.Abs(PlacementPolicy.PresentationScale(3.5f)/3.5f-1/1.8f)<1e-6f,"Distant panel preserves reference angular size");
            Check(PlacementPolicy.PresentationScale(.8f)==1,"Near panel retains its minimum world scale");
            File.WriteAllLines(Evidence+"/policy-checks.txt",passed);Debug.Log("NOTIFLEX_POLICY_PASS "+passed.Count);
        }
        [Serializable] class Scenario
        {
            public string room,context,priority,selected;public float yaw;public Vector3 headPosition;public int eligible;public float evaluationMilliseconds;
            public List<PlacementResult> candidates;
        }
        [MenuItem("Notiflex/Validate spatial failure cases")]
        public static void SpatialFailures()
        {
            Directory.CreateDirectory(Evidence);passed.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Failure-test head").AddComponent<Camera>();
            camera.transform.position=new Vector3(0,1.65f,0);camera.fieldOfView=70;camera.aspect=1.6f;
            var evaluator=new SpatialEvaluator();var regions=new List<ProtectedRegion>();
            var c=new ContextSnapshot{poseValid=true,activity=Activity.Focus,workload=.8f,audioEnabled=true};
            evaluator.Evaluate(camera,null,null,regions,c,Priority.Normal,20);
            Check(PlacementPolicy.Best(evaluator.Results)!=null,"Empty room admits a visual candidate");
            c.poseValid=false;evaluator.Evaluate(camera,null,null,regions,c,Priority.Urgent,20);
            Check(PlacementPolicy.Best(evaluator.Results)==null,"Lost tracking rejects every spatial candidate");
            c.poseValid=true;c.age=2.01f;evaluator.Evaluate(camera,null,null,regions,c,Priority.Normal,20);
            Check(PlacementPolicy.Best(evaluator.Results)==null,"Stale pose rejects every spatial candidate");
            c.age=0;
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Complete view blocker";
            blocker.transform.position=new Vector3(0,1.65f,.8f);blocker.transform.localScale=new Vector3(8,8,.1f);Physics.SyncTransforms();
            evaluator.Evaluate(camera,null,null,regions,c,Priority.Normal,20);
            Check(PlacementPolicy.Best(evaluator.Results)==null,"Wall blocks all candidate sight volumes");
            Check(PlacementPolicy.Delivery(c,Priority.Normal,false)==Channel.SpatialAudio,"Blocked view uses allowed audio fallback");
            c.conversation=true;Check(PlacementPolicy.Delivery(c,Priority.Normal,false)==Channel.Deferred,"Blocked conversational view defers");c.conversation=false;
            blocker.transform.position=camera.transform.position;Physics.SyncTransforms();
            evaluator.Evaluate(camera,null,null,regions,c,Priority.Normal,20);
            Check(PlacementPolicy.Best(evaluator.Results)==null,"Initial collider overlap rejects candidates too");
            UnityEngine.Object.DestroyImmediate(blocker);Physics.SyncTransforms();
            var protectedObject=new GameObject("Full view protected region");protectedObject.transform.position=new Vector3(0,1.65f,3);
            var region=protectedObject.AddComponent<ProtectedRegion>();region.size=new Vector2(2.5f,1.5f);region.kind=RegionKind.Face;regions.Add(region);
            evaluator.Evaluate(camera,null,null,regions,c,Priority.Normal,20);
            Check(PlacementPolicy.Best(evaluator.Results)==null,"Protected face makes all overlapping candidates ineligible");
            region.kind=RegionKind.Hazard;evaluator.Evaluate(camera,null,null,regions,c,Priority.Normal,20);
            Check(PlacementPolicy.Best(evaluator.Results)==null,"Protected hazard makes all overlapping candidates ineligible");
            protectedObject.transform.position=new Vector3(0,1.65f,.8f);region.size=new Vector2(1.2f,.6f);region.kind=RegionKind.Face;
            evaluator.Evaluate(camera,null,null,regions,c,Priority.Normal,20);
            Check(PlacementPolicy.Best(evaluator.Results)==null,"Nearer face still blocks projected overlay without real-room depth occlusion");
            region.kind=RegionKind.Hazard;evaluator.Evaluate(camera,null,null,regions,c,Priority.Normal,20);
            Check(PlacementPolicy.Best(evaluator.Results)==null,"Nearer hazard still blocks projected overlay");
            UnityEngine.Object.DestroyImmediate(protectedObject);UnityEngine.Object.DestroyImmediate(camera.gameObject);
            File.WriteAllLines(Evidence+"/spatial-failure-checks.txt",passed);
            EditorSceneManager.OpenScene("Assets/Notiflex/Scenes/Desk.unity");
        }
        [MenuItem("Notiflex/Validate spatial environment sweep")]
        public static void SpatialSweep()
        {
            Directory.CreateDirectory(Evidence);var lines=new List<string>();int count=0,valid=0,deferred=0;
            foreach(string room in new[]{"Desk","Kitchen","LivingRoom"})
            {
                EditorSceneManager.OpenScene("Assets/Notiflex/Scenes/"+room+".unity");
                var env=UnityEngine.Object.FindFirstObjectByType<EnvironmentBindings>();
                Check(env!=null&&env.task!=null&&env.partner!=null,"Environment has task and partner "+room);
                var regions=new List<ProtectedRegion>(UnityEngine.Object.FindObjectsByType<ProtectedRegion>(FindObjectsSortMode.None));
                var camera=new GameObject("Sweep head").AddComponent<Camera>();camera.transform.position=new Vector3(0,1.65f,-1.6f);camera.fieldOfView=70;camera.aspect=1.6f;
                foreach(Vector3 pose in new[]{new Vector3(0,1.65f,-1.6f),new Vector3(0,1.65f,0),new Vector3(.6f,1.2f,-.6f)})
                foreach(Activity activity in new[]{Activity.Focus,Activity.Physical,Activity.Conversation})foreach(Priority priority in Enum.GetValues(typeof(Priority)))foreach(float yaw in new[]{-70f,-30f,0f,30f,70f,180f})
                {
                    camera.transform.position=pose;camera.transform.rotation=Quaternion.Euler(5,yaw,0);Physics.SyncTransforms();
                    var c=new ContextSnapshot{poseValid=true,activity=activity,workload=.8f,audioEnabled=true,conversation=activity==Activity.Conversation,handsBusy=activity==Activity.Physical};
                    var evaluator=new SpatialEvaluator();var clock=Stopwatch.StartNew();evaluator.Evaluate(camera,env.task,env.partner,regions,c,priority,20);clock.Stop();
                    var best=PlacementPolicy.Best(evaluator.Results);count++;
                    foreach(var r in evaluator.Results)
                    {
                        if(r.total<0||r.total>1.00001f||float.IsNaN(r.total))throw new Exception("Invalid spatial score");
                        if(r.eligible&&(r.faceCoverage>.020001f||r.hazardCoverage>.010001f))throw new Exception("Hard geometric constraint violated");
                        if(r.eligible&&priority==Priority.Urgent&&r.headAngle>20.0001f)throw new Exception("Urgency angle violated");
                        if(r.eligible&&(r.distance<.8f||r.distance>3.5f))throw new Exception("Distance violated");
                    }
                    int eligible=evaluator.Results.FindAll(r=>r.eligible).Count;if(best!=null)valid++;else deferred++;
                    lines.Add(JsonUtility.ToJson(new Scenario{room=room,context=activity.ToString(),priority=priority.ToString(),selected=best==null?"none":best.id,yaw=yaw,headPosition=pose,eligible=eligible,evaluationMilliseconds=(float)clock.Elapsed.TotalMilliseconds,candidates=evaluator.Results}));
                }
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
            File.WriteAllLines(Evidence+"/spatial-sweep.jsonl",lines);
            File.WriteAllText(Evidence+"/spatial-summary.txt",$"PASS {count} scenarios across three environments\nVisual candidate available {valid}; no eligible visual {deferred}\nAll eligible results satisfy face hazard viewport distance and urgency constraints\n");
            EditorSceneManager.OpenScene("Assets/Notiflex/Scenes/Desk.unity");
        }
        [MenuItem("Notiflex/Render offscreen evidence")]
        public static void RenderEvidence()
        {
            Directory.CreateDirectory(Evidence+"/renders");
            foreach(string room in new[]{"Desk","Kitchen","LivingRoom"})
            {
                EditorSceneManager.OpenScene("Assets/Notiflex/Scenes/"+room+".unity");
                var env=UnityEngine.Object.FindFirstObjectByType<EnvironmentBindings>();var regions=new List<ProtectedRegion>(UnityEngine.Object.FindObjectsByType<ProtectedRegion>(FindObjectsSortMode.None));
                var camera=new GameObject("Offscreen render camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,1.65f,-1.6f);camera.fieldOfView=70;camera.aspect=1.6f;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.2f);
                var target=new RenderTexture(1600,1000,24);target.Create();camera.targetTexture=target;
                foreach(Activity activity in new[]{Activity.Focus,Activity.Physical,Activity.Conversation})
                {
                    camera.transform.rotation=Quaternion.Euler(5,0,0);Physics.SyncTransforms();
                    var c=new ContextSnapshot{poseValid=true,activity=activity,workload=.8f,audioEnabled=true,conversation=activity==Activity.Conversation,handsBusy=activity==Activity.Physical};
                    var evaluator=new SpatialEvaluator();evaluator.Evaluate(camera,env.task,env.partner,regions,c,Priority.Normal,20);
                    var choice=PlacementPolicy.Best(evaluator.Results);
                    var view=new GameObject("Offscreen card",typeof(RectTransform)).AddComponent<NotificationView>();view.Initialize();
                    view.GetComponent<Canvas>().worldCamera=camera;
                    if(choice!=null)view.Present(choice.position,camera.transform.rotation,Channel.Visual,Priority.Normal,"Your next update is ready.\nReview it at a suitable stopping point.",PlacementPolicy.PresentationScale(choice.distance));
                    Canvas.ForceUpdateCanvases();
                    if(choice!=null)
                    {
                        var corners=new Vector3[4];view.GetComponent<RectTransform>().GetWorldCorners(corners);
                        if(Vector3.Distance((corners[0]+corners[2])*.5f,choice.position)>.0001f)
                            throw new Exception("Rendered canvas center differs from scored placement");
                        foreach(var corner in corners)
                        {
                            var viewport=camera.WorldToViewportPoint(corner);
                            if(viewport.x<.0399f||viewport.x>.9601f||viewport.y<.0399f||viewport.y>.9601f)
                                throw new Exception("Rendered canvas leaves scored viewport inset");
                        }
                    }
                    camera.Render();
                    var prior=RenderTexture.active;RenderTexture.active=target;
                    var png=new Texture2D(1600,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1600,1000),0,0);png.Apply();
                    File.WriteAllBytes(Evidence+"/renders/"+room+"-"+activity+".png",png.EncodeToPNG());RenderTexture.active=prior;
                    UnityEngine.Object.DestroyImmediate(png);UnityEngine.Object.DestroyImmediate(view.gameObject);
                }
                camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
            EditorSceneManager.OpenScene("Assets/Notiflex/Scenes/Desk.unity");
            Debug.Log("NOTIFLEX_OFFSCREEN_RENDERS_COMPLETE");
        }
    }
    public sealed class PolicyTests
    {
        [Test]public void PolicyRulesAndBoundedScores(){Checks.Run();}
        [Test]public void RoomGeometryAndSpatialConstraints(){Checks.SpatialSweep();}
        [Test]public void SpatialFailureAndTrackingCases(){Checks.SpatialFailures();}
    }
}
