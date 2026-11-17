using System;
using System.Collections;
using System.Collections.Generic;
using Meta.XR;
using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.Android;

namespace Notiflex.VR
{
    public sealed class PlacementCandidate
    {
        public string id,source,anchorId;
        public Vector3 position;
        public Quaternion rotation;
        public float uncertainty;
    }
    public static class SpatialCandidateBuilder
    {
        // Basis is established at notification arrival; head turns do not drag
        // task-proximal cards. A tracked object's translation may move its card.
        public static void Around(List<PlacementCandidate> output,string prefix,Vector3 position,Vector3 arrivalHead,string anchor,float sigma,string source)
        {
            Vector3 forward=Vector3.ProjectOnPlane(position-arrivalHead,Vector3.up).normalized;
            if(forward.sqrMagnitude<.5f)return;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            foreach(int side in new[]{-1,1})output.Add(new PlacementCandidate {id=prefix+" "+anchor+"/"+side,anchorId=anchor,source=source,
                position=position+side*.65f*right+Vector3.up*.35f-forward*.3f,rotation=Quaternion.LookRotation(forward,Vector3.up),uncertainty=sigma});
        }
    }

    // Loads the OS's actual saved scene, never a sample-room fallback. Scene
    // anchors provide world locking; dynamic environment raycasts add live depth.
    public sealed class LiveSpatialEnvironment : MonoBehaviour
    {
        public string Status {get;private set;}="inactive";
        public string DepthStatus {get;private set;}="inactive";
        public bool HasRoom => room!=null&&room.Anchors.Count>0;
        public int SurfaceCount => surfaces.Count;
        readonly List<MRUKAnchor> surfaces=new List<MRUKAnchor>();
        readonly List<GameObject> geometry=new List<GameObject>();
        readonly List<GroundedObservation> grounded=new List<GroundedObservation>();
        readonly Dictionary<string,Vector2> surfaceCenters=new Dictionary<string,Vector2>();
        public readonly SpatialObjectTracker Tracker=new SpatialObjectTracker();
        MRUK mruk;
        MRUKRoom room;
        EnvironmentRaycastManager depth;
        Camera head;
        Transform trackingSpace;
        bool wanted,loading,permissionRequested,initialized,roomAttempted;
        int roomHash;
        float nextRoomCheck;
        Vector3 lastSpacePosition;
        Quaternion lastSpaceRotation;
        public void BeginRequest(){surfaceCenters.Clear();}
        public void ReloadRoom(){roomAttempted=false;room=null;nextRoomCheck=0;}
        public void Initialize(Camera camera,Transform space){head=camera;trackingSpace=space;StartCoroutine(Setup());}
        public void SetSensing(bool value)
        {
            wanted=value;
            if(depth!=null)depth.gameObject.SetActive(value);
            foreach(var go in geometry)if(go!=null)go.SetActive(value);
            if(mruk!=null)mruk.EnableWorldLock=value;
            if(!value){Tracker.Clear();Status="inactive";}
        }
        IEnumerator Setup()
        {
            while(!wanted||!OVRPlugin.initialized)yield return null;
            var root=new GameObject("Notiflex local room sensing");root.transform.SetParent(transform);root.SetActive(false);
            mruk=root.AddComponent<MRUK>();
            mruk.SceneSettings=new MRUK.MRUKSettings {LoadSceneOnStartup=false,DataSource=MRUK.SceneDataSource.Device};
            mruk.EnableWorldLock=true;root.SetActive(true);
            if(EnvironmentRaycastManager.IsSupported)
            {
                var go=new GameObject("Notiflex live depth raycasts");go.transform.SetParent(transform);go.SetActive(false);
                depth=go.AddComponent<EnvironmentRaycastManager>();depth.CustomTrackingSpace=trackingSpace;go.SetActive(wanted);
                DepthStatus="initializing";
            }
            else DepthStatus="not_supported";
            initialized=true;
        }
        void Update()
        {
            if(!wanted||!initialized)return;
            if(Vector3.Distance(lastSpacePosition,trackingSpace.position)>.02f||Quaternion.Angle(lastSpaceRotation,trackingSpace.rotation)>.5f)Tracker.Clear();
            lastSpacePosition=trackingSpace.position;lastSpaceRotation=trackingSpace.rotation;
            if(!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
            {
                Status="spatial_permission_required";
                if(!permissionRequested)
                {permissionRequested=true;SensorPermissions.Request(OVRPermissionsRequester.ScenePermission);}
                return;
            }
            if(room==null&&!loading&&!roomAttempted)LoadRoom();
            if(Time.unscaledTime>=nextRoomCheck)
            {
                nextRoomCheck=Time.unscaledTime+1;
                var current=mruk.GetCurrentRoom();if(current==null&&mruk.Rooms.Count>0)current=mruk.Rooms[0];
                int hash=current==null?0:current.Anchors.Count;
                if(current!=null)foreach(var a in current.Anchors)hash=unchecked(hash*31+a.Anchor.Uuid.GetHashCode()+a.PlaneRect.GetHashCode()+a.VolumeBounds.GetHashCode());
                if(current!=room||hash!=roomHash){room=current;roomHash=hash;RebuildGeometry();}
            }
        }
        async void LoadRoom()
        {
            loading=true;roomAttempted=true;Status="loading_saved_room";
            try
            {
                // The system may request a scan when none exists. No fabricated
                // scene is silently substituted if permission/capture is declined.
                var result=await mruk.LoadSceneFromDevice(true);
                if(this!=null)Status=result==MRUK.LoadDeviceResult.Success?"saved_room_loaded":result.ToString();
            }
            catch(Exception error){if(this!=null){Status="room_load_failed";Debug.LogWarning("NOTIFLEX_ROOM "+error.Message);}}
            finally{if(this!=null){loading=false;nextRoomCheck=0;}}
        }
        void RebuildGeometry()
        {
            foreach(var go in geometry)if(go!=null)Destroy(go);geometry.Clear();surfaces.Clear();surfaceCenters.Clear();
            if(room==null)return;
            foreach(var a in room.Anchors)
            {
                if(a==null)continue;
                var go=new GameObject("Notiflex observed collision geometry");go.transform.SetParent(a.transform,false);
                geometry.Add(go);go.SetActive(wanted);
                if(a.VolumeBounds.HasValue)
                {var b=a.VolumeBounds.Value;var c=go.AddComponent<BoxCollider>();c.center=b.center;c.size=b.size;}
                else if(a.PlaneRect.HasValue)
                {
                    var r=a.PlaneRect.Value;var c=go.AddComponent<BoxCollider>();c.center=new Vector3(r.center.x,r.center.y,0);c.size=new Vector3(r.width,r.height,.02f);
                    if(r.width>=.65f&&r.height>=.32f)surfaces.Add(a);
                }
                if(a==room.GlobalMeshAnchor&&a.GlobalMesh!=null)
                {var c=go.AddComponent<MeshCollider>();c.sharedMesh=a.GlobalMesh;}
            }
            Status="mapped_room_geometry";
            Debug.Log("NOTIFLEX_ROOM anchors="+room.Anchors.Count+" placementPlanes="+surfaces.Count);
        }
        public bool TryDepth(Ray ray,float maxDistance,out Vector3 point,out Vector3 normal)
        {
            point=normal=Vector3.zero;if(!wanted||depth==null||!depth.isActiveAndEnabled)return false;
            bool hit=depth.Raycast(ray,out var result,maxDistance);
            DepthStatus=result.status.ToString();
            if(!hit||!AutomaticContext.Finite(result.point)||Vector3.Distance(ray.origin,result.point)<.15f)return false;
            point=result.point;normal=result.normal;return true;
        }
        public void Observe(IList<CameraDetection> detections,float capturedAt,float now)
        {
            grounded.Clear();
            foreach(var d in detections)
            {
                var o=new GroundedObservation {detection=d,time=now,depthSource="bearing_only"};
                float lag=now-capturedAt;
                if(lag>=0&&lag<=.55f&&TryGround(d,lag,out var p,out var normal,out float variance))
                {o.metric=true;o.position=p;o.normal=normal;o.variance=variance;o.depthSource="live_environment_depth";}
                grounded.Add(o);
            }
            Tracker.Update(grounded,now);
        }
        bool TryGround(CameraDetection d,float lag,out Vector3 position,out Vector3 normal,out float variance)
        {
            position=normal=Vector3.zero;variance=1;
            var distances=new List<float>();var normals=new List<Vector3>();float centerDistance=-1;
            Vector2[] offsets={Vector2.zero,new Vector2(-.2f,0),new Vector2(.2f,0),new Vector2(0,-.2f),new Vector2(0,.2f)};
            foreach(var offset in offsets)
            {
                Vector2 uv=d.viewport.center+Vector2.Scale(offset,d.viewport.size);
                Vector3 direction=d.RayAt(uv);
                if(TryDepth(new Ray(d.origin,direction),8,out var p,out var n))
                {float distance=Vector3.Dot(p-d.origin,direction);distances.Add(distance);normals.Add(n);if(offset==Vector2.zero)centerDistance=distance;}
            }
            bool fused=GroundingMath.Fuse(d.origin,d.direction,distances,normals,lag,out position,out normal,out variance);
            // Neighbor rays must support the center rather than replacing a
            // foreground object with a majority of background-wall samples.
            return fused&&centerDistance>0&&Mathf.Abs(centerDistance-Vector3.Distance(position,d.origin))<=.15f+Mathf.Min(.1f,Mathf.Sqrt(variance));
        }
        public void AddSurfaceCandidates(List<PlacementCandidate> output,Vector2 panelSize,int limit=4)
        {
            if(!wanted||room==null)return;
            var choices=new List<PlacementCandidate>();
            foreach(var a in surfaces)
            {
                if(a==null||!a.PlaneRect.HasValue)continue;
                Rect r=a.PlaneRect.Value;Vector3 local=a.transform.InverseTransformPoint(head.transform.position);
                Vector3 normal=a.transform.forward*(local.z>=0?1:-1);
                if(Mathf.Abs(Vector3.Dot(normal,Vector3.up))>.65f)continue; // Keep text upright and readable.
                string anchorKey=a.Anchor.Uuid.ToString();
                Vector2 center=new Vector2(Mathf.Clamp(local.x,r.xMin+panelSize.x/2+.05f,r.xMax-panelSize.x/2-.05f),
                    Mathf.Clamp(local.y,r.yMin+panelSize.y/2+.05f,r.yMax-panelSize.y/2-.05f));
                if(surfaceCenters.TryGetValue(anchorKey,out var retained))center=retained;else surfaceCenters[anchorKey]=center;
                Vector2[] offsets={Vector2.zero,new Vector2(-.4f,0),new Vector2(.4f,0),new Vector2(0,.3f)};
                for(int i=0;i<offsets.Length;i++)
                {
                    Vector2 c=center+offsets[i];
                    if(c.x-panelSize.x/2<r.xMin+.03f||c.x+panelSize.x/2>r.xMax-.03f||c.y-panelSize.y/2<r.yMin+.03f||c.y+panelSize.y/2>r.yMax-.03f)continue;
                    Vector3 p=a.transform.TransformPoint(new Vector3(c.x,c.y,0))+normal*.08f;
                    float distance=Vector3.Distance(head.transform.position,p);
                    if(distance<.8f||distance>3.5f||Vector3.Angle(head.transform.forward,p-head.transform.position)>50)continue;
                    Quaternion orientation=Quaternion.LookRotation(-normal,Vector3.up);
                    float scale=PlacementPolicy.PresentationScale(distance);bool fits=true;
                    foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})
                    {
                        Vector3 corner=a.transform.InverseTransformPoint(p+orientation*new Vector3(x*panelSize.x*scale/2,y*panelSize.y*scale/2,0));
                        if(!r.Contains(new Vector2(corner.x,corner.y))||!a.IsPositionInBoundary(new Vector2(corner.x,corner.y)))fits=false;
                    }
                    if(!fits)continue;
                    string id=a.Anchor.Uuid.ToString();
                    choices.Add(new PlacementCandidate {id="Surface "+id+"/"+i,source="scene_anchor_plane",anchorId=id,position=p,
                        rotation=orientation});
                }
            }
            choices.Sort((a,b)=>Vector3.Angle(head.transform.forward,a.position-head.transform.position).CompareTo(Vector3.Angle(head.transform.forward,b.position-head.transform.position)));
            for(int i=0;i<Mathf.Min(limit,choices.Count);i++)output.Add(choices[i]);
        }
        public bool Occluded(Vector3 origin,Vector3[] corners,out string provenance)
        {
            provenance=HasRoom?"mapped_room":"unmapped_room";int observed=0;
            // SDK raycasts complement the conservative prism/collider checks in
            // SpatialEvaluator. Missing depth is logged, never called a free ray.
            Vector3 center=Vector3.zero;foreach(var c in corners)center+=c/corners.Length;
            for(int i=-1;i<corners.Length;i++)
            {
                Vector3 target=i<0?center:corners[i],delta=target-origin;
                float distance=delta.magnitude;
                if(TryDepth(new Ray(origin,delta.normalized),distance+.1f,out var point,out _))
                {observed++;if(Vector3.Distance(origin,point)<distance-.06f){provenance+="_depth_blocked";return true;}}
            }
            provenance+="_depth_hits_"+observed+"_of_5";
            return false;
        }
        public MRUKAnchor FindAnchor(string uuid)
        {if(room!=null)foreach(var a in room.Anchors)if(a.Anchor.Uuid.ToString()==uuid)return a;return null;}
        void OnDestroy(){foreach(var go in geometry)if(go!=null)Destroy(go);}
    }

    public static class GroundingMath
    {
        public static bool Fuse(Vector3 origin,Vector3 bearing,List<float> distances,List<Vector3> normals,float lag,
            out Vector3 position,out Vector3 normal,out float variance)
        {
            position=normal=Vector3.zero;variance=1;
            if(!AutomaticContext.Finite(origin)||!AutomaticContext.Finite(bearing)||bearing.sqrMagnitude<.5f||distances==null||distances.Count<3||!PlacementPolicy.Finite(lag)||lag<0||lag>.55f)return false;
            var valid=new List<float>();foreach(float d in distances)if(PlacementPolicy.Finite(d)&&d>=.2f&&d<=8)valid.Add(d);
            if(valid.Count<3)return false;valid.Sort();float median=valid[valid.Count/2];
            var deviations=new List<float>();foreach(float d in valid)deviations.Add(Mathf.Abs(d-median));deviations.Sort();
            float mad=deviations[deviations.Count/2];int inliers=0;
            foreach(float d in valid)if(Mathf.Abs(d-median)<=Mathf.Max(.08f,3*mad))inliers++;
            if(inliers<3||mad>.25f)return false;
            float sigma=.035f+1.4826f*mad+.5f*lag;
            variance=sigma*sigma;position=origin+bearing.normalized*median;
            foreach(var n in normals)if(AutomaticContext.Finite(n))normal+=n;
            normal=normal.sqrMagnitude>.1f?normal.normalized:-bearing.normalized;return true;
        }
    }
}
