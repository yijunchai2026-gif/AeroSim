Shader "Hidden/AeroSim/HeroCliffDiagnostics"
{
 Properties
 {
  [HideInInspector] _BaseColor("Atlas Metrics",2D)="white"{}
  _RoughnessMap("Roughness",2D)="white"{}
  _AOMap("AO",2D)="white"{}
  _WetCavityMask("Wet/Cavity/Fracture",2D)="black"{}
  _LodColor("LOD Color",Color)=(1,0,1,1)
  _MaterialId("Material ID",Color)=(1,0,1,1)
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" }
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   sampler2D _RoughnessMap, _AOMap, _WetCavityMask;
   float4 _BaseColor_TexelSize;
   float _HeroCliffDiagnosticMode;
   float4 _LodColor, _MaterialId;
   struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 worldNormal:TEXCOORD1; float eyeDepth:TEXCOORD2; };
   v2f vert(appdata input)
   {
    v2f output;
    output.pos=UnityObjectToClipPos(input.vertex);
    output.uv=input.uv;
    output.worldNormal=UnityObjectToWorldNormal(input.normal);
    output.eyeDepth=-UnityObjectToViewPos(input.vertex).z;
    return output;
   }
   float2 UniqueAtlasUV(float2 uv)
   {
    float2 gutter=_BaseColor_TexelSize.xy*17.0;
    return clamp(uv,gutter,1.0-gutter);
   }
   fixed4 frag(v2f input):SV_Target
   {
    float3 normal=normalize(input.worldNormal);
    float2 uv=UniqueAtlasUV(input.uv);
    float mode=_HeroCliffDiagnosticMode;
    if(mode<1.5) { float depth=saturate(input.eyeDepth/100.0); return float4(depth,depth,depth,1); }
    if(mode<2.5) return float4(normal*0.5+0.5,1);
    if(mode<3.5) { float roughness=tex2D(_RoughnessMap,uv).r; return float4(roughness,roughness,roughness,1); }
    if(mode<4.5) { float ao=tex2D(_AOMap,uv).r; return float4(ao,ao,ao,1); }
    if(mode<5.5) { float wetness=tex2D(_WetCavityMask,uv).r; return float4(wetness,wetness,wetness,1); }
    if(mode<6.5) return float4(_MaterialId.rgb,1);
    return float4(_LodColor.rgb,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
