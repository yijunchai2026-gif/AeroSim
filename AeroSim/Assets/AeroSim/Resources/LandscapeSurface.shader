Shader "AeroSim/LandscapeSurface"
{
    Properties {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Seamless Albedo", 2D) = "white" {}
        _RockTex ("Slope Albedo", 2D) = "white" {}
        _Scale ("Meters per Tile", Float) = 6
        _Macro ("Macro Variation", Range(0,1)) = 0.2
        _Terrain ("Slope Blend", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.15
        _Wetness ("Weather wetness", Range(0,1)) = 0
        _DetailDistance ("Detail distance", Float) = 0
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma shader_feature_local AEROSIM_LANDSCAPE_DETAIL
        #pragma shader_feature_local AEROSIM_LANDSCAPE_ULTRA
        sampler2D _MainTex, _RockTex;
        fixed4 _Color;
        float _Scale, _Macro, _Terrain, _Smoothness, _Wetness, _DetailDistance;
        struct Input { float3 worldPos; float3 worldNormal; };
        float3 Triplanar(sampler2D source, float3 p, float3 weights)
        {
            return tex2D(source,p.zy).rgb*weights.x+tex2D(source,p.xz).rgb*weights.y+tex2D(source,p.xy).rgb*weights.z;
        }
        void surf(Input IN, inout SurfaceOutputStandard o) {
            float3 weights=pow(abs(IN.worldNormal),4);
            weights/=max(weights.x+weights.y+weights.z,0.001);
            float3 p=IN.worldPos/max(_Scale,0.01);
            float3 base=Triplanar(_MainTex,p,weights);
            float slope=saturate((1-abs(IN.worldNormal.y))*3)*_Terrain;
            float3 stone=Triplanar(_RockTex,p*.72,weights);
            float broad=sin(IN.worldPos.x*.018+sin(IN.worldPos.z*.013)*2.1)*sin(IN.worldPos.z*.021-IN.worldPos.x*.006);
            float patch=sin(IN.worldPos.x*.071+sin(IN.worldPos.z*.051)*2)*sin(IN.worldPos.z*.063);
            float3 albedo=lerp(base*_Color.rgb,stone*.72,slope)*(1+broad*_Macro+patch*_Macro*.22);
            #if defined(AEROSIM_LANDSCAPE_DETAIL)
                float fade=saturate(1-distance(_WorldSpaceCameraPos,IN.worldPos)/max(_DetailDistance,1));
                float3 detail=Triplanar(_MainTex,IN.worldPos/max(_Scale*.18,.08),weights);
                albedo*=lerp(1,lerp(.78,1.18,dot(detail,float3(.299,.587,.114))),fade*.24);
                #if defined(AEROSIM_LANDSCAPE_ULTRA)
                    float3 micro=Triplanar(_RockTex,IN.worldPos/max(_Scale*.075,.04),weights);
                    albedo*=lerp(1,lerp(.88,1.10,dot(micro,float3(.299,.587,.114))),fade*.16);
                #endif
            #endif
            o.Albedo=albedo*lerp(1,.82,_Wetness*.34);
            o.Smoothness=saturate(_Smoothness+_Wetness*.25*(1-slope*.45));
            o.Occlusion=lerp(.96,.90,slope*.25); o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
