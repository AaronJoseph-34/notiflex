using System.Collections.Generic;
using UnityEngine;

namespace Notiflex.VR
{
    public sealed class MultimodalContextEstimator
    {
        int incumbent=-1;
        public void Reset(){incumbent=-1;}
        public AutomaticEstimate Fuse(AutomaticEstimate visual,IList<CameraDetection> detections,IList<SpatialTrack> tracks,
            HandActivityEvidence hands,SpeechEvidence speech,Vector3 headPosition,Vector3 headForward,float headTurn,float now,float headSpeed=0)
        {
            visual.personPresent=false;visual.partnerTrackId=-1;visual.partnerMetric=false;visual.interactionSupport=0;visual.interactionEvidence="no_visible_partner";
            if(visual.proposed==Activity.Conversation)
            {
                // Person presence must not hide otherwise valid work/manipulation
                // evidence when microphone evidence does not support interaction.
                var taskObjects=new List<CameraDetection>();foreach(var d in detections)if(d!=null&&d.classId!=0)taskObjects.Add(d);
                visual=AutomaticContext.Estimate(taskObjects,headForward,headSpeed,headTurn,hands);
                visual.partnerTrackId=-1;visual.interactionEvidence="person_presence_without_speech_evidence";
            }
            SpatialTrack partner=null;float best=0,second=0;
            foreach(var track in tracks)
            {
                if(track.classId!=0||now-track.lastSeen>.8f||track.observations<2)continue;
                visual.personPresent=true;
                if(ScreenDepiction(track,tracks,now)){visual.interactionEvidence="possible_person_on_screen";continue;}
                Vector3 delta=track.Grounded(now)?track.Position(now)-headPosition:track.bearing;
                float angle=Vector3.Angle(headForward,delta);
                if(angle>35||track.viewport.width*track.viewport.height<.06f)continue;
                if(track.Grounded(now)&&(delta.magnitude<.4f||delta.magnitude>3.5f))continue;
                float value=track.confidence*Mathf.Exp(-.5f*Mathf.Pow(angle/40,2));
                if(track.id==incumbent)value+=.05f;
                if(value>best){second=best;best=value;partner=track;}else second=Mathf.Max(second,value);
            }
            if(partner!=null&&best-second<.08f){partner=null;visual.interactionEvidence="multiple_people_ambiguous";}
            if(partner!=null)
            {
                incumbent=partner.id;visual.partnerTrackId=partner.id;visual.partnerMetric=partner.Grounded(now);
                visual.partnerDirection=(visual.partnerMetric?partner.Position(now)-headPosition:partner.bearing).normalized;
                visual.partnerAngle=Vector3.Angle(headForward,visual.partnerDirection);
                visual.interactionEvidence=speech.available?"person_without_recent_speech":"person_presence_microphone_unavailable";
                if(speech.available&&now-speech.lastSpeechTime<=2&&speech.recentSpeechRatio>=.12f)
                {
                    bool turnTaking=speech.anonymousSpeakers>=2&&speech.turnSwitches>=2;
                    float acoustic=turnTaking?.95f:.72f;
                    float support=Mathf.Clamp01(best)*acoustic;
                    visual.interactionEvidence=turnTaking?"anonymous_turn_taking_with_visible_person":"speech_with_visible_person";
                    visual.interactionSupport=support;
                    if(support>=.55f)
                    {visual.proposed=Activity.Conversation;visual.confidence=support;visual.reason="multimodal_likely_conversation";}
                }
            }
            // Fine finger movement around a desk object is compatible with focused
            // work; moving both wrists is stronger physical-manipulation evidence.
            if(visual.proposed!=Activity.Conversation&&hands.valid&&hands.wristSpeed<.07f&&headTurn<15)
            {
                foreach(var track in tracks)
                {
                    if(!AutomaticContext.WorkObject(track.classId)||!track.Grounded(now)||now-track.lastSeen>.8f)continue;
                    Vector3 p=track.Position(now);
                    bool near=hands.leftWorking&&Vector3.Distance(hands.leftPosition,p)<.5f||hands.rightWorking&&Vector3.Distance(hands.rightPosition,p)<.5f;
                    float angle=Vector3.Angle(headForward,p-headPosition);
                    if(near&&angle<=24&&track.confidence>=.7f)
                    {visual.proposed=Activity.Focus;visual.confidence=track.confidence;visual.taskAngle=angle;visual.handsBusy=false;visual.movement=0;visual.reason="grounded_desk_object_and_fine_hand_activity";break;}
                }
            }
            visual.workload=visual.proposed==Activity.Physical?Mathf.Clamp(.5f+.3f*hands.speed,.5f,.9f):
                visual.proposed==Activity.Focus?.65f:visual.proposed==Activity.Conversation?.6f:.1f;
            return visual;
        }
        public static bool ScreenDepiction(SpatialTrack person,IList<SpatialTrack> tracks,float now)
        {
            if(!person.Grounded(now))return false;
            foreach(var screen in tracks)
            {
                if((screen.classId!=62&&screen.classId!=63)||!screen.Grounded(now)||now-screen.lastSeen>.8f)continue;
                if(PlacementPolicy.Coverage(screen.viewport,person.viewport)<.8f)continue;
                if(Mathf.Sqrt(person.filter.PredictedVariance(now)+screen.filter.PredictedVariance(now))>.2f)continue;
                if(Vector3.Distance(person.Position(now),screen.Position(now))<.12f)return true;
            }
            return false;
        }
    }

    public sealed class NotificationAudioGate
    {
        int played=-1;
        public bool TryConsume(int request)
        {if(request<=0||request==played)return false;played=request;return true;}
    }
}
