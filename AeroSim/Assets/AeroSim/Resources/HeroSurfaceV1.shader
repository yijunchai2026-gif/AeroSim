Shader "AeroSim/HeroSurfaceV1"
{
    Properties
    {
        _Color("Semantic tint", Color) = (.5,.5,.5,1)
        _BaseMap("P01-P06 candidate source", 2D) = "white" {}
        _TextureStrength("Candidate source strength", Range(0,1)) = 0
        _Semantic("Soil 0 / rock 1 / concrete 2 / metal 3 / vegetation 4 / marking 5", Range(0,5)) = 0
        _Scale("Meters per feature", Float) = 2
        _Smoothness("Dry smoothness", Range(0,1)) = .15
        _Wetness("Environment wetness", Range(0,1)) = 0
        _Puddle("Environment puddle amount", Range(0,1)) = 0
        _CloudCover("Cloud shadow amount", Range(0,1)) = .25
        _Quality("Procedural detail budget", Range(0,1)) = .68
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma shader_feature_local AEROSIM_HERO_HIGH
        #pragma shader_feature_local AEROSIM_HERO_ULTRA
        sampler2D _BaseMap;
        fixed4 _Color;
        float _Semantic, _Scale, _Smoothness, _Wetness, _Puddle, _CloudCover, _Quality, _TextureStrength;
        struct Input { float3 worldPos; float3 worldNormal; };
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
        float noise(float2 p)
        {
            float2 c=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(hash(c),hash(c+float2(1,0)),f.x),lerp(hash(c+float2(0,1)),hash(c+1),f.x),f.y);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 weights=pow(abs(IN.worldNormal),4); weights/=max(weights.x+weights.y+weights.z,.001);
            float3 p=IN.worldPos/max(_Scale,.05);
            float n=noise(p.xz*2.3)+noise(p.zy*2.3)*weights.x+noise(p.xy*2.3)*weights.z;
            float macro=noise(p.xz*.23+17.4);
            float detail=0;
            #if defined(AEROSIM_HERO_HIGH)
                detail+=noise(p.xz*8.7)*.10;
            #endif
            #if defined(AEROSIM_HERO_ULTRA)
                detail+=noise(p.xz*19.1+5.2)*.045;
            #endif
            float metal=1-saturate(abs(_Semantic-3));
            float vegetation=1-saturate(abs(_Semantic-4));
            float marking=1-saturate(abs(_Semantic-5));
            float slope=saturate(1-IN.worldNormal.y);
            float crevice=smoothstep(.62,.92,n*.55+macro*.45);
            float wetMask=_Wetness*saturate(.45+macro*.55)+_Puddle*smoothstep(.78,.95,IN.worldNormal.y)*smoothstep(.55,.88,macro);
            float3 albedo=_Color.rgb*(.83+n*.13+detail*_Quality);
            float2 candidateUv=IN.worldPos.xz/max(_Scale,.05)*.18;
            float3 candidate=tex2D(_BaseMap,candidateUv).rgb;
            float candidateLuma=max(candidate.r,max(candidate.g,candidate.b));
            candidate=lerp(candidate, lerp(_Color.rgb, candidate, smoothstep(.025,.14,candidateLuma)), vegetation);
            albedo=lerp(albedo, candidate*_Color.rgb*1.35, _TextureStrength);
            float cloudShadow=smoothstep(.48,.72,noise(IN.worldPos.xz*.006+float2(_Time.y*.006,-_Time.y*.003)))*_CloudCover;
            albedo*=lerp(1,.72,cloudShadow*.58);
            albedo*=lerp(1,.68,wetMask*.55);
            albedo=lerp(albedo,albedo*float3(.68,.31,.16),metal*crevice*.42);
            albedo*=lerp(1,.82,slope*crevice*(1-marking));
            o.Albedo=albedo;
            o.Metallic=metal*.62;
            o.Smoothness=saturate(_Smoothness+wetMask*.43-metal*crevice*.2+vegetation*.02);
            o.Occlusion=lerp(.98,.78,crevice*(1-marking));
            o.Emission=_Color.rgb*marking*.035*(.45+_CloudCover);
            o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
