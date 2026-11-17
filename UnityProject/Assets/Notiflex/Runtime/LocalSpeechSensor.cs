using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Android;

namespace Notiflex.VR
{
    // All waveform buffers and anonymous embeddings are session memory only.
    // This component performs no file/network operations and creates no transcript.
    public sealed class LocalSpeechSensor : MonoBehaviour
    {
        struct Packet {public float[] samples;public float time;}
        struct VoiceFrame {public float time;public bool voice;}
        public string Status {get;private set;}="inactive";
        public string SpeakerStatus {get;private set;}="not_ready";
        public float VadElapsedMs {get;private set;}
        public float SpeakerElapsedMs {get;private set;}
        public float MaxScheduleSliceMs {get;private set;}
        public int DroppedAudioBlocks {get;private set;}
        SpeechEvidence evidence;
        float evidenceTime=-100;
        public SpeechEvidence Evidence {get=>Time.unscaledTime-evidenceTime<=.5f?evidence:default;private set{evidence=value;evidenceTime=Time.unscaledTime;}}
        readonly Queue<Packet> packets=new Queue<Packet>();
        readonly List<VoiceFrame> history=new List<VoiceFrame>();
        readonly List<float> utterance=new List<float>();
        readonly AudioResampler resampler=new AudioResampler();
        readonly AnonymousSpeakerTurns turns=new AnonymousSpeakerTurns();
        readonly float[] block=new float[512],context=new float[64],vadInputData=new float[576];
        int blockCount,lastRead,generation;
        bool wanted,optIn,permissionRequested,failed,voice,resetState,embeddingBusy,paused;
        float suppressedUntil,lastVoice=-100,lastEmbedded=-100,utteranceStart;
        AudioClip capture;
        float[] captureData;
        Worker vad,speaker;
        Tensor<float> vadInput,vadState;
        public void Initialize(bool consent){optIn=consent;StartCoroutine(Guard(VadLoop()));}
        public void SetConsent(bool consent){optIn=consent;if(!consent)StopCapture();}
        public void SetSensing(bool value)
        {wanted=value;if(!value)StopCapture();}
        public void SuppressOwnAudio(float until){suppressedUntil=until;packets.Clear();utterance.Clear();resetState=true;Evidence=default;}
        IEnumerator Guard(IEnumerator routine)
        {
            while(true)
            {
                bool more;
                try{more=routine.MoveNext();}
                catch(Exception error){failed=true;Status="local_speech_processing_failed";StopCapture();Debug.LogWarning("NOTIFLEX_SPEECH "+error.Message);more=false;}
                if(!more)yield break;yield return routine.Current;
            }
        }
        void Update()
        {
            if(!wanted||!optIn||paused||!OVRManager.hasVrFocus)
            {if(capture!=null)StopCapture();Status=!optIn?"microphone_opt_in_disabled":"inactive";return;}
            if(failed)return;
            if(!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Status="microphone_permission_required";
                if(!permissionRequested){permissionRequested=true;SensorPermissions.Request(Permission.Microphone);}
                return;
            }
            if(capture==null)
            {
                if(Microphone.devices.Length==0){Status="no_microphone";return;}
                capture=Microphone.Start(null,true,2,16000);
                if(capture==null){Status="microphone_start_failed";return;}
                captureData=new float[capture.samples*capture.channels];lastRead=0;resampler.Reset();resetState=true;
                Status="microphone_warming_up";
            }
            int position=Microphone.GetPosition(null);
            if(position<0||position==lastRead)return;
            int available=(position-lastRead+capture.samples)%capture.samples;
            if(available>capture.samples/2){lastRead=position;packets.Clear();resetState=true;Status="audio_backpressure_reset";return;}
            if(!capture.GetData(captureData,0))return;
            for(int i=0;i<available;i++)
            {
                int sample=(lastRead+i)%capture.samples;float mono=0;
                for(int c=0;c<capture.channels;c++)mono+=captureData[sample*capture.channels+c]/capture.channels;
                resampler.Push(mono,capture.frequency,PushSample);
            }
            lastRead=position;
        }
        void PushSample(float sample)
        {
            block[blockCount++]=sample;if(blockCount<512)return;blockCount=0;
            if(packets.Count>=4){DroppedAudioBlocks+=packets.Count;packets.Clear();resetState=true;utterance.Clear();Status="audio_backpressure_reset";}
            var copy=new float[512];Array.Copy(block,copy,512);packets.Enqueue(new Packet {samples=copy,time=Time.unscaledTime});
        }
        IEnumerator VadLoop()
        {
            while(true)
            {
                if(!wanted||!optIn||paused||capture==null||!OVRManager.hasVrFocus||packets.Count==0||failed){yield return null;continue;}
                if(vad==null)
                {
                    if(!LoadModels()){yield return null;continue;}
                }
                var packet=packets.Dequeue();
                if(Time.unscaledTime-packet.time>.3f||Time.unscaledTime<suppressedUntil)
                {DroppedAudioBlocks++;resetState=true;Array.Clear(packet.samples,0,packet.samples.Length);yield return null;continue;}
                if(resetState)
                {resetState=false;Array.Clear(context,0,context.Length);vadState.Upload(new float[256]);voice=false;history.Clear();utterance.Clear();}
                int runGeneration=generation;
                Array.Copy(context,vadInputData,64);Array.Copy(packet.samples,0,vadInputData,64,512);
                Array.Copy(packet.samples,448,context,0,64);
                vadInput.Upload(vadInputData);vad.SetInput("input",vadInput);vad.SetInput("state",vadState);
                var elapsed=System.Diagnostics.Stopwatch.StartNew();var schedule=vad.ScheduleIterable();var budget=System.Diagnostics.Stopwatch.StartNew();
                while(true)
                {
                    var slice=System.Diagnostics.Stopwatch.StartNew();bool more=schedule.MoveNext();
                    MaxScheduleSliceMs=Mathf.Max(MaxScheduleSliceMs,(float)slice.Elapsed.TotalMilliseconds);
                    if(!more)break;if(budget.Elapsed.TotalMilliseconds>=1){yield return null;budget.Restart();}
                }
                var output=vad.PeekOutput("output") as Tensor<float>;var nextState=vad.PeekOutput("stateN") as Tensor<float>;
                if(output==null||nextState==null){failed=true;Status="speech_model_output_invalid";yield break;}
                output.ReadbackRequest();nextState.ReadbackRequest();
                while(!output.IsReadbackRequestDone()||!nextState.IsReadbackRequestDone())yield return null;
                if(runGeneration!=generation||!wanted){resetState=true;yield return null;continue;}
                VadElapsedMs=(float)elapsed.Elapsed.TotalMilliseconds;float probability=output.DownloadToArray()[0];vadState.Upload(nextState.DownloadToArray());
                AcceptVoice(packet,probability);
                Array.Clear(packet.samples,0,packet.samples.Length);
            }
        }
        bool LoadModels()
        {
            try
            {
                var v=Resources.Load<ModelAsset>("Notiflex/VoiceActivity");var s=Resources.Load<ModelAsset>("Notiflex/SpeakerEmbedding");
                if(v==null||s==null){failed=true;Status="local_speech_models_missing";return false;}
                vad=new Worker(ModelLoader.Load(v),BackendType.CPU);speaker=new Worker(ModelLoader.Load(s),BackendType.CPU);
                vadInput=new Tensor<float>(new TensorShape(1,576));vadState=new Tensor<float>(new TensorShape(2,1,128));
                resetState=true;Status="local_voice_activity";SpeakerStatus="anonymous_speaker_model_ready";return true;
            }
            catch(Exception error){failed=true;Status="speech_model_load_failed";Debug.LogWarning("NOTIFLEX_SPEECH "+error.Message);return false;}
        }
        void AcceptVoice(Packet packet,float probability)
        {
            if(!PlacementPolicy.Finite(probability)){resetState=true;return;}
            probability=Mathf.Clamp01(probability);
            if(probability>=.65f){if(!voice)utteranceStart=packet.time;voice=true;lastVoice=packet.time;}
            else if(probability<.35f&&packet.time-lastVoice>.24f)voice=false;
            history.Add(new VoiceFrame {time=packet.time,voice=voice});history.RemoveAll(x=>packet.time-x.time>5);
            int voiced=0;foreach(var x in history)if(x.voice)voiced++;
            if(voice)
            {
                utterance.AddRange(packet.samples);
                if(utterance.Count>SpeakerFeatures.Samples)utterance.RemoveRange(0,utterance.Count-SpeakerFeatures.Samples);
                if(utterance.Count==SpeakerFeatures.Samples&&packet.time-lastEmbedded>=2&&!embeddingBusy)
                {lastEmbedded=packet.time;StartCoroutine(Guard(Embed(utterance.ToArray(),packet.time)));}
            }
            else utterance.Clear();
            turns.Update(packet.time);
            Evidence=new SpeechEvidence {available=true,speaking=voice,probability=probability,recentSpeechRatio=history.Count==0?0:(float)voiced/history.Count,
                lastSpeechTime=lastVoice,anonymousSpeakers=turns.Speakers,turnSwitches=turns.Switches};
            Status="local_voice_activity";
        }
        IEnumerator Embed(float[] waveform,float capturedAt)
        {
            embeddingBusy=true;int runGeneration=generation;
            Task<float[]> task=Task.Run(()=>SpeakerFeatures.Extract(waveform));
            while(!task.IsCompleted)yield return null;
            Array.Clear(waveform,0,waveform.Length);
            if(task.IsFaulted||runGeneration!=generation||!wanted){embeddingBusy=false;SpeakerStatus="embedding_cancelled";yield break;}
            using(var input=new Tensor<float>(new TensorShape(1,SpeakerFeatures.Frames,SpeakerFeatures.Bins),task.Result))
            {
                speaker.SetInput("feats",input);var elapsed=System.Diagnostics.Stopwatch.StartNew();var schedule=speaker.ScheduleIterable();var budget=System.Diagnostics.Stopwatch.StartNew();
                while(true)
                {
                    var slice=System.Diagnostics.Stopwatch.StartNew();bool more=schedule.MoveNext();
                    MaxScheduleSliceMs=Mathf.Max(MaxScheduleSliceMs,(float)slice.Elapsed.TotalMilliseconds);
                    if(!more)break;if(budget.Elapsed.TotalMilliseconds>=1){yield return null;budget.Restart();}
                }
                var output=speaker.PeekOutput() as Tensor<float>;
                if(output!=null)
                {
                    output.ReadbackRequest();while(!output.IsReadbackRequestDone())yield return null;
                    if(runGeneration==generation&&wanted&&Time.unscaledTime-capturedAt<3)
                    {
                        SpeakerElapsedMs=(float)elapsed.Elapsed.TotalMilliseconds;int id=turns.Observe(output.DownloadToArray(),2.015f,capturedAt);
                        SpeakerStatus=id<0?"speaker_unattributed":"anonymous_turn_evidence";
                    }
                }
            }
            embeddingBusy=false;
        }
        void StopCapture()
        {
            generation++;if(capture!=null){Microphone.End(null);Destroy(capture);capture=null;}
            if(captureData!=null)Array.Clear(captureData,0,captureData.Length);
            packets.Clear();utterance.Clear();history.Clear();turns.Clear();resampler.Reset();blockCount=0;
            Evidence=default;voice=false;resetState=true;lastVoice=-100;lastEmbedded=-100;
        }
        void OnApplicationPause(bool value){paused=value;if(value)StopCapture();}
        void OnDestroy(){StopCapture();vad?.Dispose();speaker?.Dispose();vadInput?.Dispose();vadState?.Dispose();}
    }
}
