using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.InferenceEngine;
using UnityEngine;
namespace Notiflex.VR.Editor
{
    public sealed class PaperCoverageTests
    {
        static GroundedObservation Observation(int cls,Vector3 position,float confidence=.95f)=>new GroundedObservation {
            detection=new CameraDetection {classId=cls,score=confidence,viewport=new Rect(.3f,.25f,.4f,.5f),direction=position.normalized},
            metric=true,position=position,variance=.001f,normal=Vector3.back};
        [Test] public void AssignmentTrackingAndExplicitReferent()
        {
            CollectionAssert.AreEqual(new[]{1,0},SpatialObjectTracker.Assignment(new float[,]{{1,2},{1,100}}),"Global minimum differs from greedy matching");
            Assert.Throws<ArgumentException>(()=>SpatialObjectTracker.Assignment(new float[,]{{float.NaN}}));
            var tracker=new SpatialObjectTracker();var obs=new List<GroundedObservation>{Observation(63,new Vector3(0,0,2))};
            tracker.Update(obs,0);tracker.Update(obs,.2f);
            Assert.IsNull(tracker.Resolve(new NotificationRequest(),Vector3.zero,Vector3.forward,.2f,out string state));
            Assert.AreEqual("no_referent_metadata",state);
            var req=new NotificationRequest {referentClassId=63};
            Assert.IsNotNull(tracker.Resolve(req,Vector3.zero,Vector3.forward,.2f,out state));
            Assert.IsNull(tracker.Resolve(req,Vector3.zero,Vector3.forward,1.2f,out state),"Stale metric geometry cannot anchor a request");
            obs.Add(Observation(63,new Vector3(.5f,0,2)));tracker.Update(obs,.3f);tracker.Update(obs,.5f);
            Assert.AreEqual(2,tracker.Tracks.Count);
            Assert.IsNull(tracker.Resolve(req,Vector3.zero,Vector3.forward,.5f,out state));Assert.AreEqual("ambiguous_referent",state);
            req.referentTrackId=tracker.Tracks[0].id;Assert.IsNotNull(tracker.Resolve(req,Vector3.zero,Vector3.forward,.5f,out state));
            var invalid=Observation(63,Vector3.forward);invalid.detection.score=float.NaN;tracker.Update(new[]{invalid},.6f);Assert.AreEqual(2,tracker.Tracks.Count);
            var filter=new PositionFilter();Assert.IsTrue(filter.Update(Vector3.forward,.001f,0));Assert.IsFalse(filter.Update(Vector3.one*100,.001f,.1f));
            Assert.IsTrue(filter.Update(Vector3.forward*2,.001f,2),"A long gap reinitializes the position filter");
        }
        [Test] public void DepthGroundingRejectsUnknownAndUnstableGeometry()
        {
            var normals=new List<Vector3>{Vector3.back,Vector3.back,Vector3.back,Vector3.back,Vector3.back};
            Assert.IsTrue(GroundingMath.Fuse(Vector3.zero,Vector3.forward,new List<float>{2,2.01f,1.99f,2,6},normals,.2f,out var p,out _,out float variance));
            Assert.AreEqual(2,p.z,.03f);Assert.Greater(variance,.01f,"Capture/depth time mismatch adds uncertainty");
            Assert.IsFalse(GroundingMath.Fuse(Vector3.zero,Vector3.forward,new List<float>{1,2,3,4,5},normals,0,out _,out _,out _));
            Assert.IsFalse(GroundingMath.Fuse(Vector3.zero,Vector3.forward,new List<float>{2,2},normals,0,out _,out _,out _));
            Assert.IsFalse(GroundingMath.Fuse(Vector3.zero,Vector3.forward,new List<float>{2,2,2},normals,.6f,out _,out _,out _));
        }
        [Test] public void WorldCandidatesAndProtectedGeometry()
        {
            var list=new List<PlacementCandidate>();SpatialCandidateBuilder.Around(list,"Task",Vector3.forward*2,Vector3.zero,"1",.04f,"tracked");
            Assert.AreEqual(2,list.Count);Vector3 original=list[0].position;
            var go=new GameObject("Spatial test head");
            try
            {
                var head=go.AddComponent<Camera>();head.fieldOfView=90;head.aspect=1.6f;
                var evaluator=new SpatialEvaluator();var c=new ContextSnapshot {poseValid=true,age=0,activity=Activity.Focus,workload=.65f};
                evaluator.Evaluate(head,null,null,null,c,Priority.Normal,10,null,null,list,null,Vector3.forward*2);
                Assert.IsTrue(evaluator.Results.Exists(r=>r.source=="tracked"&&r.hasReferent));
                head.transform.rotation=Quaternion.Euler(0,10,0);evaluator.Evaluate(head,null,null,null,c,Priority.Normal,10,null,null,list);
                Assert.AreEqual(original,evaluator.Results.Find(r=>r.id==list[0].id).position,"Head turns preserve world candidate positions");
                var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.position=new Vector3(0,0,1);obstacle.transform.localScale=new Vector3(4,4,.1f);
                Physics.SyncTransforms();evaluator.Evaluate(head,null,null,null,c,Priority.Normal,10);
                Assert.IsNull(PlacementPolicy.Best(evaluator.Results));UnityEngine.Object.DestroyImmediate(obstacle);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void SpeechPersonFusionAndAnonymousTurns()
        {
            var tracker=new SpatialObjectTracker();var obs=new[]{Observation(0,Vector3.forward*2)};tracker.Update(obs,0);tracker.Update(obs,.2f);
            var fusion=new MultimodalContextEstimator();var visual=new AutomaticEstimate {proposed=Activity.Conversation,partnerAngle=0};
            var e=fusion.Fuse(visual,new List<CameraDetection>(),tracker.Tracks,default,default,Vector3.zero,Vector3.forward,0,.2f);
            Assert.AreEqual(Activity.Unknown,e.proposed,"A visible person alone is not called a conversation");
            var taskObjects=new List<CameraDetection>{obs[0].detection,Observation(63,Vector3.forward*2).detection};
            var work=fusion.Fuse(visual,taskObjects,tracker.Tracks,default,default,Vector3.zero,Vector3.forward,0,.2f);
            Assert.AreEqual(Activity.Focus,work.proposed,"A silent person does not erase valid desk-work evidence");
            var speech=new SpeechEvidence {available=true,speaking=true,lastSpeechTime=.2f,recentSpeechRatio=.5f};
            e=fusion.Fuse(visual,new List<CameraDetection>(),tracker.Tracks,default,speech,Vector3.zero,Vector3.forward,0,.2f);
            Assert.AreEqual(Activity.Conversation,e.proposed);StringAssert.Contains("speech_with_visible_person",e.interactionEvidence);Assert.IsTrue(e.partnerMetric);
            e=fusion.Fuse(visual,new List<CameraDetection>(),tracker.Tracks,default,speech,Vector3.zero,Vector3.forward,0,5);
            Assert.AreEqual(Activity.Unknown,e.proposed);
            var turns=new AnonymousSpeakerTurns();int a=turns.Observe(new[]{1f,0,0},2,0),b=turns.Observe(new[]{0f,1,0},2,3);
            Assert.AreNotEqual(a,b);Assert.AreEqual(a,turns.Observe(new[]{1f,0,0},2,6));Assert.AreEqual(2,turns.Switches);
            Assert.AreEqual(-1,turns.Observe(new[]{.7f,.7f,0},2,7),"Acoustically ambiguous mixtures stay unattributed");
            turns.Update(40);Assert.AreEqual(0,turns.Switches);Assert.AreEqual(0,turns.Speakers);
            var screen=Observation(62,Vector3.forward*2);tracker.Update(new[]{obs[0],screen},.4f);tracker.Update(new[]{obs[0],screen},.6f);
            Assert.IsTrue(MultimodalContextEstimator.ScreenDepiction(tracker.Tracks.Find(t=>t.classId==0),tracker.Tracks,.6f));
        }
        [Test] public void PresentationRiskAndInteractionControls()
        {
            SocialRiskModel.SetProfile(new SocialRiskProfile());
            Assert.Greater(SocialRiskModel.Score(20,30,60,Channel.Visual),SocialRiskModel.Score(20,30,5,Channel.Visual));
            Assert.Greater(SocialRiskModel.Score(20,30,20,Channel.Visual),SocialRiskModel.Score(20,30,20,Channel.Peripheral));
            Assert.AreEqual(0,SocialRiskModel.Score(20,30,20,Channel.Deferred));Assert.IsFalse(SocialRiskModel.Profile.HasParticipantFit);
            Assert.IsFalse(SocialRiskModel.SetProfile(new SocialRiskProfile {wordsPerMinute=float.NaN}));
            var audio=new NotificationAudioGate();Assert.IsTrue(audio.TryConsume(1));Assert.IsFalse(audio.TryConsume(1));Assert.IsTrue(audio.TryConsume(2));
            var pinch=new PinchIntentGate();Assert.IsFalse(pinch.Update(true,0));pinch.Update(false,.1f);pinch.Update(false,.4f);Assert.IsTrue(pinch.Update(true,.5f));Assert.IsFalse(pinch.Update(true,2));
            pinch.Reset();Assert.IsFalse(pinch.Update(true,3));
            string text="";for(int i=0;i<60;i++)text+=(i>0?" ":"")+"word";
            var pages=NotificationText.Pages(text);Assert.Greater(pages.Length,1);Assert.AreEqual(text,string.Join(" ",pages));
            foreach(string page in pages)Assert.LessOrEqual(page.Length,100);
            var go=new GameObject("Hit test",typeof(RectTransform));
            try{var view=go.AddComponent<NotificationView>();view.Initialize();view.Present(Vector3.forward*2,Quaternion.identity,Channel.Peripheral,Priority.Normal,"test");Assert.IsTrue(view.Hit(new Ray(Vector3.zero,Vector3.forward)));Assert.IsFalse(view.Hit(new Ray(Vector3.zero,Vector3.right)));}
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Serializable] sealed class ReplayDecision
        {public string provenance="synthetic_input_replay_not_headset",context,priority,channel,selected,referent;public List<PlacementResult> candidates;}
        [Test] public void SyntheticPipelineAcrossThreeContexts()
        {
            var go=new GameObject("Replay head");var rows=new List<string>();
            try
            {
                var head=go.AddComponent<Camera>();head.fieldOfView=90;head.aspect=1.6f;
                foreach(Activity activity in new[]{Activity.Focus,Activity.Physical,Activity.Conversation})
                {
                    int cls=activity==Activity.Focus?63:activity==Activity.Physical?41:0;
                    var detections=new List<CameraDetection>{Observation(cls,Vector3.forward*2).detection};
                    var tracker=new SpatialObjectTracker();tracker.Update(new[]{Observation(cls,Vector3.forward*2)},0);tracker.Update(new[]{Observation(cls,Vector3.forward*2)},.2f);
                    var hands=activity==Activity.Physical?new HandActivityEvidence {valid=true,inWorkspace=true,bimanual=true,trackedHands=2,speed=.2f,activeFor=1.5f,direction=Vector3.forward}:default;
                    var speech=activity==Activity.Conversation?new SpeechEvidence {available=true,lastSpeechTime=.2f,recentSpeechRatio=.6f,anonymousSpeakers=2,turnSwitches=2}:default;
                    var visual=AutomaticContext.Estimate(detections,Vector3.forward,0,0,hands);
                    var fused=new MultimodalContextEstimator().Fuse(visual,detections,tracker.Tracks,hands,speech,Vector3.zero,Vector3.forward,0,.2f);
                    Assert.AreEqual(activity,fused.proposed);
                    var request=new NotificationRequest {referentClassId=activity==Activity.Conversation?-1:cls};
                    var referent=tracker.Resolve(request,Vector3.zero,Vector3.forward,.2f,out string status);
                    var candidates=new List<PlacementCandidate>();if(referent!=null)SpatialCandidateBuilder.Around(candidates,"Task",referent.Position(.2f),Vector3.zero,referent.id.ToString(),.04f,"tracked_referent_proximal");
                    foreach(Priority priority in Enum.GetValues(typeof(Priority)))
                    {
                        var c=new ContextSnapshot {poseValid=true,age=0,activity=fused.proposed,conversation=activity==Activity.Conversation,interactionSupport=fused.interactionSupport,
                            handsBusy=fused.handsBusy,movement=fused.movement,workload=fused.workload,audioEnabled=activity!=Activity.Conversation};
                        var protectedRegions=activity==Activity.Conversation?new List<ViewportRegion>{new ViewportRegion {rect=new Rect(.42f,.3f,.16f,.4f),kind=RegionKind.Face}}:null;
                        var evaluator=new SpatialEvaluator();evaluator.Evaluate(head,null,null,null,c,priority,20,protectedRegions,activity==Activity.Conversation?(Vector3?)Vector3.forward:null,candidates,null,referent!=null?(Vector3?)referent.Position(.2f):null);
                        var best=PlacementPolicy.Best(evaluator.Results);Channel delivery=PlacementPolicy.Delivery(c,priority,best!=null);
                        if(priority==Priority.Normal)Assert.AreEqual(activity==Activity.Focus?Channel.Visual:activity==Activity.Physical?Channel.SpatialAudio:Channel.Peripheral,delivery);
                        rows.Add(JsonUtility.ToJson(new ReplayDecision {context=activity.ToString(),priority=priority.ToString(),channel=delivery.ToString(),selected=best?.id,referent=status,candidates=evaluator.Results}));
                    }
                }
                string dir=Path.GetFullPath(Application.dataPath+"/../../evidence/paper-coverage-0.6.0");Directory.CreateDirectory(dir);File.WriteAllLines(dir+"/synthetic-pipeline.jsonl",rows);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Serializable] sealed class Reference {public float[] features,speaker,state;public float vad;}
        static Reference Fixture()=>JsonUtility.FromJson<Reference>(File.ReadAllText("Assets/Notiflex/Editor/Fixtures/speech-reference.json"));
        [Test] public void SpeechFeaturesMatchIndependentKaldiReference()
        {
            var waveform=new float[SpeakerFeatures.Samples];for(int i=0;i<waveform.Length;i++)waveform[i]=(float)(Math.Sin(i*.023)*(.3+.2*Math.Sin(i*.001))+.01*Math.Cos(i*.37));
            var features=SpeakerFeatures.Extract(waveform);var expected=Fixture().features;float maximum=0;
            for(int i=0;i<features.Length;i++)maximum=Math.Max(maximum,Math.Abs(features[i]-expected[i]));
            Assert.Less(maximum,.004f,"Kaldi preprocessing parity, including mean normalization");
            var low=new List<float>();var high=new List<float>();var resampler=new AudioResampler();
            for(int i=0;i<4800;i++)resampler.Push((float)Math.Sin(2*Math.PI*1000*i/48000),48000,low.Add);
            resampler.Reset();for(int i=0;i<4800;i++)resampler.Push((float)Math.Sin(2*Math.PI*12000*i/48000),48000,high.Add);
            Assert.AreEqual(1600,low.Count);double l=0,h=0;for(int i=100;i<low.Count;i++){l+=low[i]*low[i];h+=high[i]*high[i];}Assert.Less(h/l,.001,"Downsampling suppresses out-of-band aliasing");
        }
        [Test] public void BundledSpeechModelsExecuteAndMatchReference()
        {
            var expected=Fixture();var timer=System.Diagnostics.Stopwatch.StartNew();
            using(var worker=new Worker(ModelLoader.Load(Resources.Load<ModelAsset>("Notiflex/VoiceActivity")),BackendType.CPU))
            using(var input=new Tensor<float>(new TensorShape(1,576),new float[576]))
            using(var state=new Tensor<float>(new TensorShape(2,1,128),new float[256]))
            {
                worker.SetInput("input",input);worker.SetInput("state",state);worker.Schedule();
                var output=worker.PeekOutput("output") as Tensor<float>;var next=worker.PeekOutput("stateN") as Tensor<float>;
                Assert.IsNotNull(output);Assert.IsNotNull(next);output.CompleteAllPendingOperations();next.CompleteAllPendingOperations();
                Assert.AreEqual(expected.vad,output.DownloadToArray()[0],.0001f);var values=next.DownloadToArray();
                for(int i=0;i<values.Length;i++)Assert.AreEqual(expected.state[i],values[i],.0001f);
            }
            long vadMs=timer.ElapsedMilliseconds;timer.Restart();
            var data=new float[16000];for(int i=0;i<data.Length;i++)data[i]=(float)Math.Sin(i*.017);
            using(var worker=new Worker(ModelLoader.Load(Resources.Load<ModelAsset>("Notiflex/SpeakerEmbedding")),BackendType.CPU))
            using(var input=new Tensor<float>(new TensorShape(1,200,80),data))
            {
                worker.Schedule(input);var output=worker.PeekOutput() as Tensor<float>;Assert.IsNotNull(output);output.CompleteAllPendingOperations();var embedding=output.DownloadToArray();Assert.AreEqual(256,embedding.Length);
                for(int i=0;i<embedding.Length;i++)Assert.AreEqual(expected.speaker[i],embedding[i],.002f);
            }
            string dir=Path.GetFullPath(Application.dataPath+"/../../evidence/paper-coverage-0.6.0");Directory.CreateDirectory(dir);
            File.WriteAllText(dir+"/unity-speech-contract.txt","Unity CPU synthetic-reference parity passed; includes model load / first-run compile. Desktop VAD ms="+vadMs+"; speaker ms="+timer.ElapsedMilliseconds+". Not headset latency or speech/speaker accuracy validation.\n");
        }
    }
}
