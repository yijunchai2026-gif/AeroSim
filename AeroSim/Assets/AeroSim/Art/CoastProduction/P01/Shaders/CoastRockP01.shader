Shader "AeroSim/Coast/CoastRockP01"
{
    Properties
    {
        _BaseMap ("Generated Rock Albedo", 2D) = "white" {}
        _Tint ("Rock Tint", Color) = (0.72,0.75,0.76,1)
        _MetersPerTile ("Meters Per Tile", Range(0.5,16)) = 4
        _StrataScale ("Strata Scale", Range(0.05,2)) = 0.34
        _StrataStrength ("Strata Strength", Range(0,1)) = 0.42
        _MacroStrength ("Macro Variation", Range(0,1)) = 0.18
        _Roughness ("Roughness", Range(0,1)) = 0.84
        _Wetness ("Wetness", Range(0,1)) = 0.08
        _EdgeLight ("Eroded Edge Lift", Range(0,1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 250
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _BaseMap;
        fixed4 _Tint;
        float _MetersPerTile, _StrataScale, _StrataStrength, _MacroStrength, _Roughness, _Wetness, _EdgeLight;
        struct Input { float3 worldPos; float3 worldNormal; };
        float3 SampleTri(float3 p, float3 n)
        {
            float3 w = pow(abs(n), 5.0); w /= max(dot(w, 1.0), 0.001);
            return tex2D(_BaseMap, p.zy).rgb*w.x + tex2D(_BaseMap, p.xz).rgb*w.y + tex2D(_BaseMap, p.xy).rgb*w.z;
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(IN.worldNormal);
            float3 p = IN.worldPos / max(_MetersPerTile, 0.01);
            float3 rock = SampleTri(p, n);
            float strata = sin(IN.worldPos.y / max(_StrataScale,0.01) * 6.28318 + rock.r*2.4);
            float broad = sin(IN.worldPos.x*.071 + IN.worldPos.z*.047) * sin(IN.worldPos.z*.031);
            float facing = saturate(1.0 - abs(n.y));
            float3 albedo = rock * _Tint.rgb;
            albedo *= 1.0 + broad * _MacroStrength;
            albedo *= lerp(1.0, lerp(.72,1.12,strata*.5+.5), facing*_StrataStrength);
            albedo += pow(saturate(1.0-abs(n.y)), 6.0) * _EdgeLight * .08;
            o.Albedo = albedo * lerp(1.0,.78,_Wetness);
            o.Metallic = 0;
            o.Smoothness = saturate(1.0-_Roughness + _Wetness*.28);
            o.Occlusion = .91;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
