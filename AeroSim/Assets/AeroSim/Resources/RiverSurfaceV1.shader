Shader "AeroSim/RiverSurfaceV1"
{
    Properties
    {
        _Color("Lake water",Color)=(.04,.23,.34,1)
        _SunDirection("Sun direction",Vector)=(0,.5,-.5,0)
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
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Color,_SunDirection;
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;UNITY_FOG_COORDS(1)};
            v2f vert(appdata_base v)
            {
                v2f o;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                // Render-only centimetre ripples; no hidden replacement of the collision floor.
                o.world.y+=sin(o.world.x*.17+o.world.z*.13+_Time.y*.8)*.022;
                o.pos=mul(UNITY_MATRIX_VP,float4(o.world,1)); UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=i.world.xz;
                float t=_Time.y;
                float dx=cos(p.x*.83+p.y*.41+t*1.2)*.12+cos(p.x*2.73-p.y*1.65+t*1.7)*.06;
                float dz=sin(p.x*.31+p.y*.92+t)*.12+sin(p.x*1.46+p.y*2.57-t*1.3)*.05;
                float3 n=normalize(float3(dx,1,dz));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float fresnel=pow(1-saturate(dot(n,view)),4);
                float spec=pow(saturate(dot(reflect(-normalize(_SunDirection.xyz),n),view)),145);
                float3 color=lerp(_Color.rgb,float3(.45,.62,.70),fresnel*.66);
                color+=spec*float3(.90,.85,.70)*.8;
                float ripple=sin(p.x*3.12+p.y*1.73+t)*sin(p.x*.58-p.y*2.6-t*.7);
                color*=.94+ripple*.055;
                fixed4 result=float4(color,1); UNITY_APPLY_FOG(i.fogCoord,result);return result;
            }
            ENDCG
        }
    }
    Fallback Off
}
