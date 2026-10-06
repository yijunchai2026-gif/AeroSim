Shader "AeroSim/NeedleFoliage"
{
    Properties {
        _Color("Needle Tint",Color)=(.7,.8,.65,1)
        _MainTex("Needle Cluster RGBA",2D)="white"{}
        _Cutoff("Alpha Cutoff",Range(0,1))=.42
    }
    SubShader {
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard alphatest:_Cutoff addshadow fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;fixed4 _Color;
        struct Input {float2 uv_MainTex;};
        void surf(Input IN,inout SurfaceOutputStandard o){fixed4 c=tex2D(_MainTex,IN.uv_MainTex);o.Albedo=c.rgb*_Color.rgb;o.Alpha=c.a;o.Smoothness=.12;o.Occlusion=.95;}
        ENDCG
    }
    Fallback "Transparent/Cutout/Diffuse"
}
