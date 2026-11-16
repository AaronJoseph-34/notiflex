using UnityEngine;

namespace Notiflex.VR
{
    public struct HandActivityEvidence
    {
        public bool valid,inWorkspace,bimanual;
        public int trackedHands;
        public float speed,activeFor;
        public Vector3 direction;
    }

    // Position smoothing precedes differentiation so stationary tracking jitter
    // is not treated as hand work. A jump/reacquisition never becomes a velocity.
    public sealed class HandMotionHistory
    {
        public bool Valid {get;private set;}
        public Vector3 Position {get;private set;}
        public float Speed {get;private set;}
        Vector3 index,thumb;
        bool previousFingerConfidence;
        float time;
        public void Reset(){Valid=false;Speed=0;}
        public void Sample(Vector3 position,Vector3 indexLocal,Vector3 thumbLocal,float now,bool fingersReliable=true)
        {
            if(!fingersReliable){indexLocal=Vector3.zero;thumbLocal=Vector3.zero;}
            if(!AutomaticContext.Finite(position)||!AutomaticContext.Finite(indexLocal)||!AutomaticContext.Finite(thumbLocal)||!PlacementPolicy.Finite(now))
            {Reset();return;}
            float dt=now-time;
            if(!Valid||dt<=0||dt>.2f||Vector3.Distance(position,Position)>.20f)
            {Position=position;index=indexLocal;thumb=thumbLocal;time=now;Speed=0;Valid=true;previousFingerConfidence=fingersReliable;return;}
            float blend=1-Mathf.Exp(-dt/.06f);
            Vector3 p=Vector3.Lerp(Position,position,blend);
            Vector3 i=fingersReliable&&previousFingerConfidence?Vector3.Lerp(index,indexLocal,blend):indexLocal;
            Vector3 t=fingersReliable&&previousFingerConfidence?Vector3.Lerp(thumb,thumbLocal,blend):thumbLocal;
            float wrist=Vector3.Distance(p,Position)/dt;
            float fingers=fingersReliable&&previousFingerConfidence?(Vector3.Distance(i,index)+Vector3.Distance(t,thumb))/(2*dt):0;
            float speed=Mathf.Max(wrist,fingers);
            if(speed>2){Reset();return;}
            Speed=Mathf.Lerp(Speed,speed,1-Mathf.Exp(-dt/.25f));
            Position=p;index=i;thumb=t;time=now;previousFingerConfidence=fingersReliable;
        }
    }

    public sealed class BareHandActivity
    {
        public HandActivityEvidence Evidence {get;private set;}
        public string Status {get;private set;}="no_bare_hands";
        readonly HandMotionHistory[] histories={new HandMotionHistory(),new HandMotionHistory()};
        readonly OVRPlugin.HandState[] states=new OVRPlugin.HandState[2];
        readonly double[] stamps=new double[2];
        readonly float[] received=new float[2];
        float activeFor;
        string previousStatus;

        public void Reset()
        {
            foreach(var history in histories)history.Reset();
            System.Array.Clear(stamps,0,stamps.Length);System.Array.Clear(received,0,received.Length);
            activeFor=0;Evidence=default;Status="inactive";
        }
        public void Update(Transform head,Transform trackingSpace,float now,float dt)
        {
            int count=0,tracked=0;bool left=false,right=false;Vector3 direction=Vector3.zero;float speed=0;
            for(int h=0;h<2;h++)
            {
                bool valid=Read(h,trackingSpace,now);
                if(!valid)continue;
                tracked++;
                Vector3 delta=histories[h].Position-head.position;
                bool workspace=InWorkspace(delta,head.forward);
                if(!workspace)continue;
                count++;if(h==0)left=true;else right=true;
                if(histories[h].Speed>=speed){speed=histories[h].Speed;direction=delta.normalized;}
            }
            bool bimanual=left&&right&&Vector3.Distance(histories[0].Position,histories[1].Position)<.65f;
            if(count>0&&speed>=.07f)activeFor=Mathf.Min(2,activeFor+Mathf.Clamp(dt,0,.1f));
            else activeFor=Mathf.Max(0,activeFor-2*Mathf.Clamp(dt,0,.1f));
            Evidence=new HandActivityEvidence {valid=tracked>0,inWorkspace=count>0,trackedHands=tracked,bimanual=bimanual,
                direction=direction,speed=speed,activeFor=activeFor};
            Status=!OVRPlugin.GetHandTrackingEnabled()?"hand_tracking_inactive":tracked==0?"no_confident_bare_hands":count==0?"hands_outside_lower_workspace":bimanual?"two_bare_hands":"one_bare_hand";
            if(Status!=previousStatus){previousStatus=Status;Debug.Log("NOTIFLEX_HANDS "+Status);}
        }
        public static bool InWorkspace(Vector3 offset,Vector3 headForward) => AutomaticContext.Finite(offset)&&
            offset.magnitude>=.15f&&offset.magnitude<=1.25f&&offset.y<-.10f&&offset.y>-.95f&&
            Vector3.Dot(offset,headForward)>.08f;

        bool Read(int h,Transform trackingSpace,float now)
        {
            var hand=(OVRPlugin.Hand)h;
            bool valid=OVRPlugin.GetHandState(OVRPlugin.Step.Render,hand,ref states[h])&&
                (states[h].Status&OVRPlugin.HandStatus.HandTracked)!=0&&
                (states[h].Status&OVRPlugin.HandStatus.SystemGestureInProgress)==0&&
                states[h].HandConfidence==OVRPlugin.TrackingConfidence.High&&
                OVRInput.GetControllerIsInHandState((OVRInput.Hand)h)!=OVRInput.ControllerInHandState.ControllerInHand;
            // Controller-driven or inferred hand poses cannot count as bare hands.
            if(OVRPlugin.GetHandPoseSourceInferred(-1,hand,out bool inferred)==OVRPlugin.Result.Success&&inferred)valid=false;
            double stamp=states[h].SampleTimeStamp;
            valid=valid&&!double.IsNaN(stamp)&&!double.IsInfinity(stamp)&&stamp>0&&
                OVRPlugin.HandSkeletonVersion==OVRHandSkeletonVersion.OpenXR&&states[h].BonePositions!=null&&states[h].BonePositions.Length>=26;
            if(!valid){histories[h].Reset();return false;}
            if(stamp<stamps[h]-.5){histories[h].Reset();stamps[h]=0;}
            if(stamp<=stamps[h])
            {
                if(now-received[h]>.2f)histories[h].Reset();
                return histories[h].Valid;
            }
            stamps[h]=stamp;received[h]=now;
            Vector3 wrist=trackingSpace.TransformPoint(states[h].RootPose.Position.FromFlippedZVector3f());
            Vector3 index=states[h].BonePositions[(int)OVRPlugin.BoneId.XRHand_IndexTip].FromFlippedZVector3f();
            Vector3 thumb=states[h].BonePositions[(int)OVRPlugin.BoneId.XRHand_ThumbTip].FromFlippedZVector3f();
            bool fingersReliable=states[h].FingerConfidences!=null&&states[h].FingerConfidences.Length>=2&&
                states[h].FingerConfidences[(int)OVRPlugin.HandFinger.Index]==OVRPlugin.TrackingConfidence.High&&
                states[h].FingerConfidences[(int)OVRPlugin.HandFinger.Thumb]==OVRPlugin.TrackingConfidence.High;
            histories[h].Sample(wrist,index,thumb,now,fingersReliable);return histories[h].Valid;
        }
    }
}
