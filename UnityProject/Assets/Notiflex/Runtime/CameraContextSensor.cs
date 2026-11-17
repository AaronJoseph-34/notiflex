using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Meta.XR;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR;
using Debug=UnityEngine.Debug;

namespace Notiflex.VR
{
    // Camera images and tensors remain in memory. Only derived observations are
    // exposed to diagnostics; no image saving or network inference. Microphone
    // processing lives in the separate, permission-gated LocalSpeechSensor.
    public sealed class CameraContextSensor : MonoBehaviour
    {
        public readonly List<CameraDetection> Detections=new List<CameraDetection>();
        public readonly List<ViewportRegion> Regions=new List<ViewportRegion>();
        public string Status {get;private set;}="inactive";
        public float InferenceMs {get;private set;}
        public float ObservationTime {get;private set;}=float.NegativeInfinity;
        public int FrameSequence {get;private set;}
        public AutomaticEstimate Estimate {get;private set;}
        public readonly BareHandActivity Hands=new BareHandActivity();
        public LiveSpatialEnvironment Spatial {get;private set;}
        public LocalSpeechSensor Speech {get;private set;}
        readonly MultimodalContextEstimator contextEstimator=new MultimodalContextEstimator();
        public float Age => Time.unscaledTime-ObservationTime;
        public bool Fresh => cameraAccess!=null && cameraAccess.IsPlaying && OVRManager.hasVrFocus &&
            HeadTracked && !paused && wanted && AutomaticContext.Fresh(Age,captureHead,head.transform.position);
        public bool HasObservation => Fresh;
        Camera head;
        Transform trackingSpace;
        PassthroughCameraAccess cameraAccess;
        GameObject cameraObject;
        Worker worker;
        Tensor<float> input;
        Material letterboxMaterial;
        RenderTexture modelImage;
        int width,height,generation;
        bool wanted,paused,permissionRequested,failed;
        Vector3 captureHead,previousHead;
        Quaternion previousRotation;
        float headSpeed,angularSpeed;
        DateTime lastFrame;
        string lastStatus;
        bool HeadTracked
        {
            get
            {
                var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                return device.isValid&&device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked)&&tracked;
            }
        }

        public void Initialize(Camera camera,Transform space,LiveSpatialEnvironment spatial=null,LocalSpeechSensor speech=null)
        {
            Spatial=spatial;Speech=speech;
            head=camera;trackingSpace=space;previousHead=head.transform.position;previousRotation=head.transform.rotation;
            cameraObject=new GameObject("Local context camera");cameraObject.transform.SetParent(transform);cameraObject.SetActive(false);
            cameraAccess=cameraObject.AddComponent<PassthroughCameraAccess>();
            cameraAccess.enabled=false;cameraAccess.CameraPosition=PassthroughCameraAccess.CameraPositionType.Left;
            cameraAccess.RequestedResolution=new Vector2Int(640,480);cameraAccess.MaxFramerate=15;cameraAccess.enabled=true;
            StartCoroutine(Loop());
        }
        public void SetCapture(bool value)
        {
            if(wanted==value)return;
            wanted=value;generation++;Invalidate();
            Spatial?.SetSensing(value);Speech?.SetSensing(value);contextEstimator.Reset();
            if(!value){cameraObject.SetActive(false);SetStatus("manual_or_simulation");}
        }
        void SetStatus(string value)
        {
            Status=value;if(value==lastStatus)return;lastStatus=value;
            Debug.Log("NOTIFLEX_CAMERA "+value);
        }
        void Invalidate()
        {
            ObservationTime=float.NegativeInfinity;Detections.Clear();Regions.Clear();
            Estimate=new AutomaticEstimate {proposed=Activity.Unknown,reason="no_fresh_camera_observation",taskAngle=180,partnerAngle=180};
        }
        void Update()
        {
            if(head==null)return;
            float dt=Time.unscaledDeltaTime;
            if(dt>0 && dt<.5f)
            {
                float blend=1-Mathf.Exp(-dt/.25f);
                headSpeed=Mathf.Lerp(headSpeed,Vector3.Distance(previousHead,head.transform.position)/dt,blend);
                angularSpeed=Mathf.Lerp(angularSpeed,Quaternion.Angle(previousRotation,head.transform.rotation)/dt,blend);
            }
            previousHead=head.transform.position;previousRotation=head.transform.rotation;
            if(!wanted||paused||failed||!OVRManager.hasVrFocus||!HeadTracked)
            {
                if(cameraObject.activeSelf){generation++;cameraObject.SetActive(false);Invalidate();}
                if(wanted&&!failed)SetStatus(paused?"paused":!OVRManager.hasVrFocus?"waiting_vr_focus":"waiting_head_tracking");
                return;
            }
            if(!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.PassthroughCameraAccessPermission))
            {
                if(cameraObject.activeSelf){generation++;cameraObject.SetActive(false);Invalidate();}
                if(!permissionRequested)
                {
                    permissionRequested=true;
                    SetStatus("requesting_camera_permission");
                    SensorPermissions.Request(OVRPermissionsRequester.PassthroughCameraAccessPermission,granted=>{if(!granted)SetStatus("camera_permission_denied_manual_available");});
                }
                return;
            }
            if(!cameraObject.activeSelf){cameraObject.SetActive(true);SetStatus("starting_camera");}
            if(!cameraAccess.enabled){Invalidate();SetStatus("camera_start_failed_manual_available");return;}
            if(Fresh)
            {
                Estimate=EstimateContext();
                UpdateRegions();
            }
            else Regions.Clear();
        }
        void LateUpdate()
        {
            if(wanted&&!paused&&OVRManager.hasVrFocus&&HeadTracked)
                Hands.Update(head.transform,trackingSpace,Time.unscaledTime,Time.unscaledDeltaTime);
            else Hands.Reset();
        }
        void UpdateRegions()
        {
            Regions.Clear();
            foreach(var d in Detections)
                if(AutomaticContext.Relevant(d.classId) && AutomaticContext.Project(d,head,out Rect rect))
                    Regions.Add(new ViewportRegion {rect=rect,kind=d.classId==0?RegionKind.Face:RegionKind.Task});
        }
        IEnumerator Loop()
        {
            while(true)
            {
                if(!wanted||paused||failed||!OVRManager.hasVrFocus||!HeadTracked||!cameraAccess.IsPlaying||cameraAccess.Timestamp==lastFrame)
                {yield return null;continue;}
                IEnumerator run=Infer();bool more=true;
                while(more)
                {
                    try { more=run.MoveNext(); }
                    catch(Exception error)
                    {
                        failed=true;Invalidate();SetStatus("inference_failed_manual_available");Debug.LogException(error);
                        (run as IDisposable)?.Dispose();more=false;
                    }
                    if(more)yield return run.Current;
                }
                // At most ~2 observations/s. Slow inference automatically lowers sampling.
                yield return new WaitForSecondsRealtime(Mathf.Clamp(InferenceMs*.001f,.5f,2));
            }
        }
        IEnumerator Infer()
        {
            if(worker==null)
            {
                SetStatus("loading_local_detector");
                var asset=Resources.Load<ModelAsset>("Notiflex/ContextDetector");
                if(asset==null)throw new InvalidOperationException("Bundled context detector missing");
                var model=ModelLoader.Load(asset);height=model.inputs[0].shape.Get(2);width=model.inputs[0].shape.Get(3);
                if(width<=0||height<=0||model.outputs.Count!=3)throw new InvalidOperationException("Unexpected detector tensor contract");
                worker=new Worker(model,BackendType.CPU);
                input=new Tensor<float>(new TensorShape(1,3,height,width));
                var shader=Resources.Load<Shader>("Notiflex/DetectorLetterbox");
                if(shader==null)throw new InvalidOperationException("Detector preprocessing shader missing");
                letterboxMaterial=new Material(shader);
                modelImage=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                modelImage.Create();
                yield return null;
            }
            if(!wanted||!cameraAccess.IsPlaying)yield break;
            var texture=cameraAccess.GetTexture();if(texture==null)yield break;
            lastFrame=cameraAccess.Timestamp;
            float frameAge=(float)(DateTime.UtcNow-lastFrame).TotalSeconds;
            if(!PlacementPolicy.Finite(frameAge)||frameAge<-.05f||frameAge>AutomaticContext.MaxObservationAge)
            {Invalidate();SetStatus("camera_timestamp_stale");yield break;}
            int capturedGeneration=generation;
            float capturedAt=Time.unscaledTime-Mathf.Max(0,frameAge);
            Vector3 capturedHead=head.transform.position;
            Pose pose=cameraAccess.GetCameraPose();
            float rotationLength=pose.rotation.x*pose.rotation.x+pose.rotation.y*pose.rotation.y+pose.rotation.z*pose.rotation.z+pose.rotation.w*pose.rotation.w;
            if(!AutomaticContext.Finite(pose.position)||!PlacementPolicy.Finite(rotationLength)||rotationLength<.9f||rotationLength>1.1f)
            {Invalidate();SetStatus("camera_pose_invalid");yield break;}
            // Cache affine ray basis before inference; never combine old detections
            // with the current camera pose or hard-code a camera FOV/aspect ratio.
            Vector3 ray0=cameraAccess.ViewportPointToRay(Vector2.zero,pose).direction;
            Vector3 rayX=cameraAccess.ViewportPointToRay(Vector2.right,pose).direction-ray0;
            Vector3 rayY=cameraAccess.ViewportPointToRay(Vector2.up,pose).direction-ray0;
            var watch=Stopwatch.StartNew();
            Rect content=AutomaticContext.ContentRect(texture.width,texture.height,width,height);
            letterboxMaterial.SetVector("_Content",new Vector4(content.x,content.y,content.width,content.height));
            // MRUK exposes an sRGB camera target in a linear-rendering project.
            // Re-encode GPU-decoded samples so the model sees RGB [0,1] like training.
            letterboxMaterial.SetFloat("_EncodeSRGB",GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat)?1:0);
            Graphics.Blit(texture,modelImage,letterboxMaterial);
            TextureConverter.ToTensor(modelImage,input,new TextureTransform().SetDimensions(width,height,3).SetCoordOrigin(CoordOrigin.TopLeft));
            var schedule=worker.ScheduleIterable(input);
            var budget=Stopwatch.StartNew();
            while(schedule.MoveNext())
            {
                if(budget.Elapsed.TotalMilliseconds>=2){yield return null;budget.Restart();}
            }
            var boxes=worker.PeekOutput(0) as Tensor<float>;
            var ids=worker.PeekOutput(1) as Tensor<int>;
            var scores=worker.PeekOutput(2) as Tensor<float>;
            if(boxes==null||ids==null||scores==null)throw new InvalidOperationException("Unexpected detector outputs");
            boxes.ReadbackRequest();ids.ReadbackRequest();scores.ReadbackRequest();
            while(!boxes.IsReadbackRequestDone()||!ids.IsReadbackRequestDone()||!scores.IsReadbackRequestDone())yield return null;
            InferenceMs=(float)watch.Elapsed.TotalMilliseconds;
            if(capturedGeneration!=generation||!wanted||!cameraAccess.IsPlaying||!OVRManager.hasVrFocus||!HeadTracked)yield break;
            if(!AutomaticContext.Fresh(Time.unscaledTime-capturedAt,capturedHead,head.transform.position))
            {Invalidate();SetStatus("inference_observation_stale");yield break;}
            var b=boxes.DownloadToArray();var c=ids.DownloadToArray();var s=scores.DownloadToArray();
            AutomaticContext.Decode(b,c,s,width,height,ray0,rayX,rayY,Detections,content);
            foreach(var detection in Detections)detection.origin=pose.position;
            Spatial?.Observe(Detections,capturedAt,Time.unscaledTime);
            captureHead=capturedHead;ObservationTime=capturedAt;FrameSequence++;
            Estimate=EstimateContext();UpdateRegions();SetStatus("observing");
            Debug.Log("NOTIFLEX_OBSERVATION frame="+FrameSequence+" objects="+Detections.Count+" inferenceMs="+InferenceMs.ToString("F1")+
                " proposed="+Estimate.proposed+" confidence="+Estimate.confidence.ToString("F2")+" reason="+Estimate.reason+
                " bareHands="+Hands.Evidence.trackedHands+" handSpeed="+Hands.Evidence.speed.ToString("F2"));
        }
        AutomaticEstimate EstimateContext()
        {
            var visual=AutomaticContext.Estimate(Detections,head.transform.forward,headSpeed,angularSpeed,Hands.Evidence);
            if(Spatial==null)return visual;
            return contextEstimator.Fuse(visual,Detections,Spatial.Tracker.Tracks,Hands.Evidence,Speech==null?default:Speech.Evidence,
                head.transform.position,head.transform.forward,angularSpeed,Time.unscaledTime,headSpeed);
        }
        void OnApplicationPause(bool value)
        {
            paused=value;generation++;Invalidate();
            if(value && cameraObject!=null)cameraObject.SetActive(false);
        }
        void OnDestroy()
        {
            StopAllCoroutines();
            // Finish owned work before freeing its tensors, including an inference
            // interrupted by app shutdown. Readback requests create no tensor clones.
            if(worker!=null)
            {
                worker.Dispose();worker=null;
            }
            input?.Dispose();input=null;
            if(modelImage!=null){modelImage.Release();Destroy(modelImage);}
            if(letterboxMaterial!=null)Destroy(letterboxMaterial);
        }
    }
}
