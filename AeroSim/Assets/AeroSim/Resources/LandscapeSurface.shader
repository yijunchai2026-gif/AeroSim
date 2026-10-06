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
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex, _RockTex;
        fixed4 _Color;
        float _Scale, _Macro, _Terrain, _Smoothness;
        struct Input { float3 worldPos; float3 worldNormal; };
        void surf(Input IN, inout SurfaceOutputStandard o) {
            float3 weights=pow(abs(IN.worldNormal),4);
            weights/=max(weights.x+weights.y+weights.z,0.001);
            float3 p=IN.worldPos/max(_Scale,0.01);
            float3 base=tex2D(_MainTex,p.zy).rgb*weights.x+tex2D(_MainTex,p.xz).rgb*weights.y+tex2D(_MainTex,p.xy).rgb*weights.z;
            float slope=saturate((1-abs(IN.worldNormal.y))*3)*_Terrain;
            float3 stone=tex2D(_RockTex,p.xz).rgb;
            float patch=sin(IN.worldPos.x*.037+sin(IN.worldPos.z*.051)*2)*sin(IN.worldPos.z*.023);
            o.Albedo=lerp(base*_Color.rgb,stone*.72,slope)*(1+patch*_Macro);
            o.Smoothness=_Smoothness; o.Occlusion=.96; o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
