using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;
namespace Notiflex.VR
{
    // Android permits one permission request flow at a time. Serialize sensors
    // so camera/scene/microphone prompts cannot overwrite each other's callback.
    public sealed class SensorPermissions : MonoBehaviour
    {
        sealed class RequestItem {public string permission;public Action<bool> complete;}
        static SensorPermissions instance;
        readonly Queue<RequestItem> queue=new Queue<RequestItem>();
        RequestItem active;
        PermissionCallbacks callbacks;
        public static void Request(string permission,Action<bool> complete=null)
        {
            if(Permission.HasUserAuthorizedPermission(permission)){complete?.Invoke(true);return;}
            if(instance==null){instance=new GameObject("Notiflex permission sequence").AddComponent<SensorPermissions>();DontDestroyOnLoad(instance.gameObject);}
            instance.queue.Enqueue(new RequestItem {permission=permission,complete=complete});
        }
        void Update()
        {
            if(active!=null)
            {if(Permission.HasUserAuthorizedPermission(active.permission))Finish(active,true);return;}
            if(queue.Count==0||!OVRManager.hasVrFocus)return;
            active=queue.Dequeue();
            if(Permission.HasUserAuthorizedPermission(active.permission)){Finish(active,true);return;}
            var item=active;callbacks=new PermissionCallbacks();callbacks.PermissionGranted+=_=>Finish(item,true);
            callbacks.PermissionDenied+=_=>Finish(item,false);callbacks.PermissionDeniedAndDontAskAgain+=_=>Finish(item,false);
            Permission.RequestUserPermission(active.permission,callbacks);
        }
        void Finish(RequestItem item,bool granted){if(active!=item)return;active=null;item?.complete?.Invoke(granted);}
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
