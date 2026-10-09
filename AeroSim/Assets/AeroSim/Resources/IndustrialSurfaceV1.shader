Shader "AeroSim/IndustrialSurfaceV1"
{
    Properties
    {
        _Color("Albedo tint",Color)=(.7,.7,.7,1)
        _MainTex("Tileable original albedo",2D)="white"{}
        _UseTexture("Use supplied albedo",Range(0,1))=0
        _Scale("Meters per tile",Float)=3
        _Kind("Concrete 0 / steel 1",Float)=0
        _Metallic("Metallic",Range(0,1))=0
        _Smoothness("Smoothness",Range(0,1))=.18
        _Emission("Luminous trim",Range(0,4))=0
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma multi_compile_instancing
        sampler2D _MainTex;
        fixed4 _Color;
        float _UseTexture,_Scale,_Kind,_Metallic,_Smoothness,_Emission;
        struct Input {float3 worldPos; float3 worldNormal;};
        float hash21(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float noise21(float2 p)
        {
            float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float3 weights=pow(abs(IN.worldNormal),4);
            weights/=max(weights.x+weights.y+weights.z,.001);
            float3 p=IN.worldPos/max(_Scale,.05);
            float3 tex=tex2D(_MainTex,p.zy).rgb*weights.x+tex2D(_MainTex,p.xz).rgb*weights.y+tex2D(_MainTex,p.xy).rgb*weights.z;
            float2 planar=IN.worldPos.xy*weights.z+IN.worldPos.zy*weights.x+IN.worldPos.xz*weights.y;
            float grain=noise21(planar*19), weather=noise21(planar*.34+7.1), detail=noise21(planar*3.6);
            float3 base=_Color.rgb*lerp(float3(1,1,1),tex,_UseTexture);
            // Small concrete pores and low-frequency rain streaks, not baked scene shadows.
            float pores=(1-smoothstep(.05,.17,grain))*.14;
            float streak=noise21(float2(planar.x*2.7,planar.y*.15));
            float variation=.90+grain*.16+detail*.07-pores-streak*.07;
            float rust=smoothstep(.48,.79,weather)*saturate(_Kind);
            o.Albedo=lerp(base*variation,float3(.27,.095,.038),rust*.36);
            o.Metallic=_Metallic*(1-rust*.65);
            o.Smoothness=saturate(_Smoothness+detail*.08-rust*.18);
            o.Occlusion=.94+grain*.06;
            o.Emission=_Color.rgb*_Emission;
            o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
