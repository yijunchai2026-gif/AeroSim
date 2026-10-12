Shader "AeroSim/NeedleFoliage"
{
    Properties {
        _Color("Needle Tint",Color)=(.7,.8,.65,1)
        _MainTex("Needle Cluster RGBA",2D)="white"{}
        _Cutoff("Alpha Cutoff",Range(0,1))=.42
        _Wind("Wind",Range(0,1))=0
        _Wetness("Wetness",Range(0,1))=0
    }
    SubShader {
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard alphatest:_Cutoff addshadow fullforwardshadows vertex:vert
        #pragma target 3.0
        #pragma shader_feature_local AEROSIM_FOLIAGE_WIND
        #pragma shader_feature_local AEROSIM_FOLIAGE_ULTRA
        #include "UnityCG.cginc"
        sampler2D _MainTex;fixed4 _Color;float _Wind,_Wetness;
        struct Input {float2 uv_MainTex;};
        void vert(inout appdata_full v)
        {
            #if defined(AEROSIM_FOLIAGE_WIND)
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
                float phase=world.x*.19+world.z*.13+_Time.y*(1.1+_Wind);
                v.vertex.xz+=float2(sin(phase),cos(phase*.83))*_Wind*.035*saturate(v.texcoord.y);
                #if defined(AEROSIM_FOLIAGE_ULTRA)
                    v.vertex.xz+=float2(sin(phase*2.73),cos(phase*2.17))*_Wind*.012*saturate(v.texcoord.y*v.texcoord.y);
                #endif
            #endif
        }
        void surf(Input IN,inout SurfaceOutputStandard o){fixed4 c=tex2D(_MainTex,IN.uv_MainTex);o.Albedo=c.rgb*_Color.rgb*lerp(1,.82,_Wetness*.28);o.Alpha=c.a;o.Smoothness=.12+_Wetness*.18;o.Occlusion=.95;}
        ENDCG
    }
    Fallback "Transparent/Cutout/Diffuse"
}
