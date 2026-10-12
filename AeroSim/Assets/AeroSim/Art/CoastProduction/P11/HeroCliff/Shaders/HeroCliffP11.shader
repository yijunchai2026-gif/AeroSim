Shader "AeroSim/Coast/HeroCliffP11"
{
 Properties
 {
  // Non-tiling high-to-low bakes: sampled exactly once through the LOD UV0 atlas.
  _BaseColor("Unique UV Base Color",2D)="white"{} _NormalMap("Unique UV Normal",2D)="bump"{}
  _RoughnessMap("Unique UV Roughness",2D)="white"{} _AOMap("Unique UV AO",2D)="white"{}
  _HeightMap("Unique UV Height",2D)="gray"{} _CurvatureMap("Unique UV Curvature",2D)="gray"{}
  _WetCavityMask("Unique UV Wet/Cavity/Breakup",2D)="black"{}
  _DetailBaseColor("Scanned Cliff Detail Base Color",2D)="gray"{}
  _DetailNormal("Scanned Cliff Detail Normal",2D)="bump"{}
  _DetailRoughness("Scanned Cliff Detail Roughness",2D)="white"{}
  _DetailHeight("Scanned Cliff Detail Height",2D)="gray"{}
  _DetailMetersPerTile("Scanned Detail Scale (m)",Range(.5,6))=1.8
  _DetailStrength("Scanned Detail Strength",Range(0,1))=.72
  _Tint("Tint",Color)=(1,1,1,1) _MacroTint("Macro Recess Tint",Color)=(0.72,0.78,0.75,1)
  _MacroMetersPerTile("Macro Feature Scale (m)",Range(4,40))=14
  _MicroMetersPerTile("Micro Feature Scale (m)",Range(.04,2))=.09
  _MacroStrength("Macro Strength",Range(0,.5))=.22 _MicroStrength("Micro Strength",Range(0,1))=.18
  _MicroNormalStrength("Micro Normal Strength",Range(0,1.5))=.15
  _NormalStrength("Baked Normal Strength",Range(0,2))=1 _HeightStrength("Height Influence",Range(0,0.15))=.06
  _Wetness("Environment Wetness",Range(0,1))=0
  _WaterLevel("Environment Water Level",Range(-10,10))=1 _Precipitation("Environment Precipitation",Range(0,1))=0
  _WaterlineWidth("Waterline Width",Range(.1,8))=2.2 _CavityWetness("Cavity Wetness",Range(0,1))=.55
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "Queue"="Geometry" } LOD 400
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.5
  #pragma multi_compile_local __ HERO_CLIFF_MICRO
  sampler2D _BaseColor, _NormalMap, _RoughnessMap, _AOMap, _HeightMap, _CurvatureMap, _WetCavityMask;
  sampler2D _DetailBaseColor, _DetailNormal, _DetailRoughness, _DetailHeight;
  float4 _BaseColor_TexelSize;
  fixed4 _Tint, _MacroTint;
  float _MacroMetersPerTile, _MicroMetersPerTile, _MacroStrength, _MicroStrength, _MicroNormalStrength, _NormalStrength, _HeightStrength;
  float _DetailMetersPerTile, _DetailStrength;
  float _Wetness, _WaterLevel, _Precipitation, _WaterlineWidth, _CavityWetness;

  struct Input
  {
   float2 uv_BaseColor;
   float3 worldPos;
  };

  float2 UniqueAtlasUV(float2 uv)
  {
   // The bake has a 16 px gutter. Keeping filtered samples inside it prevents Repeat wrap
   // and the black atlas exterior from leaking into distant mips at the mesh boundary.
   float2 gutter = _BaseColor_TexelSize.xy * 17.0;
   return clamp(uv, gutter, 1.0 - gutter);
  }

  float RockNoise(float3 p)
  {
   // Texture-free 3D breakup: no UV stretch, projection seam, or obvious triplanar tile.
   float a = sin(dot(p, float3(1.173, 1.927, 2.417)) + 0.31);
   float b = sin(dot(p, float3(-2.113, 1.337, 0.719)) + a * 0.83 + 1.79);
   float c = sin(dot(p, float3(0.617, -2.731, 1.541)) + b * 0.67 + 4.13);
   return saturate(0.5 + a * 0.23 + b * 0.17 + c * 0.10);
  }

  float DetailVisibility(float3 p)
  {
   // Fade only frequencies smaller than a pixel; never collapse the unique bake.
   float footprint = max(length(ddx(p)), length(ddy(p)));
   return 1.0 - smoothstep(0.22, 0.8, footprint);
  }

  void surf(Input IN, inout SurfaceOutputStandard o)
  {
   // Surface identity always comes from the unique, non-overlapping UV0 bake atlas.
   float2 uv = UniqueAtlasUV(IN.uv_BaseColor);
   fixed4 baseSample = tex2D(_BaseColor,uv);
   fixed4 mask = tex2D(_WetCavityMask, uv);
   float rough = tex2D(_RoughnessMap, uv).r;
   float ao = tex2D(_AOMap, uv).r;
   float height = tex2D(_HeightMap, uv).r;
   float curvature = tex2D(_CurvatureMap, uv).r;
   float3 bakedNormal = UnpackNormal(tex2D(_NormalMap,uv));
   float2 detailUvA = float2(IN.worldPos.z, IN.worldPos.y) / max(_DetailMetersPerTile, 0.01);
   float2 detailUvB = detailUvA * 0.577 + float2(0.37, 0.61);
   float2 detailUvTop = float2(IN.worldPos.x, IN.worldPos.z) / max(_DetailMetersPerTile, 0.01);
   float2 detailUvSide = float2(IN.worldPos.x, IN.worldPos.y) / max(_DetailMetersPerTile, 0.01);
   float3 projectionNormal = normalize(cross(ddy(IN.worldPos), ddx(IN.worldPos)));
   float3 projectionWeights = pow(abs(projectionNormal), 4.0);
   projectionWeights /= max(dot(projectionWeights, 1.0), 0.001);
   float detailBlend = RockNoise(IN.worldPos / 5.7 + 9.13);
   float3 detailFrontColor = lerp(tex2D(_DetailBaseColor, detailUvA).rgb, tex2D(_DetailBaseColor, detailUvB).rgb, detailBlend);
   float3 detailColor = detailFrontColor * projectionWeights.x + tex2D(_DetailBaseColor, detailUvTop).rgb * projectionWeights.y + tex2D(_DetailBaseColor, detailUvSide).rgb * projectionWeights.z;
   float3 detailFrontNormal = normalize(lerp(UnpackNormal(tex2D(_DetailNormal, detailUvA)), UnpackNormal(tex2D(_DetailNormal, detailUvB)), detailBlend));
   float3 detailNormal = normalize(detailFrontNormal * projectionWeights.x + UnpackNormal(tex2D(_DetailNormal, detailUvTop)) * projectionWeights.y + UnpackNormal(tex2D(_DetailNormal, detailUvSide)) * projectionWeights.z);
   float detailFrontRoughness = lerp(tex2D(_DetailRoughness, detailUvA).r, tex2D(_DetailRoughness, detailUvB).r, detailBlend);
   float detailRoughness = detailFrontRoughness * projectionWeights.x + tex2D(_DetailRoughness, detailUvTop).r * projectionWeights.y + tex2D(_DetailRoughness, detailUvSide).r * projectionWeights.z;
   float detailFrontHeight = lerp(tex2D(_DetailHeight, detailUvA).r, tex2D(_DetailHeight, detailUvB).r, detailBlend);
   float detailHeight = detailFrontHeight * projectionWeights.x + tex2D(_DetailHeight, detailUvTop).r * projectionWeights.y + tex2D(_DetailHeight, detailUvSide).r * projectionWeights.z;

   // Supplemental world-space 3D frequencies add scale cues without replacing or
   // periodically re-projecting any unique bake. Micro is derivative-faded at range.
   float3 macroPosition = IN.worldPos / max(_MacroMetersPerTile, 0.01);
   float macroSigned = (RockNoise(macroPosition) - 0.5) * 2.0;
   float microSigned = 0.0;
   float2 microNormal = 0.0;
   float microVisibility = 0.0;
   #if defined(HERO_CLIFF_MICRO)
    float3 microPosition = IN.worldPos / max(_MicroMetersPerTile, 0.01);
    microVisibility = DetailVisibility(microPosition);
    microSigned = (RockNoise(microPosition + 17.31) - 0.5) * 2.0;
    microNormal.x = (RockNoise(microPosition + float3(3.17, 11.73, 5.29)) - 0.5) * 2.0;
    microNormal.y = (RockNoise(microPosition + float3(13.41, 2.61, 19.07)) - 0.5) * 2.0;
   #endif

   float recessTint = saturate(-macroSigned) * _MacroStrength;
   float macroValue = 1.0 + macroSigned * _MacroStrength * 0.42;
   float microValue = 1.0 + microSigned * _MicroStrength * microVisibility * 0.30;
   float heightValue = 1.0 + (height - 0.5) * _HeightStrength * 2.0;
   float curvatureValue = 1.0 + (curvature - 0.5) * 0.08;
   float3 albedo = baseSample.rgb * _Tint.rgb;
   albedo *= lerp(float3(1.0, 1.0, 1.0), _MacroTint.rgb, recessTint);
   albedo *= macroValue * microValue * heightValue * curvatureValue;
   float detailCavity = saturate((detailHeight - 0.35) * 1.55);
   float3 scannedRock = detailColor * lerp(0.78, 1.14, detailCavity);
   albedo = lerp(albedo, albedo * scannedRock * 2.05, _DetailStrength);

   float waterline = saturate(1.0 - abs(IN.worldPos.y - _WaterLevel) / max(_WaterlineWidth, 0.01));
   float wet = saturate(_Wetness * (0.35 + 0.65 * mask.r) + _Precipitation * 0.25 + waterline * 0.72 + mask.g * _CavityWetness);
   o.Albedo = albedo * lerp(1.0, 0.62, wet);

   // Normal is kept in tangent space. The former world/triplanar normal was being
   // written into this tangent-space output and produced dark projection seams.
   bakedNormal.xy *= _NormalStrength;
   bakedNormal.xy += detailNormal.xy * (_DetailStrength * 0.72);
   bakedNormal.xy += microNormal * (_MicroNormalStrength * microVisibility);
   float normalLength = max(1.0, length(bakedNormal.xy));
   bakedNormal.xy /= normalLength;
   bakedNormal.z = sqrt(saturate(1.0 - dot(bakedNormal.xy, bakedNormal.xy)));
   o.Normal = normalize(bakedNormal);
   rough = lerp(rough, detailRoughness, _DetailStrength * 0.72);
   rough = saturate(rough - macroSigned * _MacroStrength * 0.08 - microSigned * _MicroStrength * microVisibility * 0.05);
   o.Metallic = 0.0;
   o.Smoothness = saturate(1.0 - rough + wet * 0.48 + waterline * 0.12);
   o.Occlusion = saturate(lerp(1.0, ao, 0.86) * lerp(0.94, 1.02, curvature));
   o.Alpha = 1.0;
  }
  ENDCG
 }
 FallBack "Standard"
}
