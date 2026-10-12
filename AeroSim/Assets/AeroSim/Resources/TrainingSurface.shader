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
        _CavityStrength ("Contact / Cavity", Range(0,1)) = 0.25
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
        half _CavityStrength;
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

        inline half2 WeaveGradient(float2 uv, half scale)
        {
            half2 p = uv * scale;
            half warp = sin((p.x + p.y) * 3.14159h);
            half weft = sin((p.x - p.y) * 3.14159h);
            return half2(warp + weft, warp - weft);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 texel = tex2D(_MainTex, IN.uv_MainTex).rgb;
            half wave = StableWave(IN.uv_MainTex, _DetailScale);
            half isMetal = step(0.5h, _MaterialType) * (1.0h - step(1.5h, _MaterialType));
            half isCarbon = step(1.5h, _MaterialType) * (1.0h - step(2.5h, _MaterialType));
            half isGlass = step(2.5h, _MaterialType) * (1.0h - step(3.5h, _MaterialType));
            half isRubber = step(3.5h, _MaterialType);
            half isPlastic = 1.0h - saturate(isMetal + isCarbon + isGlass + isRubber);

            // One shared shader separates molded grain, brushed alloy, clear-coated weave and optics.
            half plasticVariation = wave * _MicroDetail * 0.018h;
            half brushedVariation = sin(IN.uv_MainTex.y * _DetailScale * 6.28318h) * _MicroDetail * 0.045h;
            half carbonLuma = dot(texel, fixed3(0.299h, 0.587h, 0.114h));
            half carbonVariation = (carbonLuma - 0.42h) * _MicroDetail * 0.22h;
            half rubberVariation = -abs(wave) * _MicroDetail * 0.012h;
            half surfaceVariation = plasticVariation * isPlastic + brushedVariation * isMetal
                + carbonVariation * isCarbon + rubberVariation * isRubber;

            // Procedural tangent-space micro-normal: no extra sampler/material slot and fades by material type.
            half2 weave = WeaveGradient(IN.uv_MainTex, _DetailScale);
            half2 grain = half2(wave, StableWave(IN.uv_MainTex.yx + 0.173h, _DetailScale * 0.73h));
            half2 brushed = half2(0, cos(IN.uv_MainTex.y * _DetailScale * 6.28318h));
            half2 normalXY = grain * (0.018h * isPlastic + 0.026h * isRubber)
                + brushed * 0.022h * isMetal + weave * 0.038h * isCarbon;
            normalXY *= _MicroDetail * (1.0h - isGlass);
            o.Normal = normalize(half3(normalXY.x, normalXY.y, 1.0h));

            half cavitySource = saturate(abs(wave) * 0.45h + (1.0h - carbonLuma) * isCarbon * 0.35h);
            half cavity = lerp(1.0h, 1.0h - cavitySource, _CavityStrength * (1.0h - isGlass));
            o.Albedo = max(0, texel * _Color.rgb * lerp(0.94h, 1.0h, cavity) + surfaceVariation);
            o.Metallic = saturate(_Metallic * (1.0h - isRubber));
            o.Smoothness = saturate(_Smoothness + brushedVariation * isMetal
                + carbonVariation * 0.5h * isCarbon + wave * 0.015h * isPlastic
                - abs(wave) * 0.08h * isRubber);
            o.Occlusion = cavity;

            // Schlick-shaped grazing response stays material-relative and avoids a painted outline.
            half ndv = saturate(dot(normalize(IN.viewDir), o.Normal));
            half schlick = pow(1.0h - ndv, 5.0h);
            half clearCoat = isCarbon * 0.45h + isGlass * 0.85h;
            o.Emission = _EmissionColor.rgb + _FresnelColor.rgb * schlick
                * _FresnelStrength * (1.0h + clearCoat);
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Standard"
}
