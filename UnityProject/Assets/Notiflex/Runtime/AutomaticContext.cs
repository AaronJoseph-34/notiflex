using System;
using System.Collections.Generic;
using UnityEngine;

namespace Notiflex.VR
{
    // Integrate evidence duration rather than demanding uninterrupted samples.
    // A 2 s window requires 1.2 s support (60% occupancy). Long frame gaps and
    // stale observations never count as evidence. Manual/simulation dwell is separate.
    public sealed class AutomaticContextDwell
    {
        struct Interval {public float start,end;public Activity activity;}
        readonly List<Interval> intervals=new List<Interval>();
        readonly float[] support=new float[4];
        float lastTime;
        Activity previous;
        bool initialized;
        public Activity Current {get;private set;}=Activity.Unknown;
        public float Support(Activity activity) => support[(int)activity];
        public void Reset()
        {intervals.Clear();Array.Clear(support,0,support.Length);initialized=false;Current=Activity.Unknown;}
        public bool Update(Activity proposed,float now,bool fresh)
        {
            Activity before=Current;
            if(!fresh||!PlacementPolicy.Finite(now)){Reset();return before!=Current;}
            float dt=now-lastTime;
            if(initialized&&(dt<0||dt>.25f)){Reset();}
            if(initialized&&dt>0)
                intervals.Add(new Interval {start=lastTime,end=now,activity=previous});
            lastTime=now;previous=proposed;initialized=true;
            float cutoff=now-2;
            intervals.RemoveAll(x=>x.end<=cutoff);
            Array.Clear(support,0,support.Length);
            foreach(var x in intervals)support[(int)x.activity]+=x.end-Mathf.Max(cutoff,x.start);
            if(support[(int)proposed]>=1.2f-1e-4f)Current=proposed;
            else if(support[(int)Activity.Unknown]>=1.2f-1e-4f||support[(int)Current]<1e-4f)Current=Activity.Unknown;
            return before!=Current;
        }
    }

    // COCO labels supplied with Meta's quantized YOLOv9t sample. A detection score
    // is object-model confidence, not a calibrated probability of an activity.
    public sealed class CameraDetection
    {
        public int classId;
        public float score;
        public Rect viewport;
        public Vector3 direction;
        public Vector3[] cornerDirections;
        public Vector3 origin,ray0,rayX,rayY;
        public Vector3 RayAt(Vector2 uv) => ray0.sqrMagnitude>0?(ray0+rayX*uv.x+rayY*uv.y).normalized:direction.normalized;
    }

    public struct ViewportRegion
    {
        public Rect rect;
        public RegionKind kind;
    }

    public struct AutomaticEstimate
    {
        public Activity proposed;
        public float confidence, taskAngle, partnerAngle, movement;
        public bool handsBusy;
        public Vector3 partnerDirection;
        public string reason;
        public string interactionEvidence;
        public bool personPresent,partnerMetric;
        public int partnerTrackId;
        public float interactionSupport,workload;
    }

    public static class AutomaticContext
    {
        public const float MaxObservationAge=1.5f, MaxHeadTranslation=.10f;
        public const float DetectionThreshold=.5f, DecisionThreshold=.55f;
        public static bool WorkObject(int id) => id==62 || id==63 || id==64 || id==66 || id==73;
        public static bool ManipulableObject(int id) => (id>=39 && id<=45) || id==76 || id==78 || id==79;
        public static bool Relevant(int id) => id==0 || WorkObject(id) || ManipulableObject(id);

        public static AutomaticEstimate Estimate(IList<CameraDetection> detections,Vector3 headForward,
            float headSpeed,float headAngularSpeed,HandActivityEvidence hands)
        {
            var result=new AutomaticEstimate { proposed=Activity.Unknown, reason="insufficient_visual_evidence",taskAngle=180,partnerAngle=180 };
            if(!PlacementPolicy.Finite(headSpeed)||!PlacementPolicy.Finite(headAngularSpeed)||!PlacementPolicy.Finite(hands.speed))
            {result.reason="invalid_motion";return result;}
            bool activeHands=hands.valid&&hands.inWorkspace&&hands.activeFor>=1&&hands.speed>=.10f;
            float handMotion=Mathf.Clamp01(hands.speed/.16f);
            float social=0,focus=0,physical=activeHands&&hands.bimanual?.85f*handMotion:0,focusAngle=180,toolAngle=Vector3.Angle(headForward,hands.direction);
            string physicalReason="sustained_bimanual_manipulation_proxy";
            foreach(var d in detections)
            {
                if(!Valid(d))continue;
                float angle=Vector3.Angle(headForward,d.direction);
                float alignment=Mathf.Exp(-.5f*Mathf.Pow(angle/24,2));
                // A large central person triggers a conservative social-presence proxy.
                // This does not detect faces, speech, mutual gaze, or a conversation.
                if(d.classId==0 && angle<=35 && d.viewport.width*d.viewport.height>=.06f)
                {
                    float evidence=d.score*Mathf.Exp(-.5f*Mathf.Pow(angle/40,2));
                    if(evidence>social){social=evidence;result.partnerAngle=angle;result.partnerDirection=d.direction;}
                }
                if(WorkObject(d.classId) && angle<=24 && headSpeed<.12f && headAngularSpeed<15 && (!hands.valid||hands.speed<.07f))
                {
                    float evidence=d.score*alignment;
                    if(evidence>focus){focus=evidence;focusAngle=angle;}
                }
                // Associate a real moving hand with a visual task object by bearing.
                // Metric contact is not inferred from a 2D object rectangle.
                if((ManipulableObject(d.classId)||WorkObject(d.classId)) && angle<=70 && activeHands &&
                    Vector3.Angle(hands.direction,d.direction)<=25)
                {
                    float evidence=d.score*handMotion;
                    if(evidence>physical){physical=evidence;toolAngle=angle;physicalReason="object_and_bare_hand_manipulation_proxy";}
                }
            }
            // Social presence takes precedence to avoid an audio cue over a person.
            if(social>=DecisionThreshold)
            {result.proposed=Activity.Conversation;result.confidence=social;result.reason="person_aligned_social_presence_proxy";}
            else if(physical>=DecisionThreshold)
            {result.proposed=Activity.Physical;result.confidence=physical;result.reason=physicalReason;result.handsBusy=true;result.taskAngle=toolAngle;result.movement=Mathf.Max(.25f,Mathf.Clamp01(hands.speed/.4f));}
            else if(focus>=DecisionThreshold)
            {result.proposed=Activity.Focus;result.confidence=focus;result.reason="work_object_and_stable_head_proxy";result.taskAngle=focusAngle;}
            return result;
        }

        public static bool Valid(CameraDetection d) => d!=null && d.classId>=0 && d.classId<80 &&
            PlacementPolicy.Finite(d.score) && d.score>=DetectionThreshold && d.score<=1 &&
            Finite(d.direction) && d.direction.sqrMagnitude>1e-8f &&
            PlacementPolicy.Finite(d.viewport.x)&&PlacementPolicy.Finite(d.viewport.y)&&
            PlacementPolicy.Finite(d.viewport.width)&&PlacementPolicy.Finite(d.viewport.height)&&
            d.viewport.width>0 && d.viewport.height>0 && d.viewport.xMin>=0 && d.viewport.xMax<=1 &&
            d.viewport.yMin>=0 && d.viewport.yMax<=1;
        public static bool Finite(Vector3 v) => PlacementPolicy.Finite(v.x)&&PlacementPolicy.Finite(v.y)&&PlacementPolicy.Finite(v.z);

        public static bool Fresh(float age,Vector3 captureHead,Vector3 currentHead) =>
            PlacementPolicy.Finite(age)&&age>=0&&age<=MaxObservationAge&&Finite(captureHead)&&Finite(currentHead)&&
            Vector3.Distance(captureHead,currentHead)<=MaxHeadTranslation;

        public static float IoU(Rect a,Rect b)
        {
            float area=Mathf.Max(0,Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin))*
                Mathf.Max(0,Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin));
            float union=a.width*a.height+b.width*b.height-area;
            return union>0?area/union:0;
        }
        public static void Suppress(List<CameraDetection> detections)
        {
            detections.RemoveAll(d=>!Valid(d));detections.Sort((a,b)=>b.score.CompareTo(a.score));
            for(int i=0;i<detections.Count;i++)
                for(int j=detections.Count-1;j>i;j--)
                    if(detections[i].classId==detections[j].classId && IoU(detections[i].viewport,detections[j].viewport)>.5f)
                        detections.RemoveAt(j);
            if(detections.Count>24)detections.RemoveRange(24,detections.Count-24);
        }

        public static Rect ContentRect(int sourceWidth,int sourceHeight,int width,int height)
        {
            if(sourceWidth<=0||sourceHeight<=0||width<=0||height<=0)throw new ArgumentException("Invalid image dimensions");
            float scale=Mathf.Min((float)width/sourceWidth,(float)height/sourceHeight);
            int w=Mathf.Max(1,Mathf.RoundToInt(sourceWidth*scale)),h=Mathf.Max(1,Mathf.RoundToInt(sourceHeight*scale));
            int x=(width-w)/2,top=(height-h)/2;
            return new Rect((float)x/width,(float)(height-h-top)/height,(float)w/width,(float)h/height);
        }

        public static void Decode(float[] boxes,int[] classes,float[] scores,int width,int height,
            Vector3 ray0,Vector3 rayX,Vector3 rayY,List<CameraDetection> output,Rect? contentRect=null)
        {
            if(width<=0||height<=0||boxes.Length!=classes.Length*4||classes.Length!=scores.Length)
                throw new ArgumentException("Mismatched detector output contract");
            output.Clear();
            Rect content=contentRect??new Rect(0,0,1,1);
            if(content.width<=0||content.height<=0)throw new ArgumentException("Invalid content rectangle");
            Vector3 Ray(float x,float y) => (ray0+rayX*x+rayY*y).normalized;
            for(int i=0;i<classes.Length;i++)
            {
                if(scores[i]<DetectionThreshold||!Relevant(classes[i]))continue;
                int j=i*4;
                if(!PlacementPolicy.Finite(boxes[j])||!PlacementPolicy.Finite(boxes[j+1])||
                    !PlacementPolicy.Finite(boxes[j+2])||!PlacementPolicy.Finite(boxes[j+3]))continue;
                float x0=Mathf.Clamp01((boxes[j]/width-content.x)/content.width),x1=Mathf.Clamp01((boxes[j+2]/width-content.x)/content.width);
                float y0=Mathf.Clamp01((1-boxes[j+3]/height-content.y)/content.height),y1=Mathf.Clamp01((1-boxes[j+1]/height-content.y)/content.height);
                var r=Rect.MinMaxRect(x0,y0,x1,y1);
                output.Add(new CameraDetection {classId=classes[i],score=scores[i],viewport=r,direction=Ray(r.center.x,r.center.y),ray0=ray0,rayX=rayX,rayY=rayY,
                    cornerDirections=new[]{Ray(x0,y0),Ray(x0,y1),Ray(x1,y0),Ray(x1,y1)}});
            }
            Suppress(output);
        }

        // Direction-only protection: no object distance is invented. Camera/head
        // baseline, limited translation since capture, and near-object uncertainty
        // are covered by a conservative 12-degree angular expansion. This is still
        // approximate and is not metric scene reconstruction or a safety guarantee.
        public static bool Project(CameraDetection d,Camera head,out Rect rect)
        {
            rect=default;if(!Valid(d)||d.cornerDirections==null||d.cornerDirections.Length!=4)return false;
            float x0=float.PositiveInfinity,y0=x0,x1=float.NegativeInfinity,y1=x1;
            bool behind=false,inFront=false;
            foreach(var ray in d.cornerDirections)
            {
                if(!Finite(ray)||ray.sqrMagnitude<1e-8f)return false;
                Vector3 p=head.WorldToViewportPoint(head.transform.position+ray.normalized*2);
                if(p.z<=0){behind=true;continue;}
                inFront=true;
                x0=Mathf.Min(x0,p.x);x1=Mathf.Max(x1,p.x);y0=Mathf.Min(y0,p.y);y1=Mathf.Max(y1,p.y);
            }
            if(!inFront)return false;
            // A box crossing the head-view plane must not disappear from protection.
            if(behind){rect=new Rect(0,0,1,1);return true;}
            // Expand in tangent space, so padding scales with the projection FOV.
            float padY=Mathf.Tan(12*Mathf.Deg2Rad)*Mathf.Abs(head.projectionMatrix.m11)*.5f;
            float padX=Mathf.Tan(12*Mathf.Deg2Rad)*Mathf.Abs(head.projectionMatrix.m00)*.5f;
            rect=Rect.MinMaxRect(x0-padX,y0-padY,x1+padX,y1+padY);return true;
        }
    }
}
