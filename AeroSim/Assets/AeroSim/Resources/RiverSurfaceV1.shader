Shader "AeroSim/RiverSurfaceV1"
{
    Properties
    {
        _Color("Lake water",Color)=(.04,.23,.34,1)
        _SkyColor("Reflected sky",Color)=(.45,.62,.70,1)
        _SunDirection("Sun direction",Vector)=(0,.5,-.5,0)
        _Rain("Rain",Range(0,1))=0
        _Wind("Wind",Range(0,1))=.2
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature_local AEROSIM_WATER_HIGH
            #pragma shader_feature_local AEROSIM_WATER_ULTRA
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Color,_SkyColor,_SunDirection;float _Rain,_Wind;
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;UNITY_FOG_COORDS(1)};
            v2f vert(appdata_base v)
            {
                v2f o;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.world.y+=sin(o.world.x*.17+o.world.z*.13+_Time.y*(.55+_Wind*.5))*.022;
                o.pos=mul(UNITY_MATRIX_VP,float4(o.world,1)); UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=i.world.xz;float t=_Time.y*(.7+_Wind*.45);
                float dx=cos(p.x*.83+p.y*.41+t*1.2)*.12;float dz=sin(p.x*.31+p.y*.92+t)*.12;
                #if defined(AEROSIM_WATER_HIGH)
                    dx+=cos(p.x*2.73-p.y*1.65+t*1.7)*.06;dz+=sin(p.x*1.46+p.y*2.57-t*1.3)*.05;
                #endif
                #if defined(AEROSIM_WATER_ULTRA)
                    dx+=cos(p.x*5.13+p.y*3.27-t*2.1)*.025;dz+=sin(p.x*4.67-p.y*5.31+t*1.9)*.022;
                #endif
                float rain=(hash(floor(p*5)+floor(t*9))-.5)*_Rain*.09;
                float3 n=normalize(float3(dx+rain,1,dz+rain));float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float fresnel=pow(1-saturate(dot(n,view)),4);
                float spec=pow(saturate(dot(reflect(-normalize(_SunDirection.xyz),n),view)),lerp(145,72,_Rain));
                float3 color=lerp(_Color.rgb,_SkyColor.rgb,fresnel*(.62+_Rain*.12));
                color+=spec*float3(.90,.85,.70)*.8*(1-_Rain*.55);
                float ripple=sin(p.x*3.12+p.y*1.73+t)*sin(p.x*.58-p.y*2.6-t*.7);color*=.94+ripple*.045;
                fixed4 result=float4(color,1); UNITY_APPLY_FOG(i.fogCoord,result);return result;
            }
            ENDCG
        }
    }
    Fallback Off
}
