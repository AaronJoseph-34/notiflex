using System;
using System.Collections.Generic;
using UnityEngine;

namespace Notiflex.VR
{
    [Serializable]
    public sealed class NotificationRequest
    {
        public string id,title="Notiflex",body="Your next update is ready.",source="local_demo";
        public Priority priority=Priority.Normal;
        // Metadata is supplied by the producing application, never guessed from
        // an unrelated detection. -1 means there is no object referent.
        public int referentClassId=-1,referentTrackId=-1;
        public string referentSceneId;
        public bool demonstration;
        public int Words => string.IsNullOrWhiteSpace(body)?0:body.Split(new[]{' ','\n','\t'},StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public struct GroundedObservation
    {
        public CameraDetection detection;
        public bool metric;
        public Vector3 position,normal;
        public float variance,time;
        public string depthSource;
    }

    // Constant-velocity Kalman state with one position/velocity covariance per
    // axis. Measurement variance includes depth dispersion and capture/depth lag.
    public sealed class PositionFilter
    {
        public Vector3 Position {get;private set;}
        public Vector3 Velocity {get;private set;}
        public float Variance {get;private set;}=1;
        float p00,p01,p11,time;
        public bool Initialized {get;private set;}
        public void Reset(Vector3 position,float variance,float now)
        {Position=position;Velocity=Vector3.zero;p00=Mathf.Max(.0001f,variance);p01=0;p11=1;Variance=p00;time=now;Initialized=true;}
        public Vector3 Predict(float now) => Position+Velocity*Mathf.Clamp(now-time,0,.5f);
        public float PredictedVariance(float now)
        {float dt=Mathf.Clamp(now-time,0,1);return Mathf.Max(.0001f,p00+2*dt*p01+dt*dt*p11+.5f*dt*dt*dt*dt/4);}
        public float Innovation(Vector3 measurement,float variance,float now)
        {
            float s=PredictedVariance(now)+Mathf.Max(.0001f,variance);
            return (measurement-Predict(now)).sqrMagnitude/Mathf.Max(.0001f,s);
        }
        public bool Update(Vector3 measurement,float variance,float now)
        {
            if(!AutomaticContext.Finite(measurement)||!PlacementPolicy.Finite(variance)||variance<=0||!PlacementPolicy.Finite(now))return false;
            if(!Initialized||now-time>1||now<time){Reset(measurement,variance,now);return true;}
            float dt=Mathf.Max(0,now-time),q=.5f;
            float a=p00+2*dt*p01+dt*dt*p11+q*dt*dt*dt*dt/4;
            float b=p01+dt*p11+q*dt*dt*dt/2;
            float c=p11+q*dt*dt;
            Vector3 prediction=Position+Velocity*dt;
            float s=a+variance;
            if((measurement-prediction).sqrMagnitude/s>11.345f)return false;
            float k0=a/s,k1=b/s;
            Vector3 innovation=measurement-prediction;
            Position=prediction+k0*innovation;Velocity=Vector3.ClampMagnitude(Velocity+k1*innovation,3);
            // Joseph covariance update preserves nonnegative variances.
            p00=(1-k0)*(1-k0)*a+k0*k0*variance;
            p01=(1-k0)*(b-k1*a)+k0*k1*variance;
            p11=c-2*k1*b+k1*k1*a+k1*k1*variance;
            Variance=Mathf.Max(.0001f,p00);time=now;return true;
        }
    }

    public sealed class SpatialTrack
    {
        public int id,classId,observations;
        public float confidence,lastSeen,lastMetricTime;
        public Rect viewport;
        public Vector3 bearing,normal;
        public string depthSource;
        public readonly PositionFilter filter=new PositionFilter();
        public bool Grounded(float now) => filter.Initialized&&now>=lastMetricTime&&now-lastMetricTime<=.8f&&filter.PredictedVariance(now)<=.16f;
        public Vector3 Position(float now) => filter.Predict(now);
    }

    public sealed class SpatialObjectTracker
    {
        public readonly List<SpatialTrack> Tracks=new List<SpatialTrack>();
        int nextId=1;
        public void Clear(){Tracks.Clear();}
        public void Update(IList<GroundedObservation> observations,float now)
        {
            if(!PlacementPolicy.Finite(now))return;
            var valid=new List<GroundedObservation>();
            if(observations!=null)foreach(var original in observations)
            {
                var o=original;var d=o.detection;
                if(d==null||d.classId<0||d.classId>=80||!PlacementPolicy.Finite(d.score)||d.score<.5f||d.score>1||!AutomaticContext.Finite(d.direction)||d.direction.sqrMagnitude<.5f||
                    !PlacementPolicy.Finite(d.viewport.x)||!PlacementPolicy.Finite(d.viewport.y)||!PlacementPolicy.Finite(d.viewport.width)||!PlacementPolicy.Finite(d.viewport.height)||d.viewport.width<=0||d.viewport.height<=0)continue;
                if(o.metric&&(!AutomaticContext.Finite(o.position)||!PlacementPolicy.Finite(o.variance)||o.variance<=0))o.metric=false;
                valid.Add(o);if(valid.Count>=32)break;
            }
            observations=valid;
            Tracks.RemoveAll(t=>now-t.lastSeen>1.5f||now<t.lastSeen);
            if(Tracks.Count>64)Tracks.RemoveRange(0,Tracks.Count-64);
            int n=Tracks.Count,m=observations.Count;
            // Square assignment includes dummy tracks/detections, so a poor pair
            // is never forced merely because the cardinalities happen to match.
            int size=n+m;if(size==0)return;
            var costs=new float[size,size];
            for(int i=0;i<size;i++)for(int j=0;j<size;j++)
                costs[i,j]=i<n&&j<m?Association(Tracks[i],observations[j],now):i<n||j<m?1.5f:0;
            int[] assigned=Assignment(costs);var used=new bool[m];
            for(int i=0;i<n;i++)
            {
                int j=assigned[i];if(j>=m||costs[i,j]>=1.5f)continue;
                Apply(Tracks[i],observations[j],now);used[j]=true;
            }
            for(int j=0;j<m;j++)if(!used[j])
            {
                var t=new SpatialTrack {id=nextId++,classId=observations[j].detection.classId};
                Apply(t,observations[j],now);Tracks.Add(t);
            }
        }
        static float Association(SpatialTrack t,GroundedObservation o,float now)
        {
            if(t.classId!=o.detection.classId)return 1000;
            float angle=Vector3.Angle(t.bearing,o.detection.direction);
            float iou=AutomaticContext.IoU(t.viewport,o.detection.viewport);
            if(o.metric&&t.Grounded(now))
            {
                float innovation=t.filter.Innovation(o.position,o.variance,now);
                if(innovation>11.345f)return 1000;
                return .8f*innovation/11.345f+.2f*(1-iou);
            }
            if(angle>18||iou<.02f)return 1000;
            return .6f*(1-iou)+.4f*angle/18;
        }
        static void Apply(SpatialTrack t,GroundedObservation o,float now)
        {
            t.viewport=o.detection.viewport;t.bearing=o.detection.direction;t.confidence=o.detection.score;t.lastSeen=now;t.observations++;
            if(o.metric&&t.filter.Update(o.position,o.variance,now))
            {t.lastMetricTime=now;t.normal=o.normal;t.depthSource=o.depthSource;}
        }
        public SpatialTrack Resolve(NotificationRequest request,Vector3 headPosition,Vector3 headForward,float now,out string status)
        {
            status="no_referent_metadata";
            if(request==null||request.referentClassId<0&&request.referentTrackId<0)return null;
            SpatialTrack best=null;float bestScore=float.NegativeInfinity,second=float.NegativeInfinity;
            foreach(var t in Tracks)
            {
                if(!t.Grounded(now)||t.observations<2||now-t.lastSeen>.8f)continue;
                if(request.referentTrackId>=0&&t.id!=request.referentTrackId)continue;
                if(request.referentClassId>=0&&t.classId!=request.referentClassId)continue;
                float score=t.confidence-.35f*Mathf.Clamp01(Vector3.Angle(headForward,t.Position(now)-headPosition)/90)-.1f*Mathf.Sqrt(t.filter.PredictedVariance(now));
                if(score>bestScore){second=bestScore;bestScore=score;best=t;}else second=Mathf.Max(second,score);
            }
            if(best==null){status="referent_not_grounded_or_stale";return null;}
            if(request.referentTrackId<0&&bestScore-second<.12f){status="ambiguous_referent";return null;}
            status="tracked_depth_referent";return best;
        }
        // Hungarian minimum-cost perfect assignment, O(n^3), including dummies.
        public static int[] Assignment(float[,] cost)
        {
            int n=cost.GetLength(0);if(n!=cost.GetLength(1))throw new ArgumentException("Square assignment required");
            foreach(float value in cost)if(!PlacementPolicy.Finite(value))throw new ArgumentException("Finite costs required");
            var u=new float[n+1];var v=new float[n+1];var p=new int[n+1];var way=new int[n+1];
            for(int i=1;i<=n;i++)
            {
                p[0]=i;int j0=0;var min=new float[n+1];var used=new bool[n+1];
                for(int j=1;j<=n;j++)min[j]=float.PositiveInfinity;
                do
                {
                    used[j0]=true;int i0=p[j0],j1=0;float delta=float.PositiveInfinity;
                    for(int j=1;j<=n;j++)if(!used[j])
                    {float value=cost[i0-1,j-1]-u[i0]-v[j];if(value<min[j]){min[j]=value;way[j]=j0;}if(min[j]<delta){delta=min[j];j1=j;}}
                    for(int j=0;j<=n;j++)if(used[j]){u[p[j]]+=delta;v[j]-=delta;}else min[j]-=delta;
                    j0=j1;
                }while(p[j0]!=0);
                do{int j1=way[j0];p[j0]=p[j1];j0=j1;}while(j0!=0);
            }
            var assignment=new int[n];for(int j=1;j<=n;j++)assignment[p[j]-1]=j-1;return assignment;
        }
    }
}
