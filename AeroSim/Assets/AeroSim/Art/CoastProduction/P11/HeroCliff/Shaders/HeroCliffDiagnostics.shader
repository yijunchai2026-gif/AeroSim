Shader "Hidden/AeroSim/HeroCliffDiagnostics"
{
 Properties
 {
  [HideInInspector] _BaseColor("Atlas Metrics",2D)="white"{}
  _NormalMap("Normal",2D)="bump"{}
  _RoughnessMap("Roughness",2D)="white"{}
  _AOMap("AO",2D)="white"{}
  _WetCavityMask("Wet/Cavity/Fracture",2D)="black"{}
  _DetailNormal("Scanned Detail Normal",2D)="bump"{}
  _DetailRoughness("Scanned Detail Roughness",2D)="white"{}
  [HideInInspector] _DetailMetersPerTile("Scanned Detail Scale",Float)=1.8
  [HideInInspector] _DetailStrength("Scanned Detail Strength",Float)=.72
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
   sampler2D _NormalMap, _RoughnessMap, _AOMap, _WetCavityMask, _DetailNormal, _DetailRoughness;
   float4 _BaseColor_TexelSize;
   float _HeroCliffDiagnosticMode;
   float _MacroMetersPerTile, _MicroMetersPerTile, _MacroStrength, _MicroStrength, _MicroNormalStrength, _NormalStrength;
   float _DetailMetersPerTile, _DetailStrength;
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
   float Hash31(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }
   float ValueNoise3D(float3 p)
   {
    float3 cell=floor(p); float3 local=frac(p); local=local*local*(3.0-2.0*local);
    float x00=lerp(Hash31(cell),Hash31(cell+float3(1,0,0)),local.x);
    float x10=lerp(Hash31(cell+float3(0,1,0)),Hash31(cell+float3(1,1,0)),local.x);
    float x01=lerp(Hash31(cell+float3(0,0,1)),Hash31(cell+float3(1,0,1)),local.x);
    float x11=lerp(Hash31(cell+float3(0,1,1)),Hash31(cell+float3(1,1,1)),local.x);
    return lerp(lerp(x00,x10,local.y),lerp(x01,x11,local.y),local.z);
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
    float microSigned=(ValueNoise3D(microPosition+17.31)-.5)*2.0;
    float2 microNormal;
    microNormal.x=(ValueNoise3D(microPosition+float3(3.17,11.73,5.29))-.5)*2.0;
    microNormal.y=(ValueNoise3D(microPosition+float3(13.41,2.61,19.07))-.5)*2.0;
    float3 tangentNormal=UnpackNormal(tex2D(_NormalMap,uv));
    float2 detailUvA=float2(input.worldPos.z,input.worldPos.y)/max(_DetailMetersPerTile,.01);
    float2 detailUvB=detailUvA*.577+float2(.37,.61);
    float2 detailUvTop=float2(input.worldPos.x,input.worldPos.z)/max(_DetailMetersPerTile,.01);
    float2 detailUvSide=float2(input.worldPos.x,input.worldPos.y)/max(_DetailMetersPerTile,.01);
    float3 projectionWeights=pow(abs(normalize(input.worldNormal)),4.0);
    projectionWeights/=max(dot(projectionWeights,1.0),.001);
    float detailBlend=ValueNoise3D(input.worldPos/5.7+9.13);
    float3 detailFrontNormal=normalize(lerp(UnpackNormal(tex2D(_DetailNormal,detailUvA)),UnpackNormal(tex2D(_DetailNormal,detailUvB)),detailBlend));
    float3 detailNormal=normalize(detailFrontNormal*projectionWeights.x+UnpackNormal(tex2D(_DetailNormal,detailUvTop))*projectionWeights.y+UnpackNormal(tex2D(_DetailNormal,detailUvSide))*projectionWeights.z);
    float detailFrontRoughness=lerp(tex2D(_DetailRoughness,detailUvA).r,tex2D(_DetailRoughness,detailUvB).r,detailBlend);
    float detailRoughness=detailFrontRoughness*projectionWeights.x+tex2D(_DetailRoughness,detailUvTop).r*projectionWeights.y+tex2D(_DetailRoughness,detailUvSide).r*projectionWeights.z;
    float cameraDistance=distance(input.worldPos,_WorldSpaceCameraPos);
    float effectiveDetailStrength=0.0;
    tangentNormal.xy=tangentNormal.xy*_NormalStrength+detailNormal.xy*(effectiveDetailStrength*.72)+microNormal*(_MicroNormalStrength*microVisibility);
    tangentNormal.xy/=max(1.0,length(tangentNormal.xy));
    tangentNormal.z=sqrt(saturate(1.0-dot(tangentNormal.xy,tangentNormal.xy)));
    float3 normal=normalize(input.worldTangent*tangentNormal.x+input.worldBinormal*tangentNormal.y+input.worldNormal*tangentNormal.z);
    float mode=_HeroCliffDiagnosticMode;
    if(mode<1.5) { float depth=saturate(input.eyeDepth/100.0); return float4(depth,depth,depth,1); }
    if(mode<2.5) return float4(normal*0.5+0.5,1);
    if(mode<3.5)
    {
     float macroSigned=(ValueNoise3D(input.worldPos/max(_MacroMetersPerTile,.01))-.5)*2.0;
     float roughMacro=ValueNoise3D(input.worldPos/7.5+float3(7.1,2.4,13.7));
     float roughSecondary=ValueNoise3D(input.worldPos/16.0+float3(1.8,17.3,5.9));
     float geologyRoughness=lerp(.28,.84,saturate(roughMacro*.72+roughSecondary*.28));
     float roughness=lerp(geologyRoughness,detailRoughness,effectiveDetailStrength*.16);
     roughness+=tex2D(_WetCavityMask,uv).g*.08-projectionWeights.y*.10;
     roughness=saturate(roughness-macroSigned*_MacroStrength*.05-microSigned*_MicroStrength*microVisibility*.04);
     return float4(roughness,roughness,roughness,1);
    }
    if(mode<4.5)
    {
     float ao=saturate(tex2D(_AOMap,uv).r);
     ao=0.58+0.42*smoothstep(0.05,0.95,ao);
     return float4(ao,ao,ao,1);
    }
    if(mode<5.5) { float wetness=tex2D(_WetCavityMask,uv).r; return float4(wetness,wetness,wetness,1); }
    if(mode<6.5) return float4(_MaterialId.rgb,1);
    return float4(_LodColor.rgb,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
