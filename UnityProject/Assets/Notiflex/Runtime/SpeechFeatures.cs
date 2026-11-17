using System;
using System.Collections.Generic;

namespace Notiflex.VR
{
    public struct SpeechEvidence
    {
        public bool available,speaking;
        public float probability,recentSpeechRatio,lastSpeechTime;
        public int anonymousSpeakers,turnSwitches;
    }

    public sealed class AudioResampler
    {
        double next;
        long frame;
        float previous;
        readonly double[] taps=new double[64];
        readonly float[] ring=new float[64];
        int rate,ringIndex;
        public void Reset(){next=0;frame=0;previous=0;rate=0;ringIndex=0;Array.Clear(ring,0,ring.Length);}
        public void Push(float value,int sourceRate,Action<float> output)
        {
            if(sourceRate<8000||sourceRate>192000||float.IsNaN(value)||float.IsInfinity(value))return;
            if(rate!=sourceRate)
            {
                Reset();rate=sourceRate;double sum=0,cutoff=.45*Math.Min(16000,sourceRate)/sourceRate;
                for(int i=0;i<64;i++){double x=i-31.5;taps[i]=(Math.Sin(2*Math.PI*cutoff*x)/(Math.PI*x))*(.5-.5*Math.Cos(2*Math.PI*i/63));sum+=taps[i];}
                for(int i=0;i<64;i++)taps[i]/=sum;
            }
            if(sourceRate!=16000)
            {
                ring[ringIndex]=value;double filtered=0;
                for(int i=0;i<64;i++)filtered+=taps[i]*ring[(ringIndex-i+64)%64];
                ringIndex=(ringIndex+1)%64;value=(float)filtered;
            }
            while(next<=frame)
            {
                double t=frame==0?1:Math.Max(0,Math.Min(1,next-(frame-1)));
                output((float)(previous+(value-previous)*t));next+=(double)sourceRate/16000;
            }
            previous=value;frame++;
        }
    }

    public static class SpeakerFeatures
    {
        public const int Samples=32240,Frames=200,Bins=80;
        // WeSpeaker's documented Kaldi-style fbank contract: 16 kHz PCM16 scale,
        // 25 ms frames, 10 ms shift, Hamming, DC removal, 0.97 pre-emphasis,
        // power FFT512, 80 mel bins, snip_edges and utterance mean subtraction.
        public static float[] Extract(float[] samples)
        {
            if(samples==null||samples.Length!=Samples)throw new ArgumentException("Expected 32240 mono 16 kHz samples");
            var result=new float[Frames*Bins];var re=new double[512];var im=new double[512];
            var window=new double[400];var weights=new double[Bins,257];
            for(int i=0;i<400;i++)window[i]=.54-.46*Math.Cos(2*Math.PI*i/399);
            double melLow=1127*Math.Log(1+20.0/700),melHigh=1127*Math.Log(1+8000.0/700);
            for(int b=0;b<Bins;b++)
            {
                double left=melLow+(melHigh-melLow)*b/(Bins+1),mid=melLow+(melHigh-melLow)*(b+1)/(Bins+1),right=melLow+(melHigh-melLow)*(b+2)/(Bins+1);
                for(int k=0;k<=256;k++)
                {double mel=1127*Math.Log(1+(k*16000.0/512)/700);weights[b,k]=Math.Max(0,Math.Min((mel-left)/(mid-left),(right-mel)/(right-mid)));}
            }
            for(int f=0;f<Frames;f++)
            {
                Array.Clear(re,0,re.Length);Array.Clear(im,0,im.Length);double mean=0;
                for(int i=0;i<400;i++)mean+=samples[f*160+i]*32768.0/400;
                for(int i=0;i<400;i++)
                {
                    double current=samples[f*160+i]*32768.0-mean;
                    double prior=samples[f*160+Math.Max(0,i-1)]*32768.0-mean;
                    re[i]=(current-.97*prior)*window[i];
                }
                Fft(re,im);
                for(int b=0;b<Bins;b++)
                {double energy=0;for(int k=0;k<=256;k++)energy+=weights[b,k]*(re[k]*re[k]+im[k]*im[k]);result[f*Bins+b]=(float)Math.Log(Math.Max(1.1920928955078125e-7,energy));}
            }
            for(int b=0;b<Bins;b++)
            {double mean=0;for(int f=0;f<Frames;f++)mean+=result[f*Bins+b]/Frames;for(int f=0;f<Frames;f++)result[f*Bins+b]-=(float)mean;}
            return result;
        }
        static void Fft(double[] real,double[] imag)
        {
            int n=real.Length;
            for(int i=1,j=0;i<n;i++)
            {int bit=n>>1;for(; (j&bit)!=0;bit>>=1)j^=bit;j^=bit;if(i<j){double t=real[i];real[i]=real[j];real[j]=t;}}
            for(int length=2;length<=n;length<<=1)
            {
                double angle=-2*Math.PI/length,wr0=Math.Cos(angle),wi0=Math.Sin(angle);
                for(int i=0;i<n;i+=length)
                {
                    double wr=1,wi=0;
                    for(int j=0;j<length/2;j++)
                    {
                        int a=i+j,b=a+length/2;double vr=real[b]*wr-imag[b]*wi,vi=real[b]*wi+imag[b]*wr;
                        real[b]=real[a]-vr;imag[b]=imag[a]-vi;real[a]+=vr;imag[a]+=vi;
                        double next=wr*wr0-wi*wi0;wi=wr*wi0+wi*wr0;wr=next;
                    }
                }
            }
        }
        public static float[] Normalize(float[] embedding)
        {
            double norm=0;foreach(float v in embedding){if(float.IsNaN(v)||float.IsInfinity(v))return null;norm+=(double)v*v;}
            if(norm<1e-10)return null;var result=new float[embedding.Length];
            for(int i=0;i<result.Length;i++)result[i]=(float)(embedding[i]/Math.Sqrt(norm));return result;
        }
        public static float Similarity(float[] a,float[] b)
        {if(a==null||b==null||a.Length!=b.Length)return -1;double dot=0;for(int i=0;i<a.Length;i++)dot+=(double)a[i]*b[i];return (float)dot;}
    }

    // Session-only anonymous acoustic clusters. No biometric enrollment, names,
    // audio recordings or embeddings are written to logs or persistent storage.
    public sealed class AnonymousSpeakerTurns
    {
        sealed class Cluster {public int id;public float[] mean;public float last;}
        struct Turn {public int speaker;public float time;}
        readonly List<Cluster> clusters=new List<Cluster>();
        readonly List<Turn> turns=new List<Turn>();
        int nextId=1;
        public int Speakers {get;private set;}
        public int Switches {get;private set;}
        public void Clear(){clusters.Clear();turns.Clear();Speakers=Switches=0;}
        public int Observe(float[] embedding,float duration,float now)
        {
            if(duration<1.5f||float.IsNaN(now)||float.IsInfinity(now))return -1;
            var vector=SpeakerFeatures.Normalize(embedding);if(vector==null)return -1;
            clusters.RemoveAll(c=>now-c.last>45);
            Cluster best=null;float score=-1,second=-1;
            foreach(var c in clusters)
            {float s=SpeakerFeatures.Similarity(c.mean,vector);if(s>score){second=score;score=s;best=c;}else second=Math.Max(second,s);}
            int id;
            if(best!=null&&score>=.72f&&score-second>=.08f)
            {
                var average=new float[vector.Length];for(int i=0;i<vector.Length;i++)average[i]=.85f*best.mean[i]+.15f*vector[i];
                best.mean=SpeakerFeatures.Normalize(average);best.last=now;id=best.id;
            }
            else if((best==null||score<.35f)&&clusters.Count<4)
            {var c=new Cluster {id=nextId++,mean=vector,last=now};clusters.Add(c);id=c.id;}
            else return -1; // Similar/short/mixed speech is explicitly unattributed.
            turns.Add(new Turn {speaker=id,time=now});Update(now);return id;
        }
        public void Update(float now)
        {
            turns.RemoveAll(t=>now-t.time>30||now<t.time);var ids=new HashSet<int>();Switches=0;
            for(int i=0;i<turns.Count;i++)
            {ids.Add(turns[i].speaker);if(i>0&&turns[i].speaker!=turns[i-1].speaker&&turns[i].time-turns[i-1].time<=10)Switches++;}
            Speakers=ids.Count;
        }
    }
}
