Shader "AeroSim/OceanSurface"
{
    Properties {
        _Color("Deep Water",Color)=(.025,.15,.21,1)
        _Shallow("Shallow Water",Color)=(.07,.36,.38,1)
        _SunDirection("Sun Direction",Vector)=(0,.5,-.5,0)
        _Evening("Evening",Range(0,1))=0
    }
    SubShader {
        Tags {"RenderType"="Opaque"}
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Color,_Shallow,_SunDirection;
            float _Evening;
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;UNITY_FOG_COORDS(1)};
            v2f vert(appdata_base v){v2f o;float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;w.y+=sin(w.x*.018+w.z*.024+_Time.y*.5)*.08;o.world=w;o.pos=mul(UNITY_MATRIX_VP,float4(w,1));UNITY_TRANSFER_FOG(o,o.pos);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            float height(float2 p,float t){return noise(p*.43+float2(t*.14,t*.07))*.14+noise(p*1.23-float2(t*.11,t*.09))*.04;}
            fixed4 frag(v2f i):SV_Target {
                float2 p=i.world.xz;float t=_Time.y;
                float h=height(p,t);
                float3 n=normalize(float3(h-height(p+float2(.18,0),t),.20,h-height(p+float2(0,.18),t)));
                float3 v=normalize(_WorldSpaceCameraPos-i.world);
                float fresnel=pow(1-saturate(dot(v,n)),4);
                float shore=saturate((i.world.x+145)/65);
                float3 c=lerp(_Color.rgb,_Shallow.rgb,shore);
                c=lerp(c,float3(.43,.62,.71),fresnel*.36);
                float glint=pow(saturate(dot(reflect(-normalize(_SunDirection.xyz),n),v)),220);
                c+=lerp(float3(1,.97,.81),float3(1,.5,.22),_Evening)*glint*.9;
                float foam=smoothstep(.55,.98,sin(p.y*.2+t*.8)+sin(p.x*.23+t*.5)*.25)*smoothstep(-87,-80,i.world.x);
                c=lerp(c,float3(.76,.86,.84),foam*.7);
                fixed4 result=float4(c,1);UNITY_APPLY_FOG(i.fogCoord,result);return result;
            }
            ENDCG
        }
    }
    Fallback Off
}
