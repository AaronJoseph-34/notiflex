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
        public bool automaticMode=true;
        public float annotationDistance=2;
        public bool NotificationActive => active;
        public bool NotificationVisible => view!=null && view.gameObject.activeSelf;
        public Channel DeliveryChannel => channel;
        public Activity InferredActivity => CameraMode?automaticDwell.Current:dwell.Current;
        public PlacementResult SelectedPlacement => current;
        readonly SpatialEvaluator evaluator=new SpatialEvaluator();
        ContextDwell dwell=new ContextDwell();
        readonly AutomaticContextDwell automaticDwell=new AutomaticContextDwell();
        EnvironmentBindings environment;
        List<ProtectedRegion> regions=new List<ProtectedRegion>();
        NotificationView view;
        OVRManager manager;
        Transform trackingSpace;
        OVRPassthroughLayer passthrough;
        AudioSource audioSource;
        PlacementResult current;
        Channel channel;
        bool active,expanded, rightA,rightB,leftA,leftB,leftStick,rightGrip,leftGrip,rightTrigger,inspector;
        float expiry,nextEvaluation,lastPlacement,requestAt;
        int requestId;
        string logPath,lastDecision="";
        string lastControllerStatus="";
        CameraContextSensor cameraSensor;
        LiveSpatialEnvironment liveSpatial;
        LocalSpeechSensor speech;
        NotificationRequest request=new NotificationRequest();
        readonly NotificationAudioGate audioGate=new NotificationAudioGate();
        readonly List<PlacementCandidate> liveCandidates=new List<PlacementCandidate>();
        Vector3? referentPosition;
        string referentStatus="no_referent_metadata";
        int referentTrack=-1;
        float referentSigma;
        Vector3 requestHeadPosition;
        string[] pages= {"Your next update is ready."};
        int page;
        float lastHandAction=-10;
        int lastObservation=-1;
        bool rightStick;
        bool scheduledDemo,audioDiagnostic;
        float audioDiagnosticAt=-1;
        float nextDemo,demoUntil;
        bool CameraMode => OnDevice && environment!=null && environment.passthrough && automaticMode;
        string Message => pages[page]+(pages.Length>1?"\n"+(page+1)+" / "+pages.Length:"");
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
                root.SetActive(false);
                manager=root.AddComponent<OVRManager>();manager.trackingOriginType=OVRManager.TrackingOrigin.FloorLevel;
                manager.launchSimultaneousHandsControllersOnStartup=true;
                manager.controllerDrivenHandPosesType=OVRManager.ControllerDrivenHandPosesType.None;
                var rig=root.AddComponent<OVRCameraRig>();rig.EnsureGameObjectIntegrity();Head=rig.centerEyeAnchor.GetComponent<Camera>();
                trackingSpace=rig.trackingSpace;
                if(Head==null) Head=rig.centerEyeAnchor.gameObject.AddComponent<Camera>();
                passthrough=root.AddComponent<OVRPassthroughLayer>();passthrough.enabled=false;
                root.SetActive(true);
                liveSpatial=gameObject.AddComponent<LiveSpatialEnvironment>();liveSpatial.Initialize(Head,trackingSpace);
                speech=gameObject.AddComponent<LocalSpeechSensor>();speech.Initialize(true);
                cameraSensor=gameObject.AddComponent<CameraContextSensor>();cameraSensor.Initialize(Head,trackingSpace,liveSpatial,speech);
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var intent=activity.Call<AndroidJavaObject>("getIntent"))
                    speech.SetConsent(intent.Call<bool>("getBooleanExtra","notiflex_local_speech",true));
                if(Debug.isDebugBuild)
                {
                    using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
                    using var intent=activity.Call<AndroidJavaObject>("getIntent");
                    scheduledDemo=intent.Call<bool>("getBooleanExtra","notiflex_test_notifications",false);
                    audioDiagnostic=intent.Call<bool>("getBooleanExtra","notiflex_test_audio",false);
                    nextDemo=Time.unscaledTime+12;demoUntil=Time.unscaledTime+180;
                }
            }
            else
            {
                Head=new GameObject("Desktop simulated head").AddComponent<Camera>();Head.transform.SetParent(transform);
                Head.transform.position=new Vector3(0,1.65f,-1.6f);Head.transform.rotation=Quaternion.Euler(5,0,0);Head.fieldOfView=70;
            }
            Head.nearClipPlane=.08f;Head.farClipPlane=50;Head.tag="MainCamera";
            if(Head.GetComponent<AudioListener>()==null)Head.gameObject.AddComponent<AudioListener>();
            view=new GameObject("World space notification",typeof(RectTransform)).AddComponent<NotificationView>();view.transform.SetParent(transform);view.Initialize();
            audioSource=new GameObject("Notification sound").AddComponent<AudioSource>();audioSource.transform.SetParent(transform);audioSource.spatialBlend=1;audioSource.volume=.4f;
            audioSource.rolloffMode=AudioRolloffMode.Linear;audioSource.minDistance=1;audioSource.maxDistance=6;
            var samples=new float[11025];for(int i=0;i<samples.Length;i++) samples[i]=.35f*Mathf.Sin(i*2*Mathf.PI*660/44100)*Mathf.Sin(Mathf.PI*i/samples.Length);
            audioSource.clip=AudioClip.Create("Chime",samples.Length,1,44100,false);audioSource.clip.SetData(samples,0);
            logPath=Path.Combine(Application.persistentDataPath,"decisions-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".jsonl");
            SceneManager.sceneLoaded+=Loaded;BindEnvironment();
            var profile=Resources.Load<TextAsset>("Notiflex/SocialRiskProfile");
            if(profile!=null)try{SocialRiskModel.SetProfile(JsonUtility.FromJson<SocialRiskProfile>(profile.text));}catch(Exception){Debug.LogWarning("NOTIFLEX_PROFILE invalid; using hypothesis default");}
            if(OnDevice)Invoke(nameof(ReadLaunchRequest),2);
        }
        void OnDestroy() { SceneManager.sceneLoaded-=Loaded; }
        void Loaded(Scene scene,LoadSceneMode mode) { BindEnvironment(); }
        void BindEnvironment()
        {
            environment=FindFirstObjectByType<EnvironmentBindings>();regions=new List<ProtectedRegion>(FindObjectsByType<ProtectedRegion>(FindObjectsSortMode.None));
            active=false;expanded=false;current=null;dwell=new ContextDwell();automaticDwell.Reset();lastDecision="";view.Hide();
            if(environment!=null && OnDevice)
            {
                manager.isInsightPassthroughEnabled=environment.passthrough;passthrough.enabled=environment.passthrough;
                Head.clearFlags=CameraClearFlags.SolidColor;Head.backgroundColor=environment.passthrough?Color.clear:new Color(.12f,.16f,.2f);
                cameraSensor.SetCapture(CameraMode);
            }
        }
        public void Send()
        {
            Send(new NotificationRequest {body="Your next update is ready. Review it at a suitable stopping point.",priority=priority,demonstration=true});
        }
        // Producer API: referent metadata must be explicit. Generic notifications
        // never acquire a referent just because an object happens to be visible.
        public void Send(NotificationRequest notification)
        {
            if(notification==null||string.IsNullOrWhiteSpace(notification.body)||notification.body.Length>600)throw new ArgumentException("Notification body must contain 1–600 characters");
            if((int)notification.priority<0||(int)notification.priority>2)throw new ArgumentException("Invalid priority");
            request=notification;priority=notification.priority;
            pages=NotificationText.Pages(request.body);page=0;requestHeadPosition=Head.transform.position;liveSpatial?.BeginRequest();
            requestId++;requestAt=Time.unscaledTime;expiry=requestAt+16;active=true;expanded=false;lastDecision="";Tick(true);Record("notification");
        }
        void ReadLaunchRequest()
        {
            using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
            using var intent=activity.Call<AndroidJavaObject>("getIntent");
            string json=intent.Call<string>("getStringExtra","notiflex_notification");
            if(string.IsNullOrWhiteSpace(json))return;
            try{Send(JsonUtility.FromJson<NotificationRequest>(json));}catch(Exception error){Debug.LogWarning("NOTIFLEX_REQUEST rejected: "+error.Message);}
        }
        public void Read() { if(active){if(expanded&&page+1<pages.Length)page++;expanded=true;expiry=Time.unscaledTime+Mathf.Max(16,SocialRiskModel.Exposure(request.Words,Channel.Visual));Tick(true);Record("read_requested");} }
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
            if(Time.unscaledTime>=nextEvaluation) { Tick(false);nextEvaluation=Time.unscaledTime+.1f; }
            bool handHover=false;
            if(OnDevice&&CameraMode&&OVRManager.hasVrFocus&&active&&view.gameObject.activeSelf)
            {
                foreach(var aim in cameraSensor.Hands.AimRays)if(aim.HasValue&&view.Hit(aim.Value))handHover=true;
                if(Time.unscaledTime-lastHandAction>=.7f&&cameraSensor.Hands.PinchRay.HasValue&&view.Hit(cameraSensor.Hands.PinchRay.Value))
                {lastHandAction=Time.unscaledTime;if(expanded&&page+1>=pages.Length)Dismiss();else Read();}
            }
            view.SetHover(handHover);
            if(scheduledDemo&&Time.unscaledTime<=demoUntil&&Time.unscaledTime>=nextDemo&&CameraMode&&OVRManager.hasVrFocus&&cameraSensor.HasObservation)
            {nextDemo=Time.unscaledTime+15;Send();Record("scheduled_test_notification");}
            if(audioDiagnostic&&OVRManager.hasVrFocus)
            {
                if(audioDiagnosticAt<0)audioDiagnosticAt=Time.unscaledTime+5;
                if(Time.unscaledTime>=audioDiagnosticAt)
                {audioDiagnostic=false;Record("diagnostic_audio_requested");PlayChime();}
            }
            else if(audioDiagnostic)audioDiagnosticAt=-1;
            if(active && Time.unscaledTime>=expiry){Record("expired");active=false;view.Hide();}
        }
        // OVRManager refreshes OVRInput in Update. Read it afterward, once per frame.
        void LateUpdate() { if(OnDevice && environment!=null)Controllers(); }
        void CyclePriority(){priority=(Priority)(((int)priority+1)%3);lastDecision="";}
        void CycleActivity(){if(CameraMode)SetAutomatic(false);suppliedActivity=(Activity)(((int)suppliedActivity+1)%4);lastDecision="";}
        void SetAutomatic(bool value)
        {
            automaticMode=value;dwell=new ContextDwell();automaticDwell.Reset();lastDecision="";
            cameraSensor?.SetCapture(CameraMode);
            Debug.Log("NOTIFLEX_MODE "+(CameraMode?"automatic_camera":"manual_or_simulation"));
        }
        bool Press(OVRInput.RawButton button,OVRInput.Controller controller,ref bool prior)
        {bool value=OVRInput.Get(button,controller);bool down=value&&!prior;prior=value;return down;}
        void ControllerAction(string name,Action action)
        {Debug.Log("NOTIFLEX_INPUT "+name);action();}
        void Controllers()
        {
            const OVRInput.Controller right=OVRInput.Controller.RTouch,left=OVRInput.Controller.LTouch;
            string status=OVRInput.GetConnectedControllers()+" focus="+OVRManager.hasVrFocus+
                " rightTracked="+OVRInput.GetControllerPositionTracked(right)+" leftTracked="+OVRInput.GetControllerPositionTracked(left);
            if(status!=lastControllerStatus){lastControllerStatus=status;Debug.Log("NOTIFLEX_INPUT_STATUS "+status);}
            if(!OVRManager.hasVrFocus)
            {
                // Suppress presses begun in system menus until they have been released.
                rightA=OVRInput.Get(OVRInput.RawButton.A,right);rightB=OVRInput.Get(OVRInput.RawButton.B,right);
                leftA=OVRInput.Get(OVRInput.RawButton.X,left);leftB=OVRInput.Get(OVRInput.RawButton.Y,left);
                leftStick=OVRInput.Get(OVRInput.RawButton.LThumbstick,left);rightTrigger=OVRInput.Get(OVRInput.RawButton.RIndexTrigger,right);
                rightGrip=OVRInput.Get(OVRInput.RawButton.RHandTrigger,right);leftGrip=OVRInput.Get(OVRInput.RawButton.LHandTrigger,left);
                rightStick=OVRInput.Get(OVRInput.RawButton.RThumbstick,right);return;
            }
            if(Press(OVRInput.RawButton.A,right,ref rightA))ControllerAction("right_a_send",Send);
            if(Press(OVRInput.RawButton.B,right,ref rightB))ControllerAction("right_b_dismiss",Dismiss);
            if(Press(OVRInput.RawButton.X,left,ref leftA))
            {ControllerAction("left_x_scene",()=>SceneManager.LoadScene((SceneManager.GetActiveScene().buildIndex+1)%SceneManager.sceneCountInBuildSettings));return;}
            if(Press(OVRInput.RawButton.Y,left,ref leftB))ControllerAction("left_y_urgency",CyclePriority);
            if(Press(OVRInput.RawButton.LThumbstick,left,ref leftStick))ControllerAction("left_stick_activity",CycleActivity);
            if(Press(OVRInput.RawButton.RIndexTrigger,right,ref rightTrigger))ControllerAction("right_trigger_read",Read);
            if(environment.passthrough)
            {
                if(Press(OVRInput.RawButton.RThumbstick,right,ref rightStick))ControllerAction("right_stick_automatic_mode",()=>SetAutomatic(!automaticMode));
                if(Press(OVRInput.RawButton.RHandTrigger,right,ref rightGrip)&&!CameraMode)ControllerAction("right_grip_task",()=>Annotate(right,false));
                if(Press(OVRInput.RawButton.LHandTrigger,left,ref leftGrip)&&!CameraMode)ControllerAction("left_grip_partner",()=>Annotate(left,true));
                Vector2 stick=OVRInput.Get(OVRInput.RawAxis2D.RThumbstick,right);
                annotationDistance=Mathf.Clamp(annotationDistance+stick.y*Time.unscaledDeltaTime,.8f,3.2f);
            }
        }
        void Annotate(OVRInput.Controller controller,bool face)
        {
            if(!OVRInput.GetControllerPositionTracked(controller)||!OVRInput.GetControllerOrientationTracked(controller))return;
            if(CameraMode)SetAutomatic(false);
            Vector3 p=OVRInput.GetLocalControllerPosition(controller);Quaternion q=OVRInput.GetLocalControllerRotation(controller);
            Transform previous=face?environment.partner:environment.task;
            if(previous!=null){var old=previous.GetComponent<ProtectedRegion>();regions.Remove(old);Destroy(previous.gameObject);}
            var anchor=new GameObject(face?"Supplied partner annotation":"Supplied task annotation");
            anchor.transform.position=trackingSpace.TransformPoint(p+q*Vector3.forward*annotationDistance);
            anchor.transform.rotation=Quaternion.LookRotation(anchor.transform.position-Head.transform.position,Vector3.up);
            var region=anchor.AddComponent<ProtectedRegion>();region.kind=face?RegionKind.Face:RegionKind.Task;region.size=face?new Vector2(.4f,.5f):new Vector2(.8f,.55f);
            regions.Add(region);if(face)environment.partner=anchor.transform;else environment.task=anchor.transform;Record("manual_annotation");
        }
        public ContextSnapshot Snapshot()
        {
            bool valid=true;
            if(OnDevice){var d=InputDevices.GetDeviceAtXRNode(XRNode.Head);valid=d.isValid&&d.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked)&&tracked;}
            if(CameraMode)
            {
                bool fresh=cameraSensor.HasObservation;
                var e=cameraSensor.Estimate;
                return new ContextSnapshot {poseValid=valid,age=fresh?cameraSensor.Age:PlacementPolicy.MaxContextAge+1,
                    taskAngle=fresh?e.taskAngle:180,partnerAngle=fresh?e.partnerAngle:180,
                    conversation=fresh&&e.proposed==Activity.Conversation,handsBusy=fresh&&e.handsBusy,
                    movement=fresh?e.movement:0,workload=fresh?e.workload:.1f,interactionSupport=fresh?e.interactionSupport:0,
                    audioEnabled=audioEnabled&&(!fresh||!cameraSensor.Detections.Exists(d=>d.classId==0)),speaking=userSpeaking||speech!=null&&speech.Evidence.available&&speech.Evidence.speaking};
            }
            return new ContextSnapshot {poseValid=valid,age=0,taskAngle=environment.task==null?180:Vector3.Angle(Head.transform.forward,environment.task.position-Head.transform.position),
                partnerAngle=environment.partner==null?180:Vector3.Angle(Head.transform.forward,environment.partner.position-Head.transform.position),
                conversation=suppliedActivity==Activity.Conversation&&environment.partner!=null,handsBusy=suppliedActivity==Activity.Physical,
                movement=suppliedActivity==Activity.Physical?.65f:.04f,workload=suppliedActivity==Activity.Unknown?.1f:.8f,audioEnabled=audioEnabled,speaking=userSpeaking};
        }
        void Tick(bool immediate)
        {
            ContextSnapshot c=Snapshot();
            Activity proposed=PlacementPolicy.Infer(c);
            bool changed=CameraMode?automaticDwell.Update(proposed,Time.unscaledTime,c.poseValid&&cameraSensor.HasObservation):dwell.Update(proposed,Time.unscaledTime);
            if(changed)lastDecision="";c.activity=InferredActivity;
            // Retain the busy-hands assumption while Physical is latched by dwell;
            // brief hand occlusion must not promote an audio cue to a full card.
            if(CameraMode&&c.activity==Activity.Physical)c.handsBusy=true;
            ResolveSpatial(c);
            evaluator.Evaluate(Head,CameraMode?null:environment.task,CameraMode?null:environment.partner,
                CameraMode?null:regions,c,priority,request.Words,
                CameraMode&&cameraSensor.HasObservation?cameraSensor.Regions:null,
                CameraMode&&cameraSensor.HasObservation&&cameraSensor.Estimate.partnerTrackId>=0?(Vector3?)cameraSensor.Estimate.partnerDirection:null,
                liveCandidates,CameraMode?liveSpatial:null,referentPosition);
            var best=PlacementPolicy.Best(evaluator.Results);
            var incumbent=current==null?null:evaluator.Results.Find(r=>r.id==current.id);
            bool replace=immediate||lastDecision==""||incumbent==null||!incumbent.eligible||best==null||
                (best.id!=incumbent.id&&Time.unscaledTime-lastPlacement>=1&&incumbent.total-best.total>.04f);
            if(replace){current=best;lastPlacement=Time.unscaledTime;}else current=incumbent;
            channel=PlacementPolicy.Delivery(c,priority,current!=null);
            if(expanded&&current!=null&&c.poseValid&&c.age<=PlacementPolicy.MaxContextAge)channel=Channel.Visual;
            string sig=c.activity+"/"+priority+"/"+channel+"/"+(current==null?"none":current.id);
            if(sig!=lastDecision){lastDecision=sig;Record("decision");}
            if(active&&channel==Channel.SpatialAudio&&audioGate.TryConsume(requestId))PlayChime();
            if(CameraMode&&cameraSensor.FrameSequence!=lastObservation)
            {lastObservation=cameraSensor.FrameSequence;Record("camera_observation");}
            if(active && current!=null && (channel==Channel.Visual||channel==Channel.Peripheral))view.Present(current.position,current.rotation,channel,priority,Message,PlacementPolicy.PresentationScale(current.distance),request.title);
            else view.Hide();
        }
        void ResolveSpatial(ContextSnapshot context)
        {
            liveCandidates.Clear();referentPosition=null;referentTrack=-1;referentSigma=0;referentStatus="no_referent_metadata";
            if(!CameraMode||liveSpatial==null||!cameraSensor.HasObservation)return;
            float now=Time.unscaledTime;
            if(!string.IsNullOrEmpty(request.referentSceneId))
            {
                var anchor=liveSpatial.FindAnchor(request.referentSceneId);
                if(anchor!=null){referentPosition=anchor.GetAnchorCenter();referentStatus="scene_anchor_referent";}
                else referentStatus="scene_anchor_not_found";
            }
            else
            {
                var track=liveSpatial.Tracker.Resolve(request,Head.transform.position,Head.transform.forward,now,out referentStatus);
                if(track!=null){referentPosition=track.Position(now);referentTrack=track.id;referentSigma=Mathf.Sqrt(track.filter.PredictedVariance(now));}
            }
            if(referentPosition.HasValue)SpatialCandidateBuilder.Around(liveCandidates,"Referent",referentPosition.Value,requestHeadPosition,
                referentTrack>=0?referentTrack.ToString():request.referentSceneId,referentSigma,"tracked_referent_proximal");
            if(context.conversation&&cameraSensor.Estimate.partnerMetric)
            {
                int id=cameraSensor.Estimate.partnerTrackId;
                var partner=liveSpatial.Tracker.Tracks.Find(t=>t.id==id&&t.Grounded(now));
                if(partner!=null)SpatialCandidateBuilder.Around(liveCandidates,"Partner",partner.Position(now),requestHeadPosition,id.ToString(),Mathf.Sqrt(partner.filter.PredictedVariance(now)),"tracked_partner_proximal");
            }
            liveSpatial.AddSurfaceCandidates(liveCandidates,evaluator.PanelSize);
        }
        void PlayChime()
        {
            audioSource.transform.position=CameraMode||environment.task==null?Head.transform.TransformPoint(Vector3.forward*2):environment.task.position;
            audioSource.Play();Debug.Log("NOTIFLEX_AUDIO play_requested volume="+audioSource.volume+" listener="+AudioListener.volume+" playing="+audioSource.isPlaying);
            speech?.SuppressOwnAudio(Time.unscaledTime+.6f);
            Record("audio_play_requested");
        }
        [Serializable] class EventRecord
        {
            public string utc,eventType,environment,inputSource,context,priority,channel,selected;
            public int request;public bool poseValid;public List<PlacementResult> candidates;
            public string cameraStatus,proposedContext,contextReason;public float contextConfidence,observationAge,inferenceMs;
            public int[] detectedClasses;
            public int bareHands;public float handSpeed,handActivitySeconds,contextSupportSeconds;public string handStatus;
            public bool audioAllowed,handsBusy;public float movement;
            public string notificationId,notificationSource,referentState,roomStatus,depthStatus,speechStatus,interactionEvidence,socialProfile;
            public bool demonstration,participantFitted,partnerMetric;
            public float vadElapsedMs,speakerElapsedMs,maxSpeechScheduleSliceMs;public int droppedAudioBlocks;
            public int referentTrack,anonymousSpeakers,turnSwitches;public float referentSigma,voiceProbability,speechRatio,interactionSupport,presentationSeconds,presentationSocial;
        }
        void Record(string type)
        {
            if(environment==null||logPath==null)return;
            var r=new EventRecord {utc=DateTime.UtcNow.ToString("o"),eventType=type,environment=environment.environmentName,inputSource=CameraMode?"local_camera_hands_room_depth_and_optional_speech":environment.passthrough?"tracked_head_with_manual_annotations_and_supplied_activity":"simulation_ground_truth",
                context=InferredActivity.ToString(),priority=priority.ToString(),channel=channel.ToString(),selected=current==null?"none":current.id,request=requestId,poseValid=Snapshot().poseValid,candidates=evaluator.Results};
            r.notificationId=request.id;r.notificationSource=request.source;r.demonstration=request.demonstration;r.referentState=referentStatus;r.referentTrack=referentTrack;r.referentSigma=referentSigma;
            r.socialProfile=SocialRiskModel.Profile.id;r.participantFitted=SocialRiskModel.Profile.HasParticipantFit;
            r.presentationSeconds=SocialRiskModel.Exposure(request.Words,channel)/(channel==Channel.Visual&&current!=null?Mathf.Max(.6f,current.viewIncidence):1);
            r.presentationSocial=InferredActivity==Activity.Conversation&&current!=null?SocialRiskModel.Score(current.headAngle,current.partnerAngle,request.Words,channel,cameraSensor!=null?cameraSensor.Estimate.interactionSupport:1,current.viewIncidence):0;
            if(CameraMode)
            {
                bool fresh=cameraSensor.HasObservation;
                r.cameraStatus=cameraSensor.Status;r.proposedContext=fresh?cameraSensor.Estimate.proposed.ToString():"Unknown";
                r.contextReason=fresh?cameraSensor.Estimate.reason:"no_fresh_camera_observation";
                r.contextConfidence=fresh?cameraSensor.Estimate.confidence:0;
                r.observationAge=PlacementPolicy.Finite(cameraSensor.Age)?cameraSensor.Age:-1;r.inferenceMs=cameraSensor.InferenceMs;
                r.detectedClasses=fresh?cameraSensor.Detections.ConvertAll(d=>d.classId).ToArray():Array.Empty<int>();
                r.bareHands=cameraSensor.Hands.Evidence.trackedHands;r.handSpeed=cameraSensor.Hands.Evidence.speed;
                r.handActivitySeconds=cameraSensor.Hands.Evidence.activeFor;r.handStatus=cameraSensor.Hands.Status;
                r.contextSupportSeconds=automaticDwell.Support(fresh?cameraSensor.Estimate.proposed:Activity.Unknown);
                var snapshot=Snapshot();r.audioAllowed=snapshot.audioEnabled;r.handsBusy=snapshot.handsBusy;r.movement=snapshot.movement;
                r.vadElapsedMs=speech.VadElapsedMs;r.speakerElapsedMs=speech.SpeakerElapsedMs;r.maxSpeechScheduleSliceMs=speech.MaxScheduleSliceMs;r.droppedAudioBlocks=speech.DroppedAudioBlocks;
                r.roomStatus=liveSpatial.Status;r.depthStatus=liveSpatial.DepthStatus;r.speechStatus=speech.Status;
                r.interactionEvidence=cameraSensor.Estimate.interactionEvidence;r.interactionSupport=cameraSensor.Estimate.interactionSupport;r.partnerMetric=cameraSensor.Estimate.partnerMetric;
                var voice=speech.Evidence;r.voiceProbability=voice.probability;r.speechRatio=voice.recentSpeechRatio;r.anonymousSpeakers=voice.anonymousSpeakers;r.turnSwitches=voice.turnSwitches;
            }
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
