Shader "AeroSim/RotorBlur"
{
    Properties {_Color("Tint",Color)=(.12,.13,.14,.13)}
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
            fixed4 frag(v2f i):SV_Target {float r=length((i.uv-.5)*2);return fixed4(_Color.rgb,_Color.a*smoothstep(1,.6,r)*smoothstep(.06,.4,r));}
            ENDCG
        }
    }
}
