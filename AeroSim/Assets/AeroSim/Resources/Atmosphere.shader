Shader "AeroSim/Atmosphere"
{
    Properties {
        _Zenith("Zenith",Color)=(.14,.36,.58,1)
        _Horizon("Horizon",Color)=(.72,.8,.83,1)
        _SunDirection("Sun Direction",Vector)=(0,.5,-.5,0)
        _CloudCover("Cloud Cover",Range(0,1))=.3
        _SunTint("Sun Tint",Color)=(1,.95,.8,1)
    }
    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct v2f {float4 pos:SV_POSITION; float3 dir:TEXCOORD0;};
            float4 _Zenith,_Horizon,_SunDirection,_SunTint;
            float _CloudCover;
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            fixed4 frag(v2f i):SV_Target {
                float3 d=normalize(i.dir);
                float3 sky=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),.46));
                float sun=saturate(dot(d,normalize(_SunDirection.xyz)));
                sky+=_SunTint.rgb*(pow(sun,900)*2+pow(sun,20)*.09);
                float2 p=d.xz/max(d.y+.19,.13)*2.1+float2(8,5);
                float clouds=noise(p)*.55+noise(p*2.03)*.25+noise(p*4.13)*.13+noise(p*8.7)*.07;
                float mask=smoothstep(.68-_CloudCover*.34,.83-_CloudCover*.27,clouds)*smoothstep(.01,.18,d.y);
                sky=lerp(sky,lerp(_Horizon.rgb,float3(.92,.93,.94),saturate(clouds)),mask*.82);
                return float4(sky,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
