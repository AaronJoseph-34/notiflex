using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.InferenceEngine;
using UnityEngine;

namespace Notiflex.VR.Editor
{
    public sealed class AutomaticContextTests
    {
        static CameraDetection Detection(int id,float score=.9f,float yaw=0,float size=.4f)
        {
            Quaternion r=Quaternion.Euler(0,yaw,0);
            return new CameraDetection {classId=id,score=score,viewport=new Rect(.5f-size/2,.5f-size/2,size,size),direction=r*Vector3.forward,
                cornerDirections=new[]{r*new Vector3(-.2f,-.2f,1),r*new Vector3(-.2f,.2f,1),r*new Vector3(.2f,-.2f,1),r*new Vector3(.2f,.2f,1)}};
        }
        static AutomaticEstimate Infer(List<CameraDetection> input,float headSpeed=0,float headTurn=0,float hands=0) =>
            AutomaticContext.Estimate(input,Vector3.forward,headSpeed,headTurn,hands);

        [Test]
        public void ContextEvidenceAndFreshness()
        {
            Assert.AreEqual(Activity.Unknown,Infer(new List<CameraDetection>()).proposed);
            Assert.AreEqual(Activity.Focus,Infer(new List<CameraDetection>{Detection(63)}).proposed);
            Assert.AreEqual(Activity.Focus,Infer(new List<CameraDetection>{Detection(73)}).proposed);
            Assert.AreEqual(Activity.Unknown,Infer(new List<CameraDetection>{Detection(63)},.2f).proposed);
            Assert.AreEqual(Activity.Unknown,Infer(new List<CameraDetection>{Detection(63)},headTurn:20).proposed);
            Assert.AreEqual(Activity.Unknown,Infer(new List<CameraDetection>{Detection(63,yaw:40)}).proposed);
            Assert.AreEqual(Activity.Unknown,Infer(new List<CameraDetection>{Detection(63,score:.52f)}).proposed);
            Assert.AreEqual(Activity.Unknown,Infer(new List<CameraDetection>{Detection(45)}).proposed,"Bowl presence alone is not activity");
            Assert.AreEqual(Activity.Unknown,Infer(new List<CameraDetection>{Detection(68)},hands:.5f).proposed,"Oven presence is not cooking");
            var physical=Infer(new List<CameraDetection>{Detection(43)},hands:.4f);
            Assert.AreEqual(Activity.Physical,physical.proposed);Assert.IsTrue(physical.handsBusy);
            Assert.AreEqual(Activity.Unknown,Infer(new List<CameraDetection>(),hands:.5f).proposed);
            Assert.AreEqual(Activity.Unknown,Infer(new List<CameraDetection>{Detection(43)},hands:float.NaN).proposed);
            var social=Infer(new List<CameraDetection>{Detection(0),Detection(63)},hands:.5f);
            Assert.AreEqual(Activity.Conversation,social.proposed);StringAssert.Contains("proxy",social.reason);
            Assert.AreEqual(Activity.Unknown,Infer(new List<CameraDetection>{Detection(0,size:.1f)}).proposed,"Tiny background person is insufficient");
            Assert.AreEqual(Activity.Unknown,Infer(new List<CameraDetection>{Detection(0,yaw:50)}).proposed);
            Assert.IsTrue(AutomaticContext.Fresh(.5f,Vector3.zero,Vector3.zero));
            Assert.IsFalse(AutomaticContext.Fresh(1.51f,Vector3.zero,Vector3.zero));
            Assert.IsFalse(AutomaticContext.Fresh(-.1f,Vector3.zero,Vector3.zero));
            Assert.IsFalse(AutomaticContext.Fresh(float.NaN,Vector3.zero,Vector3.zero));
            Assert.IsFalse(AutomaticContext.Fresh(.5f,Vector3.zero,new Vector3(.11f,0,0)));
            var dwell=new ContextDwell();
            dwell.Update(Activity.Focus,0);dwell.Update(Activity.Focus,1);Assert.AreEqual(Activity.Unknown,dwell.Current);
            dwell.Update(Activity.Focus,1.21f);Assert.AreEqual(Activity.Focus,dwell.Current);
            dwell.Update(Activity.Conversation,1.3f);dwell.Update(Activity.Conversation,2.51f);Assert.AreEqual(Activity.Conversation,dwell.Current);
            var c=new ContextSnapshot {poseValid=true,age=3,audioEnabled=true,activity=Activity.Focus};
            Assert.AreEqual(Channel.Deferred,PlacementPolicy.Delivery(c,Priority.Urgent,true));
            // Detected person suppresses audio in automatic snapshots even when no
            // visual position survives. Verify the unchanged policy respects that.
            c.age=0;c.audioEnabled=false;c.conversation=true;
            Assert.AreEqual(Channel.Deferred,PlacementPolicy.Delivery(c,Priority.Normal,false));
        }

        [Test]
        public void DetectionCoordinatesNmsAndProtection()
        {
            var detections=new List<CameraDetection>();
            AutomaticContext.Decode(new[]{32f,16f,96f,80f},new[]{63},new[]{.9f},320,320,
                new Vector3(-1,-1,1),Vector3.right*2,Vector3.up*2,detections);
            Assert.AreEqual(1,detections.Count);Assert.AreEqual(.1f,detections[0].viewport.xMin,.0001f);
            Assert.AreEqual(.75f,detections[0].viewport.yMin,.0001f);Assert.AreEqual(.95f,detections[0].viewport.yMax,.0001f);
            Assert.Less(detections[0].direction.x,0);Assert.Greater(detections[0].direction.y,0);
            Rect content=AutomaticContext.ContentRect(640,480,640,640);
            Assert.AreEqual(new Rect(0,.125f,1,.75f),content);
            AutomaticContext.Decode(new[]{64f,128f,320f,512f},new[]{63},new[]{.9f},640,640,
                new Vector3(-1,-1,1),Vector3.right*2,Vector3.up*2,detections,content);
            Assert.AreEqual(.1f,detections[0].viewport.xMin,.0001f);
            Assert.AreEqual(.1f,detections[0].viewport.yMin,.0001f);Assert.AreEqual(.9f,detections[0].viewport.yMax,.0001f);
            AutomaticContext.Decode(new[]{0f,0f,640f,40f},new[]{0},new[]{.9f},640,640,
                new Vector3(-1,-1,1),Vector3.right*2,Vector3.up*2,detections,content);
            Assert.IsEmpty(detections,"Detections entirely within padding are excluded");
            Assert.Throws<ArgumentException>(()=>AutomaticContext.ContentRect(0,480,640,640));
            var a=Detection(63);var b=Detection(63,.8f);var person=Detection(0,.7f);
            detections=new List<CameraDetection>{b,person,a};AutomaticContext.Suppress(detections);
            Assert.AreEqual(2,detections.Count,"Class-aware NMS preserves overlapping person and laptop");
            Assert.AreSame(a,detections[0]);
            Assert.AreEqual(1,AutomaticContext.IoU(a.viewport,a.viewport),.0001f);
            Assert.AreEqual(0,AutomaticContext.IoU(new Rect(0,0,.1f,.1f),new Rect(.9f,.9f,.1f,.1f)));
            var invalid=Detection(0);invalid.score=float.NaN;detections.Add(invalid);
            AutomaticContext.Suppress(detections);Assert.AreEqual(2,detections.Count);
            Assert.Throws<ArgumentException>(()=>AutomaticContext.Decode(Array.Empty<float>(),new[]{0},new[]{.9f},320,320,Vector3.zero,Vector3.right,Vector3.up,detections));
            var go=new GameObject("Automatic protection test camera");
            try
            {
                var camera=go.AddComponent<Camera>();camera.fieldOfView=70;camera.aspect=1.6f;
                Assert.IsTrue(AutomaticContext.Project(person,camera,out Rect rect));
                Assert.Less(rect.xMin,.5f);Assert.Greater(rect.xMax,.5f);
                var regions=new List<ViewportRegion>{new ViewportRegion {rect=new Rect(0,0,1,1),kind=RegionKind.Face}};
                var evaluator=new SpatialEvaluator();
                evaluator.Evaluate(camera,null,null,null,new ContextSnapshot {poseValid=true,age=0,activity=Activity.Conversation,workload=.65f},Priority.Normal,10,regions,Vector3.forward);
                Assert.IsNull(PlacementPolicy.Best(evaluator.Results),"Full-view person protection rejects overlapping panels");
                Assert.IsTrue(evaluator.Results.Exists(r=>r.rejected=="camera_person_coverage"));
                var crossing=Detection(0,yaw:85);
                Assert.IsTrue(AutomaticContext.Project(crossing,camera,out Rect clipped));
                Assert.AreEqual(new Rect(0,0,1,1),clipped,"Partially behind-view person remains protected");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void BundledDetectorExecutesAndMatchesContract()
        {
            var asset=Resources.Load<ModelAsset>("Notiflex/ContextDetector");Assert.IsNotNull(asset);
            var model=ModelLoader.Load(asset);Assert.AreEqual(3,model.outputs.Count);
            int height=model.inputs[0].shape.Get(2),width=model.inputs[0].shape.Get(3);
            Assert.Greater(width,0);Assert.Greater(height,0);
            using var input=new Tensor<float>(new TensorShape(1,3,height,width),new float[3*height*width]);
            using var worker=new Worker(model,BackendType.CPU);worker.Schedule(input);
            var boxes=worker.PeekOutput(0) as Tensor<float>;var ids=worker.PeekOutput(1) as Tensor<int>;var scores=worker.PeekOutput(2) as Tensor<float>;
            Assert.IsNotNull(boxes);Assert.IsNotNull(ids);Assert.IsNotNull(scores);
            boxes.CompleteAllPendingOperations();ids.CompleteAllPendingOperations();scores.CompleteAllPendingOperations();
            var b=boxes.DownloadToArray();var c=ids.DownloadToArray();var s=scores.DownloadToArray();
            Assert.Greater(c.Length,0);Assert.AreEqual(c.Length*4,b.Length);Assert.AreEqual(c.Length,s.Length);
            foreach(float value in b)Assert.IsTrue(PlacementPolicy.Finite(value));
            foreach(int value in c)Assert.IsTrue(value>=0&&value<80);
            foreach(float value in s)Assert.IsTrue(PlacementPolicy.Finite(value)&&value>=0&&value<=1);
            string path=Path.GetFullPath(Application.dataPath+"/../../evidence/automatic-mode");Directory.CreateDirectory(path);
            File.WriteAllText(path+"/detector-contract.txt",$"CPU inference passed: input {width}x{height}; {c.Length} output proposals; finite boxes, COCO class range, bounded scores. Blank input is not an accuracy test.\n");
        }
    }
}
