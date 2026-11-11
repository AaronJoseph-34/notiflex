using System.Collections.Generic;
using UnityEngine;

namespace Notiflex.VR
{
    // The notification is rendered in world space. Scoring uses center-head projection,
    // never eye tracking; conservative per-eye viewport margins are checked separately.
    public sealed class SpatialEvaluator
    {
        public readonly List<PlacementResult> Results=new List<PlacementResult>();
        public Vector2 PanelSize=new Vector2(.62f,.29f);
        public void Evaluate(Camera head,Transform referent,Transform partner,IList<ProtectedRegion> regions,ContextSnapshot context,Priority priority,int words)
        {
            Results.Clear();
            var h=head.transform;
            Add("View center",h.TransformPoint(new Vector3(0,0,1.8f)),head,partner,regions,context,priority,words);
            Add("View left",h.TransformPoint(new Vector3(-.85f,.08f,1.8f)),head,partner,regions,context,priority,words);
            Add("View right",h.TransformPoint(new Vector3(.85f,.08f,1.8f)),head,partner,regions,context,priority,words);
            Add("View upper",h.TransformPoint(new Vector3(0,.55f,1.8f)),head,partner,regions,context,priority,words);
            if(referent!=null)
            {
                Add("Referent left",referent.position-h.right*.65f+h.up*.35f-(referent.position-h.position).normalized*.3f,head,partner,regions,context,priority,words);
                Add("Referent right",referent.position+h.right*.65f+h.up*.35f-(referent.position-h.position).normalized*.3f,head,partner,regions,context,priority,words);
            }
            if(partner!=null && context.conversation)
            {
                Add("Partner left",partner.position-h.right*.6f+h.up*.35f-(partner.position-h.position).normalized*.35f,head,partner,regions,context,priority,words);
                Add("Partner right",partner.position+h.right*.6f+h.up*.35f-(partner.position-h.position).normalized*.35f,head,partner,regions,context,priority,words);
            }
        }
        void Add(string id,Vector3 position,Camera head,Transform partner,IList<ProtectedRegion> regions,ContextSnapshot context,Priority priority,int words)
        {
            Transform h=head.transform;
            Vector3 direction=position-h.position;
            var r=new PlacementResult { id=id,position=position,distance=direction.magnitude,headAngle=Vector3.Angle(h.forward,direction),
                partnerAngle=partner==null?0:Vector3.Angle(partner.position-h.position,direction),eligible=true,rejected="" };
            Results.Add(r);
            if(!context.poseValid || !PlacementPolicy.Finite(context.age) || context.age<0 || context.age>PlacementPolicy.MaxContextAge) PlacementPolicy.Reject(r,"invalid_or_stale_pose");
            else if(r.distance<PlacementPolicy.MinDistance || r.distance>PlacementPolicy.MaxDistance) PlacementPolicy.Reject(r,"reading_distance");
            else if(priority==Priority.Urgent && r.headAngle>PlacementPolicy.UrgentAngle) PlacementPolicy.Reject(r,"urgent_noticeability");
            float scale=PlacementPolicy.PresentationScale(r.distance);
            Vector3 right=h.right*PanelSize.x*scale/2,up=h.up*PanelSize.y*scale/2;
            Vector3[] corners={position-right-up,position-right+up,position+right-up,position+right+up};
            Rect panel;
            if(!Project(head,corners,out panel) || panel.xMin<.04f || panel.xMax>.96f || panel.yMin<.04f || panel.yMax>.96f) PlacementPolicy.Reject(r,"outside_view");
            if(head.stereoEnabled)
            {
                foreach(Camera.MonoOrStereoscopicEye eye in new[]{Camera.MonoOrStereoscopicEye.Left,Camera.MonoOrStereoscopicEye.Right})
                    foreach(var corner in corners)
                    {
                        var v=head.WorldToViewportPoint(corner,eye);
                        if(v.z<=0 || v.x<.04f || v.x>.96f || v.y<.04f || v.y>.96f) PlacementPolicy.Reject(r,"outside_stereo_view");
                    }
            }
            foreach(var region in regions)
            {
                if(region==null || !region.isActiveAndEnabled) continue;
                Rect protectedRect;
                // Passthrough has no environment-depth occlusion in this MVP. A virtual
                // card behind an annotated person can still composite over their image.
                // Conservatively protect projected regions regardless of relative depth.
                if(!Project(head,region.Corners(),out protectedRect)) continue;
                float coverage=PlacementPolicy.Coverage(panel,protectedRect);
                r.occlusion=Mathf.Max(r.occlusion,coverage);
                if(region.kind==RegionKind.Face)r.faceCoverage=Mathf.Max(r.faceCoverage,coverage);
                if(region.kind==RegionKind.Hazard)r.hazardCoverage=Mathf.Max(r.hazardCoverage,coverage);
                if(region.kind==RegionKind.Face && coverage>PlacementPolicy.MaxFaceCoverage) PlacementPolicy.Reject(r,"face_coverage");
                if(region.kind==RegionKind.Hazard && coverage>PlacementPolicy.MaxHazardCoverage) PlacementPolicy.Reject(r,"hazard_coverage");
            }
            // A swept rectangular prism conservatively contains all head-to-panel rays.
            // It rejects more than a visibility pyramid, but does not miss thin occluders
            // between sparse sample rays. This covers supplied colliders, not unknown room geometry.
            var halfExtents=new Vector3(PanelSize.x*scale/2,PanelSize.y*scale/2,.015f);
            if(Physics.CheckBox(h.position,halfExtents,h.rotation,~0,QueryTriggerInteraction.Ignore) ||
                Physics.BoxCast(h.position,halfExtents,direction.normalized,
                out RaycastHit hit,h.rotation,Mathf.Max(0,direction.magnitude-.03f),~0,QueryTriggerInteraction.Ignore))
                PlacementPolicy.Reject(r,"blocked_by_geometry");
            PlacementPolicy.Score(r,context,priority,words);
        }
        public static bool Project(Camera camera,Vector3[] corners,out Rect rect)
        {
            float x0=float.PositiveInfinity,y0=x0,x1=float.NegativeInfinity,y1=x1;
            foreach(var point in corners)
            {
                Vector3 p=camera.WorldToViewportPoint(point);
                if(p.z<=camera.nearClipPlane) { rect=default; return false; }
                x0=Mathf.Min(x0,p.x);x1=Mathf.Max(x1,p.x);y0=Mathf.Min(y0,p.y);y1=Mathf.Max(y1,p.y);
            }
            rect=Rect.MinMaxRect(x0,y0,x1,y1); return true;
        }
    }
}
