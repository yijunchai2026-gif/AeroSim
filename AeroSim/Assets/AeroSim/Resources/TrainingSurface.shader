Shader "AeroSim/TrainingSurface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
        _MaterialType ("Material Type", Range(0,4)) = 0
        _MicroDetail ("Micro Detail", Range(0,1)) = 0.15
        _DetailScale ("Detail Scale", Range(1,256)) = 64
        _FresnelColor ("Fresnel Color", Color) = (0.22,0.28,0.32,1)
        _FresnelStrength ("Fresnel Strength", Range(0,1)) = 0.08
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma multi_compile_instancing

        fixed4 _Color;
        sampler2D _MainTex;
        half _Metallic;
        half _Smoothness;
        half _MaterialType;
        half _MicroDetail;
        half _DetailScale;
        fixed4 _FresnelColor;
        half _FresnelStrength;
        fixed4 _EmissionColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        inline half StableWave(float2 uv, half scale)
        {
            half a = sin((uv.x + uv.y * 0.37h) * scale * 6.28318h);
            half b = sin((uv.y - uv.x * 0.21h) * scale * 3.14159h);
            return a * b;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 texel = tex2D(_MainTex, IN.uv_MainTex).rgb;
            half wave = StableWave(IN.uv_MainTex, _DetailScale);
            half isMetal = step(0.5h, _MaterialType) * (1.0h - step(1.5h, _MaterialType));
            half isCarbon = step(1.5h, _MaterialType) * (1.0h - step(2.5h, _MaterialType));
            half isGlass = step(2.5h, _MaterialType) * (1.0h - step(3.5h, _MaterialType));

            // Tiny UV-stable variation separates molded grain, directional anodizing and clear-coated weave.
            half plasticVariation = wave * _MicroDetail * 0.018h;
            half brushedVariation = sin(IN.uv_MainTex.y * _DetailScale * 6.28318h) * _MicroDetail * 0.045h;
            half carbonLuma = dot(texel, fixed3(0.299h, 0.587h, 0.114h));
            half surfaceVariation = lerp(plasticVariation, brushedVariation, isMetal);

            o.Albedo = max(0, texel * _Color.rgb + surfaceVariation);
            o.Metallic = _Metallic;
            o.Smoothness = saturate(_Smoothness + brushedVariation * isMetal
                + (carbonLuma - 0.28h) * 0.16h * isCarbon + wave * 0.015h * (1.0h - isGlass));
            o.Normal = half3(0, 0, 1);

            // Schlick-shaped grazing response: subtle material reflection, never a fixed outline.
            half ndv = saturate(dot(normalize(IN.viewDir), o.Normal));
            half schlick = pow(1.0h - ndv, 5.0h);
            half clearCoat = isCarbon * 0.45h + isGlass * 0.85h;
            o.Emission = _EmissionColor.rgb + _FresnelColor.rgb * schlick
                * _FresnelStrength * (1.0h + clearCoat);
            o.Occlusion = 1;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Standard"
}
