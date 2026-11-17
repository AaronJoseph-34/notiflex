using System;
using UnityEngine;

namespace Notiflex.VR
{
    [Serializable]
    public sealed class SocialRiskProfile
    {
        public int schema=1;
        public string id="hypothesis_default",dataOrigin="unfitted_prototype";
        public bool fitted;
        public float wordsPerMinute=180,startReadingSeconds=.8f,cueSeconds=.65f,chimeSeconds=.25f;
        public float intercept=-2,headCoefficient=1,partnerCoefficient=1,durationCoefficient=1,interactionCoefficient=1;
        public int trainingParticipants,testParticipants,testExamples;
        public float testBrierScore,testAuc;
        public bool Valid => schema==1&&Range(wordsPerMinute,60,500)&&Range(startReadingSeconds,0,5)&&Range(cueSeconds,.1f,3)&&Range(chimeSeconds,.1f,3)&&
            Range(intercept,-20,20)&&Range(headCoefficient,0,20)&&Range(partnerCoefficient,0,20)&&Range(durationCoefficient,0,20)&&Range(interactionCoefficient,0,20);
        public bool HasParticipantFit => Valid&&fitted&&dataOrigin=="participant_study"&&trainingParticipants>=2&&testParticipants>=1&&testExamples>=10&&Range(testBrierScore,0,1)&&Range(testAuc,0,1);
        static bool Range(float x,float min,float max)=>PlacementPolicy.Finite(x)&&x>=min&&x<=max;
    }

    public static class SocialRiskModel
    {
        public static SocialRiskProfile Profile {get;private set;}=new SocialRiskProfile();
        public static bool SetProfile(SocialRiskProfile value)
        {if(value==null||!value.Valid)return false;Profile=value;return true;}
        public static float Exposure(int words,Channel channel)
        {
            if(channel==Channel.Deferred)return 0;
            if(channel==Channel.Peripheral)return Profile.cueSeconds;
            if(channel==Channel.SpatialAudio)return Profile.chimeSeconds;
            return Profile.startReadingSeconds+Mathf.Max(0,words)*60/Profile.wordsPerMinute;
        }
        public static float Score(float headAngle,float partnerAngle,int words,Channel channel,float interactionSupport=1,float viewIncidence=1)
        {
            if(!PlacementPolicy.Finite(headAngle)||!PlacementPolicy.Finite(partnerAngle)||!PlacementPolicy.Finite(interactionSupport)||!PlacementPolicy.Finite(viewIncidence))return 1;
            if(channel==Channel.Deferred)return 0;
            float h=Mathf.Clamp01(headAngle/90),p=Mathf.Clamp01(partnerAngle/90),d=Mathf.Clamp01(Exposure(words,channel)/(channel==Channel.Visual?Mathf.Max(.6f,viewIncidence):1)/8);
            // Chimes do not ask for a visual attention shift. Conversation audio
            // suppression is enforced separately by the delivery policy.
            if(channel==Channel.SpatialAudio){h=0;p=0;}
            if(Profile.HasParticipantFit)
            {
                float z=Profile.intercept+Profile.headCoefficient*h+Profile.partnerCoefficient*p+Profile.durationCoefficient*d+Profile.interactionCoefficient*p*d;
                return Mathf.Clamp01(interactionSupport)/(1+Mathf.Exp(-Mathf.Clamp(z,-20,20)));
            }
            return Mathf.Clamp01(interactionSupport)*Mathf.Clamp01((.35f*h+.65f*p)*(.25f+.75f*d));
        }
    }
}
