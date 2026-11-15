Shader "Hidden/Notiflex/DetectorLetterbox"
{
    Properties { _MainTex ("Camera", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Content;
            float _EncodeSRGB;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv=(i.uv-_Content.xy)/_Content.zw;
                if(uv.x<0 || uv.y<0 || uv.x>1 || uv.y>1) return fixed4(0.447058824,0.447058824,0.447058824,1);
                fixed4 color=tex2D(_MainTex,uv);
                if(_EncodeSRGB>0.5) color.rgb=LinearToGammaSpace(color.rgb);
                return color;
            }
            ENDCG
        }
    }
}
