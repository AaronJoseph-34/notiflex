using UnityEngine;
using UnityEngine.UI;
namespace Notiflex.VR
{
    public sealed class NotificationView : MonoBehaviour
    {
        Canvas canvas;
        Text title,body;
        Image background;
        public void Initialize()
        {
            canvas=gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace;
            var rect=GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(620,290);transform.localScale=Vector3.one*.001f;
            background=gameObject.AddComponent<Image>();background.color=new Color(.08f,.14f,.18f,.94f);
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            {
                Vector2 q=new Vector2(Mathf.Max(0,Mathf.Abs(x-31.5f)-23),Mathf.Max(0,Mathf.Abs(y-31.5f)-23));
                texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(9-q.magnitude)));
            }
            texture.Apply();background.sprite=Sprite.Create(texture,new Rect(0,0,64,64),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));background.type=Image.Type.Sliced;
            title=Label("Title",new Vector2(28,-24),new Vector2(560,65),32,FontStyle.Bold);
            body=Label("Body",new Vector2(28,-105),new Vector2(560,155),25,FontStyle.Normal);
            gameObject.SetActive(false);
        }
        Text Label(string name,Vector2 position,Vector2 size,int fontSize,FontStyle style)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(transform,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;
            var t=go.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=fontSize;t.fontStyle=style;t.color=Color.white;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
        public void Present(Vector3 position,Quaternion rotation,Channel channel,Priority priority,string message,float scale=1)
        {
            gameObject.SetActive(channel==Channel.Visual || channel==Channel.Peripheral);
            transform.SetPositionAndRotation(position,rotation);
            transform.localScale=Vector3.one*(.001f*scale);
            bool cue=channel==Channel.Peripheral;
            GetComponent<RectTransform>().sizeDelta=cue?new Vector2(330,130):new Vector2(620,290);
            title.rectTransform.anchoredPosition=cue?new Vector2(18,-12):new Vector2(28,-24);
            title.rectTransform.sizeDelta=cue?new Vector2(294,32):new Vector2(560,65);
            body.rectTransform.anchoredPosition=cue?new Vector2(18,-52):new Vector2(28,-105);
            body.rectTransform.sizeDelta=cue?new Vector2(294,68):new Vector2(560,155);
            title.fontSize=cue?28:42;
            title.text=priority==Priority.Urgent?"Priority update":"Notiflex";
            body.text=cue?(Application.platform==RuntimePlatform.Android?"Update available\nTrigger to read":"Update available\nE to read"):message;
            background.color=priority==Priority.Urgent?new Color(.24f,.13f,.06f,.96f):new Color(.08f,.14f,.18f,.94f);
            // A cue occupies the upper portion of the same scored full-card envelope.
            body.fontSize=cue?22:32;
        }
        public void Hide() { gameObject.SetActive(false); }
    }
}
