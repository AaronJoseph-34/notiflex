using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Notiflex.VR
{
    // Explicit opt-in test harness; never activates during an ordinary launch.
    public sealed class RuntimeAudit : MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Application.platform==RuntimePlatform.Android || Array.IndexOf(Environment.GetCommandLineArgs(),"--notiflex-audit")<0)return;
            if(FindFirstObjectByType<RuntimeAudit>()==null)new GameObject("Opt-in runtime audit").AddComponent<RuntimeAudit>();
        }
        void Awake(){DontDestroyOnLoad(gameObject);}
        void Verify(bool condition,string label)
        {
            results.Add((condition?"PASS ":"FAIL ")+label);
            File.WriteAllLines(Path.Combine(output,"runtime-audit.txt"),results);
            if(!condition){Debug.LogError("NOTIFLEX_RUNTIME_AUDIT_FAILED: "+label);Application.Quit(2);throw new Exception(label);}
        }
        IEnumerator Start()
        {
            output=Environment.GetEnvironmentVariable("NOTIFLEX_AUDIT_OUTPUT");
            if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"audit");Directory.CreateDirectory(output);
            yield return null;
            foreach(string room in new[]{"Desk","Kitchen","LivingRoom"})
            {
                yield return SceneManager.LoadSceneAsync(room);
                yield return null;
                var runtime=FindFirstObjectByType<NotiflexRuntime>();var env=FindFirstObjectByType<EnvironmentBindings>();
                Verify(runtime!=null&&env!=null,room+" runtime and scene bindings exist");
                Verify(FindObjectsByType<NotiflexRuntime>(FindObjectsSortMode.None).Length==1,room+" has exactly one persistent runtime");
                Verify(!runtime.NotificationActive&&!runtime.NotificationVisible,room+" starts with no notification");
                foreach(Activity activity in new[]{Activity.Focus,Activity.Physical,Activity.Conversation})
                {
                    runtime.suppliedActivity=activity;runtime.priority=Priority.Normal;
                    runtime.Head.transform.position=new Vector3(0,1.65f,-1.6f);
                    var target=activity==Activity.Conversation?env.partner:env.task;
                    runtime.Head.transform.rotation=Quaternion.LookRotation(target.position-runtime.Head.transform.position,Vector3.up);
                    yield return new WaitForSecondsRealtime(1.6f);
                    Verify(runtime.InferredActivity==activity,room+" infers "+activity+" after dwell");
                    runtime.Send();yield return new WaitForSecondsRealtime(.2f);
                    var expected=activity==Activity.Physical?Channel.SpatialAudio:activity==Activity.Conversation?Channel.Peripheral:Channel.Visual;
                    Verify(runtime.NotificationActive&&runtime.DeliveryChannel==expected,room+" "+activity+" chooses "+expected);
                    Verify(runtime.NotificationVisible==(expected!=Channel.SpatialAudio),room+" "+activity+" visibility matches delivery");
                    Verify(runtime.SelectedPlacement!=null&&runtime.SelectedPlacement.eligible,room+" "+activity+" has a valid scored placement");
                    Capture(runtime.Head,room+"-"+activity+"-delivery.png");
                    runtime.Read();yield return null;
                    Verify(runtime.NotificationVisible&&runtime.DeliveryChannel==Channel.Visual,room+" "+activity+" read expands to visual");
                    Capture(runtime.Head,room+"-"+activity+"-read.png");
                    runtime.Dismiss();yield return null;
                    Verify(!runtime.NotificationActive&&!runtime.NotificationVisible,room+" "+activity+" dismissal clears notification");
                }
            }
            var expiring=FindFirstObjectByType<NotiflexRuntime>();expiring.Send();expiring.Send();
            Verify(expiring.NotificationActive,"Replacement keeps one active notification");
            yield return new WaitForSecondsRealtime(16.3f);
            Verify(!expiring.NotificationActive&&!expiring.NotificationVisible,"Notification expires after 16 seconds");
            expiring.Send();Verify(expiring.NotificationActive,"Notification is active before scene switch");
            yield return SceneManager.LoadSceneAsync("Passthrough");yield return null;
            var final=FindFirstObjectByType<NotiflexRuntime>();
            Verify(!final.NotificationActive&&!final.NotificationVisible,"Passthrough scene switch clears prior notification");
            Verify(FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length==0,"Passthrough contains no simulated room renderers");
            results.Add("COMPLETE desktop player lifecycle audit; headset tracking and compositor require physical hardware");
            File.WriteAllLines(Path.Combine(output,"runtime-audit.txt"),results);Debug.Log("NOTIFLEX_RUNTIME_AUDIT_PASS");Application.Quit(0);
        }
        void Capture(Camera camera,string filename)
        {
            var target=new RenderTexture(1600,1000,24);target.Create();var previous=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,filename),image.EncodeToPNG());camera.targetTexture=previous;RenderTexture.active=active;
            target.Release();Destroy(target);Destroy(image);
        }
    }
}
