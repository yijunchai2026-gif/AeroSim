Shader "AeroSim/Atmosphere"
{
    Properties {
        _Zenith("Zenith",Color)=(.14,.36,.58,1)
        _Horizon("Horizon",Color)=(.72,.8,.83,1)
        _SunDirection("Sun Direction",Vector)=(0,.5,-.5,0)
        _CloudCover("Cloud Cover",Range(0,1))=.3
        _SunTint("Sun Tint",Color)=(1,.95,.8,1)
        _Rain("Rain",Range(0,1))=0
        _CloudSpeed("Cloud Speed",Float)=.004
    }
    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local AEROSIM_SKY_HIGH
            #pragma shader_feature_local AEROSIM_SKY_ULTRA
            #include "UnityCG.cginc"
            struct v2f {float4 pos:SV_POSITION; float3 dir:TEXCOORD0;};
            float4 _Zenith,_Horizon,_SunDirection,_SunTint;
            float _CloudCover,_Rain,_CloudSpeed;
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(c),hash(c+float2(1,0)),f.x),lerp(hash(c+float2(0,1)),hash(c+1),f.x),f.y);}
            fixed4 frag(v2f i):SV_Target {
                float3 d=normalize(i.dir);
                float haze=pow(1-saturate(d.y),5);
                float3 sky=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),.46));
                float sun=saturate(dot(d,normalize(_SunDirection.xyz)));
                sky+=_SunTint.rgb*(pow(sun,900)*2*(1-_CloudCover)+pow(sun,20)*.09*(1-_CloudCover*.8));
                float2 drift=float2(_Time.y*_CloudSpeed,_Time.y*_CloudSpeed*.37);
                float2 p=d.xz/max(d.y+.19,.13)*2.1+float2(8,5)+drift;
                float clouds=noise(p)*.60+noise(p*2.03)*.28+noise(p*4.13)*.12;
                #if defined(AEROSIM_SKY_HIGH)
                    clouds=clouds*.92+noise(p*8.7)*.08;
                #endif
                #if defined(AEROSIM_SKY_ULTRA)
                    clouds=clouds*.96+noise(p*15.3-float2(_Time.y*_CloudSpeed*.5,0))*.04;
                #endif
                float mask=smoothstep(.68-_CloudCover*.34,.83-_CloudCover*.27,clouds)*smoothstep(.01,.18,d.y);
                float silver=pow(saturate(dot(normalize(float3(d.x,.22,d.z)),normalize(_SunDirection.xyz))),10)*(1-_Rain);
                float3 cloudColor=lerp(_Horizon.rgb,float3(.91,.92,.93),saturate(clouds+silver*.16));
                cloudColor=lerp(cloudColor,float3(.38,.42,.45),_Rain*(.4+.6*clouds));
                sky=lerp(sky,cloudColor,mask*.88);
                sky=lerp(sky,_Horizon.rgb,haze*.18);
                return float4(sky,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
