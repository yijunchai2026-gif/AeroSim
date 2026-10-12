Shader "AeroSim/CoastalWaterDetail"
{
    Properties
    {
        _DeepColor("Deep", Color) = (.025,.12,.16,1)
        _ShallowColor("Shallow", Color) = (.10,.39,.38,1)
        _FoamColor("Foam", Color) = (.78,.87,.84,1)
        _WetRockColor("Wet rock", Color) = (.055,.075,.07,1)
        _DetailAtlas("P02/P07 candidate atlas", 2D) = "white" {}
        _Mode("Tide pool 0 / wet band 1 / breaker 2", Range(0,2)) = 0
        _Wetness("Wetness", Range(0,1)) = 0
        _Wind("Wind", Range(0,1)) = .25
        _Quality("Quality", Range(0,1)) = .68
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        #pragma shader_feature_local AEROSIM_COAST_HIGH
        #pragma shader_feature_local AEROSIM_COAST_ULTRA
        sampler2D _DetailAtlas;
        fixed4 _DeepColor, _ShallowColor, _FoamColor, _WetRockColor;
        float _Mode, _Wetness, _Wind, _Quality;
        struct Input { float3 worldPos; float3 worldNormal; };
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
        float noise(float2 p)
        {
            float2 c=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(hash(c),hash(c+float2(1,0)),f.x),lerp(hash(c+float2(0,1)),hash(c+1),f.x),f.y);
        }
        void vert(inout appdata_full v)
        {
            float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;
            float tidePool=1-saturate(abs(_Mode));
            v.vertex.y += tidePool*(sin(w.x*.63+_Time.y*(.25+_Wind))+sin(w.z*.77-_Time.y*.37))*.012*_Quality;
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 p=IN.worldPos.xz;
            float macro=noise(p*.11+13.2), ripples=noise(p*1.7+_Time.y*float2(.10,-.07));
            #if defined(AEROSIM_COAST_HIGH)
                ripples=lerp(ripples,noise(p*3.9-_Time.y*.13),.32);
            #endif
            #if defined(AEROSIM_COAST_ULTRA)
                ripples=lerp(ripples,noise(p*8.1+_Time.y*.19),.18);
            #endif
            float tidePool=1-saturate(abs(_Mode));
            float wetBand=1-saturate(abs(_Mode-1));
            float breaker=1-saturate(abs(_Mode-2));
            float foamLine=smoothstep(.60,.78,sin(p.x*.71+p.y*.17+_Time.y*(.65+_Wind))* .22 + ripples*.78);
            float3 atlas=tex2D(_DetailAtlas,frac(p*.035+_Time.y*float2(.003,-.002))).rgb;
            float atlasMask=max(atlas.r,max(atlas.g,atlas.b));
            foamLine=max(foamLine,smoothstep(.18,.62,atlasMask)*breaker);
            float3 water=lerp(_DeepColor.rgb,_ShallowColor.rgb,saturate(.35+macro*.65));
            float3 color=water*tidePool + _WetRockColor.rgb*(.66+macro*.2)*wetBand + lerp(_ShallowColor.rgb,_FoamColor.rgb,foamLine)*breaker;
            color=lerp(color,_FoamColor.rgb,foamLine*breaker*(.68+_Wind*.2));
            o.Albedo=color;
            o.Metallic=0;
            o.Smoothness=saturate(tidePool*.86+wetBand*(.58+_Wetness*.3)+breaker*.5-ripples*.08);
            o.Occlusion=lerp(.96,.86,wetBand*(1-macro));
            o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
