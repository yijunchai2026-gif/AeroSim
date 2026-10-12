Shader "Hidden/AeroSim/HeroCliffDiagnostics"
{
 Properties
 {
  [HideInInspector] _BaseColor("Atlas Metrics",2D)="white"{}
  _NormalMap("Normal",2D)="bump"{}
  _RoughnessMap("Roughness",2D)="white"{}
  _AOMap("AO",2D)="white"{}
  _WetCavityMask("Wet/Cavity/Fracture",2D)="black"{}
  [HideInInspector] _MacroMetersPerTile("Macro Scale",Float)=14
  [HideInInspector] _MicroMetersPerTile("Micro Scale",Float)=.09
  [HideInInspector] _MacroStrength("Macro Strength",Float)=.22
  [HideInInspector] _MicroStrength("Micro Strength",Float)=.72
  [HideInInspector] _MicroNormalStrength("Micro Normal Strength",Float)=.58
  [HideInInspector] _NormalStrength("Baked Normal Strength",Float)=1
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
   sampler2D _NormalMap, _RoughnessMap, _AOMap, _WetCavityMask;
   float4 _BaseColor_TexelSize;
   float _HeroCliffDiagnosticMode;
   float _MacroMetersPerTile, _MicroMetersPerTile, _MacroStrength, _MicroStrength, _MicroNormalStrength, _NormalStrength;
   float4 _LodColor, _MaterialId;
   struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float4 tangent:TANGENT; float2 uv:TEXCOORD0; };
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 worldNormal:TEXCOORD1; float eyeDepth:TEXCOORD2; float3 worldPos:TEXCOORD3; float3 worldTangent:TEXCOORD4; float3 worldBinormal:TEXCOORD5; };
   v2f vert(appdata input)
   {
    v2f output;
    output.pos=UnityObjectToClipPos(input.vertex);
    output.uv=input.uv;
    output.worldNormal=UnityObjectToWorldNormal(input.normal);
    output.worldTangent=UnityObjectToWorldDir(input.tangent.xyz);
    output.worldBinormal=cross(output.worldNormal,output.worldTangent)*input.tangent.w*unity_WorldTransformParams.w;
    output.worldPos=mul(unity_ObjectToWorld,input.vertex).xyz;
    output.eyeDepth=-UnityObjectToViewPos(input.vertex).z;
    return output;
   }
   float2 UniqueAtlasUV(float2 uv)
   {
    float2 gutter=_BaseColor_TexelSize.xy*17.0;
    return clamp(uv,gutter,1.0-gutter);
   }
   float RockNoise(float3 p)
   {
    float a=sin(dot(p,float3(1.173,1.927,2.417))+.31);
    float b=sin(dot(p,float3(-2.113,1.337,.719))+a*.83+1.79);
    float c=sin(dot(p,float3(.617,-2.731,1.541))+b*.67+4.13);
    return saturate(.5+a*.23+b*.17+c*.10);
   }
   float DetailVisibility(float3 p)
   {
    float footprint=max(length(ddx(p)),length(ddy(p)));
    return 1.0-smoothstep(.22,.8,footprint);
   }
   fixed4 frag(v2f input):SV_Target
   {
    float2 uv=UniqueAtlasUV(input.uv);
    float3 microPosition=input.worldPos/max(_MicroMetersPerTile,.01);
    float microVisibility=DetailVisibility(microPosition);
    float microSigned=(RockNoise(microPosition+17.31)-.5)*2.0;
    float2 microNormal;
    microNormal.x=(RockNoise(microPosition+float3(3.17,11.73,5.29))-.5)*2.0;
    microNormal.y=(RockNoise(microPosition+float3(13.41,2.61,19.07))-.5)*2.0;
    float3 tangentNormal=UnpackNormal(tex2D(_NormalMap,uv));
    tangentNormal.xy=tangentNormal.xy*_NormalStrength+microNormal*(_MicroNormalStrength*microVisibility);
    tangentNormal.xy/=max(1.0,length(tangentNormal.xy));
    tangentNormal.z=sqrt(saturate(1.0-dot(tangentNormal.xy,tangentNormal.xy)));
    float3 normal=normalize(input.worldTangent*tangentNormal.x+input.worldBinormal*tangentNormal.y+input.worldNormal*tangentNormal.z);
    float mode=_HeroCliffDiagnosticMode;
    if(mode<1.5) { float depth=saturate(input.eyeDepth/100.0); return float4(depth,depth,depth,1); }
    if(mode<2.5) return float4(normal*0.5+0.5,1);
    if(mode<3.5)
    {
     float macroSigned=(RockNoise(input.worldPos/max(_MacroMetersPerTile,.01))-.5)*2.0;
     float roughness=tex2D(_RoughnessMap,uv).r;
     roughness=saturate(roughness-macroSigned*_MacroStrength*.12-microSigned*_MicroStrength*microVisibility*.30);
     return float4(roughness,roughness,roughness,1);
    }
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
