using System;
using System.Collections.Generic;
using UnityEngine;

namespace Notiflex.VR
{
    public enum Activity { Unknown, Focus, Physical, Conversation }
    public enum Priority { Low, Normal, Urgent }
    public enum Channel { Deferred, Visual, Peripheral, SpatialAudio }
    public enum RegionKind { Task, Referent, Face, Hazard }

    [Serializable]
    public struct ContextSnapshot
    {
        public bool poseValid, handsBusy, conversation, audioEnabled, speaking;
        public float workload, movement, taskAngle, partnerAngle, age;
        public Activity activity;
    }
    [Serializable]
    public sealed class PlacementResult
    {
        public string id, rejected;
        public Vector3 position;
        public float interruption, occlusion, social, total, headAngle, partnerAngle, distance, faceCoverage, hazardCoverage;
        public bool eligible;
    }
    [Serializable]
    public struct WeightProfile
    {
        public float interruption, occlusion, social;
        public WeightProfile(float i,float o,float s) { interruption=i;occlusion=o;social=s; }
    }
    // Deterministic policy. Context inputs are observations or explicitly supplied experiment
    // ground truth. These are prototype surrogates, not validated probabilities of human costs.
    public static class PlacementPolicy
    {
        public const float MaxContextAge=2, MinDistance=.8f, MaxDistance=3.5f, UrgentAngle=20;
        public const float MaxFaceCoverage=.02f, MaxHazardCoverage=.01f, ViewportMargin=.04f;
        // Preserve angular text size beyond the 1.8 m reference distance.
        public static float PresentationScale(float distance) => Mathf.Max(1,distance/1.8f);
        public static Activity Infer(ContextSnapshot c)
        {
            if(!c.poseValid || !Finite(c.age) || c.age<0 || c.age>MaxContextAge) return Activity.Unknown;
            if(c.conversation && c.partnerAngle<65) return Activity.Conversation;
            if(c.handsBusy && c.movement>=.25f) return Activity.Physical;
            if(c.taskAngle<=24 && c.movement<.25f && c.workload>=.3f) return Activity.Focus;
            return Activity.Unknown;
        }
        public static WeightProfile Weights(Activity a,Priority p)
        {
            if(a==Activity.Conversation) return p==Priority.Low?new WeightProfile(.15f,.25f,.6f):p==Priority.Normal?new WeightProfile(.2f,.25f,.55f):new WeightProfile(.1f,.35f,.55f);
            if(a==Activity.Physical) return p==Priority.Low?new WeightProfile(.3f,.7f,0):p==Priority.Normal?new WeightProfile(.25f,.75f,0):new WeightProfile(.15f,.85f,0);
            if(a==Activity.Focus) return p==Priority.Low?new WeightProfile(.7f,.3f,0):p==Priority.Normal?new WeightProfile(.55f,.45f,0):new WeightProfile(.2f,.8f,0);
            return new WeightProfile(.4f,.6f,0);
        }
        public static void Score(PlacementResult r,ContextSnapshot c,Priority p,int words)
        {
            float central=Mathf.Exp(-.5f*Mathf.Pow(r.headAngle/18,2));
            float shift=Mathf.Clamp01(r.headAngle/90),load=Mathf.Clamp01(c.workload);
            r.interruption=c.activity==Activity.Physical?load*(.2f*central+.8f*shift):load*(.75f*central+.25f*shift);
            r.occlusion=Mathf.Clamp01(r.occlusion);
            float seconds=1.5f+Mathf.Max(0,words)/3.3f;
            r.social=c.activity==Activity.Conversation?Mathf.Clamp01((.4f*shift+.6f*Mathf.Clamp01(r.partnerAngle/90))*(.5f+.5f*Mathf.Clamp01(seconds/8))):0;
            var w=Weights(c.activity,p);
            r.total=w.interruption*r.interruption+w.occlusion*r.occlusion+w.social*r.social;
            if(!Finite(r.total)) { r.total=1; r.interruption=1;r.occlusion=1;r.social=1;Reject(r,"nonfinite_cost"); }
        }
        public static bool Finite(float x) { return !float.IsNaN(x)&&!float.IsInfinity(x); }
        public static void Reject(PlacementResult r,string reason) { r.eligible=false; r.rejected=reason; }
        public static PlacementResult Best(IList<PlacementResult> choices)
        {
            PlacementResult best=null;
            foreach(var c in choices) if(c.eligible && (best==null || c.total<best.total)) best=c;
            return best;
        }
        public static Channel Delivery(ContextSnapshot c,Priority p,bool visualAvailable)
        {
            if(!c.poseValid || !Finite(c.age) || c.age<0 || c.age>MaxContextAge) return Channel.Deferred;
            if(!visualAvailable) return c.audioEnabled && !c.conversation && !c.speaking?Channel.SpatialAudio:Channel.Deferred;
            if(p==Priority.Urgent) return Channel.Visual;
            if(p==Priority.Low || c.activity==Activity.Unknown || c.activity==Activity.Conversation) return Channel.Peripheral;
            if(c.activity==Activity.Physical && c.handsBusy && c.audioEnabled && !c.speaking) return Channel.SpatialAudio;
            return Channel.Visual;
        }
        public static float Coverage(Rect panel,Rect target)
        {
            float area=target.width*target.height;
            if(area<1e-8f) return 0;
            return Mathf.Clamp01(Mathf.Max(0,Mathf.Min(panel.xMax,target.xMax)-Mathf.Max(panel.xMin,target.xMin))*
                Mathf.Max(0,Mathf.Min(panel.yMax,target.yMax)-Mathf.Max(panel.yMin,target.yMin))/area);
        }
    }

    public sealed class ContextDwell
    {
        public Activity Current {get;private set;}=Activity.Unknown;
        Activity pending=Activity.Unknown;
        float since;
        public bool Update(Activity proposed,float now)
        {
            if(pending!=proposed) { pending=proposed; since=now; }
            if(Current!=pending && now-since>=1.2f) { Current=pending; return true; }
            return false;
        }
    }
}
