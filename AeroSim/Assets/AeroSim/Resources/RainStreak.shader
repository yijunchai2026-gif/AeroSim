Shader "AeroSim/RainStreak"
{
    Properties
    {
        _Color("Rain color",Color)=(.72,.82,.88,1)
        _Opacity("Opacity",Range(0,1))=.3
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color; float _Opacity;
            struct appdata {float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target {float edge=saturate(1-abs(i.uv.x*2-1));float along=smoothstep(0,.12,i.uv.y)*(1-smoothstep(.72,1,i.uv.y));return fixed4(_Color.rgb,i.color.a*_Opacity*edge*along);}
            ENDCG
        }
    }
    Fallback Off
}
