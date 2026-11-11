using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace Notiflex.VR
{
    public sealed class NotiflexRuntime : MonoBehaviour
    {
        public Camera Head {get;private set;}
        public Priority priority=Priority.Normal;
        public Activity suppliedActivity=Activity.Focus;
        public bool audioEnabled=true;
        public bool userSpeaking;
        public float annotationDistance=2;
        public bool NotificationActive => active;
        public bool NotificationVisible => view!=null && view.gameObject.activeSelf;
        public Channel DeliveryChannel => channel;
        public Activity InferredActivity => dwell.Current;
        public PlacementResult SelectedPlacement => current;
        readonly SpatialEvaluator evaluator=new SpatialEvaluator();
        ContextDwell dwell=new ContextDwell();
        EnvironmentBindings environment;
        List<ProtectedRegion> regions=new List<ProtectedRegion>();
        NotificationView view;
        OVRManager manager;
        OVRPassthroughLayer passthrough;
        AudioSource audioSource;
        PlacementResult current;
        Channel channel;
        bool active,expanded, rightA,rightB,leftA,leftB,leftStick,rightGrip,leftGrip,rightTrigger,inspector;
        float expiry,nextEvaluation,lastPlacement,requestAt;
        int requestId;
        string logPath,lastDecision="";
        const string Message="Your next update is ready. Review it at a suitable stopping point.";
        bool OnDevice {get{return Application.platform==RuntimePlatform.Android;}}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(FindFirstObjectByType<NotiflexRuntime>()==null) new GameObject("Notiflex Runtime").AddComponent<NotiflexRuntime>();
        }
        void Awake()
        {
            DontDestroyOnLoad(gameObject); Application.runInBackground=true;Application.targetFrameRate=72;QualitySettings.vSyncCount=0;
            if(OnDevice)
            {
                var root=new GameObject("Tracked XR rig");root.transform.SetParent(transform);
                manager=root.AddComponent<OVRManager>();manager.trackingOriginType=OVRManager.TrackingOrigin.FloorLevel;
                var rig=root.AddComponent<OVRCameraRig>();rig.EnsureGameObjectIntegrity();Head=rig.centerEyeAnchor.GetComponent<Camera>();
                if(Head==null) Head=rig.centerEyeAnchor.gameObject.AddComponent<Camera>();
                passthrough=root.AddComponent<OVRPassthroughLayer>();passthrough.enabled=false;
            }
            else
            {
                Head=new GameObject("Desktop simulated head").AddComponent<Camera>();Head.transform.SetParent(transform);
                Head.transform.position=new Vector3(0,1.65f,-1.6f);Head.transform.rotation=Quaternion.Euler(5,0,0);Head.fieldOfView=70;
            }
            Head.nearClipPlane=.08f;Head.farClipPlane=50;Head.tag="MainCamera";
            if(Head.GetComponent<AudioListener>()==null)Head.gameObject.AddComponent<AudioListener>();
            view=new GameObject("World space notification",typeof(RectTransform)).AddComponent<NotificationView>();view.transform.SetParent(transform);view.Initialize();
            audioSource=new GameObject("Notification sound").AddComponent<AudioSource>();audioSource.transform.SetParent(transform);audioSource.spatialBlend=1;audioSource.volume=.2f;
            var samples=new float[11025];for(int i=0;i<samples.Length;i++) samples[i]=.18f*Mathf.Sin(i*2*Mathf.PI*660/44100)*Mathf.Sin(Mathf.PI*i/samples.Length);
            audioSource.clip=AudioClip.Create("Chime",samples.Length,1,44100,false);audioSource.clip.SetData(samples,0);
            logPath=Path.Combine(Application.persistentDataPath,"decisions-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".jsonl");
            SceneManager.sceneLoaded+=Loaded;BindEnvironment();
        }
        void OnDestroy() { SceneManager.sceneLoaded-=Loaded; }
        void Loaded(Scene scene,LoadSceneMode mode) { BindEnvironment(); }
        void BindEnvironment()
        {
            environment=FindFirstObjectByType<EnvironmentBindings>();regions=new List<ProtectedRegion>(FindObjectsByType<ProtectedRegion>(FindObjectsSortMode.None));
            active=false;expanded=false;current=null;dwell=new ContextDwell();lastDecision="";view.Hide();
            if(environment!=null && OnDevice)
            {
                manager.isInsightPassthroughEnabled=environment.passthrough;passthrough.enabled=environment.passthrough;
                Head.clearFlags=CameraClearFlags.SolidColor;Head.backgroundColor=environment.passthrough?Color.clear:new Color(.12f,.16f,.2f);
            }
        }
        public void Send()
        {
            requestId++;requestAt=Time.unscaledTime;expiry=requestAt+16;active=true;expanded=false;lastDecision="";Tick(true);Record("notification");
        }
        public void Read() { if(active){expanded=true;expiry=Time.unscaledTime+16;Tick(true);Record("read_requested");} }
        public void Dismiss() { Record("acknowledged");active=false;view.Hide(); }
        void Update()
        {
            if(environment==null) return;
            if(!OnDevice)
            {
                if(Input.GetKeyDown(KeyCode.Space))Send();if(Input.GetKeyDown(KeyCode.Return))Dismiss();
                if(Input.GetKeyDown(KeyCode.E))Read();
                if(Input.GetKeyDown(KeyCode.U))CyclePriority();if(Input.GetKeyDown(KeyCode.C))CycleActivity();
                if(Input.GetKeyDown(KeyCode.Tab))inspector=!inspector;
                for(int i=0;i<4;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))SceneManager.LoadScene(i);
                float yaw=(Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.LeftArrow)?1:0);
                float pitch=(Input.GetKey(KeyCode.DownArrow)?1:0)-(Input.GetKey(KeyCode.UpArrow)?1:0);
                Head.transform.Rotate(Vector3.up,yaw*40*Time.unscaledDeltaTime,Space.World);Head.transform.Rotate(Vector3.right,pitch*30*Time.unscaledDeltaTime,Space.Self);
                Vector3 move=new Vector3((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),0,(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0));
                Vector3 forward=Vector3.ProjectOnPlane(Head.transform.forward,Vector3.up).normalized;
                Head.transform.position+=(Head.transform.right*move.x+forward*move.z)*Time.unscaledDeltaTime;
            }
            else Controllers();
            if(Time.unscaledTime>=nextEvaluation) { Tick(false);nextEvaluation=Time.unscaledTime+.1f; }
            if(active && Time.unscaledTime>=expiry){Record("expired");active=false;view.Hide();}
        }
        void CyclePriority(){priority=(Priority)(((int)priority+1)%3);lastDecision="";}
        void CycleActivity(){suppliedActivity=(Activity)(((int)suppliedActivity+1)%4);lastDecision="";}
        bool Press(InputDevice d,InputFeatureUsage<bool> feature,ref bool prior)
        {d.TryGetFeatureValue(feature,out bool value);bool down=value&&!prior;prior=value;return down;}
        void Controllers()
        {
            var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if(Press(right,CommonUsages.primaryButton,ref rightA))Send();if(Press(right,CommonUsages.secondaryButton,ref rightB))Dismiss();
            if(Press(left,CommonUsages.primaryButton,ref leftA))SceneManager.LoadScene((SceneManager.GetActiveScene().buildIndex+1)%4);
            if(Press(left,CommonUsages.secondaryButton,ref leftB))CyclePriority();
            if(Press(left,CommonUsages.primary2DAxisClick,ref leftStick))CycleActivity();
            if(Press(right,CommonUsages.triggerButton,ref rightTrigger))Read();
            if(environment.passthrough)
            {
                if(Press(right,CommonUsages.gripButton,ref rightGrip))Annotate(right,false);
                if(Press(left,CommonUsages.gripButton,ref leftGrip))Annotate(left,true);
                if(right.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 stick))annotationDistance=Mathf.Clamp(annotationDistance+stick.y*Time.unscaledDeltaTime,.8f,3.2f);
            }
        }
        void Annotate(InputDevice device,bool face)
        {
            if(!device.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p) || !device.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q))return;
            Transform previous=face?environment.partner:environment.task;
            if(previous!=null){var old=previous.GetComponent<ProtectedRegion>();regions.Remove(old);Destroy(previous.gameObject);}
            var anchor=new GameObject(face?"Supplied partner annotation":"Supplied task annotation");
            anchor.transform.position=transform.TransformPoint(p+q*Vector3.forward*annotationDistance);
            anchor.transform.rotation=Quaternion.LookRotation(anchor.transform.position-Head.transform.position,Vector3.up);
            var region=anchor.AddComponent<ProtectedRegion>();region.kind=face?RegionKind.Face:RegionKind.Task;region.size=face?new Vector2(.4f,.5f):new Vector2(.8f,.55f);
            regions.Add(region);if(face)environment.partner=anchor.transform;else environment.task=anchor.transform;Record("manual_annotation");
        }
        public ContextSnapshot Snapshot()
        {
            bool valid=true;
            if(OnDevice){var d=InputDevices.GetDeviceAtXRNode(XRNode.Head);valid=d.isValid&&d.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked)&&tracked;}
            return new ContextSnapshot {poseValid=valid,age=0,taskAngle=environment.task==null?180:Vector3.Angle(Head.transform.forward,environment.task.position-Head.transform.position),
                partnerAngle=environment.partner==null?180:Vector3.Angle(Head.transform.forward,environment.partner.position-Head.transform.position),
                conversation=suppliedActivity==Activity.Conversation&&environment.partner!=null,handsBusy=suppliedActivity==Activity.Physical,
                movement=suppliedActivity==Activity.Physical?.65f:.04f,workload=suppliedActivity==Activity.Unknown?.1f:.8f,audioEnabled=audioEnabled,speaking=userSpeaking};
        }
        void Tick(bool immediate)
        {
            ContextSnapshot c=Snapshot();if(dwell.Update(PlacementPolicy.Infer(c),Time.unscaledTime))lastDecision="";c.activity=dwell.Current;
            evaluator.Evaluate(Head,environment.task,environment.partner,regions,c,priority,Message.Split(' ').Length);
            var best=PlacementPolicy.Best(evaluator.Results);
            var incumbent=current==null?null:evaluator.Results.Find(r=>r.id==current.id);
            bool replace=immediate||lastDecision==""||incumbent==null||!incumbent.eligible||best==null||
                (best.id!=incumbent.id&&Time.unscaledTime-lastPlacement>=1&&incumbent.total-best.total>.04f);
            if(replace){current=best;lastPlacement=Time.unscaledTime;}else current=incumbent;
            channel=PlacementPolicy.Delivery(c,priority,current!=null);
            if(expanded&&current!=null&&c.poseValid&&c.age<=PlacementPolicy.MaxContextAge)channel=Channel.Visual;
            string sig=c.activity+"/"+priority+"/"+channel+"/"+(current==null?"none":current.id);
            if(sig!=lastDecision){lastDecision=sig;Record("decision");if(active&&channel==Channel.SpatialAudio)PlayChime();}
            if(active && current!=null && (channel==Channel.Visual||channel==Channel.Peripheral))view.Present(current.position,Head.transform.rotation,channel,priority,Message,PlacementPolicy.PresentationScale(current.distance));
            else view.Hide();
        }
        void PlayChime(){audioSource.transform.position=environment.task==null?Head.transform.TransformPoint(Vector3.forward*2):environment.task.position;audioSource.Play();}
        [Serializable] class EventRecord
        { public string utc,eventType,environment,inputSource,context,priority,channel,selected;public int request;public bool poseValid;public List<PlacementResult> candidates; }
        void Record(string type)
        {
            if(environment==null||logPath==null)return;
            var r=new EventRecord {utc=DateTime.UtcNow.ToString("o"),eventType=type,environment=environment.environmentName,inputSource=environment.passthrough?"tracked_head_with_manual_annotations_and_supplied_activity":"simulation_ground_truth",
                context=dwell.Current.ToString(),priority=priority.ToString(),channel=channel.ToString(),selected=current==null?"none":current.id,request=requestId,poseValid=Snapshot().poseValid,candidates=evaluator.Results};
            File.AppendAllText(logPath,JsonUtility.ToJson(r)+"\n");
        }
        void OnGUI()
        {
            if(OnDevice)return;
            GUI.Box(new Rect(16,16,640,80),"Notiflex VR simulation\n1 Desk   2 Kitchen   3 Living room   4 Passthrough setup\nWASD move / arrows look / Space send / E read / Enter dismiss / C context / U urgency / Tab inspect");
            if(inspector)
            {
                var weights=PlacementPolicy.Weights(dwell.Current,priority);
                string detail="Supplied: "+suppliedActivity+" / inferred: "+dwell.Current+" / "+priority+" / "+channel+
                    "\nWeights I/O/S: "+weights.interruption.ToString("0.00")+" / "+weights.occlusion.ToString("0.00")+" / "+weights.social.ToString("0.00")+
                    "\nSelected: "+(current==null?"none":current.id)+"\nCandidate | I | O | S | total | eligibility";
                foreach(var candidate in evaluator.Results)
                    detail+="\n"+candidate.id+" | "+candidate.interruption.ToString("0.000")+" | "+candidate.occlusion.ToString("0.000")+" | "+
                        candidate.social.ToString("0.000")+" | "+candidate.total.ToString("0.000")+" | "+(candidate.eligible?"eligible":candidate.rejected);
                GUI.Box(new Rect(16,110,640,40+(evaluator.Results.Count+4)*20),detail);
            }
        }
    }
}
