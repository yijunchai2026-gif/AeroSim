Shader "AeroSim/P07/FoamAtlas"
{
    Properties
    {
        _FoamAtlas ("Foam Atlas (RGB)", 2D) = "black" {}
        _AtlasRect ("Atlas Rect (Offset XY, Scale ZW)", Vector) = (0,0,1,1)
        [HDR] _Tint ("Foam Tint", Color) = (0.86,0.94,0.96,0.82)
        _Intensity ("Intensity", Range(0,2)) = 1
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.03
        _EdgeSoftness ("Edge Softness", Range(0.001,0.5)) = 0.12
        _FlowDirection ("Flow Direction XZ", Vector) = (1,0,0,0)
        _FlowSpeed ("Flow Speed", Range(-1,1)) = 0.02
        _Decay ("Decay", Range(0,1)) = 0
        _DepthFadeMeters ("Depth Fade Metres", Range(0.01,10)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local _WEATHER_CLEAR _WEATHER_OVERCAST _WEATHER_RAIN
            #pragma shader_feature_local _QUALITY_LOW _QUALITY_HIGH _QUALITY_ULTRA
            #include "UnityCG.cginc"

            sampler2D _FoamAtlas;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _FoamAtlas_ST;
            float4 _AtlasRect;
            float4 _Tint;
            float _Intensity;
            float _AlphaThreshold;
            float _EdgeSoftness;
            float4 _FlowDirection;
            float _FlowSpeed;
            float _Decay;
            float _DepthFadeMeters;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 localUv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                float4 color : COLOR;
            };

            float2 AtlasUv(float2 localUv)
            {
                return _AtlasRect.xy + frac(localUv) * _AtlasRect.zw;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float2 flow = normalize(_FlowDirection.xz + float2(1e-5, 0));
                o.localUv = TRANSFORM_TEX(v.uv, _FoamAtlas) + flow * (_Time.y * _FlowSpeed);
                o.screenPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.eyeDepth);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = AtlasUv(i.localUv);
                fixed3 atlas = tex2D(_FoamAtlas, uv).rgb;

                #if defined(_QUALITY_HIGH) || defined(_QUALITY_ULTRA)
                    fixed3 flowLayer = tex2D(_FoamAtlas, AtlasUv(i.localUv * 1.013 + float2(0.017, -0.011))).rgb;
                    atlas = max(atlas, flowLayer * 0.42);
                #endif
                #if defined(_QUALITY_ULTRA)
                    float distortion = (atlas.g - atlas.b) * 0.012;
                    fixed3 detailLayer = tex2D(_FoamAtlas, AtlasUv(i.localUv * 0.987 + distortion)).rgb;
                    atlas = max(atlas, detailLayer * 0.28);
                #endif

                float coverage = max(atlas.r, max(atlas.g, atlas.b));
                coverage = smoothstep(_AlphaThreshold, _AlphaThreshold + _EdgeSoftness, coverage);

                float3 weatherTint = float3(1.0, 1.0, 1.0);
                float weatherIntensity = 1.0;
                #if defined(_WEATHER_CLEAR)
                    weatherTint = float3(1.00, 1.03, 1.04);
                    weatherIntensity = 0.90;
                #elif defined(_WEATHER_OVERCAST)
                    weatherTint = float3(0.94, 0.98, 1.00);
                    weatherIntensity = 1.02;
                #elif defined(_WEATHER_RAIN)
                    weatherTint = float3(0.88, 0.94, 0.97);
                    weatherIntensity = 1.14;
                #endif

                float depthFade = 1.0;
                #if defined(_QUALITY_HIGH) || defined(_QUALITY_ULTRA)
                    float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                    depthFade = saturate((sceneDepth - i.eyeDepth) / max(_DepthFadeMeters, 0.01));
                #endif

                float alpha = saturate(coverage * _Tint.a * i.color.a * _Intensity * weatherIntensity * (1.0 - _Decay) * depthFade);
                fixed3 rgb = _Tint.rgb * weatherTint * atlas * i.color.rgb * _Intensity;
                return fixed4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
